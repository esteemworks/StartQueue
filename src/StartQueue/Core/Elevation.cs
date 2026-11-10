using System.ComponentModel;
using System.Security.Principal;

namespace StartQueue.Core;

public static class Elevation
{
    public const string TaskFolder = @"\StartQueue\";
    public const string ElevatedTaskName = "Elevated";

    /// <summary>
    /// For the sandbox test (the kit's ESTEEMWORKS_ASSUME_STANDARD=1): its account has no UAC token split, so everything
    /// runs elevated and nothing would ever ask. With it StartQueue acts as an administrator under UAC does and asks
    /// wherever it would ask, so the test can count the prompts.
    /// </summary>
    public static bool AssumeStandard => KitElevation.AssumeStandard;

    /// <summary>This process runs with admin rights, as the kit sees it (never with <see cref="AssumeStandard"/>).</summary>
    public static bool IsElevated => KitApp.IsElevated;

    /// <summary>The admin helper task exists now (after "Set up fully", before StartQueue starts again).</summary>
    public static bool ElevatedTaskRegistered() => ElevatedTaskExists();

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

    /// <summary>
    /// Runs StartQueue once with a UAC prompt and waits for it, through the kit, which logs every such start ("Elevated
    /// start: ..."), so the sandbox test can count them. Returns false if the user cancelled.
    /// </summary>
    public static bool RunSelfElevated(string arguments) => KitElevation.Run(arguments, Timeout.Infinite);
}
