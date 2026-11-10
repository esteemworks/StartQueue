using System.ComponentModel;
using System.Windows.Media;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>One entry Windows still starts by itself.</summary>
public sealed class WindowsItemVm : INotifyPropertyChanged
{
    ImageSource? icon;
    bool iconLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public WindowsItemVm(WindowsItem item)
    {
        Item = item;
        var native = item.Item;
        var file = native.Origin.Kind == OriginKind.StartupTask ? "" : Path.GetFileName(native.ResolvedTarget.Length > 0 ? native.ResolvedTarget : native.Target);
        Origin = string.IsNullOrEmpty(file) ? native.Origin.Describe() : $"{native.Origin.Describe()}: {file}";
        (StatusText, StatusTip) = item.Status switch
        {
            WindowsItemStatus.Kept => KeptReason(item.Note),
            WindowsItemStatus.StoreUnmanaged => (Strings.StatusStore, Strings.SettingsStoreAppsHelp),
            WindowsItemStatus.MachineWide => (Strings.StatusMachine, Strings.ModeAdminText),
            WindowsItemStatus.MachineLocked => (Strings.StatusLocked, Strings.ModeUserText),
            _ => (Strings.StatusPending, ""),
        };
    }

    static (string, string) KeptReason(KeepNote? note) => note?.Reason switch
    {
        KeepReason.PartOfWindows => (Strings.KeepWindows, Strings.KeepWindowsTip),
        KeepReason.PartOfDriver => (Strings.KeepDriver, Strings.KeepDriverTip),
        KeepReason.ReturnedByUser => (Strings.KeepReturned, Strings.F(Strings.KeepReturnedTip, note.At)),
        KeepReason.NotifyOnly => (Strings.KeepNotify, Strings.F(Strings.KeepNotifyTip, note.At)),
        KeepReason.LeftWithWindows => (Strings.KeepLeft, Strings.F(Strings.KeepLeftTip, note.At)),
        _ => (Strings.KeepUnknown, Strings.KeepUnknownTip),
    };

    public WindowsItem Item { get; }
    public string Name => Item.Item.DisplayName;
    public string Origin { get; }
    public string StatusText { get; }
    public string StatusTip { get; }
    public bool IsStore => Item.Status == WindowsItemStatus.StoreUnmanaged;
    public bool CanTakeOver => Item.Status != WindowsItemStatus.MachineLocked;
    public bool ShowShield => Item.Item.Origin.NeedsAdmin && Item.Status != WindowsItemStatus.MachineLocked && !Elevation.IsElevated;
    public string ButtonText => CanTakeOver ? Strings.TakeOver : Strings.NeedsAdmin;

    public ImageSource? Icon
    {
        get
        {
            if (iconLoaded) return icon;
            iconLoaded = true;
            var native = Item.Item;
            return icon = IconLoader.Get(native.Kind == EntryKind.PackagedApp
                ? @"shell:AppsFolder\" + native.Target
                : native.ResolvedTarget.Length > 0 ? native.ResolvedTarget : native.Target, image =>
            {
                icon = image;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            });
        }
    }
}

/// <summary>A scheduled task that starts at sign-in.</summary>
public sealed class TaskItemVm : INotifyPropertyChanged
{
    ImageSource? icon;
    bool iconLoaded;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TaskItemVm(TaskInfo task)
    {
        Task = task;
        var program = string.IsNullOrEmpty(task.Exe) ? "" : $"{task.Exe} {task.Arguments}".Trim();
        // The root folder "\" says nothing, so the program stands alone there.
        var folder = task.Folder is "" or @"\" ? "" : task.Folder;
        Detail = program.Length == 0 ? task.Folder : folder.Length == 0 ? program : $"{folder}: {program}";
        (StatusText, StatusTip) = task.Block switch
        {
            TaskBlock.None when task.Repeat.Length > 0 => (Strings.F(Strings.TaskRepeats, task.Repeat), Strings.F(Strings.TaskRepeatsTip, task.Repeat)),
            TaskBlock.SeveralActions => (Strings.TaskSeveralActions, Strings.TaskSeveralActionsTip),
            TaskBlock.StoredPassword => (Strings.TaskPassword, Strings.TaskPasswordTip),
            TaskBlock.NoProgram => (Strings.TaskNoProgram, Strings.TaskNoProgramTip),
            _ when task.OtherTriggers.Length > 0 => (Strings.F(Strings.TaskOtherTriggers, task.OtherTriggers), Strings.F(Strings.TaskOtherTriggersTip, task.OtherTriggers)),
            _ => (Strings.TaskOnlyLogon, Strings.TasksHintTakeOver),
        };
    }

    public TaskInfo Task { get; }
    public string Name => Task.Name;
    public string Detail { get; }
    public string StatusText { get; }
    public string StatusTip { get; }
    public bool CanTakeOver => Task.Block == TaskBlock.None;
    public bool ShowShield => CanTakeOver && Task.Elevated && !Elevation.IsElevated;

