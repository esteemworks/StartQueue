using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace StartQueue.Core;

/// <summary>
/// Sets StartQueue up on a device and removes it again. Called by setup.exe and its uninstaller, and from the settings.
/// User part, no admin rights: start at sign-in (own scheduled task, or the user's Run key where tasks are not
/// allowed) and the context menu. Admin part, one UAC prompt, only for users who are administrators: the elevated
/// helper task, trust for the package certificate and with it the Windows 11 context menu.
/// </summary>
public static class Setup
{
    const string PackageName = "StartQueue.ContextMenu";
    const string Publisher = "CN=StartQueue";
    const string TaskFolderName = "StartQueue";
    const string LogonTaskName = "Logon";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static readonly string[] VerbKeys = [@"Software\Classes\exefile\shell\StartQueue.Add", @"Software\Classes\lnkfile\shell\StartQueue.Add"];

    static string Folder => Path.GetDirectoryName(Paths.Exe)!;
    static string ShellDll => Path.Combine(Folder, "StartQueueShell.dll");
    static string CertificateFile => Path.Combine(Folder, "StartQueue.cer");
    static string PackageFile => Path.Combine(Folder, "StartQueue.msix");

    // ---------- What this user may do ----------

    /// <summary>The signed-in user is an administrator (UAC asks to confirm, not for another account's password).</summary>
    public static bool UserIsAdmin => Elevation.IsElevated || ElevationType() == TokenElevationTypeLimited ||
        // The sandbox test acts as an administrator under UAC (ESTEEMWORKS_ASSUME_STANDARD=1) on an account without it.
        (Elevation.AssumeStandard && new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator));

    /// <summary>Running elevated through UAC, so the same user also has a token with normal rights.</summary>
    public static bool CanDropElevation => ElevationType() == TokenElevationTypeFull;

    // ---------- Install ----------

    /// <summary>The user part; with <paramref name="admin"/> also the admin part, after one UAC prompt.</summary>
    /// <param name="isNew">A new installation for this user (--new): StartQueue changes nothing in Windows until switched on.</param>
    public static void Install(bool admin, bool isNew = false)
    {
        Log.Info($"Setup in {Folder}{(admin ? " with admin features" : "")}{(isNew ? ", new installation" : "")}");
        StopOtherInstances();
        // Before the first start, so it opens with the introduction; a data folder with a list counts as switched on.
        if (isNew) KitIntro.MarkNew();
        RegisterSignIn();
        if (admin && UserIsAdmin)
        {
            if (Elevation.IsElevated) InstallAdmin(signIn: false);
            else if (!Elevation.RunSelfElevated("--setup-admin")) Log.Warning("Admin features not set up: the UAC prompt was cancelled");
        }
        SetUpContextMenu();
        Log.Info($"Setup done: {Describe()}");
        Log.Event(KitEvents.SetUp, LogLevel.Info, $"StartQueue {KitApp.Version.ToString(3)} was set up{(admin ? " with admin features" : "")}.");
    }

    /// <summary>The admin part; runs elevated, as the signed-in user (only offered to administrators).</summary>
    /// <param name="signIn">Register the sign-in start again; false when this process already did, elevated.</param>
    public static void InstallAdmin(bool signIn = true)
    {
        // Elevated, a sign-in task an older setup left to the administrators can be replaced, now with the user's
        // write access, so updates without UAC can replace it from then on.
        if (signIn) RegisterSignIn();

        RegisterTask(Elevation.ElevatedTaskName, TaskXml("--elevated", logonTrigger: false, highest: true, queue: true,
            "Run by StartQueue on demand: takes over machine-wide autostart entries and starts apps marked as administrator."));
        Log.Info("Admin task registered");

        using var certificate = X509CertificateLoader.LoadCertificateFromFile(CertificateFile);
        using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadWrite);
        // Trusted People, not a root authority: this trusts the StartQueue package and nothing else.
        if (store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count == 0) store.Add(certificate);
        Log.Info("Package certificate trusted");
        // Already elevated here, so the family's event log source costs no second UAC prompt.
        KitEvents.Register();
    }

    /// <summary>Before files are replaced: stop StartQueue and let go of the context menu DLL.</summary>
    public static void PrepareUpdate()
    {
        // StartQueue has its own --prepare-update, so the kit's backup before every update is done here: also an
        // update by running a setup by hand. One from inside the app has just done it (KitUpdate), so not twice.
        if (KitSettings.IsConfigured && !KitSettings.BackedUpWithin(TimeSpan.FromMinutes(10))) KitSettings.Backup("update");
        StopOtherInstances();
        RemovePackage();
        ReleaseShellDll();
    }

    /// <summary>
    /// Exactly one way to start at sign-in: the scheduled task, or the Run key where tasks are not allowed. Never both,
    /// or StartQueue starts twice.
    /// </summary>
    static void RegisterSignIn()
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        try
        {
            // Registered elevated, the task would belong to the administrators and a later setup without UAC (every
            // update) could not replace it. So the user gets write access, as for a task the user registered.
            RegisterTask(LogonTaskName, TaskXml("--autostart", logonTrigger: true, highest: false, queue: false,
                "Starts StartQueue at sign-in: tray app and the startup sequence."), Elevation.IsElevated ? UserWritable() : null);
            run.DeleteValue("StartQueue", false);
            Log.Info("Sign-in: scheduled task");
        }
        catch (Exception ex) when (LogonTaskStartsThisApp())
        {
            // Registered elevated by an older setup: the user may read it but not replace it. It still starts this
            // StartQueue, so it stays the one way in, and a Run key from an earlier setup goes.
            run.DeleteValue("StartQueue", false);
            Log.Info($"Sign-in: scheduled task, kept as it is (cannot replace it without admin rights: {ex.Message.Trim()})");
        }
        catch (Exception ex)
        {
            // Some devices do not let users create tasks; the Run key always works. A task that starts something else
            // (another folder, switched off) goes if the user may delete it.
            DeleteTask(LogonTaskName);
            run.SetValue("StartQueue", $"\"{Paths.Exe}\" --autostart");
            Log.Info($"Sign-in: Run key (no scheduled task: {ex.Message.Trim()})" +
                     (TaskExists(LogonTaskName) ? "; an older sign-in task that does not start this StartQueue is left, it needs admin rights to remove" : ""));
        }
    }

    /// <summary>Full access for administrators, the system and the signed-in user.</summary>
    static string UserWritable() => $"D:(A;;FA;;;BA)(A;;FA;;;SY)(A;;FA;;;{WindowsIdentity.GetCurrent().User!.Value})";

    /// <summary>The sign-in task exists, is switched on and starts this StartQueue with --autostart for this user.</summary>
    static bool LogonTaskStartsThisApp()
    {
        try
        {
            dynamic task = Service().GetFolder("\\" + TaskFolderName).GetTask(LogonTaskName);
            if (!(bool)task.Enabled) return false;
            var xml = XDocument.Parse((string)task.Xml);
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            using var identity = WindowsIdentity.GetCurrent();
            var user = xml.Descendants(ns + "Principal").Elements(ns + "UserId").FirstOrDefault()?.Value.Trim();
            if (user != null && !string.Equals(user, identity.User!.Value, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(user, identity.Name, StringComparison.OrdinalIgnoreCase)) return false;
            return xml.Descendants(ns + "Exec").Any(exec =>
                string.Equals(Environment.ExpandEnvironmentVariables(exec.Element(ns + "Command")?.Value.Trim().Trim('"') ?? ""),
                    Paths.Exe, StringComparison.OrdinalIgnoreCase) &&
                (exec.Element(ns + "Arguments")?.Value ?? "").Contains("--autostart", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Windows 11 menu where the package certificate is trusted, otherwise the classic menu.</summary>
    public static void SetUpContextMenu()
    {
        if (CertificateTrusted())
        {
            try
            {
                RemovePackage();
                ReleaseShellDll();
                var options = new AddPackageOptions { ExternalLocationUri = new Uri(Folder + "\\"), ForceUpdateFromAnyVersion = true };
                var result = new PackageManager().AddPackageByUriAsync(new Uri(PackageFile), options).AsTask().GetAwaiter().GetResult();
                if (result.ExtendedErrorCode != null) throw result.ExtendedErrorCode;
                RemoveClassicMenu();
                Log.Info("Context menu: Windows 11 menu");
                return;
            }
            catch (Exception ex)
            {
                Log.Warning($"Windows 11 context menu not possible: {ex.Message.Trim()}");
            }
        }
        WriteClassicMenu();
        Log.Info("Context menu: classic menu");
    }

    /// <summary>"Show more options" menu, per user, no admin rights needed.</summary>
    static void WriteClassicMenu()
    {
        foreach (var path in VerbKeys)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue("MUIVerb", Strings.AddToStartQueue);
            key.SetValue("Icon", $"\"{Paths.Exe}\",0");
            using var command = key.CreateSubKey("command");
            command.SetValue("", $"\"{Paths.Exe}\" --add \"%1\"");
        }
    }

    /// <summary>After a language change: the classic menu entry carries its text itself.</summary>
    public static void RelabelClassicMenu()
    {
        foreach (var path in VerbKeys)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, true);
            key?.SetValue("MUIVerb", Strings.AddToStartQueue);
        }
    }

    static void RemoveClassicMenu()
    {
        foreach (var path in VerbKeys) Registry.CurrentUser.DeleteSubKeyTree(path, false);
    }

    // ---------- Uninstall ----------

    /// <summary>
    /// Gives every taken-over entry back to Windows and removes what setup added. Machine-wide entries and the
    /// admin task need admin rights: if the admin task exists, one UAC prompt does that part first.
    /// </summary>
    public static int Uninstall()
    {
        StopOtherInstances();
        // First, while the event log source still exists: the admin part below removes it.
        Log.Event(KitEvents.Uninstalled, LogLevel.Info, "StartQueue is being uninstalled; taken-over entries go back to Windows.");
        var adminPart = TaskExists(Elevation.ElevatedTaskName) || Elevation.IsElevated;
        if (TaskExists(Elevation.ElevatedTaskName) && !Elevation.IsElevated && !Elevation.RunSelfElevated("--uninstall-admin"))
            Log.Warning("Uninstall without admin rights: machine-wide entries and the admin task stay");
        if (Elevation.IsElevated) UninstallAdmin();

        var left = RestoreAll();
        DeleteTask(LogonTaskName);
        using (var run = Registry.CurrentUser.OpenSubKey(RunKey, true)) run?.DeleteValue("StartQueue", false);
        RemoveClassicMenu();
        RemovePackage();
        ReleaseShellDll();
        Family.Unregister();
        // Set up through "Set up" under About without the admin features: one UAC prompt to remove it. With them,
        // the admin part above already did, or was declined; then no second prompt.
        if (!adminPart) KitEvents.UnregisterElevated();
        Log.Info("Uninstalled");

        if (left.Count > 0)
            MessageBox(IntPtr.Zero, Strings.F(Strings.UninstallLeftText, string.Join("\n", left), Paths.Data), "StartQueue", 0x30);
        return 0;
    }

    /// <summary>The admin part of uninstalling; runs elevated.</summary>
    public static void UninstallAdmin()
    {
        RestoreAll();
        DeleteTask(Elevation.ElevatedTaskName);
        DeleteTask(LogonTaskName);
        try
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            foreach (var certificate in store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, Publisher, false))
                store.Remove(certificate);
        }
        catch (Exception ex)
        {
            Log.Warning($"Removing the package certificate failed: {ex.Message}");
        }
        try
        {
            Service().GetFolder("\\").DeleteFolder(TaskFolderName, 0);
        }
        catch
        {
            // Not empty or already gone.
        }
        KitEvents.Unregister();
    }

    /// <summary>Returns the names of entries that could not go back (machine-wide ones without admin rights).</summary>
    static List<string> RestoreAll()
    {
        var left = new List<string>();
        // Shown before switching on, never taken: Windows has it already, and restoring it would add it a second time
        // (a startup folder shortcut as "name (2)").
        ConfigStore.Update(c => c.Entries.RemoveAll(e => e.StillInWindows));
        foreach (var entry in ConfigStore.Load().Entries.Where(e => e.Origin != null))
        {
            try
            {
                NativeAutostart.Restore(entry);
                ConfigStore.Update(c => c.Entries.RemoveAll(x => x.Id == entry.Id));
                Log.Event(Events.EntryReturned, LogLevel.Info, $"Returned \"{entry.Name}\" to Windows");
            }
            catch (Exception ex)
            {
                left.Add(entry.Name);
                Log.Event(Events.ReturnFailed, LogLevel.Warning, $"Returning \"{entry.Name}\" failed: {ex.Message}");
            }
        }
        return left;
    }

    // ---------- State, for the settings page ----------

    public sealed record State(bool LogonTask, bool RunKeyStart, bool ModernMenu, bool ClassicMenu);

    public static State Current()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        using var verb = Registry.CurrentUser.OpenSubKey(VerbKeys[0]);
        return new State(TaskExists(LogonTaskName), run?.GetValue("StartQueue") != null, PackageRegistered(), verb != null);
    }

    /// <summary>One line on how StartQueue is set up; also in the diagnostics package, since the kit's report only
    /// knows the Run key and StartQueue signs in through a scheduled task.</summary>
    public static string Describe()
    {
        var state = Current();
        return $"logon task {state.LogonTask}, Run key {state.RunKeyStart}, admin task {TaskExists(Elevation.ElevatedTaskName)}, " +
               $"Windows 11 menu {state.ModernMenu}, classic menu {state.ClassicMenu}";
    }

    // ---------- Helpers ----------

    static void StopOtherInstances()
    {
        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("StartQueue"))
        {
            using (process)
            {
                if (process.Id == self) continue;
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch
                {
                    // An elevated helper run; it ends by itself.
                }
            }
        }
    }

    static bool CertificateTrusted()
    {
        try
        {
            using var certificate = X509CertificateLoader.LoadCertificateFromFile(CertificateFile);
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false).Count > 0;
        }
        catch
        {
            return false;
        }
    }

    static bool PackageRegistered()
    {
        try
        {
            return new PackageManager().FindPackagesForUser(string.Empty, PackageName, Publisher).Any();
        }
        catch
        {
            return false;
        }
    }

    static void RemovePackage()
    {
        try
        {
            var manager = new PackageManager();
            foreach (var package in manager.FindPackagesForUser(string.Empty, PackageName, Publisher).ToList())
                manager.RemovePackageAsync(package.Id.FullName).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Warning($"Removing the context menu package failed: {ex.Message}");
        }
    }

    /// <summary>Explorer's COM surrogate keeps the context menu DLL loaded; end it so the file can be replaced.</summary>
    static void ReleaseShellDll()
    {
        foreach (var process in Process.GetProcessesByName("dllhost"))
        {
            using (process)
            {
                try
                {
                    if (process.Modules.Cast<ProcessModule>().Any(m => string.Equals(m.FileName, ShellDll, StringComparison.OrdinalIgnoreCase)))
                        process.Kill();
                }
                catch
                {
                    // Not ours to inspect.
                }
            }
        }
    }

    static string TaskXml(string arguments, bool logonTrigger, bool highest, bool queue, string description)
    {
        var user = WindowsIdentity.GetCurrent().User!.Value;
        var trigger = logonTrigger ? $"<Triggers><LogonTrigger><Enabled>true</Enabled><UserId>{user}</UserId></LogonTrigger></Triggers>" : "";
        // Priority 4 is normal; the default 7 would hand below-normal priority to every app started.
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo><Description>{SecurityElement.Escape(description)}</Description></RegistrationInfo>
              {trigger}
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>{(highest ? "HighestAvailable" : "LeastPrivilege")}</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>{(queue ? "Queue" : "IgnoreNew")}</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(Paths.Exe)}</Command>
                  <Arguments>{arguments}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    const int CreateOrUpdate = 6;
    const int InteractiveToken = 3;

    static void RegisterTask(string name, string xml, string? sddl = null)
    {
        dynamic service = Service();
        dynamic folder;
        try
        {
            folder = service.GetFolder("\\" + TaskFolderName);
        }
        catch
        {
            folder = service.GetFolder("\\").CreateFolder(TaskFolderName, null);
        }
        dynamic task = folder.RegisterTask(name, xml, CreateOrUpdate, null, null, InteractiveToken, sddl);
        // Replacing a task keeps its old security descriptor and ignores the one given above (seen in Windows Sandbox).
        if (sddl != null) task.SetSecurityDescriptor(sddl, 0);
    }

    static bool TaskExists(string name)
    {
        try
        {
            Service().GetFolder("\\" + TaskFolderName).GetTask(name);
            return true;
        }
        catch
        {
            return false;
        }
    }

    static void DeleteTask(string name)
    {
        try
        {
            Service().GetFolder("\\" + TaskFolderName).DeleteTask(name, 0);
        }
        catch
        {
            // Not there, or it needs admin rights (the admin task).
        }
    }

    static dynamic Service()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Task Scheduler is not available.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    const int TokenElevationTypeInfo = 18;
    const int TokenElevationTypeFull = 2;
    const int TokenElevationTypeLimited = 3;

    static int ElevationType()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return GetTokenInformation(identity.Token, TokenElevationTypeInfo, out var type, sizeof(int), out _) ? type : 0;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool GetTokenInformation(IntPtr token, int infoClass, out int info, int length, out int returned);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
