using System;
using System.IO;
using System.IO.Ports;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;

public class FakeCilia : MonoBehaviour
{
    /*Serializable*/
    [SerializeField] private RPMAnimate[] mRPM = new RPMAnimate[6];
    [SerializeField] private ColorPicker2[] mColorPickers = new ColorPicker2[6];
    [SerializeField] private InputField mInputField;
    /*Class Variables*/
    private char mMychar;
    private char mOldchar;
    private bool mKeepAlive = true;
    private string[] mFans = new string[6];
    private string[] mNeopixels = new string[7];
    private SerialPort mCOMX;
    private bool mSuccess = false;
    private byte[] mBuffer = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
    private string[] mFan = { "000", "000", "000", "000", "000", "000" };
    private string[] mOldfan = { "", "", "", "", "", "" };
    private string[] mLight = { "000000000", "000000000", "000000000", "000000000", "000000000", "000000000", "000000000" };
    private string[] mOldlight = { "", "", "", "", "", "" };
    private int mComInt;
    /**
     * Initializes our fake Cilias fan speeds to 0 and neopixel rgb values to off.
     * */
    void Start()
    {
        mCOMX = new SerialPort();
        for (int i = 0; i < 6; i++)
        {
            mFans[i] = "000";
            mNeopixels[i] = "000,000,000";
        }
    }
    /**
     * Sets up the serial Connection for the fake Cilia
     * */
    public void SetComPort()
    {
        try
        {
            //create a list of available serial/COM ports
            string[] comportNames = SerialPort.GetPortNames();
            List<string> comportList = new List<string>(comportNames);
            //if the input field is empty set to 0
            if (mInputField.text.Equals(""))
                mInputField.text = "0";
            //construct strings for serial port pair
            mComInt = int.Parse(mInputField.text);
            string comstring = "COM" + mComInt;
            string fcstring = "COMFC" + mComInt;
            //check if port in use by a physical Cilia
            if (comportList.Contains(comstring) && !comportList.Contains(fcstring))
            {
                Debug.Log("Com port in use");
                return; //this is the case where there is a physical cilia using the port
            }
            //if virtual pair has not yet been created create it
            else if (!comportList.Contains(fcstring))
            {
                CreateNewPair(mComInt); //this is the case where no one is using the port and a virtual port has not been created
            }
            //setup serial port settings and start new thread for reading from serial port
            mCOMX.PortName = fcstring;
            mCOMX.BaudRate =  19200;
            mCOMX.ReadTimeout = 500;
            mCOMX.WriteTimeout = 500;
            mCOMX.Open();
            mSuccess = true;
            Thread comThread = new Thread(DoReadCom);
            comThread.Start();
        }
        //catches errors such as user typing in invalid port
        catch
        {
            if (mCOMX.IsOpen)
                mCOMX.Close();
            Debug.Log("COM Port Does Not Exist");
        }
        
    }
    /**
     * Sends message over named pipe to the SDK to indicate that a fake Cilia has been spun up
     * */
    public void Confirm()
    {
        using (NamedPipeClientStream ciliaClient =
            new NamedPipeClientStream(".", "ciliapipe", PipeDirection.Out))
        {
            ciliaClient.Connect();
            try
            {
                using (StreamWriter streamWriter = new StreamWriter(ciliaClient))
                {
                    streamWriter.AutoFlush = true;
                    streamWriter.WriteLine("COM"+mComInt+",Attach");
                    streamWriter.Close();
                }
            }
            catch (IOException e)
            {
            }

        }      
    }
    /**
     * Helps to autocorrect entries to values between 0 and 255 in UI
     * */
    public void FixComNumber()
    {
        //if there is an active Cilia. Shouldn't need this check but it is a good extra measure.
        //If empty that means the person pressed backspace. Just return
        if (mInputField.text.Equals(""))
                return;
        //get rid of negative symbol since we don't want negative
        mInputField.text = mInputField.text.Replace("-", "");
            //OK now that we are past the first two checks we need to get or value to make sure it is less than 255
        int inputFieldint = int.Parse(mInputField.text);
        //if value is greater than 255 remove the last character by using / by 10.
        if (inputFieldint > byte.MaxValue)
        {
            inputFieldint = inputFieldint / 10;
            mInputField.text = inputFieldint.ToString();
        }
    }
    /**
     * Checks to see if there are updates to the fans speeds or colors and calls setFanSpeed or UpdateColorPicker accordingly
     */
    void Update()
    {
        if (mSuccess == true)
        {
            for (int i = 0; i < mFan.Length; i++)
            {
                if(!mFan[i].Equals(mOldfan[i]))
                {
                    mRPM[i].setFanSpeed(float.Parse(mFan[i]));
                    mOldfan[i] = mFan[i];
                }
                if (!mLight[i].Equals(mOldlight[i]))
                {
                    mColorPickers[i].UpdateColorPicker(mLight[i]);
                    mOldlight[i] = mLight[i];
                }
            }
        }
    }
    /**
     * Sends a message to the SDK letting it know that this fake Cilia is shutting down.
     */
    void OnApplicationQuit()
    {
        mKeepAlive = false;
        if (mCOMX.IsOpen)
            mCOMX.Close();
        using (NamedPipeClientStream ciliaClient =
            new NamedPipeClientStream(".", "ciliapipe", PipeDirection.Out))
        {
            ciliaClient.Connect();
            try
            {
                using (StreamWriter streamWriter = new StreamWriter(ciliaClient))
                {
                    streamWriter.AutoFlush = true;
                    streamWriter.WriteLine("COM"+mComInt+",Detach");
                    streamWriter.Close();
                }
            }
            catch (IOException e)
            {
            }

        }
    }
    /**
     * Reads information being sent over serial by the SDK and emulates a Cilia's behavior.
     * <pre>
     * If a C character is received sends back a CILIA message.
     * If an F is received store the fan information so that the main thread can animate it.
     * If an N is received store the neopixel lighting information so the main thread can illuminate it.
     * </pre>
     */
    void DoReadCom()
    {
        while (mKeepAlive)
        {
            if (mSuccess == true)
            {
                try
                {
                    switch ((char)mCOMX.ReadChar())
                    {
                        case 'C':
                            mCOMX.Write("CILIA\n");
                            break;
                        case 'F':
                            mCOMX.Read(mBuffer, 0, 4);
                            mFan[mBuffer[0] - 49] = "" + (char)mBuffer[1] + (char)mBuffer[2] + (char)mBuffer[3];
                            break;
                        case 'N':
                            mCOMX.Read(mBuffer, 0, 10);
                            try
                            {
                                mLight[mBuffer[0] - 49] = "" + (char)mBuffer[1] + (char)mBuffer[2] + (char)mBuffer[3] + (char)mBuffer[4] + (char)mBuffer[5] + (char)mBuffer[6] + (char)mBuffer[7] + (char)mBuffer[8] + (char)mBuffer[9];
                            }
                            catch
                            {
                            }
                            break;
                        default:
                            break;
                    }
                }
                catch
                {
                }
            }
        }
    }
    /**
     * Used to tell COM-0-COM to create a new pair of COM ports.
     * Creates a bat file with the appropriate command and runs it.
     */
    public void CreateNewPair(int mComInt)
    {
        string createNewPairFile = "cd \"C:\\Program Files (x86)\\com0com\"\nsetupc install PortName=COMFC" + mComInt + " PortName=COM" + mComInt;
        System.IO.File.WriteAllText("CreateNewPair.bat", createNewPairFile);
        System.Diagnostics.ProcessStartInfo createNewPairProcess = new System.Diagnostics.ProcessStartInfo();
        string directory = Directory.GetCurrentDirectory();
        createNewPairProcess.FileName = directory + "\\CreateNewPair.bat";
        var createPairProcess = System.Diagnostics.Process.Start(createNewPairProcess);
        createPairProcess.WaitForExit();
    }
}
