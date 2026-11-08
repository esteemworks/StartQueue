using Microsoft.Win32;

namespace StartQueue.Core;

/// <summary>
/// Windows' own delay for its autostart: after sign-in it waits a few seconds and then starts the entries one by
/// one once the system is idle. Two values for the current user switch that off; without them Windows decides.
/// They are not officially documented, but widely used; removing them restores Windows' behaviour exactly.
/// </summary>
public static class WindowsDelay
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
    const string Delay = "StartupDelayInMSec";
    const string Idle = "WaitForIdleState";

    public static bool IsOff
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            return key?.GetValue(Delay) is 0 && key.GetValue(Idle) is 0;
        }
    }

    public static void SetOff(bool off)
    {
        if (off)
        {
            using var key = Registry.CurrentUser.CreateSubKey(Key);
            key.SetValue(Delay, 0, RegistryValueKind.DWord);
            key.SetValue(Idle, 0, RegistryValueKind.DWord);
        }
        else
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key, true);
            key?.DeleteValue(Delay, false);
            key?.DeleteValue(Idle, false);
        }
        Log.Event(Events.WindowsDelayChanged, LogLevel.Info, off ? "Windows startup delay switched off" : "Windows startup delay back to Windows' default");
    }
}
