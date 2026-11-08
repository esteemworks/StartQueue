using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace StartQueue.Core;

/// <summary>Store apps: their startup tasks, starting them by AUMID, and listing installed apps.</summary>
public static class PackagedApps
{
    public const int StateDisabled = 0;
    public const int StateDisabledByUser = 1;
    public const int StateEnabled = 2;
    public const int StateDisabledByPolicy = 3;
    public const int StateEnabledByPolicy = 4;

    const string StateRoot = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";

    public static List<NativeItem> ScanStartupTasks()
    {
        var items = new List<NativeItem>();
        var manager = new PackageManager();

        foreach (var package in manager.FindPackagesForUser(string.Empty))
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage) continue;
                var manifest = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                if (!File.Exists(manifest)) continue;
                var text = File.ReadAllText(manifest);
                if (!text.Contains("windows.startupTask", StringComparison.OrdinalIgnoreCase)) continue;

                var familyName = package.Id.FamilyName;
                foreach (var extension in XDocument.Parse(text).Descendants()
                             .Where(x => x.Name.LocalName == "Extension" && (string?)x.Attribute("Category") == "windows.startupTask"))
                {
                    var task = extension.Elements().FirstOrDefault(x => x.Name.LocalName == "StartupTask");
                    var taskId = (string?)task?.Attribute("TaskId");
                    var application = extension.Ancestors().FirstOrDefault(x => x.Name.LocalName == "Application");
                    var appId = (string?)application?.Attribute("Id");
                    if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(appId)) continue;

                    var manifestEnabled = string.Equals((string?)task!.Attribute("Enabled"), "true", StringComparison.OrdinalIgnoreCase);
                    var state = ReadState(familyName, taskId);
                    var active = state is StateEnabled || (state == null && manifestEnabled);
                    if (!active) continue; // Off, or enabled by policy (cannot be changed).

                    var executable = (string?)extension.Attribute("Executable") ?? (string?)application!.Attribute("Executable") ?? "";
                    var aumid = $"{familyName}!{appId}";
                    var parameters = (string?)task.Attribute("Parameters") ?? "";
                    var origin = new NativeOrigin
                    {
                        Kind = OriginKind.StartupTask,
                        Location = "Package",
                        Name = $"{familyName}!{taskId}",
                        Data = aumid,
                        WasEnabled = true,
                    };
                    items.Add(new NativeItem(origin, DisplayName(package, familyName), true, EntryKind.PackagedApp,
                        aumid, parameters, "", CommandLine.ProcessNameOf(executable), executable));
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"Reading startup tasks of {package.Id.FullName} failed: {ex.Message}", "store");
            }
        }
        return items;
    }

    static string DisplayName(Windows.ApplicationModel.Package package, string familyName)
    {
        try
        {
            var name = package.DisplayName;
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
                return name;
        }
        catch
        {
            // Some system packages do not resolve their display name.
        }
        return familyName.Split('_')[0];
    }

    static int? ReadState(string familyName, string taskId)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{StateRoot}\{familyName}\{taskId}");
        return key?.GetValue("State") is int state ? state : null;
    }

    /// <summary>Sets the state Windows uses for a startup task, the same switch as in Settings and Task Manager.</summary>
    public static void SetTaskState(string familyAndTask, int state)
    {
        var parts = familyAndTask.Split('!');
        using var key = Registry.CurrentUser.CreateSubKey($@"{StateRoot}\{parts[0]}\{parts[1]}", true);
        key.SetValue("State", state, RegistryValueKind.DWord);
    }

    public static uint Activate(string aumid, string arguments)
    {
        uint processId = 0;
        Sta.Run(() =>
        {
            var manager = (IApplicationActivationManager)new ApplicationActivationManager();
            try
            {
                var hr = manager.ActivateApplication(aumid, string.IsNullOrWhiteSpace(arguments) ? null : arguments, 0, out processId);
                Marshal.ThrowExceptionForHR(hr);
            }
            finally
            {
                Marshal.FinalReleaseComObject(manager);
            }
        });
        return processId;
    }

    /// <summary>
    /// Starts the program a Store app's startup task runs, inside its package, so it starts the way Windows
    /// would start it at logon (usually quietly into the tray) instead of opening the app window. Uses the
    /// documented Invoke-CommandInDesktopPackage cmdlet; returns false if that does not work for the app.
    /// </summary>
    public static bool StartInPackage(string aumid, string executable, string arguments)
    {
        try
        {
            var parts = aumid.Split('!');
            var package = new PackageManager().FindPackagesForUser(string.Empty, parts[0]).FirstOrDefault();
            if (package == null) return false;
            var fullPath = Path.Combine(package.InstalledLocation.Path, executable);
            if (!File.Exists(fullPath)) return false;

            static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
            var script = $"try {{ Invoke-CommandInDesktopPackage -PackageFamilyName {Quote(parts[0])} -AppId {Quote(parts[1])} " +
                         $"-Command {Quote(fullPath)}" +
                         (string.IsNullOrWhiteSpace(arguments) ? "" : $" -Args {Quote(arguments)}") +
                         " -ErrorAction Stop; exit 0 } catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }";

            var info = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-EncodedCommand",
                         Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)) })
                info.ArgumentList.Add(arg);

            using var shell = Process.Start(info)!;
            var error = shell.StandardError.ReadToEndAsync();
            if (!shell.WaitForExit(20_000))
            {
                shell.Kill();
                Log.Warning($"Start in package of {aumid} timed out");
                return false;
            }
            if (shell.ExitCode != 0) Log.Warning($"Start in package of {aumid} failed: {error.Result.Trim()}");
            return shell.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Log.Warning($"Start in package of {aumid} failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>All apps of the Start menu (Store and desktop), as name and AUMID or path.</summary>
    public static List<(string Name, string Id)> ListStartApps()
    {
        var apps = new List<(string, string)>();
        Sta.Run(() =>
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return;
            dynamic shell = Activator.CreateInstance(type)!;
            try
            {
                dynamic folder = shell.NameSpace("shell:AppsFolder");
                foreach (dynamic item in folder.Items())
                {
                    string name = item.Name;
                    string id = item.Path;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id)) apps.Add((name, id));
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        });
        return apps.OrderBy(a => a.Item1, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string? arguments, int options, out uint processId);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    class ApplicationActivationManager
    {
    }
}

/// <summary>Runs shell COM calls on a single-threaded apartment, as the shell expects.</summary>
public static class Sta
{
    public static void Run(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
            return;
        }

        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (error != null) throw error;
    }
}
