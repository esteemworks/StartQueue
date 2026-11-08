using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using StartQueue.Core;

namespace StartQueue.UI;

public sealed class EntryVm : INotifyPropertyChanged
{
    readonly Action<EntryVm> edited;
    readonly Action<EntryVm, bool>? switchedInProfile;
    Entry model;
    List<Guid>? profileOn;
    LastRunItem? lastRun;
    bool dragging;
    int position;

    public EntryVm(Entry model, Action<EntryVm> edited, Action<EntryVm, bool>? switchedInProfile = null)
    {
        this.model = model;
        this.edited = edited;
        this.switchedInProfile = switchedInProfile;
    }

    /// <summary>
    /// The entries that start in the profile the list shows and edits (<see cref="Profiles.On"/>); null: Standard, the
    /// entry's own switch.
    /// </summary>
    public void ShowProfile(List<Guid>? on)
    {
        var before = Enabled;
        profileOn = on;
        if (Enabled == before) return;
        Raise(nameof(Enabled));
        Raise(nameof(ToggleLabel));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Entry Model => model;
    public Guid Id => model.Id;
    public EntryKind Kind => model.Kind;

    public bool IsDragging
    {
        get => dragging;
        set { dragging = value; Raise(); }
    }

    public int Position
    {
        get => position;
        set { if (position == value) return; position = value; Raise(); }
    }

    public string Name
    {
        get => model.Name;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == model.Name) return;
            model.Name = value.Trim();
            Edited();
        }
    }

    public bool Enabled
    {
        get => profileOn == null ? model.Enabled : profileOn.Contains(model.Id);
        set
        {
            if (Enabled == value) return;
            if (profileOn == null)
            {
                model.Enabled = value;
                Edited();
            }
            else
            {
                // Shown at once; the host saves it into the profile.
                profileOn.Remove(model.Id);
                if (value) profileOn.Add(model.Id);
                Raise();
                switchedInProfile?.Invoke(this, value);
            }
            Raise(nameof(ToggleLabel));
        }
    }

    public string ToggleLabel => Enabled ? Strings.MenuTurnOff : Strings.MenuTurnOn;

    // ---------- Window at sign-in and repetition ----------

    StartWindowState WindowNow => StartSwitches.Describe(model);

    public bool WindowNormal { get => WindowNow.Mode == WindowMode.Normal; set { if (value) SetWindow(WindowMode.Normal); } }
    public bool WindowMinimized { get => WindowNow.Mode == WindowMode.Minimized; set { if (value) SetWindow(WindowMode.Minimized); } }
    public bool WindowBackground { get => WindowNow.Mode == WindowMode.Background; set { if (value) SetWindow(WindowMode.Background); } }

    /// <summary>Apps that quit when their window closes can only start minimized.</summary>
    public bool CanBackground => !model.QuitsOnClose;

    /// <summary>Not when a shortcut or task fixes it, or the app shows no window anyway.</summary>
    public bool WindowEditable => WindowNow.Source is not (WindowSource.FixedSwitch or WindowSource.Shortcut or WindowSource.NoWindow);

    /// <summary>Why the window comes up this way, so the choice never claims more than it does.</summary>
    public string WindowNote
    {
        get
        {
            var state = WindowNow;
            if (model.QuitsOnClose && state.Source is WindowSource.StartQueue or WindowSource.None) return Strings.F(Strings.WindowQuitsOnClose, model.Name);
            return state.Source switch
            {
                WindowSource.Switch => Strings.F(Strings.WindowViaSwitch, state.Switch),
                WindowSource.FixedSwitch => Strings.F(Strings.WindowViaFixedSwitch, state.Switch),
                WindowSource.Shortcut => Strings.WindowViaShortcut,
                WindowSource.NoWindow => Strings.WindowNoWindow,
                WindowSource.StartQueue => state.Mode == WindowMode.Background ? Strings.StartWindowHelp : Strings.WindowViaStartQueueMinimized,
                _ => StartSwitches.Known(model) is { } known ? Strings.F(Strings.WindowNormalKnown, known.Text) : Strings.WindowNormalPlain,
            };
        }
    }

    /// <summary>A short fact where there is one (a start parameter, a minimized shortcut, no window); empty otherwise.</summary>
    public string WindowFact
    {
        get
        {
            var state = WindowNow;
            return state.Source switch
            {
                WindowSource.Switch => Strings.F(Strings.WindowFactSwitch, state.Switch),
                WindowSource.FixedSwitch => Strings.F(Strings.WindowFactFixedSwitch, state.Switch),
                WindowSource.Shortcut => Strings.WindowFactShortcut,
                WindowSource.NoWindow => Strings.WindowFactNoWindow,
                _ => "",
            };
        }
    }

    public bool HasWindowFact => WindowFact.Length > 0;

    /// <summary>
    /// The program's own start parameter does it where StartQueue knows one: choosing sets or takes out that
    /// parameter. Otherwise StartQueue handles the window after the start.
    /// </summary>
    void SetWindow(WindowMode mode)
    {
        // Logged, so a window that comes up differently at the next sign-in can be traced back.
        if (model.Window != mode) Log.Info($"Window at sign-in of \"{model.Name}\" set to {mode} by hand");
        model.Window = mode;
        if (model.Kind == EntryKind.Program)
        {
            // The parameter goes where the parameters in use are: own ones replacing the app's, or the app's.
            var replaced = model.Parameters == ParameterMode.Replace;
            var arguments = replaced ? model.OwnArguments : model.Arguments;
            if (StartSwitches.Find(arguments) is { } current && (mode == WindowMode.Normal || current.Mode != mode))
                arguments = StartSwitches.With(arguments, current.Text, false);
            if (StartSwitches.Find(model.ExtraArguments) is { } extra && (mode == WindowMode.Normal || extra.Mode != mode))
                model.ExtraArguments = StartSwitches.With(model.ExtraArguments, extra.Text, false);
            if (mode != WindowMode.Normal && StartSwitches.Known(model) is { } known && known.Mode == mode && StartSwitches.Find(arguments) == null)
                arguments = StartSwitches.With(arguments, known.Text, true);
            if (replaced) model.OwnArguments = arguments;
            else model.Arguments = arguments;
        }
        Edited(nameof(WindowNormal));
        foreach (var name in new[] { nameof(WindowMinimized), nameof(WindowBackground), nameof(WindowEditable), nameof(WindowNote), nameof(WindowFact), nameof(HasWindowFact),
                                     nameof(HasWindowChip), nameof(WindowChip), nameof(Arguments), nameof(Subtitle) })
            Raise(name);
        RaiseParameters();
    }

    // ---------- Own start parameters ----------

    StartCommand? original;
    bool originalRead;

    /// <summary>What the entry starts by itself; read once, a shortcut or task takes a moment to read.</summary>
    StartCommand? Original
    {
        get
        {
            if (!originalRead) (original, originalRead) = (StartCommands.Original(model), true);
            return original;
        }
    }

    public bool ParamAsBefore { get => model.Parameters == ParameterMode.AsBefore; set { if (value) SetParameters(ParameterMode.AsBefore); } }
    public bool ParamAdd { get => model.Parameters == ParameterMode.Add; set { if (value) SetParameters(ParameterMode.Add); } }
    public bool ParamReplace { get => model.Parameters == ParameterMode.Replace; set { if (value) SetParameters(ParameterMode.Replace); } }

    /// <summary>Replacing starts from the parameters in use now, so taking something out is as easy as adding.</summary>
    void SetParameters(ParameterMode mode)
    {
        if (model.Parameters == mode) return;
        if (mode == ParameterMode.Replace && model.OwnArguments.Length == 0)
            model.OwnArguments = StartCommands.Arguments(model, Original?.Arguments ?? "");
        model.Parameters = mode;
        Edited(nameof(ParamAsBefore));
        RaiseParameters();
    }

    void RaiseParameters()
    {
        foreach (var name in new[] { nameof(ParamAdd), nameof(ParamReplace), nameof(ExtraArguments), nameof(OwnArguments), nameof(OriginalArguments),
                                     nameof(EffectiveCommand), nameof(HasEffectiveCommand), nameof(Subtitle), nameof(WindowNormal), nameof(WindowMinimized),
                                     nameof(WindowBackground), nameof(WindowEditable), nameof(WindowNote), nameof(WindowFact), nameof(HasWindowFact),
                                     nameof(HasWindowChip), nameof(WindowChip) })
            Raise(name);
    }

    /// <summary>The app's own parameters; with "Add" they stay as they are.</summary>
    public string OriginalArguments => Original is { Arguments.Length: > 0 } o ? o.Arguments : Strings.ParamNone;

    public string ExtraArguments
    {
        get => model.ExtraArguments;
        set { var v = (value ?? "").Trim(); if (v == model.ExtraArguments) return; model.ExtraArguments = v; Edited(); RaiseParameters(); }
    }

    public string OwnArguments
    {
        get => model.OwnArguments;
        set { var v = (value ?? "").Trim(); if (v == model.OwnArguments) return; model.OwnArguments = v; Edited(); RaiseParameters(); }
    }

    /// <summary>The whole command line with own parameters, so it is plain what will run.</summary>
    public string EffectiveCommand => StartCommands.Describe(model);
    public bool HasEffectiveCommand => model.Parameters != ParameterMode.AsBefore && EffectiveCommand.Length > 0;

    /// <summary>Shortcuts and tasks: with own parameters StartQueue starts the program behind them itself.</summary>
    public string ParametersHelp => model.Kind is EntryKind.Shortcut or EntryKind.ScheduledTask ? Strings.ParamHelpDirect : Strings.ParamHelp;

    public bool HasWindowChip => WindowNow.Mode != WindowMode.Normal;
    public string WindowChip => WindowNow.Mode == WindowMode.Background ? Strings.BackgroundChip : Strings.MinimizedChip;

    public int RepeatMinutes
    {
        get => model.RepeatMinutes;
        set
        {
            var clamped = Math.Clamp(value, 0, 7 * 24 * 60);
            if (model.RepeatMinutes == clamped) return;
            model.RepeatMinutes = clamped;
            Edited();
            Raise(nameof(HasRepeat));
            Raise(nameof(RepeatText));
        }
    }

    public bool HasRepeat => model.RepeatMinutes > 0;
    public string RepeatText => Strings.F(Strings.RepeatChip, FormatMinutes(model.RepeatMinutes));


    /// <summary>210 as "3 h 30 min".</summary>
    static string FormatMinutes(int minutes) =>
        minutes < 60 ? $"{minutes} min" : minutes % 60 == 0 ? $"{minutes / 60} h" : $"{minutes / 60} h {minutes % 60} min";

    public bool AdminAvailable => Elevation.AdminMode;
    public string AdminHelp => Elevation.AdminMode ? Strings.RunElevatedHelpAdmin : Strings.RunElevatedHelpUser;

    public int DelaySeconds
    {
        get => model.DelaySeconds;
        set
        {
            var clamped = Math.Clamp(value, 0, 600);
            if (model.DelaySeconds == clamped) return;
            model.DelaySeconds = clamped;
            Edited();
            Raise(nameof(HasDelay));
            Raise(nameof(DelayText));
        }
    }

    public bool WaitUntilRunning
    {
        get => model.WaitUntilRunning;
        set { if (model.WaitUntilRunning == value) return; model.WaitUntilRunning = value; Edited(); }
    }

    public bool RunElevated
    {
        get => model.RunElevated;
        set { if (model.RunElevated == value) return; model.RunElevated = value; Edited(); }
    }

    public string Target
    {
        get => model.Target;
        set
        {
            var clean = (value ?? "").Trim().Trim('"');
            if (clean == model.Target || clean.Length == 0) return;
            model.Target = clean;
            if (model.Kind == EntryKind.Program)
            {
                model.ResolvedTarget = clean;
                model.ProcessName = CommandLine.ProcessNameOf(clean);
                Raise(nameof(ProcessName));
            }
            Edited();
            Raise(nameof(Icon));
            Raise(nameof(Subtitle));
        }
    }

    public string Arguments
    {
        get => model.Arguments;
        set { var v = value ?? ""; if (v == model.Arguments) return; model.Arguments = v; Edited(); Raise(nameof(Subtitle)); }
    }

    public string WorkingDirectory
    {
        get => model.WorkingDirectory;
        set { var v = value ?? ""; if (v == model.WorkingDirectory) return; model.WorkingDirectory = v; Edited(); }
    }

    public string ProcessName
    {
        get => model.ProcessName;
        set
        {
            var v = (value ?? "").Trim();
            if (v.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) v = v[..^4];
            if (v == model.ProcessName) return;
            model.ProcessName = v;
            Edited();
        }
    }

    public bool IsNew
    {
        get => model.IsNew;
        set { if (model.IsNew == value) return; model.IsNew = value; Edited(); }
    }

    public bool IsProgram => model.Kind == EntryKind.Program;
    public bool HasFileLocation => model.Kind != EntryKind.PackagedApp && File.Exists(LocationFile);
    public string LocationFile => model.Kind is EntryKind.Shortcut or EntryKind.ScheduledTask ? model.ResolvedTarget : Environment.ExpandEnvironmentVariables(model.Target);

    public string KindLabel => model.Kind switch
    {
        EntryKind.PackagedApp => Strings.KindStoreApp,
        EntryKind.Shortcut => Strings.KindShortcut,
        EntryKind.ScheduledTask => Strings.KindTask,
        _ => Strings.KindProgram,
    };

    public string Subtitle => model.Kind switch
    {
        EntryKind.PackagedApp => Strings.StoreApp,
        EntryKind.Shortcut => string.IsNullOrEmpty(model.ResolvedTarget) ? Strings.Shortcut : model.ResolvedTarget,
        // With the program it runs, as the Windows autostart view shows tasks, so the two are easy to match up.
        EntryKind.ScheduledTask => string.IsNullOrEmpty(model.ResolvedTarget)
            ? Strings.F(Strings.TaskSubtitle, model.Target)
            : $"{Strings.F(Strings.TaskSubtitle, model.Target)}: {model.ResolvedTarget}",
        _ => StartCommands.Arguments(model, model.Arguments) is { Length: > 0 } arguments ? $"{model.Target}  {arguments}" : model.Target,
    };

    // Reading an icon from the shell takes milliseconds; it happens in the background, the row shows it when ready.
    public ImageSource? Icon
    {
        get
        {
            return IconLoader.Get(ShellIcons.NameFor(model), _ => Raise(nameof(Icon)));
        }
    }

    public bool HasDelay => model.DelaySeconds > 0;
    public string DelayText => $"+{model.DelaySeconds} s";

    public bool HasOrigin => model.Origin != null;

    public string OriginText => model.Origin == null
        ? Strings.F(Strings.AddedByYou, model.AddedAt)
        : Strings.F(Strings.TakenOver, model.Origin.Describe(), model.Origin.TakenOverAt);

    public string NewText =>
        Strings.F(Strings.NewNotice, model.AddedAt);

    public string LastStartText => lastRun == null ? "" : lastRun.Failed ? Strings.LastFailedShort : $"+{lastRun.Seconds:0.#} s";
    public bool LastStartFailed => lastRun?.Failed == true;

    public string LastStartLong => lastRun == null
        ? Strings.NotStartedLastLogon
        : lastRun.Failed
            ? Strings.F(Strings.FailedAfterLogon, lastRun.Seconds, lastRun.Outcome == RunOutcome.AdminTaskMissing ? Strings.AdminTaskMissing : lastRun.Error)
            : Strings.F(Strings.StatusAfterLogon, Capitalize(OutcomeText(lastRun)), lastRun.Seconds);

    // Made here, in the current language: the last run only stores what happened.
    static string OutcomeText(LastRunItem run) =>
        (run.Outcome switch
        {
            RunOutcome.WasRunning => Strings.WasRunning,
            RunOutcome.StartedAsAdmin => Strings.StartedAsAdmin,
            _ => Strings.Started,
        }) + (run.NotDetectedAfter is { } seconds ? Strings.F(Strings.NotDetected, seconds) : "");

    public void SetLastRun(LastRunItem? item)
    {
        if (Same(lastRun, item)) return;
        lastRun = item;
        Raise(nameof(LastStartText));
        Raise(nameof(LastStartFailed));
        Raise(nameof(LastStartLong));
    }

    /// <summary>Takes a fresh copy from disk without writing it back.</summary>
    public void Load(Entry fresh)
    {
        // Every reload hands over fresh copies; only rows that really changed refresh their bindings.
        var changed = !Same(model, fresh);
        model = fresh;
        if (!changed) return;
        originalRead = false;
        Raise(string.Empty);
    }

    static bool Same(object? a, object? b) =>
        System.Text.Json.JsonSerializer.Serialize(a, Json.Options) == System.Text.Json.JsonSerializer.Serialize(b, Json.Options);

    void Edited([CallerMemberName] string? property = null)
    {
        Raise(property);
        edited(this);
    }

    void Raise([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];
}