    public ImageSource? Icon
    {
        get
        {
            if (iconLoaded) return icon;
            iconLoaded = true;
            if (!File.Exists(Task.Exe)) return null;
            return icon = IconLoader.Get(Task.Exe, image =>
            {
                icon = image;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            });
        }
    }
}

// ---------- Add app dialog ----------

/// <summary>A row of the add dialog. The search marks rows as shown; the dialog's list view filters on it.</summary>
public abstract class AddRow : INotifyPropertyChanged
{
    bool shown = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsShown
    {
        get => shown;
        set
        {
            if (shown == value) return;
            shown = value;
            Raise(nameof(IsShown));
        }
    }

    protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class AddHeader(string title, string? hint) : AddRow
{
    public string Title { get; } = title;
    public string? Hint { get; } = string.IsNullOrEmpty(hint) ? null : hint;
    public bool HasHint => Hint != null;
}

public sealed class AddNote(string text) : AddRow
{
    public string Text { get; } = text;
}

public sealed class CandidateVm : AddRow
{
    ImageSource? icon;
    bool iconLoaded;
    bool added;

    public CandidateVm(AppCandidate candidate, bool isSuggestion)
    {
        Candidate = candidate;
        IsSuggestion = isSuggestion;
    }

    public AppCandidate Candidate { get; }
    public bool IsSuggestion { get; }
    public string Name => Candidate.Name;

    public bool HasReason => IsSuggestion && Candidate.Reason != SuggestReason.None;

    public string ReasonText => Candidate.Reason switch
    {
        SuggestReason.Running => Strings.ReasonRunning,
        SuggestReason.OftenUsed => Strings.ReasonOften,
        SuggestReason.CanStartItself => Strings.ReasonCanStart,
        SuggestReason.RecentlyInstalled => Strings.ReasonRecent,
        _ => "",
    };

    public bool Added
    {
        get => added;
        set
        {
            added = value;
            foreach (var name in new[] { nameof(CanAdd), nameof(IsDone), nameof(DoneText), nameof(IsAddedSuggestion) }) Raise(name);
        }
    }

    public bool CanAdd => !Candidate.Listed && !added;
    public bool IsDone => !CanAdd;
    public string DoneText => added ? Strings.Added : Strings.AlreadyListed;
    public bool IsAddedSuggestion => IsSuggestion && added;

    // Loaded in the background so the dialog opens at once with all its rows.
    public ImageSource? Icon
    {
        get
        {
            if (iconLoaded) return icon;
            iconLoaded = true;
            return icon = IconLoader.Get(@"shell:AppsFolder\" + Candidate.Id, image =>
            {
                icon = image;
                Raise(nameof(Icon));
            });
        }
    }
}

/// <summary>Loads shell icons one after another on a background thread and hands them to the UI thread.</summary>
static class IconLoader
{
    static readonly System.Collections.Concurrent.BlockingCollection<(string Name, Action<ImageSource?> Done)> Queue = new();

    static IconLoader()
    {
        var worker = new Thread(() =>
        {
            foreach (var (name, done) in Queue.GetConsumingEnumerable())
            {
                var image = ShellIcons.ForParsingName(name);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => done(image));
            }
        }) { IsBackground = true, Name = "Icon loader" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
    }

    public static void Request(string name, Action<ImageSource?> done) => Queue.Add((name, done));

    /// <summary>The icon right away if it was read before; otherwise null now and <paramref name="loaded"/> later.</summary>
    public static ImageSource? Get(string name, Action<ImageSource?> loaded)
    {
        if (ShellIcons.TryCached(name, out var image)) return image;
        Request(name, loaded);
        return null;
    }
}

/// <summary>The rows of the add dialog: suggestions first, then every app. Built once; the search shows and hides them.</summary>
public sealed class AddAppModel
{
    const int SuggestionCount = 6;
    readonly List<CandidateVm> suggestions;
    readonly List<CandidateVm> all;
    readonly AddNote noSuggestions = new(Strings.NoSuggestions);

    public AddAppModel(List<AppCandidate> candidates)
    {
        suggestions = candidates.Where(c => c.Score > 0 && !c.Listed)
            .OrderByDescending(c => c.Score).ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(SuggestionCount).Select(c => new CandidateVm(c, true)).ToList();
        all = candidates.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).Select(c => new CandidateVm(c, false)).ToList();
        Rows = [new AddHeader(Strings.SectionSuggestions, Strings.SuggestionsHint), noSuggestions, .. suggestions,
                new AddHeader(Strings.SectionAllApps, ""), .. all];
        Filter("");
    }

    public List<AddRow> Rows { get; }

    public void Filter(string filter)
    {
        foreach (var row in suggestions.Concat(all))
            row.IsShown = filter.Length == 0 || row.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        noSuggestions.IsShown = !suggestions.Any(s => s.IsShown);
    }

    /// <summary>Marks the same app as added in both lists.</summary>
    public void MarkAdded(AppCandidate candidate)
    {
        candidate.Listed = true;
        foreach (var vm in suggestions.Concat(all).Where(v => v.Candidate == candidate)) vm.Added = true;
    }
}
