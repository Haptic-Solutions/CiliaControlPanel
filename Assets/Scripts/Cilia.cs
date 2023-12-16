using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Threading;
using UnityEditor;
using System.IO;
using System;
using System.Text;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

public class Cilia : MonoBehaviour
{
    /*Constants*/

    //constant number of possible local cilias.
    const int MAX_NUMBER_LOCAL_CILIAS = 256;
    //number of surround positions allowed.  currently one per each local cilia.
    const int NUMBER_OF_SUROUNDPOSITIONS = 256;

    /*Serializable*/

    //tcp/ip port of the SDK
    [SerializeField] private int mCiliaPort = 1995;
    //tcp/ip ip address of the SDK. May make this variable in the future when SDK may be on other machine.
    [SerializeField] private string mCiliaIP = "localhost";
    //Dropdown for selecting Specific Cilia.
    [SerializeField] private Dropdown mCiliaSelectionDropDown;
    //Dropdown for selecting Group of Cilias.
    [SerializeField] private Dropdown mViewGroupDropDown;
    //Dropdown for selecting Game Profile.
    [SerializeField] protected Dropdown mGameProfilesDropDown;
    //Dropdown for selecting Surround Group to change a Cilia to.
    [SerializeField] private Dropdown mChangeSurroundGroupDropDown;
    //UI elements for tellign the user to restart their computer.
    [SerializeField] private GameObject mRestartComputer;
    //Cilia 3D model components
    [SerializeField] private GameObject mModels;
    //Text for indicating no Cilia Selected
    [SerializeField] private GameObject mNoCiliaSelectedText;
    //A grouping of UI elements to make them easy to hide if no Cilia Selected
    [SerializeField] private GameObject mUIViewGroup1;
    //Text on button for launching fake Cilia. Also used to install COM-0-COM if not already installed
    [SerializeField] private Text mLaunchFakeCiliaText;
    //oject for handling smell library interactions
    [SerializeField] private SmellLibrary mSmellLibraryClass;
    //smell dropdown selectors.
    [SerializeField] private Dropdown[] mSmells = new Dropdown[6];
    //scripts for controlling the animation of fans.
    [SerializeField] private RPMAnimate[] mRPMAnimate = new RPMAnimate[6];
    //slider objects for controlling fan speed
    [SerializeField] private Slider[] mFanSliders = new Slider[6];
    //scripts for controlling cilia neopixel light colors
    [SerializeField] private ColorPicker[] mColorPickers = new ColorPicker[6];
    //Allows tweeking of default position groups on unity side
    [SerializeField] private string[] mSurroundPositions = { "Front Center", "Front Left", "Side Left", "Rear Left", "Rear Center", "Rear Right", "Side Right", "Front Right" };

    /*Variables for communicating between Windows Pipe thread and main thread*/

    //flag to indicate a Cilia has been added
    private bool mTimeForAdd = false;
    //string with information about Cilia added
    private string mStringToAdd = "";
    //flag to indicate a Cilia has been removed
    private bool mTimeForRemove = false;
    //index of cilia that was removed
    private int mCiliaToRemove = 0;

    /*Other Class Variables*/

    //at the end is set to false so that threads can know to close
    private bool mContinueThreads = true;
    //keep track of previously selected Cilia
    private string mOldCilia = "0";
    //get new client for talking with SDK
    private static TcpClient mCiliaClient = new TcpClient();
    //network stream we will be talking over to the SDK
    private static NetworkStream mCiliaStream;
    //buffer for messages received from SDK
    private static byte[] mBuffer = new byte[1024];
    //array to contain contents of smell library
    private static string[] mSmellLibrary;
    //Lists of what Cilias are in each surround position
    private static List<string>[] mCiliaPositions = new List<string>[NUMBER_OF_SUROUNDPOSITIONS];
    //Contains the contents of all the Cilias. What position they are in. What smells they have. What colors they are.
    private static string[][] mCiliaContents = new string[MAX_NUMBER_LOCAL_CILIAS][];
    //used for easier reading of information from streams
    private StreamReader mStreamReader;
    //object for installing or uninstalling COM-0-COM serial port emulation software.
    private COM0COMUtil mCOM0COMUtil;
    //object for managing game profiles
    private GameProfilesUtil mGameProfilesUtil;

