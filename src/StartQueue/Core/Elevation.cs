using System.ComponentModel;
using System.Security.Principal;

namespace StartQueue.Core;

public static class Elevation
{
    public const string TaskFolder = @"\StartQueue\";
    public const string ElevatedTaskName = "Elevated";

    public static bool IsElevated { get; } =
        new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Installed with admin rights: install.ps1 then registers the elevated helper task. Without it StartQueue
    /// runs in user mode, where machine-wide entries stay with Windows and nothing starts as administrator.
    /// </summary>
    public static bool AdminMode { get; } = IsElevated || ElevatedTaskExists();

    static bool ElevatedTaskExists()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Schedule.Service");
            if (type == null) return false;
            dynamic service = Activator.CreateInstance(type)!;
            service.Connect();
            dynamic folder = service.GetFolder(TaskFolder.TrimEnd('\\'));
            folder.GetTask(ElevatedTaskName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Runs the scheduled task that install.ps1 registers with highest privileges. It handles machine-wide
    /// autostart entries and starts entries marked "as administrator", without a UAC prompt.
    /// </summary>
    public static bool RunElevatedTask()
    {
        try
        {
            var info = new ProcessStartInfo("schtasks.exe", $"/run /tn \"{TaskFolder}{ElevatedTaskName}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(info)!;
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);
            if (process.ExitCode != 0) Log.Warning($"Admin task did not start: {error.Trim()}");
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Warning($"Admin task did not start: {ex.Message}");
            return false;
        }
    }

    /// <summary>Runs StartQueue once more, with the same rights, and waits for it.</summary>
    public static bool RunSelf(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(Paths.Exe, arguments) { UseShellExecute = false });
        process?.WaitForExit();
        return process?.ExitCode == 0;
    }

    /// <summary>Runs StartQueue once with a UAC prompt. Returns false if the user cancelled.</summary>
    public static bool RunSelfElevated(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Paths.Exe, arguments) { UseShellExecute = true, Verb = "runas" });
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }
}
