namespace StartQueue.Core;

public static class Launcher
{
    /// <summary>Starts all enabled entries top to bottom. Records progress after every entry.</summary>
    public static LastRun RunSequence(Config config, DateTime reference, Action<LastRun>? progress = null, bool atSignIn = false)
    {
        var settings = config.Settings;
        var waitLimit = Math.Clamp(settings.WaitLimitSeconds, 5, 600);
        var run = new LastRun { StartedAt = reference, AtSignIn = atSignIn };
        // The profile that applies now decides what starts; without one the entries' own switches do.
        var profile = Profiles.Active(DateTime.Now);
        var starting = config.Entries.Where(e => StartsBy(config, e, profile)).ToList();
        if (!config.SwitchedOn) Log.Info("Startup sequence: StartQueue is not switched on yet, Windows starts everything as before");
        Log.Info(FormattableString.Invariant($"Startup sequence: {starting.Count} apps{(profile == null ? "" : $", profile \"{profile.Name}\"")}, {(DateTime.Now - reference).TotalSeconds:0.0} s after the start reference"));

        if (settings.StartDelaySeconds > 0) Thread.Sleep(TimeSpan.FromSeconds(Math.Min(settings.StartDelaySeconds, 600)));

        var first = true;
        foreach (var entry in starting)
        {
            var delay = entry.DelaySeconds + (first ? 0 : Math.Max(0, settings.GapSeconds));
            first = false;
            if (delay > 0) Thread.Sleep(TimeSpan.FromSeconds(delay));

            var item = new LastRunItem { Id = entry.Id, Name = entry.Name };
            try
            {
                if (IsRunning(entry))
                {
                    item.Outcome = RunOutcome.WasRunning;
                }
                else if (!entry.WaitUntilRunning)
                {
                    // Started in the order of the list, but nobody waits for the start to come back: a shortcut,
                    // a task or a Store app can take many seconds for that at sign-in, and the rest would wait
                    // with it. Each start gets a thread of its own, so a slow one holds up nothing.
                    StartAside(entry, item, run);
                }
                else
                {
                    var startedAt = DateTime.Now;
                    item.Outcome = Start(entry);
                    // Window to the background or minimized if StartQueue does that, alongside the rest of the sequence.
                    StartWindow.Track(entry, startedAt);
                    if (entry.WaitUntilRunning && !WaitRunning(entry, waitLimit)) item.NotDetectedAfter = waitLimit;
                }
            }
            catch (AdminTaskMissingException ex)
            {
                item.Outcome = RunOutcome.AdminTaskMissing;
                Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{entry.Name}\" failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                item.Outcome = RunOutcome.Failed;
                item.Error = ex.Message;
                Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{entry.Name}\" failed: {ex.Message}");
            }

            item.Seconds = Math.Round((DateTime.Now - reference).TotalSeconds, 1);
            lock (run)
            {
                run.Items.Add(item);
                LastRunStore.Save(run);
            }
            progress?.Invoke(run);
        }