    /**
     * Occurs upon startup of Cilia control panel.
     * Takes care of basic setup.
     * <pre>
     * Sets up utility objects for managing resources.
     * Checks if COM-O-COM installed and change UI elements accordingly.
     * RefreshPorts for the first time to get connected to SDK and obtain Cilia Information.
     * Setup a thread for receiving Windows Pipe messages indicating when a request for new information should be made.
     * </pre>
     * */
    void Start()
    {
        mCOM0COMUtil = new COM0COMUtil();
        mGameProfilesUtil = new GameProfilesUtil(this);
        //mCiliaInformationUtil = new CiliaInformationUtil(this);

        if (!File.Exists("C:\\Program Files (x86)\\com0com\\uninstall.exe"))
        {
            mLaunchFakeCiliaText.text = "Install Com-0-Com";
        }

        RefreshPorts();
        Thread ciliaReadyThread = new Thread(doCheckForCiliaReady2);
        ciliaReadyThread.Start();
    }
    /**
     * Happens once per frame, Determines if a Cilia hase been added or removed.
     * <pre>
     * If a Cilia has been added or removed it will be indicated by mTimeForAdd or mTimeForRemove.
     * Calls AddCiliaInformation or RemoveCiliaInformation to update the state of information stored about Cilias.
     * Clears mTimeForAdd or mTimeForRemove flag. 
     * </pre>
     * */
    // Update is called once per frame
    void Update()
    {
        if (mTimeForAdd == true)
        {
            AddCiliaInformation(mStringToAdd);
            mTimeForAdd = false;
        }
        if (mTimeForRemove == true)
        {
            RemoveCiliaInformation(mCiliaToRemove);
            mTimeForRemove = false;
        }
    }
    //
/**
 * Tells the SDK to light a specific cilia's neopixel to a specific color.
 * @param ciliaNumber name of the Cilia by COM port we want to change
 * @param aLightNumber that we want to change the color of
 * @param aRedValue value the neopixel will be set to.
 * @param aGreenValue value the neopixel will be set to.
 * @param aBlueValue value the neopixel will be set to.
 * */
public static void SetLightSpecificCilia(string aCiliaNumber, uint aLightNumber, byte aRedValue, byte aGreenValue, byte aBlueValue)
    {
        if (aLightNumber > 6)
        {
            aLightNumber = 6;
        }

        string toSend = "[Specific" + aCiliaNumber + ",N" + aLightNumber.ToString("D1") + aRedValue.ToString("D3") + aGreenValue.ToString("D3") + aBlueValue.ToString("D3") + "]";
        SendMessageToCilia(toSend);

        int ciliaNum =int.Parse(aCiliaNumber);
        mCiliaContents[ciliaNum][6+aLightNumber] = aRedValue.ToString("D3") + aGreenValue.ToString("D3") + aBlueValue.ToString("D3");
    }
    /**
     * Sends a message to deluminate a cilia to the SDK.
     * This results in the neopixels for that Cilia being shut off.
     * @param aCilia that we want to deluminate.
     * */
    public static void deluminateCilia(string aCilia)
    {
        string deluminateMessage = "[!#Deluminate," + aCilia + "]";
        SendMessageToCilia(deluminateMessage);
    }
    /**
     * Tells the SDK to turn on a specific fan on a specific Cilia to a specific speed.
     * @param aCiliaNumber name of the Cilia we want this applied to.
     * @param aFanNumber of the fan we want the speed changed of.
     * @param aFanSpeed speed that we want the fan to spin.
     * */
    public static void SetFanSpecificCilia(string aCiliaNumber, uint aFanNumber, byte aFanSpeed)
    {
        if(aFanNumber > 6)
        {
            aFanNumber = 6;
        }
        string toSend = "[Specific" + aCiliaNumber + ",F" + aFanNumber.ToString("D1") + aFanSpeed.ToString("D3") + "]";
        SendMessageToCilia(toSend);
    }
    /**
     * Get library information from the SDK about Cilias and perform some basic UI setup.
     * <pre>
     * Get and setup the library of possible smells.
     * Get and store information about individual Cilias.
     * Sort Cilia Lists
     * Update which surround groups are selectable.
     * Find the first group with Cilias and add them to the ciliaSelectionDropDown.
     * Select the first Cilia and set its currently selected smells in the smell selectors.
     * </pre>
     * */
    public void GetCiliaInformation()
    {
        Cilia.SendMessageToCilia("[!#GetGroupNames]");
        string groupNames = StreamReaderReadLine();
        SetSurroundPositions(groupNames.Split(','));
        //get and setup the library of possible smells
        SendMessageToCilia("[!#GetLibrary]");
        string smellLibraryString = mStreamReader.ReadLine();
        mSmellLibraryClass.SubInit(smellLibraryString);
        
        //now we request the actual smells assigned to each active cilia
        SendMessageToCilia("[!#GetSmells]");
        Debug.Log("Sent GetSmells");
        smellLibraryString = "";
        //just in case things wont return when we read we set a read timeout of 1000
        mCiliaStream.ReadTimeout = 1000;
        //clear out the list of what cilias are in each surround position
        for (int i = 0; i < mCiliaPositions.Length; i++)
        {
            mCiliaPositions[i].Clear();
        }
        try
        {
            //loop until we either get a complete message ending in ] or fail
            int count = 0;
            do
            {
                count = mCiliaStream.Read(mBuffer, 0, mBuffer.Length);

                smellLibraryString += System.Text.Encoding.Default.GetString(mBuffer);
                Debug.Log(mBuffer[0]);
                if (smellLibraryString.Contains("]"))
                    break;
            } while (count != 0);
            //if count == 0 we failed to receive the message properly so return
            if (count == 0)
                return;
            Debug.Log("Read some bytes\n");
            Debug.Log(smellLibraryString);
            //get just message and split by row of cilias
            smellLibraryString = smellLibraryString.Split('[')[1];
            smellLibraryString = smellLibraryString.Split(']')[0];
            mSmellLibrary = smellLibraryString.Split(',', '\n');
           //clear drop down for selecting cilia
            mCiliaSelectionDropDown.options.Clear();
            //loop to store information
            for (int i = 0; i < mSmellLibrary.Length; i += 14)
            {
                int ciliaIndex = int.Parse(mSmellLibrary[i]);
                //store surround position
                mCiliaContents[ciliaIndex][0] = mSmellLibrary[i + 1];
                //store cilia in list of what Cilias are in each group based on its surround group
                int scentGroupForIndex = int.Parse(mCiliaContents[ciliaIndex][0]);
                mCiliaPositions[scentGroupForIndex].Add(mSmellLibrary[i]);
                //store Smells
                mCiliaContents[ciliaIndex][1] = mSmellLibrary[i + 2];
                mCiliaContents[ciliaIndex][2] = mSmellLibrary[i + 3];
                mCiliaContents[ciliaIndex][3] = mSmellLibrary[i + 4];
                mCiliaContents[ciliaIndex][4] = mSmellLibrary[i + 5];
                mCiliaContents[ciliaIndex][5] = mSmellLibrary[i + 6];
                mCiliaContents[ciliaIndex][6] = mSmellLibrary[i + 7];
                //store colors
                mCiliaContents[ciliaIndex][7] = mSmellLibrary[i + 8];
                mCiliaContents[ciliaIndex][8] = mSmellLibrary[i + 9];
                mCiliaContents[ciliaIndex][9] = mSmellLibrary[i + 10];
                mCiliaContents[ciliaIndex][10] = mSmellLibrary[i + 11];
                mCiliaContents[ciliaIndex][11] = mSmellLibrary[i + 12];
                mCiliaContents[ciliaIndex][12] = mSmellLibrary[i + 13];
                //send message to sdk to delluminate this Cilia
                string deluminateMessage = "[!#Deluminate," + ciliaIndex + "]";
                SendMessageToCilia(deluminateMessage);
            }

            //sort our lists of Cilias in their groups
            for (int orgi = 0; orgi < mCiliaPositions.Length; orgi++)
            {
                mCiliaPositions[orgi].Sort();
            }
            //add first group to drop down menu
            //ciliaSelectionDropDown.ClearOptions();
            //ciliaSelectionDropDown.AddOptions(mCiliaPositions[0]);

            Debug.Log("Made it thus far");
            int j;
            //ubdate which groups are selectable. based on if they have Cilias
            updateSelectable();
            //find the first group with cilias and add it to the drop down of selectable cilias
            for (j = 0; j < mCiliaPositions.Length; j++)
            {
                if (mCiliaPositions[j].Count == 0)
                {
                    continue;
                }
                else
                {
                    mViewGroupDropDown.value = j;
                    mCiliaSelectionDropDown.ClearOptions();
                    mCiliaSelectionDropDown.AddOptions(mCiliaPositions[j]);
                    break;//break after finding first list
                }
            }
            //if there are actally cilias
            if ((j < mCiliaPositions.Length) && (mCiliaPositions[j].Count > 0))
            {
                List<string> contents = mSmellLibraryClass.getSmellLibraryContents();
                string selectedCilia = mCiliaPositions[j][0];//get first cilia name
                mOldCilia = selectedCilia;
                int cilianumber = int.Parse(selectedCilia.Replace("COM", ""));
                //search through smells which is why we stop at 6
                //we set the drop downs for selecting the smells to what is in the smell library
                for (int it = 1; it < 7; it++)
                {

                    string content = mCiliaContents[cilianumber][it];
                    int key = contents.BinarySearch(content);
                    mSmells[it - 1].value = key;
                }
            }
            else
            {
                mCiliaSelectionDropDown.ClearOptions();
            }
        }
        catch
        {
            Debug.Log("Failed to read bytes\n");
            mCiliaSelectionDropDown.ClearOptions();
        }
       
    }
    /**
     * Sends a string message to the Cilia SDK.
     * Sends the message by TCP/IP.
     * @param aMessageToSend string to send by TCP/IP. 
     * */
    public static void SendMessageToCilia(string aMessageToSend)
    {
        byte[] message = System.Text.Encoding.ASCII.GetBytes(aMessageToSend);

        mCiliaStream.Write(message, 0, message.Length);
    }
    /**
     * Called by when viewGroupDropdown changes to show the correct list of Cilias and update the display.
     * */
    public void ChangeCiliaList()
    {
        mCiliaSelectionDropDown.ClearOptions();
        mCiliaSelectionDropDown.AddOptions(mCiliaPositions[mViewGroupDropDown.value]);
        ChangeCilia();
    }
    /**
     * Changes which Cilia is visibly selected.
     * <pre>
     * Turns off the neopixels on the mOldCilia.
     * If no Cilias are selected indicate as such by updating UI elements.
     * Updates the color pickers to the RGB values they should be for the currently selected Cilia.
     * Results in Illuminating the currently selectedCilia.
     * </pre>
     * */
    public void ChangeCilia()
    {
        List<string> contents = mSmellLibraryClass.getSmellLibraryContents();
        string selectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        deluminateCilia(mOldCilia);
        mOldCilia = selectedCilia;

        if (selectedCilia.Equals(""))
        {
            mUIViewGroup1.SetActive(false);
            mModels.SetActive(false);
            mNoCiliaSelectedText.SetActive(true);
        }
        else
        {
            mUIViewGroup1.SetActive(true);
            mModels.SetActive(true);
            mNoCiliaSelectedText.SetActive(false);
        
            int cilianumber = int.Parse(selectedCilia);
            for (int it = 1; it < 7; it++)
            {

                string content = mCiliaContents[cilianumber][it];
                int key = contents.BinarySearch(content);
                
                mSmells[it - 1].value = key;
            }
            //Update ColorPicker
            mColorPickers[0].UpdateColorPicker(mCiliaContents[cilianumber][7]);
            mColorPickers[1].UpdateColorPicker(mCiliaContents[cilianumber][8]);
            mColorPickers[2].UpdateColorPicker(mCiliaContents[cilianumber][9]);
            mColorPickers[3].UpdateColorPicker(mCiliaContents[cilianumber][10]);
            mColorPickers[4].UpdateColorPicker(mCiliaContents[cilianumber][11]);
            mColorPickers[5].UpdateColorPicker(mCiliaContents[cilianumber][12]);
        }
    }
    /**
     * Used to update the SDK on what smell is currently selected in selector.
     * Sends a SetSmell message that tells the SDK which cilia ID and which smell slot should be updated and what smell it should be updated to.
     * @param aSelectorNumber the selector and corresponding smell slot we are trying to update
     * */
    public void ChangeSmell(int aSelectorNumber)
    {
        string selectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        try
        {
            int ciliaNumber = int.Parse(selectedCilia);
            string smellString = mSmells[aSelectorNumber - 1].GetComponentInChildren<Text>().text;
            mCiliaContents[ciliaNumber][aSelectorNumber] = smellString;
            SendMessageToCilia("[!#SetSmell|" + ciliaNumber+"|"+aSelectorNumber+"|"+smellString+"]");
        }
        catch
        { }
    }
    /**
     * Sends message to the SDK to change what group a Cilia is in.
     * Sends a SetGroup message over TCP/IP to the SDK indicating what Cilia should be updated and what group it should be reasigned to.
     * @param aCiliaNumber ID of the Cilia to be reasigned
     * @param aGroupNumber int ID of the group the Cilia is to be reasigned to.
     * */
    public void ChangeGroup(byte aCiliaNumber, byte aGroupNumber)
    {
        try
        {
            SendMessageToCilia("[!#SetGroup|" + aCiliaNumber + "|" + aGroupNumber + "]");
        }
        catch
        { }
    }
    /**
     * Changes the surround group that the currently selected Cilia is in to the selected group to be changed to.
     * */
    public void ChangeSurroundGroup()
    {
        int oldGroup = mViewGroupDropDown.GetComponent<Dropdown>().value;
        string selectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        byte ciliaNumber = byte.Parse(selectedCilia);
        byte surroundIndex = (byte)mChangeSurroundGroupDropDown.value;
        //DropdownA.items[0].Attributes.Add("disabled", "disabled");
        mCiliaContents[ciliaNumber][0] = surroundIndex.ToString();

        int removeIndex = mCiliaPositions[oldGroup].BinarySearch(ciliaNumber.ToString());
        mCiliaPositions[oldGroup].Remove(mCiliaPositions[oldGroup][removeIndex]);

        mCiliaPositions[surroundIndex].Add(ciliaNumber.ToString());
        mCiliaPositions[surroundIndex].Sort();

        ChangeGroup(ciliaNumber, surroundIndex);

        mViewGroupDropDown.value = surroundIndex;
        mCiliaSelectionDropDown.ClearOptions();
        mCiliaSelectionDropDown.AddOptions(mCiliaPositions[surroundIndex]);
        mCiliaSelectionDropDown.value = mCiliaPositions[surroundIndex].BinarySearch(ciliaNumber.ToString());

        ChangeCilia();
        updateSelectable();
    }
    /**
     * Sends a message to the SDK saveing the current lighting configuration for a Cilia.
     * */
    public void SaveLights()
    {
        string selectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        try
        {
            int ciliaNumber = int.Parse(selectedCilia);
            string[] subC = mCiliaContents[ciliaNumber];
            SendMessageToCilia("[!#SetLights|" + ciliaNumber + "|" + subC[7] + "|" + subC[8] + "|" + subC[9] + "|" + subC[10] + "|" + subC[11] + "|" + subC[12] + "]");
        }
        catch
        { }
    }
    /**
     * Tells the SDK to set a specific fan number on the selected Cilia to the speed of the coresponding slider and also updates the virtual Cilia animation.
     * @param aFanNumber index of fan number to set.
     * */
    public void SetFanBySlider(int aFanNumber)
    {
        if ((aFanNumber > 6) || (aFanNumber == 0))
            return;
        string selectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        SetFanSpecificCilia(selectedCilia, (uint)aFanNumber, (byte)mFanSliders[aFanNumber -1].value);
        mRPMAnimate[aFanNumber-1].setFanSpeed(mFanSliders[aFanNumber - 1].value);  
    }
    /**
     * Rereshes network connection with SDK and re-retrieves information from SDK.
     * */
    public void RefreshPorts()
    {
        Debug.Log("refreshing Ports");

        //
        if (!mCiliaClient.Connected)
        try
        {
            mCiliaClient = new TcpClient();
            mCiliaClient.Connect(mCiliaIP, mCiliaPort);
            mCiliaStream = mCiliaClient.GetStream();
            mStreamReader = new StreamReader(mCiliaStream);
            Debug.Log("Cilia Connected");
        }
        catch
        {
            Debug.Log("Cilia Not Connected");
            return;
        }
        mCiliaPositions = new List<string>[NUMBER_OF_SUROUNDPOSITIONS];
        mCiliaContents = new string[MAX_NUMBER_LOCAL_CILIAS][];

        for (int cPI = 0; cPI < NUMBER_OF_SUROUNDPOSITIONS; cPI++)
        {
            mCiliaPositions[cPI] = new List<string>();
        }
        for (int cCI = 0; cCI < MAX_NUMBER_LOCAL_CILIAS; cCI++)
        {
            mCiliaContents[cCI] = new string[13];
        }
        Debug.Log("SetupGameProfiles");
        mGameProfilesUtil.SetupGameProfiles();
        GetCiliaInformation();
        ChangeCilia();
    }
    /**
     * Calls SetGameProfile function in Game Profile Utils.
     * 
     * */
    public void SetGameProfile()
    {
        mGameProfilesUtil.SetGameProfile();
    }
    /**
     * Launches a fake Cilia.
     * This function is intended to help launch fake Cilias for development purposes.
     * If COM-0-COM isn't installed install COM-0-COM.
     * Otherwise launch fake Cilia.
     * */
    public void LaunchFakeCilia()
    {
        if (!File.Exists("C:\\Program Files (x86)\\com0com\\uninstall.exe"))
        {
            mCOM0COMUtil.InstallCom0Com();
            mRestartComputer.SetActive(true);
        }
        else{
            System.Diagnostics.ProcessStartInfo fakeCiliaProcess = new System.Diagnostics.ProcessStartInfo();
            string directory = Directory.GetCurrentDirectory();
            if (Application.isEditor)
            {
                fakeCiliaProcess.FileName = directory + "\\Builds\\8_22_19\\Fake Cilia\\FakeCilia.exe";
            }
            else
                fakeCiliaProcess.FileName = directory + "/Fake Cilia/FakeCilia.exe";
            System.Diagnostics.Process.Start(fakeCiliaProcess);
        }
    }
    /**
     * Updates the list of selectable groups.
     * Itterates through the positions groups to see which groups have Cilias.
     * If a group has no cilias add it to the inactive list in Selectable.
     * */
    public void updateSelectable()
    {
        Selectable.inactiveList.Clear();
        for (int j = 0; j < mSurroundPositions.Length; j++)
        {
            if (mCiliaPositions[j].Count == 0)
            {
                Selectable.inactiveList.Add(mSurroundPositions[j]);
                continue;
            }
        }
    }
    /**
     * Calls factory reset function in Game Profile Util class
     * */
    public void factoryReset()
    {
        mGameProfilesUtil.factoryReset();
    }
    /**
     * Calls delete profile function in Game Profile Util class
     * */
    public void deleteProfile()
    {
        mGameProfilesUtil.deleteProfile();
    }
    /**
     * Waits for a message over a named pipe to set an update flag.
     * When  Refresh message is received over the ciliaControlPanelPipe the mTimeForUpdate flag is set true
     * so that the main thread knows to request new information from the SDK.
     * */
    public void doCheckForCiliaReady2()
    {
        PipeSecurity pipeSecurity = new PipeSecurity();

        pipeSecurity.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid,null), PipeAccessRights.FullControl, AccessControlType.Allow));
        
        while (mContinueThreads == true)
        {

            using (NamedPipeServerStream ciliaPipeServer =
            new NamedPipeServerStream("ciliaControlPanelPipe", PipeDirection.In, 100, PipeTransmissionMode.Message, PipeOptions.None, 1, 1, pipeSecurity))
            {
                try
                {
                    ciliaPipeServer.WaitForConnection();

                    using (StreamReader streamReader = new StreamReader(ciliaPipeServer))
                    {
                        string ciliaMessage;
                        while ((ciliaMessage = streamReader.ReadLine()) != null)
                        {
                            if (ciliaMessage.Contains("AddCilia"))
                            {
                                mTimeForAdd = true;
                                mStringToAdd = ciliaMessage.Replace("AddCilia,", "");
                            }
                            else if (ciliaMessage.Contains("RemoveCilia"))
                            {
                                ciliaMessage = ciliaMessage.Replace("RemoveCilia,", "");
                                mTimeForRemove = true;
                                mCiliaToRemove = int.Parse(ciliaMessage);
                            }
                        }
                    }
                    ciliaPipeServer.Close();
                }
                catch { }
            }
        }
    }
    /**
     * Calls UninstallCom0Com in the COM0COMUtil
     * */
    public void UninstallCom0Com()
    {
        mCOM0COMUtil.UninstallCom0Com();
        mLaunchFakeCiliaText.text = "Install Com-0-Com";
    }
    /**
     * Allow other classes to use stream reader.
     */
    public String StreamReaderReadLine()
    {
        return mStreamReader.ReadLine();
    }
    /**
     * Allows other classes to use stream reader to read into a buffer.
     * @param aBuffer to be read into
     * @param aOffset to start reading from.
     * @param aSize of buffer to be read.
     * */
    public void StreamReaderRead(byte[] aBuffer, int aOffset, int aSize)
    {
        mCiliaStream.Read(aBuffer, aOffset, aSize);
    }
    /**
     * Allows setting a timeout for the TCP/IP stream to the SDK
     * @param aTime amount of time in milliseconds before timeout.
     * */
    public void SetReaderTimeOut(int aTime)
    {
        mCiliaStream.ReadTimeout = aTime;
    }
    /**
     * Cleans up streams.
     **/
    void OnApplicationQuit()
    {
        mContinueThreads = false;
        using (NamedPipeClientStream ciliaClient =
            new NamedPipeClientStream(".", "ciliaControlPanelPipe", PipeDirection.Out))
        {
            try
            {
                ciliaClient.Connect(1000);
                try
                {
                    using (StreamWriter streamWriter = new StreamWriter(ciliaClient))
                    {
                        streamWriter.AutoFlush = true;
                        streamWriter.WriteLine("Quit");
                        streamWriter.Close();
                    }
                }
                catch (IOException e)
                {
                }
            }
            catch
            {

            }
        }
        Debug.Log("closing client\n");
        if (mCiliaStream != null)
            mCiliaStream.Close();
        if (mCiliaClient != null)
            mCiliaClient.Close();
    }
    /**
     * Adds 1 cilia at a time when interrupted by sdk.
     * @param aSmellLibraryString with information about Cilia being added.
     **/
    public void AddCiliaInformation(string aSmellLibraryString)
    {
        mSmellLibrary = aSmellLibraryString.Split(',', '\n');
        bool choseAddedCilia = false;
        int ciliaIndex = int.Parse(mSmellLibrary[0]);
        mCiliaContents[ciliaIndex][0] = mSmellLibrary[1];
        int scentGroupForIndex = int.Parse(mCiliaContents[ciliaIndex][0]);
        mCiliaPositions[scentGroupForIndex].Add(mSmellLibrary[0]);
        mCiliaPositions[scentGroupForIndex].Sort();
        //Smells
        mCiliaContents[ciliaIndex][1] = mSmellLibrary[2];
        mCiliaContents[ciliaIndex][2] = mSmellLibrary[3];
        mCiliaContents[ciliaIndex][3] = mSmellLibrary[4];
        mCiliaContents[ciliaIndex][4] = mSmellLibrary[5];
        mCiliaContents[ciliaIndex][5] = mSmellLibrary[6];
        mCiliaContents[ciliaIndex][6] = mSmellLibrary[7];
        //colors
        mCiliaContents[ciliaIndex][7] = mSmellLibrary[8];
        mCiliaContents[ciliaIndex][8] = mSmellLibrary[9];
        mCiliaContents[ciliaIndex][9] = mSmellLibrary[10];
        mCiliaContents[ciliaIndex][10] = mSmellLibrary[11];
        mCiliaContents[ciliaIndex][11] = mSmellLibrary[12];
        mCiliaContents[ciliaIndex][12] = mSmellLibrary[13];
        string deluminateMessage = "[!#Deluminate," + ciliaIndex + "]";
        SendMessageToCilia(deluminateMessage);

        //find the first group with cilias and add it to the drop down of selectable cilias
        updateSelectable();
        //get currently selected cilia
        string currentlySelectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        //if we are adding a Cilia in the currently selecte group
        if (mViewGroupDropDown.value == scentGroupForIndex)
        {
            mCiliaSelectionDropDown.ClearOptions();
            mCiliaSelectionDropDown.AddOptions(mCiliaPositions[scentGroupForIndex]);
            //set back to what it was if was if was a valid option
            if(currentlySelectedCilia.Equals("")!= true)
            {
                mCiliaSelectionDropDown.value = mCiliaPositions[scentGroupForIndex].IndexOf(currentlySelectedCilia);
            }
            //otherwise set it to the index of the cilia we just added
            else
            {
                mCiliaSelectionDropDown.value = mCiliaPositions[scentGroupForIndex].IndexOf(mSmellLibrary[0]);
                choseAddedCilia = true;
            }
        }
        //if we are adding a cilia in a different group but the current group doesn't actually have any Cilias
        else if (currentlySelectedCilia.Equals("") == true)
        {
            mViewGroupDropDown.value = scentGroupForIndex;//first we change the group to our group
            //then we set the selection drop downs to show our group
            mCiliaSelectionDropDown.ClearOptions();
            mCiliaSelectionDropDown.AddOptions(mCiliaPositions[scentGroupForIndex]);
            //then we select our cilia
            mCiliaSelectionDropDown.value = mCiliaPositions[scentGroupForIndex].IndexOf(mSmellLibrary[0]);
            choseAddedCilia = true;
        }
        //if we ended up choosing our Cilia update smell selectors
        if(choseAddedCilia == true)
        {
            List<string> contents = mSmellLibraryClass.getSmellLibraryContents();
            string selectedCilia = mSmellLibrary[0];//get first cilia name
            int cilianumber = int.Parse(selectedCilia.Replace("COM", ""));
            //search through smells which is why we stop at 6
            //we set the drop downs for selecting the smells to what is in the smell library
            for (int it = 1; it < 7; it++)
            {

                string content = mCiliaContents[cilianumber][it];
                int key = contents.BinarySearch(content);
                mSmells[it - 1].value = key;
            }
            ChangeCilia();
        }
    }
    /**
     * Removes once cilia at a time when interrupted by sdk.
     * @param aCiliaIndex to be removed
     **/
    public void RemoveCiliaInformation(int aCiliaIndex)
    {
        //remove the cilia from group
        int scentGroupForIndex = int.Parse(mCiliaContents[aCiliaIndex][0]);
        mCiliaPositions[scentGroupForIndex].Remove(aCiliaIndex.ToString());
        //update selectable groups
        updateSelectable();
        //get currently selected cilia
        string currentlySelectedCilia = mCiliaSelectionDropDown.GetComponentInChildren<Text>().text;
        //check if we deleted the currently selected Cilia
        if(int.Parse(currentlySelectedCilia) == aCiliaIndex)
        {
            mCiliaSelectionDropDown.ClearOptions();
            mCiliaSelectionDropDown.AddOptions(mCiliaPositions[scentGroupForIndex]);
            //if there are cilias left in our group
            if (mCiliaPositions[scentGroupForIndex].Count > 0)
            {
                mCiliaSelectionDropDown.value = 0;//we just pick the first one in the list if we removed the one we we are looking at
                List<string> contents = mSmellLibraryClass.getSmellLibraryContents();
                string selectedCilia = mCiliaPositions[scentGroupForIndex][0];//get first cilia name
                int cilianumber = int.Parse(selectedCilia.Replace("COM", ""));
                //search through smells which is why we stop at 6
                //we set the drop downs for selecting the smells to what is in the smell library
                for (int it = 1; it < 7; it++)
                {

                    string content = mCiliaContents[cilianumber][it];
                    int key = contents.BinarySearch(content);
                    mSmells[it - 1].value = key;
                }
            }
        }
        //see if Cilia we just removed is in the same group
        else if (mViewGroupDropDown.value == scentGroupForIndex)
        {
            //if we are in the same group and what was selected wasn't empty we will try to stick to that selection after updating the list
            if (currentlySelectedCilia.Equals("") != true)
            {
                mCiliaSelectionDropDown.ClearOptions();
                mCiliaSelectionDropDown.AddOptions(mCiliaPositions[scentGroupForIndex]);
                //set index back to the right index
                mCiliaSelectionDropDown.value = mCiliaPositions[scentGroupForIndex].IndexOf(currentlySelectedCilia);
            }
        }
        ChangeCilia();
    }
    public Dropdown GetGameProfilesDropdown()
    {
        return mGameProfilesDropDown;
    }
    public Dropdown GetViewGroupDropdown()
    {
        return mViewGroupDropDown;
    }
    public Dropdown GetChangeSurroundGroupDropdown()
    {
        return mChangeSurroundGroupDropDown;
    }
    public string[] GetSurroundPositions()
    {
        return mSurroundPositions;
    }
    public void SetSurroundPositions(string[] aValue)
    {
        mSurroundPositions = aValue;
        mViewGroupDropDown.ClearOptions();
        List<string> listOfSmells = new List<string>(mSurroundPositions);
        mViewGroupDropDown.AddOptions(listOfSmells);
        mViewGroupDropDown.value = 0;
    }
}