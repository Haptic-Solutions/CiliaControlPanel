using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameProfilesUtil
{
    Cilia mCilia;
    private Dropdown mGameProfilesDropdown;
    private Dropdown mViewGroupDropdown;
    private Dropdown mChangeSurroundGroupDropdown;

    static List<string> mCiliaGameProfiles = new List<string>();
    /**
     * Gets reference to pased in Cilia and some of its dropdown UI elements
     */
    public GameProfilesUtil(Cilia aCilia)
    {
        mCilia = aCilia;
        mGameProfilesDropdown = mCilia.GetGameProfilesDropdown();
        mViewGroupDropdown = mCilia.GetViewGroupDropdown();
        mChangeSurroundGroupDropdown = mCilia.GetChangeSurroundGroupDropdown();
    }
    /**
     * Tells the SDK to load a game profile and gets the group names, then loads the information about Cilias in that game profile.
     */
    public void SetGameProfile()
    {
        Cilia.SendMessageToCilia("[!#LoadProfile|" + mGameProfilesDropdown.GetComponentInChildren<Text>().text + "]");
        Cilia.SendMessageToCilia("[!#GetGroupNames]");
        string groupNames = mCilia.StreamReaderReadLine();
        mCilia.SetSurroundPositions(groupNames.Split(','));
        List<string> groupsList = new List<string>();

        foreach (string group in mCilia.GetSurroundPositions())
        {
            groupsList.Add(group);
        }
        mViewGroupDropdown.ClearOptions();
        mViewGroupDropdown.AddOptions(groupsList);
        mViewGroupDropdown.value = 0;
        mChangeSurroundGroupDropdown.ClearOptions();
        mChangeSurroundGroupDropdown.AddOptions(groupsList);
        mChangeSurroundGroupDropdown.value = 0;

        mCilia.GetCiliaInformation();
        mCilia.ChangeCilia();
    }
    /*
     * Gets the list of game profiles and adds them to the game profiles drop down
     */
    public void SetupGameProfiles()
    {
        Cilia.SendMessageToCilia("[!#GetProfiles]");
        string profileString = mCilia.StreamReaderReadLine();
        Debug.Log("Setting up Game Profiles" + profileString);
        string[] profiles = profileString.Replace("[", "").Replace("]", "").Split(',');

        mGameProfilesDropdown.ClearOptions();
        mCiliaGameProfiles.Clear();
        for (int i = 0; i < profiles.Length - 1; i++)
            mCiliaGameProfiles.Add(profiles[i]);
        mCiliaGameProfiles.Sort();
        mGameProfilesDropdown.AddOptions(mCiliaGameProfiles);
        mGameProfilesDropdown.value = mCiliaGameProfiles.BinarySearch(profiles[profiles.Length - 1]);
    }
    /**
     * Deletes all game profiles including the default one in the SDK
     */
    public void factoryReset()
    {
        Cilia.SendMessageToCilia("[!#FactoryReset]");
        sharedDelete();
    }
    /**
     * Deletes a specific game profile in the SDK
     */
    public void deleteProfile()
    {
        Cilia.SendMessageToCilia("[!#DeleteProfile]");
        sharedDelete();
    }
    /**
     * Re sets up game profiles after deletes.
     */
    private void sharedDelete()
    {
        byte[] throwawaybuffer = new byte[1];
        mCilia.SetReaderTimeOut(100000);
        mCilia.StreamReaderRead(throwawaybuffer, 0, 1);
        SetupGameProfiles();
        if (mGameProfilesDropdown.value == 0)
        {
            SetGameProfile();
        }
        else
            mGameProfilesDropdown.value = 0;
    }
}
