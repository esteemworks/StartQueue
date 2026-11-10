using System.Windows;
using StartQueue.Core;
using StartQueue.UI;

namespace StartQueue;

public partial class App : Application
{
    TrayHost? host;
    SingleInstance? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // The kit logs what nobody caught (KitDiagnostics.Start adds the UI thread); StartQueue goes on.
        DispatcherUnhandledException += (_, ex) => ex.Handled = true;
        Diagnostics.AddToPackage();

        if (EarlyStart.Sequence != null) Log.Info($"Sign-in start: WPF ready {SignIn.Since(EarlyStart.Reference)}");
        var args = e.Args;
        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
        var path = args.Length > 1 ? args[1] : null;

        // The family's troubleshooting lines: --diagnostics [folder], --diagnostics-mode on|off|<hours>.
        if (KitDiagnostics.TryRun(args))
        {
            Shutdown();
            return;
        }

        // From the setup's task "look for updates": --updates on|off.
        if (KitUpdate.TryRun(args))
        {
            Shutdown();
            return;
        }

        // The settings to and from a file: --export <file.zip>, --import <file.zip> (a running StartQueue reads them again).
        if (KitSettings.TryRun(args))
        {
            Shutdown();
            return;
        }

        try
        {
            switch (mode)
            {
                // Started through UAC by "Set up" under About, or by the uninstaller: the event log source.
                // StartQueue's own setup lines stay its own: it signs in through a scheduled task, not the Run key.
                case KitSetup.RegisterEventLogCommand or KitSetup.UnregisterEventLogCommand:
                    KitSetup.TryRun(args);
                    Shutdown();
                    return;
                case "--write-assets" when path != null:
                    Mark.WriteAssets(path);
                    Shutdown();
                    return;
                case "--scan-report" when path != null:
                    // Diagnostics: what the Windows autostart holds right now, without changing anything.
                    File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(NativeAutostart.Scan(), Json.Options));
                    Shutdown();
                    return;
                case "--elevated":
                    HelperModes.Elevated();
                    Shutdown();
                    return;
                case "--restore" when path != null:
                    Shutdown(HelperModes.Restore(Guid.Parse(path)) ? 0 : 1);
                    return;
                // Started through UAC by the services view: one change to one service.
                case "--service" when path != null && args.Length > 2:
                    Shutdown(Services.ApplyFromCommandLine(path, args[2]));
                    return;
                // Started through UAC: switches the automatic sign-in for prepared shutdowns on or back.
                case "--prepared-signin":
                    Shutdown(PreparedSignIn.ApplyFromCommandLine([.. args.Skip(1)]));
                    return;
                case "--restore-all":
                    HelperModes.RestoreAll();
                    Shutdown();
                    return;
                // Called by setup.exe and its uninstaller; --setup --admin also sets up the admin features, --new marks a
                // new installation, which changes nothing in Windows until the user switches it on.
                case "--setup":
                    UseSavedLanguage();
                    Setup.Install(admin: args.Skip(1).Contains("--admin", StringComparer.OrdinalIgnoreCase),
                        isNew: args.Skip(1).Contains("--new", StringComparer.OrdinalIgnoreCase));
                    Shutdown();
                    return;
                case "--setup-admin":
                    Setup.InstallAdmin();
                    Shutdown();
                    return;
                // "Set up fully" from the command line, as in the settings: one UAC prompt, then the Windows 11 menu.
                case "--setup-full":
                    UseSavedLanguage();
                    var full = KitSetup.SetUpFull();
                    if (full) Setup.SetUpContextMenu();
                    Shutdown(full ? 0 : 1);
                    return;
                case "--setup-menu":
                    UseSavedLanguage();
                    Setup.SetUpContextMenu();
                    Shutdown();
                    return;
                case "--prepare-update":
                    Setup.PrepareUpdate();
                    Shutdown();
                    return;
                case "--uninstall":
                    UseSavedLanguage();
                    Shutdown(Setup.Uninstall());
                    return;
                case "--uninstall-admin":
                    Setup.UninstallAdmin();
                    Shutdown();
                    return;
                case "--add" when path != null && Ipc.Send("add\n" + path):
                    Shutdown();
                    return;
                case "--snapshot" when path != null:
                    // Diagnostics: the running tray app draws its views into this folder.
                    // Optional: accent (startqueue, windows) and theme (system, light, dark) to draw with.
                    Shutdown(Ipc.Send(string.Join('\n', new[] { "snapshot", Path.GetFullPath(path) }.Concat(args.Skip(2)))) ? 0 : 1);
                    return;
                case "--perf" when path != null:
                    Shutdown(Ipc.Send("perf\n" + Path.GetFullPath(path)) ? 0 : 1);
                    return;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"{mode} failed", ex);
            Shutdown(1);
            return;
        }

        // Started "as administrator" (a setup run elevated, a right click): the tray app and every app it starts
        // would run elevated. Explorer starts it again with normal rights. Not when UAC is off: then there are none.
        if (mode is "" or "--show" && Setup.CanDropElevation)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Paths.Exe}\"") { UseShellExecute = false });
            Shutdown();
            return;
        }

        // At sign-in the early start holds the instance already.
        instance = EarlyStart.Instance ?? new SingleInstance(EarlyStart.InstanceName);
        if (!instance.IsFirst)
        {
            // Another instance owns the tray: hand the request over and leave. After an update nothing is asked of it.
            var message = mode switch
            {
                "--autostart" => "sweep",
                "--switch-on" => "switch-on",
                "--add" when path != null => "add\n" + path,
                KitUpdate.UpdatedArgument => null,
                _ => "show",
            };
            if (message != null) SingleInstance.HandOver(message);
            Shutdown();
            return;
        }

        KitDiagnostics.Start(args);
        // Siblings of the family may send these over the pipe; the tray takes them as from a second start.
        Family.Register("show", "add", "reload", "sweep", "autostart", "switch-on");
        System.Windows.Forms.Application.SetColorMode(System.Windows.Forms.SystemColorMode.System);
        host = new TrayHost(
            autostart: mode == "--autostart",
            showWindow: mode is "" or "--show",
            pendingAdd: mode == "--add" ? path : null);
        // After an update (--updated) only a note from the tray: no window, no startup order.
        KitUpdate.Start(args);
        // From the command line (deployment, tests), as the button at the end of the introduction.
        if (mode == "--switch-on") host.SwitchOnFromCommandLine();
        if (EarlyStart.Sequence != null) Log.Info($"Sign-in start: tray ready {SignIn.Since(EarlyStart.Reference)}");
    }

    /// <summary>Setup texts (the classic menu entry, messages) in the language chosen in StartQueue.</summary>
    static void UseSavedLanguage()
    {
        try
        {
            Strings.Choice = ConfigStore.Load().Settings.Language;
        }
        catch
        {
            // No list yet: Windows language.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var restart = host?.RestartRequested == true;
        if (host != null) KitDiagnostics.Stop();
        host?.Dispose();
        instance?.Dispose();
        // After setting up the admin features: the new instance sees them from the start.
        if (restart) Process.Start(new ProcessStartInfo(Paths.Exe, "--show") { UseShellExecute = false });
        base.OnExit(e);
    }
}

