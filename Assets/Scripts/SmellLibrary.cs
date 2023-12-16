using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SmellLibrary : MonoBehaviour
{
    [SerializeField] private GameObject mCiliaGo;
    private List<string> SmellLibraryContents = new List<string>{ "Apple", "BahamaBreeze", "CleanCotton", "Leather", "Lemon", "Rose" };
    private List<string> SmellLibraryContentsConst = new List<string> { "Apple", "BahamaBreeze", "CleanCotton", "Leather", "Lemon", "Rose" };
    [SerializeField] private Dropdown[] mSmells = new Dropdown[6];
    [SerializeField] private Dropdown mRemoveSmell;
    [SerializeField] private InputField mNewSmell;
    /**
     *  Used to initialize smell library.
     *  @param aSmellLibrary string list of smells to be stored in SmellLibraryContents
     */
    public void SubInit(string aSmellLibrary)
    {
        aSmellLibrary = aSmellLibrary.TrimStart('[').TrimEnd(']');
        SmellLibraryContents = new List<string>(aSmellLibrary.Split(','));
        SmellLibraryContents.Sort();
        UpdateSmellSelectors();
    }
    /**
     * Adds a smell to the smell library.
     * Gets the user typed smell in mNewSmell and adds it to the smell library
     */
    public void AddSmell()
    {
        string smell = mNewSmell.text.ToString();
        if(SmellLibraryContents.BinarySearch(smell) < 0)
        {
            SmellLibraryContents.Add(smell);
            SmellLibraryContents.Sort();
            UpdateSmellSelectors();
            int newSmell = SmellLibraryContents.BinarySearch(smell);
            if (newSmell > -1)
            {
                for (int i = 0; i < mSmells.Length; i++)
                {
                    if (mSmells[i].value >= newSmell)
                    {
                        mSmells[i].value++;
                    }
                }
            }
        }
        updateRemoteSmellLibrary();
    }

    /**
     * Udates the remote smell library on the SDK with the local smell library.
     */
    void updateRemoteSmellLibrary()
    {
        string smellsLibraryString = "[";
        for (int ai = 0; ai < SmellLibraryContents.Count; ai++)
        {
            smellsLibraryString += SmellLibraryContents[ai].ToString() + ",";
        }
        smellsLibraryString = smellsLibraryString.TrimEnd(',');
        smellsLibraryString += "]\n";
        Cilia.SendMessageToCilia("!#UpdateLibrary|" + smellsLibraryString);
    }
    /**
     * Removes a smell selected in mRemoveSmell dropdown from the smell library.
     */
    public void RemoveSmell()
    {
        string smell = mRemoveSmell.options[mRemoveSmell.value].text;
        int removedSmell = SmellLibraryContents.BinarySearch(smell);
        SmellLibraryContents.Remove(smell);
        UpdateSmellSelectors();

        for (int i = 0; i < mSmells.Length; i++)
        {
            if (mSmells[i].value == removedSmell)
            {
                mCiliaGo.GetComponent<Cilia>().ChangeSmell(i+1);
            }
        }

        if (removedSmell > -1)
        {
            for (int i = 0; i < mSmells.Length; i++)
            {
                if (mSmells[i].value > removedSmell)
                    mSmells[i].value--;
            }
        }
        updateRemoteSmellLibrary();
    }
    /**
     * Updates the smell selectors with the Newest smell library
     */
    private void UpdateSmellSelectors()
    {
        int index = 0;
        for(int i = 0; i < mSmells.Length;i++)
        {
            index = mSmells[i].value;
            mSmells[i].ClearOptions();
            mSmells[i].AddOptions(SmellLibraryContents);
            Debug.Log("Updating SmellSelectorsWith" + SmellLibraryContents);
            mSmells[i].value = index;//set index back after update
        }
        mRemoveSmell.ClearOptions();
        mRemoveSmell.AddOptions(SmellLibraryContents);
        mRemoveSmell.value = 0;
    }
    /**
     * Returns a copy of SmellLibraryContents
     */
    public List<string> getSmellLibraryContents()
    {
        return SmellLibraryContents;
    }
}