        Log.Info(FormattableString.Invariant($"Startup sequence done,{(DateTime.Now - reference).TotalSeconds:0.0} s after the start reference"));
        return run;
    }

    /// <summary>
    /// Starts the entry on a thread of its own and returns at once. The item says "started" right away; a start
    /// that fails later says so in the last run.
    /// </summary>
    static void StartAside(Entry entry, LastRunItem item, LastRun run)
    {
        item.Outcome = entry.RunElevated && !Elevation.IsElevated ? RunOutcome.StartedAsAdmin : RunOutcome.Started;
        var startedAt = DateTime.Now;
        new Thread(() =>
        {
            try
            {
                Start(entry);
                StartWindow.Track(entry, startedAt);
            }
            catch (Exception ex)
            {
                Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{entry.Name}\" failed: {ex.Message}");
                lock (run)
                {
                    item.Outcome = ex is AdminTaskMissingException ? RunOutcome.AdminTaskMissing : RunOutcome.Failed;
                    item.Error = ex.Message;
                    LastRunStore.Save(run);
                }
            }
        }) { IsBackground = true, Name = $"Start {entry.Name}" }.Start();
    }

    /// <summary>
    /// Whether StartQueue starts the entry by itself (at sign-in, again and again) in this profile, or none. Never before
    /// it is switched on, and never an entry Windows still starts (<see cref="Entry.StillInWindows"/>).
    /// </summary>
    public static bool StartsBy(Config config, Entry entry, FamilyProfile? profile) =>
        config.SwitchedOn && !entry.StillInWindows && Profiles.IsOn(config, entry, profile);

    /// <summary>Starts one entry. Entries marked "as administrator" go through the elevated helper task.</summary>
    public static RunOutcome Start(Entry entry)
    {
        if (entry.RunElevated && !Elevation.IsElevated)
        {
            PendingElevated.Add(entry.Id);
            if (!Elevation.RunElevatedTask()) throw new AdminTaskMissingException();
            return RunOutcome.StartedAsAdmin;
        }

        try
        {
            StartDirect(entry);
        }
        catch (UnauthorizedAccessException) when (entry.Kind == EntryKind.ScheduledTask && Elevation.AdminMode && !Elevation.IsElevated)
        {
            // A task only an administrator may run: hand it to the admin task.
            PendingElevated.Add(entry.Id);
            if (!Elevation.RunElevatedTask()) throw new AdminTaskMissingException();
        }
        return Elevation.IsElevated ? RunOutcome.StartedAsAdmin : RunOutcome.Started;
    }

    public static void StartDirect(Entry entry)
    {
        switch (entry.Kind)
        {
            case EntryKind.PackagedApp:
                if (string.IsNullOrEmpty(entry.PackageExecutable) ||
                    !PackagedApps.StartInPackage(entry.Target, entry.PackageExecutable, StartCommands.Arguments(entry, entry.Arguments)))
                    PackagedApps.Activate(entry.Target, StartCommands.Arguments(entry, entry.Arguments));
                break;
            // With own start parameters the program behind a shortcut or task is started directly: neither takes parameters from outside.
            case EntryKind.Shortcut or EntryKind.ScheduledTask when StartCommands.HasOwn(entry) && StartCommands.Original(entry) is { } behind:
                Run(behind with { Arguments = StartCommands.Arguments(entry, behind.Arguments) });
                break;
            case EntryKind.Shortcut:
                using (Process.Start(new ProcessStartInfo(entry.Target) { UseShellExecute = true })) { }
                break;
            case EntryKind.ScheduledTask:
                TaskAutostart.Run(entry.Target);
                break;
            default:
                Run(new StartCommand(Environment.ExpandEnvironmentVariables(entry.Target), StartCommands.Arguments(entry, entry.Arguments ?? ""), entry.WorkingDirectory));
                break;
        }
    }

    static void Run(StartCommand command)
    {
        var info = new ProcessStartInfo(command.Program, command.Arguments) { UseShellExecute = true };
        var folder = string.IsNullOrWhiteSpace(command.WorkingDirectory) ? Path.GetDirectoryName(command.Program) : Environment.ExpandEnvironmentVariables(command.WorkingDirectory);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) info.WorkingDirectory = folder;
        using (Process.Start(info)) { }
    }

    // Hosts that run all the time or for many things; their presence says nothing about the entry.
    internal static readonly HashSet<string> GenericHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "conhost", "cmd", "powershell", "pwsh", "wscript", "cscript", "explorer", "rundll32", "msedge", "msedge_proxy", "Update",
    };

    public static bool IsRunning(Entry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ProcessName) || GenericHosts.Contains(entry.ProcessName)) return false;
        var session = Process.GetCurrentProcess().SessionId;
        var processes = Process.GetProcessesByName(entry.ProcessName);
        try
        {
            return processes.Any(p => p.SessionId == session);
        }
        finally
        {
            foreach (var p in processes) p.Dispose();
        }
    }

    static bool WaitRunning(Entry entry, int limitSeconds)
    {
        if (string.IsNullOrWhiteSpace(entry.ProcessName)) return false;
        var until = DateTime.Now.AddSeconds(limitSeconds);
        while (DateTime.Now < until)
        {
            if (IsRunning(entry)) return true;
            Thread.Sleep(500);
        }
        return false;
    }
}