/// <summary>Short runs without a window: the elevated helper task and restoring entries to Windows.</summary>
static class HelperModes
{
    public static void Elevated()
    {
        // Someone is waiting for these: a change from the window that needs administrator rights (AdminHelper).
        AdminHelper.RunAll(Execute);

        foreach (var taskPath in PendingTaskTakeOvers.TakeAll())
        {
            try
            {
                TaskTakeOver.Apply(taskPath, adminOnly: true);
            }
            catch (Exception ex)
            {
                Log.Warning($"Taking over task {taskPath} as administrator failed: {ex.Message}");
            }
        }

        Sweeper.Run(elevated: true);

        var ids = PendingElevated.TakeAll();
        if (ids.Count > 0)
        {
            var config = ConfigStore.Load();
            foreach (var entry in ids.Select(id => config.Entries.FirstOrDefault(x => x.Id == id)).OfType<Entry>())
            {
                try
                {
                    Launcher.StartDirect(entry);
                    Log.Info($"Started \"{entry.Name}\" as administrator");
                }
                catch (Exception ex)
                {
                    Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{entry.Name}\" as administrator failed: {ex.Message}");
                }
            }
        }
        Ipc.Send("elevated-done");
    }

    /// <summary>One request to the admin helper: the same command line that would otherwise run through UAC.</summary>
    static int Execute(string[] args) => args switch
    {
        ["--restore", var id] => Restore(Guid.Parse(id)) ? 0 : 1,
        ["--prepared-signin", .. var rest] => PreparedSignIn.ApplyFromCommandLine(rest),
        ["--service", var action, var name] => Services.ApplyFromCommandLine(action, name),
        _ => 87,
    };

    /// <summary>Gives one entry back to the Windows autostart and keeps it there.</summary>
    public static bool Restore(Guid id)
    {
        var entry = ConfigStore.Load().Entries.FirstOrDefault(x => x.Id == id);
        if (entry?.Origin == null) return false;
        ReturnToWindows(entry);
        Ipc.Send("reload");
        return true;
    }

    public static void ReturnToWindows(Entry entry)
    {
        var key = entry.Origin!.Key;
        if (entry.StillInWindows)
        {
            // Shown before switching on and never taken: Windows has it already, it only leaves the order.
            ConfigStore.Update(c =>
            {
                c.Keep(key, KeepReason.LeftWithWindows);
                c.Entries.RemoveAll(x => x.Id == entry.Id);
                c.RemoveFromProfiles(entry.Id);
            });
            Log.Info($"Left \"{entry.Name}\" with Windows before switching on ({key})");
            return;
        }
        // Keep-list first, so the watcher does not take the entry straight back.
        ConfigStore.Update(c => c.Keep(key, KeepReason.ReturnedByUser));
        try
        {
            NativeAutostart.Restore(entry);
        }
        catch
        {
            ConfigStore.Update(c => c.Unkeep(key));
            throw;
        }
        ConfigStore.Update(c => c.Entries.RemoveAll(x => x.Id == entry.Id));
        Log.Event(Events.EntryReturned, LogLevel.Info, $"Returned \"{entry.Name}\" to Windows ({key})");
    }

    /// <summary>Used by uninstall.ps1: every taken-over entry goes back where it came from.</summary>
    public static void RestoreAll()
    {
        // One still with Windows (StartQueue was never switched on) is there already; restoring it would add it twice.
        foreach (var entry in ConfigStore.Load().Entries.Where(e => e.Origin != null && !e.StillInWindows))
        {
            try
            {
                NativeAutostart.Restore(entry);
                ConfigStore.Update(c => c.Entries.RemoveAll(x => x.Id == entry.Id));
                Log.Info($"Restored \"{entry.Name}\" for uninstall");
            }
            catch (Exception ex)
            {
                Log.Event(Events.ReturnFailed, LogLevel.Warning, $"Restoring \"{entry.Name}\" failed: {ex.Message}");
            }
        }
    }
}
