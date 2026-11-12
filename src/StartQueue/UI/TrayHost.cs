using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>The running app: tray icon, watcher, sweeps, the logon sequence and the window.</summary>
public sealed class TrayHost : IDisposable
{
    readonly Dispatcher dispatcher = Application.Current.Dispatcher;
    readonly KitTray tray;
    readonly HashSet<Guid> notified;
    readonly Ipc.Server ipc;
    readonly Watcher watcher;
    readonly FileSystemWatcher dataWatcher;
    readonly DispatcherTimer reloadDebounce;
    readonly DispatcherTimer periodic;
    readonly DispatcherTimer repeatTimer;

    MainWindow? window;
    Config config = new();
    LastRun? lastRun;
    SweepResult? lastSweep;
    bool sweeping, sweepAgain;
    DateTime elevatedQuietUntil = DateTime.MinValue;

    public bool Exiting { get; private set; }
    public WindowsReport? Report { get; private set; }
    public Config Config => config;
    public LastRun? LastRun => lastRun;
    public SweepResult? LastSweep => lastSweep;

    public TrayHost(bool autostart, bool showWindow, string? pendingAdd)
    {
        Directory.CreateDirectory(Paths.Data);
        config = SafeLoad() ?? new Config();
        StartSwitches.Learn(config);
        lastRun = LastRunStore.Load();
        Strings.Choice = config.Settings.Language;
        Theme.Apply(config.Settings.Theme, config.Settings.Accent.ToKit());
        CleanUpShortcuts();
        notified = config.Entries.Where(e => e.IsNew).Select(e => e.Id).ToHashSet();
        IntroMode = config.Settings.NewEntries;
        KitIntro.SwitchedOn += KitSwitchedOn;
        // Siblings read this to know that StartQueue starts them; also after an update from a version without it.
        FamilyApps.Publish(config);

        // The family's tray icon: "Open StartQueue" first, "Quit" last, StartQueue's own entries between.
        tray = new KitTray(() => ShowWindow(), Exit);
        tray.NotifyClicked += () => ShowWindow(selectFirstNew: true);
        KitUpdate.Notify = tray.Notify;
        // After an import or restore under About, "Backup and restore".
        KitSettings.Reload = ReloadSettings;
        // The profiles are the family's: another app may change them while StartQueue runs.
        FamilyProfiles.Changed += FamilyProfilesChanged;
        FamilyProfiles.Watch();
        AddMenuEntries();
        UpdateTray();

        reloadDebounce = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) =>
        {
            reloadDebounce!.Stop();
            Reload();
        }, dispatcher);
        reloadDebounce.Stop();

        dataWatcher = new FileSystemWatcher(Paths.Data) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
        FileSystemEventHandler onData = (_, e) =>
        {
            if (e.Name is "config.json" or "last-run.json") dispatcher.BeginInvoke(() => { reloadDebounce.Stop(); reloadDebounce.Start(); });
        };
        dataWatcher.Changed += onData;
        dataWatcher.Created += onData;
        dataWatcher.Renamed += (s, e) => onData(s, e);
        dataWatcher.EnableRaisingEvents = true;

        ipc = new Ipc.Server(message => dispatcher.BeginInvoke(() => Handle(message)));
        watcher = new Watcher(() => dispatcher.BeginInvoke(() => _ = SweepAsync()));
        periodic = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        periodic.Tick += (_, _) => _ = SweepAsync();
        repeatTimer = new DispatcherTimer(TimeSpan.FromSeconds(30), DispatcherPriority.Background, (_, _) => RunRepeats(), dispatcher);
        ApplyScanInterval();
        // A change of Windows' color mode or accent color while StartQueue runs: Theme.Apply above follows it by itself
        // since the kit's 731f146, the window's colors and the Fluent fields together.

        if (autostart) _ = LogonAsync();
        else _ = SweepAsync();

        if (showWindow) ShowWindow();
        if (pendingAdd != null) AddPath(pendingAdd);
    }

    // ---------- Messages from other StartQueue processes ----------

    void Handle(string message)
    {
        var parts = message.Split('\n', 2);
        switch (parts[0])
        {
            case "show":
                ShowWindow();
                break;
            case "add" when parts.Length == 2:
                AddPath(parts[1].Trim());
                break;
            case "reload":
                Reload();
                break;
            // Sent by --import from the command line, after the settings were replaced.
            case KitSettings.ReloadMessage:
                ReloadSettings();
                break;
            case "sweep":
                _ = SweepAsync();
                break;
            case "switch-on":
                SwitchOnFromCommandLine();
                break;
            // From a family app's own "start with Windows" switch: <app> and on|off.
            case "autostart" when parts.Length == 2:
                var lines = parts[1].Split('\n', StringSplitOptions.TrimEntries);
                if (lines.Length == 2 && Family.Find(lines[0]) is { } app && !app.Name.Equals(KitApp.Name, StringComparison.OrdinalIgnoreCase))
                    Change(c => FamilyApps.Set(c, app, lines[1].Equals("on", StringComparison.OrdinalIgnoreCase)));
                break;
            case "elevated-done":
                // The admin helper ran; do not call it again for a while unless asked.
                elevatedQuietUntil = DateTime.Now.AddMinutes(30);
                _ = SweepAsync();
                break;
            case "snapshot" when parts.Length == 2:
                _ = SnapshotAsync(parts[1].Split('\n', StringSplitOptions.TrimEntries));
                break;
            case "perf" when parts.Length == 2:
                _ = new MainWindow(this).PerfAsync(parts[1].Trim());
                break;
        }
    }

    /// <summary>
    /// Diagnostics: a second, invisible window draws each view into PNG files. Optionally with another accent
    /// and theme; colors are app-wide, so the saved ones come back afterwards.
    /// </summary>
    async Task SnapshotAsync(string[] args)
    {
        var accent = args.Length > 1 && Enum.TryParse<AccentChoice>(args[1], true, out var a) ? a : config.Settings.Accent;
        var theme = args.Length > 2 && Enum.TryParse<ThemeChoice>(args[2], true, out var t) ? t : config.Settings.Theme;
        var language = args.Length > 3 && Enum.TryParse<LanguageChoice>(args[3], true, out var l) ? l : config.Settings.Language;
        Theme.Apply(theme, accent.ToKit());
        // The language is app-wide too: whatever the open window builds meanwhile comes out in it.
        Strings.Choice = language;
        try
        {
            // "x2" draws at twice the pixels, for pictures that are shown larger, as in a video.
            var scale = args.FirstOrDefault(a => a.Length == 2 && a[0] == 'x' && char.IsDigit(a[1])) is { } x ? x[1] - '0' : 1;
            await new MainWindow(this) { SnapshotScale = scale }.SnapshotAsync(args[0], args.Contains("maximize", StringComparer.OrdinalIgnoreCase),
                args.Contains("colors", StringComparer.OrdinalIgnoreCase));
            // The tray menu, drawn without opening it (the kit's KitTray).
            try
            {
                tray.SaveMenuPicture(Path.Combine(args[0], "tray.png"), scale);
            }
            catch (Exception ex)
            {
                Log.Warning($"Snapshot of the tray menu failed: {ex.Message}");
            }
        }
        finally
        {
            Strings.Choice = config.Settings.Language;
            Theme.Apply(config.Settings.Theme, config.Settings.Accent.ToKit());
            if (language != config.Settings.Language)
            {
                // Texts made during the snapshot (header, tabs, task triggers) were in the other language.
                RebuildWindowQuietly();
                _ = RefreshReportAsync();
            }
        }
    }

    /// <summary>Builds the window again in the current language, in the same place and stacking order, without activating it.</summary>
    void RebuildWindowQuietly()
    {
        var old = window;
        if (old == null) return;
        window = null;
        if (!old.IsVisible)
        {
            old.CloseForGood();
            return;
        }
        var bounds = old.WindowState == WindowState.Normal ? new Rect(old.Left, old.Top, old.Width, old.Height) : old.RestoreBounds;
        var fresh = new MainWindow(this)
        {
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = old.WindowState,
        };
        window = fresh;
        fresh.Sync();
        fresh.Show();
        // Right below the old window, so it takes the old one's place instead of popping up in front.
        var oldHandle = new System.Windows.Interop.WindowInteropHelper(old).Handle;
        var freshHandle = new System.Windows.Interop.WindowInteropHelper(fresh).Handle;
        SetWindowPos(freshHandle, oldHandle, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        old.CloseForGood();
    }

    const uint SwpNoSize = 0x1, SwpNoMove = 0x2, SwpNoActivate = 0x10;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    // ---------- Logon, sweeps, reload ----------

    async Task LogonAsync()
    {
        // The sequence starts right away. A full scan right after sign-in takes half a minute while the PC is
        // busy, so it runs afterwards; apps it takes over (new since the last session) start right after it.
        // Usually the early start has begun the order already, before the tray existed; otherwise it starts here.
        var reference = EarlyStart.Reference;
        var run = EarlyStart.Sequence is { } early ? await early : null;
        if (run == null)
        {
            var snapshot = SafeLoad() ?? config;
            run = await Task.Run(() => Launcher.RunSequence(snapshot, reference, atSignIn: true));
        }
        var started = run.Items.Select(i => i.Id).ToHashSet();
        ReportPreparedStart(reference);
        await SweepAsync();
        var profile = Profiles.Active(DateTime.Now);
        foreach (var late in config.Entries.Where(e => !started.Contains(e.Id) && e.AddedAt >= reference && Launcher.StartsBy(config, e, profile)).ToList())
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (Launcher.IsRunning(late)) return;
                    Launcher.Start(late);
                    Log.Info($"Started \"{late.Name}\", taken over at this sign-in");
                }
                catch (Exception ex)
                {
                    Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{late.Name}\" failed: {ex.Message}");
                }
            });
        }
        Reload();
        if (config.Settings.NotifyAfterSequence && run.Items.Count > 0)
        {
            var failed = run.Items.Where(i => i.Failed).ToList();
            Notify(Strings.NotifySequenceTitle,
                Strings.F(Strings.NotifySequenceText, run.Items.Count, run.Items.Max(i => i.Seconds)) +
                (failed.Count > 0 ? Strings.F(Strings.NotifySequenceFailed, failed.Count, string.Join(", ", failed.Select(i => i.Name))) : ""));
        }

        // Many apps put themselves back into the autostart when they start (or switch their task's sign-in start
        // on again). One more scan once they are up catches that, with the admin task if needed.
        await Task.Delay(TimeSpan.FromMinutes(2));
        await SweepAsync(userTriggered: true);
    }

    DateTime tasksBackScanAfter;

    public async Task SweepAsync(bool userTriggered = false)
    {
        if (sweeping)
        {
            sweepAgain = true;
            return;
        }
        sweeping = true;
        try
        {
            do
            {
                sweepAgain = false;
                SweepResult result;
                try
                {
                    result = await Task.Run(() => Sweeper.Run(Elevation.IsElevated));
                }
                catch (Exception ex)
                {
                    Log.Warning($"Scan failed: {ex.Message}");
                    if (userTriggered) Notify(Strings.ScanFailed, ex.Message);
                    return;
                }

                lastSweep = result;
                if (result.NewlyKept.Count > 0 && config.Settings.NotifyNewEntries)
                    Notify(Strings.NotifyKeptTitle, Strings.F(Strings.NotifyKeptText, string.Join(", ", result.NewlyKept)));
                if (result.MachineScanNeeded && (userTriggered || DateTime.Now > elevatedQuietUntil))
                {
                    elevatedQuietUntil = DateTime.Now.AddMinutes(1);
                    _ = Task.Run(Elevation.RunElevatedTask);
                }
                Reload();
            } while (sweepAgain);
        }
        finally
        {
            sweeping = false;
        }
    }

    string? loadedState;

    public void Reload()
    {
        var fresh = SafeLoad();
        if (fresh == null) return;
        var freshRun = LastRunStore.Load();
        // Sweeps and file events often bring nothing new; then the window keeps what it shows.
        var state = System.Text.Json.JsonSerializer.Serialize(new object?[] { fresh, freshRun, lastSweep?.Leftovers }, Json.Options);
        if (state == loadedState)
        {
            if (window?.IsVisible == true) _ = RefreshReportAsync();
            return;
        }
        loadedState = state;
        config = fresh;
        StartSwitches.Learn(config);
        lastRun = freshRun;

        var newOnes = config.Entries.Where(e => e.IsNew && !notified.Contains(e.Id)).ToList();
        foreach (var entry in newOnes) notified.Add(entry.Id);
        if (!config.Settings.NotifyNewEntries) newOnes.Clear();
        if (newOnes.Count == 1)
            Notify(Strings.NotifyNewTitle,
                Strings.F(Strings.NotifyNewText, newOnes[0].Name));
        else if (newOnes.Count > 1)
            Notify(Strings.F(Strings.NotifyManyTitle, newOnes.Count),
                Strings.F(Strings.NotifyManyText, string.Join(", ", newOnes.Select(e => e.Name))));

        UpdateTray();
        window?.Sync();
        // While the window is open, the Windows autostart list stays current, whichever tab is shown.
        if (window?.IsVisible == true) _ = RefreshReportAsync();
    }

    bool reporting;

    /// <summary>Reads what Windows still starts by itself, for the Windows autostart view.</summary>
    public async Task RefreshReportAsync()
    {
        if (reporting) return;
        reporting = true;
        try
        {
            var snapshot = config;
            Report = await Task.Run(() => WindowsAutostart.Analyze(snapshot));
            window?.Sync();
            // One of our tasks starts at sign-in again: switch it off now, not at the next full scan.
            // At most every two minutes, in case it cannot be switched off (a task that needs admin rights in user mode).
            if (Report.TasksBack > 0 && DateTime.Now > tasksBackScanAfter)
            {
                tasksBackScanAfter = DateTime.Now.AddMinutes(2);
                _ = SweepAsync(userTriggered: true);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading the Windows autostart failed: {ex.Message}");
        }
        finally
        {
            reporting = false;
        }
    }

    /// <summary>Moves entries from the Windows autostart into StartQueue, from the Windows autostart view.</summary>
    public async Task TakeOverFromWindowsAsync(IReadOnlyCollection<WindowsItem> items)
    {
        if (!config.SwitchedOn)
        {
            // Before switching on: into the preview; Windows keeps starting it until then.
            Change(c =>
            {
                foreach (var item in items.Where(i => i.Status != WindowsItemStatus.MachineLocked)) c.Unkeep(item.Item.Origin.Key);
            }, reloadWindow: false);
            await SweepAsync();
            await RefreshReportAsync();
            return;
        }
        var viaAdminTask = false;
        foreach (var item in items.Where(i => i.Status != WindowsItemStatus.MachineLocked))
        {
            var native = item.Item;
            var key = native.Origin.Key;
            if (native.Origin.NeedsAdmin && !Elevation.IsElevated)
            {
                // Off the keep list; the admin task takes it over on its next run. Marked as expected, so it
                // does not show up as an entry that added itself.
                Change(c =>
                {
                    c.Unkeep(key);
                    if (!c.ExpectedTakeOvers.Contains(key, StringComparer.OrdinalIgnoreCase)) c.ExpectedTakeOvers.Add(key);
                }, reloadWindow: false);
                viaAdminTask = true;
                continue;
            }
            Change(c =>
            {
                c.Unkeep(key);
                var entry = NativeAutostart.TakeOver(native);
                c.Entries.Insert(entry.Enabled ? c.Entries.FindLastIndex(e => e.Enabled) + 1 : c.Entries.Count, entry);
                c.AddToProfiles(entry);
                Log.Event(Events.EntryTakenOver, LogLevel.Info, $"Took over {key} from the Windows autostart view");
            }, reloadWindow: false);
        }
        if (viaAdminTask)
        {
            elevatedQuietUntil = DateTime.Now.AddMinutes(1);
            await Task.Run(Elevation.RunElevatedTask);
        }
        Reload();
        await RefreshReportAsync();
    }

    /// <summary>Takes over a scheduled task from the Windows autostart view.</summary>
    public async Task TakeOverTaskAsync(TaskInfo task)
    {
        if (!config.SwitchedOn)
        {
            // Before switching on: into the preview, its sign-in start stays on until then.
            await Task.Run(() => TaskTakeOver.Preview(task.Path));
            Reload();
            await RefreshReportAsync();
            return;
        }
        try
        {
            await Task.Run(() => TaskTakeOver.Apply(task.Path, adminOnly: false));
        }
        catch (UnauthorizedAccessException) when (Elevation.AdminMode)
        {
            PendingTaskTakeOvers.Add(task.Path);
            elevatedQuietUntil = DateTime.Now.AddMinutes(1);
            await Task.Run(Elevation.RunElevatedTask);
        }
        catch (UnauthorizedAccessException)
        {
            if (window != null) await window.ShowMessageAsync(Strings.TaskTakeOverFailed, Strings.TaskNeedsAdmin);
        }
        catch (Exception ex)
        {
            Log.Event(Events.TakeOverFailed, LogLevel.Warning, $"Taking over task {task.Path} failed: {ex.Message}");
            if (window != null) await window.ShowMessageAsync(Strings.TaskTakeOverFailed, ex.Message);
        }
        Reload();
        await RefreshReportAsync();
    }

    Task<Suggestions.AppFacts>? appFacts;
    static readonly TimeSpan AppFactsLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Reads the installed apps ahead, so the add dialog opens at once. Kept for a few minutes.</summary>
    public void PrefetchApps()
    {
        var stale = appFacts == null || appFacts.IsFaulted ||
                    (appFacts.IsCompletedSuccessfully && DateTime.Now - appFacts.Result.At > AppFactsLifetime);
        if (stale) appFacts = Task.Run(Suggestions.Gather);
    }

    public async Task<List<AppCandidate>> LoadCandidatesAsync()
    {
        PrefetchApps();
        var snapshot = config;
        try
        {
            var facts = await appFacts!;
            return await Task.Run(() => Suggestions.Build(snapshot, facts));
        }
        catch (Exception ex)
        {
            Log.Warning($"Building suggestions failed: {ex.Message}");
            return new List<AppCandidate>();
        }
    }

    /// <summary>Adds an app from the add dialog. Desktop apps come in through their Start menu shortcut.</summary>
    public bool AddCandidate(AppCandidate candidate)
    {
        try
        {
            var entry = candidate.LinkPath.Length > 0 && File.Exists(candidate.LinkPath)
                ? EntryFactory.FromPath(candidate.LinkPath)
                : EntryFactory.FromStartApp(candidate.Name, candidate.Id);
            if (entry.Kind == EntryKind.Shortcut) entry.Name = candidate.Name;
            return AddEntry(entry, select: false);
        }
        catch (Exception ex)
        {
            Log.Warning($"Adding {candidate.Name} failed: {ex.Message}");
            return false;
        }
    }

    static Config? SafeLoad()
    {
        try
        {
            return ConfigStore.Load();
        }
        catch (Exception ex)
        {
            // The event log and the About page heard of it once, through KitSettings.ReportUnreadable.
            Log.Warning($"Loading config failed: {ex.Message}");
            return null;
        }
    }

    // ---------- Actions from the window ----------

    public void SaveEntry(Entry entry)
    {
        var copy = entry.Clone();
        Change(c =>
        {
            var index = c.Entries.FindIndex(x => x.Id == copy.Id);
            if (index < 0) return;
            // Taken over meanwhile (switched on while the window held an older copy): where it lives now stays.
            var stored = c.Entries[index];
            if (stored.StillInWindows != copy.StillInWindows)
            {
                copy.StillInWindows = stored.StillInWindows;
                copy.Origin = stored.Origin;
                if (stored.Kind == EntryKind.Shortcut) copy.Target = stored.Target;
            }
            c.Entries[index] = copy;
        }, reloadWindow: false);
        UpdateTray();
    }

    public void Reorder(IReadOnlyList<Guid> order)
    {
        Change(c =>
        {
            var byId = c.Entries.ToDictionary(e => e.Id);
            c.Entries = order.Where(byId.ContainsKey).Select(id => byId[id])
                .Concat(c.Entries.Where(e => !order.Contains(e.Id))).ToList();
        }, reloadWindow: false);
    }

    public void AddPath(string path)
    {
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException(Strings.FileMissing, path);
            AddEntry(EntryFactory.FromPath(path));
        }
        catch (Exception ex)
        {
            Log.Warning($"Adding {path} failed: {ex.Message}");
            ShowWindow();
            _ = window!.ShowMessageAsync(Strings.CouldNotAdd, ex.Message);
        }
    }

    /// <summary>Something dropped on the window: a file, or an app from the Start menu.</summary>
    public void AddDropped(string name, string id)
    {
        if (File.Exists(id))
        {
            AddPath(id);
            return;
        }
        try
        {
            AddEntry(EntryFactory.FromStartApp(name, id));
        }
        catch (Exception ex)
        {
            Log.Warning($"Adding dropped {id} failed: {ex.Message}");
        }
    }

    public bool AddEntry(Entry entry, bool select = true)
    {
        var current = SafeLoad() ?? config;
        var existing = current.Entries.FirstOrDefault(e => EntryFactory.SameApp(e, entry));
        if (existing != null)
        {
            if (entry.Kind == EntryKind.Shortcut && Paths.IsInside(entry.Target, Paths.Shortcuts)) File.Delete(entry.Target);
            if (select)
            {
                ShowWindow(select: existing.Id);
                Notify(Strings.AlreadyListedTitle, Strings.F(Strings.AlreadyListedText, existing.Name));
            }
            return false;
        }
        Change(c =>
        {
            c.Entries.Insert(c.Entries.FindLastIndex(e => e.Enabled) + 1, entry);
            c.AddToProfiles(entry);
        });
        Log.Info($"Added \"{entry.Name}\" by hand");
        if (select) ShowWindow(select: entry.Id);
        return true;
    }

    /// <summary>
    /// Removes an entry and returns how to undo it. A shortcut copy stays on disk so undo is complete;
    /// copies nothing refers to anymore are cleaned up at the next start.
    /// </summary>
    public Action? Remove(Guid id)
    {
        var index = config.Entries.FindIndex(e => e.Id == id);
        if (index < 0) return null;
        var entry = config.Entries[index].Clone();
        Change(c => c.Entries.RemoveAll(e => e.Id == id));
        // The grace period before the cleanup starts now, not when the shortcut was first made.
        if (Paths.IsInside(entry.Target, Paths.Shortcuts) && File.Exists(entry.Target)) File.SetLastWriteTime(entry.Target, DateTime.Now);
        Log.Info($"Removed \"{entry.Name}\"");
        return () =>
        {
            Change(c =>
            {
                if (c.Entries.Any(e => e.Id == entry.Id)) return;
                c.Entries.Insert(Math.Min(index, c.Entries.Count), entry);
            });
            Log.Info($"Undid removing \"{entry.Name}\"");
        };
    }

    /// <summary>Deletes shortcut copies no entry refers to anymore, a day after they were last needed.</summary>
    void CleanUpShortcuts()
    {
        try
        {
            if (!Directory.Exists(Paths.Shortcuts)) return;
            var used = new HashSet<string>(config.Entries.Select(e => e.Target), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(Paths.Shortcuts))
                if (!used.Contains(file) && DateTime.Now - File.GetLastWriteTime(file) > TimeSpan.FromDays(1))
                    File.Delete(file);
        }
        catch (Exception ex)
        {
            Log.Warning($"Cleaning up shortcuts failed: {ex.Message}");
        }
    }

    public void ReturnToWindows(Entry entry)
    {
        if (entry.Origin == null) return;
        if (entry.Origin.NeedsAdmin && !Elevation.IsElevated && !entry.StillInWindows)
        {
            // Set up fully: through the admin helper, without asking Windows. In Basis through UAC.
            var done = AdminHelper.Available
                ? AdminHelper.Run(["--restore", entry.Id.ToString()]) == 0
                : Elevation.RunSelfElevated($"--restore {entry.Id}");
            if (!done) return;
        }
        else
        {
            HelperModes.ReturnToWindows(entry);
        }
        Reload();
    }

    public void TakeOverKept(string key)
    {
        Change(c => c.Unkeep(key));
        _ = SweepAsync(userTriggered: true);
    }

    public void MarkAllSeen()
    {
        Change(c => c.Entries.ForEach(e => e.IsNew = false));
    }

    public void StartNow(Entry entry)
    {
        Task.Run(() =>
        {
            try
            {
                Launcher.Start(entry);
            }
            catch (Exception ex)
            {
                var reason = ex is AdminTaskMissingException ? Strings.AdminTaskMissing : ex.Message;
                dispatcher.BeginInvoke(() => Notify(Strings.F(Strings.CouldNotStart, entry.Name), reason));
            }
        });
    }

    public void RunSequenceNow()
    {
        var snapshot = SafeLoad() ?? config;
        var reference = DateTime.Now;
        Task.Run(() => Launcher.RunSequence(snapshot, reference)).ContinueWith(_ => dispatcher.BeginInvoke(Reload));
    }

    void Change(Action<Config> change, bool reloadWindow = true)
    {
        try
        {
            config = ConfigStore.Update(change);
            StartSwitches.Learn(config);
        }
        catch (Exception ex)
        {
            Log.Error("Saving failed", ex, eventId: Events.SaveFailed);
            Notify(Strings.CouldNotSave, ex.Message);
            return;
        }
        UpdateTray();
        if (reloadWindow) window?.Sync();
    }

    /// <summary>Changes the settings; theme, language and scan interval take effect right away.</summary>
    public void UpdateSettings(Action<Settings> change)
    {
        var before = config.Settings;
        Change(c => change(c.Settings), reloadWindow: false);
        var after = config.Settings;

        if (after.Theme != before.Theme || after.Accent != before.Accent) Theme.Apply(after.Theme, after.Accent.ToKit());
        if (after.ScanIntervalMinutes != before.ScanIntervalMinutes) ApplyScanInterval();
        if (after.Language != before.Language) LanguageChanged(rebuildWindow: true);
    }

    void LanguageChanged(bool rebuildWindow)
    {
        Strings.Choice = config.Settings.Language;
        tray.Rebuild();
        AddMenuEntries();
        UpdateTray();
        // Trigger and interval texts of scheduled tasks are made while reading them.
        _ = RefreshReportAsync();
        Setup.RelabelClassicMenu();
        if (!rebuildWindow || window == null) return;
        // Texts are read when a window is built, so the window is built again, once the
        // settings control that triggered this has finished its own update.
        dispatcher.BeginInvoke(() =>
        {
            var old = window;
            window = null;
            ShowWindow();
            window?.ShowSettings();
            old?.CloseForGood();
        });
    }

    /// <summary>
    /// After an import or a restore (About, "Backup and restore", or --import): the list and every setting as if
    /// StartQueue had just started with them. Entries of another PC stay as they are until the user changes them.
    /// </summary>
    void ReloadSettings()
    {
        var before = config.Settings;
        loadedState = null;
        Reload();
        var after = config.Settings;
        Theme.Apply(after.Theme, after.Accent.ToKit());
        ApplyScanInterval();
        FamilyApps.Publish(config);
        if (after.Language != before.Language) LanguageChanged(rebuildWindow: window?.IsVisible == true);
    }

    void ApplyScanInterval()
    {
        periodic.Stop();
        var minutes = config.Settings.ScanIntervalMinutes;
        if (minutes <= 0) return;
        periodic.Interval = TimeSpan.FromMinutes(minutes);
        periodic.Start();
    }

    // ---------- Window and tray ----------

    public void ShowWindow(Guid? select = null, bool selectFirstNew = false)
    {
        window ??= new MainWindow(this);
        window.Sync();
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        var target = select ?? (selectFirstNew ? config.Entries.FirstOrDefault(e => e.IsNew)?.Id : null);
        if (target != null) window.Select(target.Value);
        // Once the window has drawn, read in the background what the other views and the add dialog show,
        // so they are ready when their tab is opened.
        window.Dispatcher.BeginInvoke(PrefetchApps, DispatcherPriority.ApplicationIdle);
        window.Dispatcher.BeginInvoke(() =>
        {
            _ = RefreshReportAsync();
            _ = window?.PreloadServicesAsync();
        }, DispatcherPriority.ApplicationIdle);
    }

    void UpdateTray()
    {
        var newCount = config.Entries.Count(e => e.IsNew);
        tray.Badge = newCount > 0;
        // One line under the name, in the tooltip and the menu's head: what needs a look first, else what StartQueue does.
        var active = Profiles.Active(DateTime.Now);
        var starting = config.Entries.Count(e => Profiles.IsOn(config, e, active));
        tray.Status = newCount switch
        {
            1 => Strings.TrayOneNew,
            > 1 => Strings.F(Strings.TrayNew, newCount),
            _ when !config.SwitchedOn => Strings.TrayNotOn,
            _ when active != null => Strings.F(Strings.TrayOnProfile, starting, active.Name),
            _ => Strings.F(Strings.TrayOn, starting),
        };
    }

    void Notify(string title, string text) => tray.Notify(title, text);

    /// <summary>StartQueue's own entries in the tray menu, between "Open StartQueue" and "Quit".</summary>
    void AddMenuEntries()
    {
        // The app's main actions, then folders, then Windows' own steps; the kit adds the head and Quit.
        tray.Add(Strings.TrayScan, () => _ = SweepAsync(userTriggered: true), "\uE72C");
        tray.Add(Strings.RunSequence, RunSequenceNow, "\uE768");
        tray.AddSeparator();
        tray.Add(Strings.TrayDataFolder, () => Process.Start(new ProcessStartInfo(Paths.Data) { UseShellExecute = true }), "\uE838");
        tray.AddSeparator();
        tray.Add(Strings.TrayShutDownPrepared, () => ShutDownPrepared(restart: false), "\uE7E8");
        tray.Add(Strings.TrayRestartPrepared, () => ShutDownPrepared(restart: true), "\uE777");
    }

    // ---------- Shutting down with prepared apps ----------

    DispatcherTimer? shutdownStopped;

    /// <summary>Asks first, in the window: a shutdown from a misplaced click would cost unsaved work.</summary>
    public async void ShutDownPrepared(bool restart)
    {
        ShowWindow();
        var state = PreparedSignIn.State();
        var canSetUp = PreparedSignIn.SetupState() == PreparedSetupState.CanSetUp;
        var note = state switch
        {
            PreparedSignInState.TurnedOff or PreparedSignInState.Passwordless when canSetUp => Strings.PreparedNoteSetUp,
            PreparedSignInState.TurnedOff => Strings.PreparedNoteTurnedOff,
            PreparedSignInState.Passwordless => Strings.PreparedNotePasswordless,
            PreparedSignInState.BlockedByPolicy => Strings.PreparedNotePolicy,
            PreparedSignInState.HelloRequired => Strings.PreparedNoteHello,
            PreparedSignInState.NeedsBitLocker => Strings.PreparedNoteBitLocker,
            _ => Strings.PreparedNotePossible,
        };
        var go = await window!.ConfirmAsync(
            restart ? Strings.PreparedRestartTitle : Strings.PreparedShutDownTitle,
            Strings.PreparedBody, note,
            restart ? Strings.PreparedRestart : Strings.PreparedShutDown,
            danger: false, icon: "");
        if (!go) return;
        try
        {
            PreparedSignIn.ShutDown(restart);
        }
        catch (Exception ex)
        {
            Log.Event(Events.ShutdownFailed, LogLevel.Warning, $"Shutting down failed: {ex.Message}");
            await window.ShowMessageAsync(Strings.PreparedFailed, ex.Message);
            return;
        }
        // Still running a few minutes later: an app stopped the shutdown. The note would claim the next sign-in.
        shutdownStopped?.Stop();
        shutdownStopped = new DispatcherTimer(TimeSpan.FromMinutes(3), DispatcherPriority.Background, (_, _) =>
        {
            shutdownStopped!.Stop();
            PreparedSignIn.Forget();
            Log.Warning("The shutdown did not happen; the apps are not prepared");
        }, dispatcher);
        shutdownStopped.Start();
    }

    /// <summary>After a prepared shutdown: says whether Windows signed in by itself, once the user unlocks.</summary>
    void ReportPreparedStart(DateTime signIn)
    {
        var worked = PreparedSignIn.CheckAtSignIn(EarlyStart.LockedAtStart);
        if (worked == false)
        {
            var setup = PreparedSignIn.SetupState();
            var reason = setup == PreparedSetupState.Ready
                ? Strings.PreparedNotWorkedPossible
                : PreparedSignIn.Describe(setup, PreparedSignIn.State());
            Notify(Strings.PreparedNotWorkedTitle, Strings.PreparedNotWorkedText + reason);
            return;
        }
        if (worked != true) return;
        // Behind the lock screen nobody sees a notification: it waits for the unlock.
        var told = 0;
        SessionSwitchEventHandler? onSwitch = null;
        void Tell()
        {
            if (Interlocked.Exchange(ref told, 1) == 1) return;
            SystemEvents.SessionSwitch -= onSwitch;
            dispatcher.BeginInvoke(() => Notify(Strings.PreparedWorkedTitle, Strings.F(Strings.PreparedWorkedText, signIn)));
        }
        onSwitch = (_, e) =>
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock) Tell();
        };
        SystemEvents.SessionSwitch += onSwitch;
        if (SignIn.IsLocked() != true) Tell();
    }

    // ---------- Repeating entries ----------

    sealed class RepeatState
    {
        public int Minutes;
        public DateTime Next;
        public DateTime? Until;
    }

    readonly Dictionary<Guid, RepeatState> repeats = new();

    /// <summary>
    /// Starts entries with a repetition again when they are due. The count begins when StartQueue starts, which is at
    /// sign-in, right before the sequence starts them the first time. Only entries that start in the profile in effect.
    /// </summary>
    void RunRepeats()
    {
        var now = DateTime.Now;
        var profile = Profiles.Active(now);
        var due = config.Entries.Where(e => e.RepeatMinutes > 0 && Launcher.StartsBy(config, e, profile)).ToList();
        foreach (var id in repeats.Keys.Except(due.Select(e => e.Id)).ToList()) repeats.Remove(id);
        foreach (var entry in due)
        {
            if (!repeats.TryGetValue(entry.Id, out var state) || state.Minutes != entry.RepeatMinutes)
            {
                repeats[entry.Id] = new RepeatState
                {
                    Minutes = entry.RepeatMinutes,
                    Next = now.AddMinutes(entry.RepeatMinutes),
                    Until = entry.RepeatForMinutes > 0 ? now.AddMinutes(entry.RepeatForMinutes) : null,
                };
                continue;
            }
            if (now < state.Next || state.Until is { } until && now > until) continue;
            state.Next = now.AddMinutes(state.Minutes);
            var copy = entry.Clone();
            Task.Run(() =>
            {
                try
                {
                    Launcher.Start(copy);
                    Log.Info($"Started \"{copy.Name}\" again (every {copy.RepeatMinutes} min)");
                }
                catch (Exception ex)
                {
                    Log.Event(Events.StartFailed, LogLevel.Warning, $"Starting \"{copy.Name}\" again failed: {ex.Message}");
                }
            });
        }
    }

    // ---------- Profiles ----------
    // When a profile applies is the family's (EsteemWorks\profiles.json, shared with the other apps); which entries
    // start in it is StartQueue's, in config.json. A change by another app arrives through FamilyProfiles.Changed.

    /// <summary>
    /// Writes the family's profiles; a file that cannot be read is never written over, which is said and logged
    /// instead of losing the change silently.
    /// </summary>
    bool ChangeFamily(Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Saving the family's profiles failed", ex, eventId: Events.SaveFailed);
            Notify(Strings.CouldNotSave, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// A new profile, or one another app offered (<paramref name="takesOver"/>), with the apps switched on as in
    /// <paramref name="copyOf"/> (null: Standard).
    /// </summary>
    public Guid? AddProfile(FamilyProfile profile, Guid? takesOver, Guid? copyOf)
    {
        var done = ChangeFamily(() =>
        {
            if (takesOver is { } id)
            {
                FamilyProfiles.TakeOver(id, Profiles.App);
                FamilyProfiles.Save(profile);
            }
            else FamilyProfiles.Add(profile, Profiles.App);
        });
        if (!done) return null;
        Change(c =>
        {
            var source = Profiles.Find(copyOf);
            c.ProfileEntries[profile.Id] = c.Entries.Where(e => Profiles.IsOn(c, e, source)).Select(e => e.Id).ToList();
        });
        Log.Info(takesOver == null ? $"Added profile \"{profile.Name}\"" : $"Took over the profile \"{profile.Name}\" another app offered");
        return profile.Id;
    }

    /// <summary>Name, color, schedule, pause, offered or not: for every app that uses the profile.</summary>
    public void SaveProfile(FamilyProfile profile) => ChangeFamily(() => FamilyProfiles.Save(profile));

    public void UpdateProfile(Guid id, Action<FamilyProfile> change) => ChangeFamily(() => FamilyProfiles.Change(id, change));

    /// <summary>StartQueue no longer uses the profile; <paramref name="everywhere"/> deletes it for the other apps too.</summary>
    public void DeleteProfile(Guid id, bool everywhere)
    {
        if (!ChangeFamily(() =>
            {
                if (everywhere) FamilyProfiles.Delete(id);
                else FamilyProfiles.Leave(id, Profiles.App);
            })) return;
        Change(c => c.ProfileEntries.Remove(id));
        Log.Info(everywhere ? "Deleted a profile for all apps" : "Removed a profile");
    }

    /// <summary>Up (-1) or down (+1) in the order of precedence, among the profiles StartQueue uses.</summary>
    public void MoveProfile(Guid id, int by) => ChangeFamily(() => FamilyProfiles.Move(id, by, Profiles.App));

    /// <summary>A profile (null: Standard) by hand, for every app that uses it, up to <paramref name="until"/> or until switched back.</summary>
    public void UseProfile(Guid? id, DateTime? until)
    {
        if (ChangeFamily(() => FamilyProfiles.SetManual(id, until)))
            Log.Info($"Profile chosen by hand: {(id == null ? "Standard" : Profiles.Find(id)?.Name)}{(until is { } u ? $" until {u:g}" : ", until switched back")}");
    }

    public void BackToSchedule()
    {
        if (ChangeFamily(FamilyProfiles.ClearManual)) Log.Info("Back to the profiles' schedule");
    }

    /// <summary>Whether an entry starts in a profile; for Standard (null) that is the entry's own switch.</summary>
    public void SetOnInProfile(Guid? profileId, Guid entryId, bool on) =>
        Change(c =>
        {
            if (profileId is not { } id)
            {
                var entry = c.Entries.FirstOrDefault(e => e.Id == entryId);
                if (entry != null) entry.Enabled = on;
                return;
            }
            var list = Profiles.On(c, id);
            list.Remove(entryId);
            if (on) list.Add(entryId);
        });

    /// <summary>Another app changed the family's profiles, or this one did: the tray and the window show it.</summary>
    void FamilyProfilesChanged()
    {
        if (FamilyProfiles.Problem != null) Log.Warning($"The family's profiles cannot be read: {FamilyProfiles.Problem}");
        // Which profile applies may change by another app's hand; the log says so once per change.
        var now = DateTime.Now;
        var active = Profiles.Active(now);
        var state = $"{active?.Id}|{Profiles.Manual(now)?.Since:o}";
        if (state != lastProfileState)
        {
            lastProfileState = state;
            Log.Info($"Profile that applies now: {(active == null ? "no profile" : $"\"{active.Name}\"")}{(Profiles.Manual(now) != null ? ", chosen by hand" : "")}");
        }
        UpdateTray();
        window?.Sync();
    }

    // ---------- First start ----------

    /// <summary>The entries unticked in the introduction's step "Take over": they stay with Windows when switched on.</summary>
    public HashSet<Guid> IntroLeave { get; } = [];

    /// <summary>What happens to new autostart entries, as chosen in the introduction.</summary>
    public NewEntryMode IntroMode { get; set; }

    /// <summary>
    /// Where the first start stands, as the kit's frame shows it: the introduction open, or looking around. Kept in
    /// config.json, so the window opens the same way again and the sweep knows it (also in the admin helper).
    /// </summary>
    public void SetFirstStart(FirstStart state)
    {
        if (config.SwitchedOn || config.FirstStart == state) return;
        Change(c =>
        {
            if (!c.SwitchedOn) c.FirstStart = state;
        }, reloadWindow: false);
        Log.Info(state == FirstStart.LookingAround ? "First start: looking around before switching on" : "First start: back in the introduction");
    }

    /// <summary>
    /// Switched on by the kit (the frame's "Switch StartQueue on" in the introduction or while looking around, or
    /// --switch-on): what was unticked stays with Windows, the rest is taken over now.
    /// </summary>
    void KitSwitchedOn() => dispatcher.BeginInvoke(() =>
    {
        if (!config.SwitchedOn) _ = SwitchOnAsync(IntroLeave.ToList(), IntroMode);
    });

    /// <summary>--switch-on: as the button, with everything found taken over and the new-entry setting as it is.</summary>
    public void SwitchOnFromCommandLine()
    {
        if (config.SwitchedOn) return;
        Log.Info("Switching on from the command line");
        IntroLeave.Clear();
        // The kit ends its introduction in every open window and tells StartQueue (KitSwitchedOn).
        KitIntro.SwitchOn();
    }

    /// <summary>
    /// Switches StartQueue on: what the user unticked stays with Windows, the rest is taken over now, by the sweep (and
    /// the admin helper for machine-wide entries); chosen scheduled tasks have their sign-in start switched off.
    /// </summary>
    public async Task SwitchOnAsync(IReadOnlyCollection<Guid> leave, NewEntryMode newEntries)
    {
        Change(c => Introduction.SwitchOn(c, leave, newEntries));
        await SweepAsync(userTriggered: true);
        var needAdmin = await Task.Run(Introduction.TakeOverTasks);
        if (needAdmin.Count > 0 && Elevation.AdminMode)
        {
            foreach (var path in needAdmin) PendingTaskTakeOvers.Add(path);
            elevatedQuietUntil = DateTime.Now.AddMinutes(1);
            await Task.Run(Elevation.RunElevatedTask);
        }
        Reload();
        _ = RefreshReportAsync();
        Log.Event(Events.SwitchedOn, LogLevel.Info, $"StartQueue was switched on: {config.Entries.Count} apps in the order");
    }

    string lastProfileState = $"{Profiles.Active(DateTime.Now)?.Id}|{Profiles.Manual(DateTime.Now)?.Since:o}";
    public bool RestartRequested { get; private set; }

    /// <summary>
    /// "Set up fully" (Settings, General, or the introduction): the admin tier through the kit (KitSetup.SetUpFull), one
    /// UAC prompt for its elevated pass, which is StartQueue's --setup-admin with the family's event log in it; then
    /// StartQueue starts again and runs in admin mode, asking Windows nothing more.
    /// </summary>
    public void SetUpAdminFeatures()
    {
        if (Elevation.IsElevated) Setup.InstallAdmin();
        else if (!KitSetup.SetUpFull() && !Elevation.ElevatedTaskRegistered()) return;
        // The Windows 11 context menu needs the certificate trust just set up; this part runs as the user.
        Elevation.RunSelf("--setup-menu");
        RestartRequested = true;
        Exit();
    }

    public void Exit()
    {
        Exiting = true;
        window?.Close();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        KitIntro.SwitchedOn -= KitSwitchedOn;
        periodic.Stop();
        repeatTimer.Stop();
        watcher.Dispose();
        dataWatcher.Dispose();
        ipc.Dispose();
        tray.Dispose();
    }
}
