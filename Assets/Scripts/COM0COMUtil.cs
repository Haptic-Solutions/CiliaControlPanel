using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class COM0COMUtil 
{
    /**
     * Default Constructors
     */
    public COM0COMUtil()
    {
    }
    /**
     * Installs COM-0-COM software.
     * Installs the com-0-com software so that virtual cilias can be used.
     * Sets mRestartComputer to active so user can visually see that they need to restart their computer.
     */
    public void InstallCom0Com()
    {
        string setupFile = "cd \"com0com-3.0.0.0-i386-and-x64-signed\"\nSetup_com0com_v3.0.0.0_W7_x64_signed.exe /S";
        System.IO.File.WriteAllText("COM0COMSetup.bat", setupFile);
        System.Diagnostics.ProcessStartInfo setupProcess = new System.Diagnostics.ProcessStartInfo();
        string cdirectory = Directory.GetCurrentDirectory();
        setupProcess.FileName = cdirectory + "\\COM0COMSetup.bat";
        var sProcess = System.Diagnostics.Process.Start(setupProcess);
        sProcess.WaitForExit();
    }
    /**
     * Uninstalls COM-0-COM software.
     * If the user no longer wants COM-0-COM this method is called to uninstall the software.
     * It then sets the text back to Install COM-0-COM in case they change their mind and want to install it again.
     */
    public void UninstallCom0Com()
    {
        string setupFile = "cd \"C:\\Program Files (x86)\\com0com\"\nuninstall.exe /S";
        System.IO.File.WriteAllText("COM0COMSetup.bat", setupFile);
        System.Diagnostics.ProcessStartInfo setupProcess = new System.Diagnostics.ProcessStartInfo();
        string directory = Directory.GetCurrentDirectory();
        setupProcess.FileName = directory + "\\COM0COMSetup.bat";
        var sProcess = System.Diagnostics.Process.Start(setupProcess);
        sProcess.WaitForExit();
    }
}
