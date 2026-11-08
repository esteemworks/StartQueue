using System.ComponentModel;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>A row of the services view: a section header or a service. The search marks rows as shown.</summary>
public abstract class ServiceRow : INotifyPropertyChanged
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

/// <summary>"Services of apps" or "Windows services"; the Windows section folds away, it is long.</summary>
public sealed class ServiceHeader(bool windows) : ServiceRow
{
    bool expanded = !windows;
    int count;

    public bool IsWindows { get; } = windows;
    public string Title => IsWindows ? Strings.ServicesWindowsSection : Strings.ServicesAppsSection;
    public string Tip => IsWindows ? Strings.ServicesWindowsSectionTip : Strings.ServicesAppsSectionTip;

    public int Count
    {
        get => count;
        set { count = value; Raise(nameof(Count)); }
    }

    public bool Expanded
    {
        get => expanded;
        set { expanded = value; Raise(nameof(Expanded)); Raise(nameof(ToggleText)); }
    }

    bool searching;

    /// <summary>A search or filter shows the folded section anyway; then there is nothing to fold.</summary>
    public bool Searching
    {
        set { if (searching == value) return; searching = value; Raise(nameof(CanFold)); }
    }

    public bool CanFold => IsWindows && !searching;
    public string ToggleText => expanded ? Strings.ServicesHide : Strings.ServicesShow;
}

public sealed class ServiceVm : ServiceRow
{
    readonly Action<ServiceVm, ServiceAction> change;
    bool busy;

    public ServiceVm(ServiceInfo info, Action<ServiceVm, ServiceAction> change)
    {
        Info = info;
        this.change = change;
    }

    public ServiceInfo Info { get; private set; }

    public void Update(ServiceInfo info)
    {
        Info = info;
        Raise(string.Empty);
    }

    public string Name => Info.DisplayName;
    public string Description => Info.Description.Length > 0 ? Info.Description : Info.Name;

    public string Tip => string.Join("\n", new[]
    {
        Info.Description,
        Strings.F(Strings.ServiceTipName, Info.Name),
        Info.ImagePath.Length > 0 ? Strings.F(Strings.ServiceTipProgram, Info.ImagePath) : "",
        Info.Account.Length > 0 ? Strings.F(Strings.ServiceTipAccount, Info.Account) : "",
        Info.Company.Length > 0 ? Strings.F(Strings.ServiceTipCompany, Info.Company) : "",
    }.Where(line => line.Length > 0));

    public bool IsRunning => Info.State == ServiceState.Running;
    public string StateText => Info.State switch
    {
        ServiceState.Running => Strings.ServiceRunning,
        ServiceState.Stopped => Strings.ServiceStopped,
        ServiceState.Paused => Strings.ServicePaused,
        _ => Strings.ServicePending,
    };

    public bool IsWindows => Info.IsWindows;
    public bool Essential => Info.Essential;
    public string KindChip => Info.Essential ? Strings.ServiceEssentialChip : Strings.ServiceWindowsChip;
    public string KindTip => Info.Essential ? Strings.ServiceEssentialTip : Strings.ServiceWindowsTip;

    public static string[] StartTypes => [Strings.StartDelayed, Strings.StartAutomatic, Strings.StartManual, Strings.StartDisabled];

    /// <summary>The start type as the choice shows it; picking another one asks the window to carry it out.</summary>
    public int StartIndex
    {
        get => Info.Start switch
        {
            ServiceStart.AutomaticDelayed => 0,
            ServiceStart.Automatic => 1,
            ServiceStart.Manual => 2,
            ServiceStart.Disabled => 3,
            _ => -1,
        };
        set
        {
            if (value == StartIndex || value < 0) return;
            change(this, (ServiceAction)value);
        }
    }

    /// <summary>services.msc adds "(Trigger Start)" when Windows starts the service on an event.</summary>
    public bool HasTrigger => Info.TriggerStart;
    public bool CanChangeStart => !busy && StartIndex >= 0;

    public bool Busy
    {
        get => busy;
        set
        {
            busy = value;
            foreach (var name in new[] { nameof(Busy), nameof(CanChangeStart), nameof(CanStart), nameof(CanStop), nameof(CanRestart) }) Raise(name);
        }
    }

    public bool CanStart => !busy && Info.State == ServiceState.Stopped && Info.Start != ServiceStart.Disabled;
    public bool CanStop => !busy && Info.State == ServiceState.Running && Info.CanStop;
    public bool CanRestart => CanStop;

    /// <summary>Shown again after a refused change, so the choice goes back to what is really set.</summary>
    public void Reset() => Raise(nameof(StartIndex));

    public void Matches(string filter, bool sectionOpen) => IsShown = sectionOpen && Matches(filter);

    public bool Matches(string filter) =>
        filter.Length == 0 || Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
        || Info.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || Description.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
}

public enum ServiceSort { Name, StartType, State }

/// <summary>All rows: services of apps first, then Windows services; each section sorted and filtered the same way.</summary>
public sealed class ServicesModel
{
    readonly ServiceHeader apps = new(false);
    readonly ServiceHeader windows = new(true);
    readonly Dictionary<string, ServiceVm> byName = new(StringComparer.OrdinalIgnoreCase);
    List<ServiceVm> all = [];
    string filter = "";

    public System.Collections.ObjectModel.ObservableCollection<ServiceRow> Rows { get; } = new();

    public bool Loaded { get; private set; }

    public ServiceSort Sort { get; private set; }

    /// <summary>Start types to show; empty: all of them.</summary>
    public HashSet<ServiceStart> StartTypes { get; } = [];

    /// <summary>Running and stopped ones to show; both empty: all of them. Starting and stopping count as running.</summary>
    public bool OnlyRunning { get; set; }
    public bool OnlyStopped { get; set; }

    public int ActiveFilters => StartTypes.Count + (OnlyRunning ? 1 : 0) + (OnlyStopped ? 1 : 0);

    /// <summary>Takes in a fresh list; rows that exist already keep their place and only change their values.</summary>
    public void Load(List<ServiceInfo> services, Action<ServiceVm, ServiceAction> change)
    {
        all = services.Select(s => byName.TryGetValue(s.Name, out var vm) ? Updated(vm, s) : byName[s.Name] = new ServiceVm(s, change)).ToList();
        Loaded = true;
        Arrange();
    }

    public void SortBy(ServiceSort sort)
    {
        Sort = sort;
        Arrange();
    }

    /// <summary>
    /// By start type in the order services.msc would start them (automatic, delayed, manual, disabled); by state
    /// running ones first. Within that, by name.
    /// </summary>
    void Arrange()
    {
        IEnumerable<ServiceVm> Sorted(IEnumerable<ServiceVm> rows) => Sort switch
        {
            ServiceSort.StartType => rows.OrderBy(v => StartOrder(v.Info.Start)).ThenBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase),
            ServiceSort.State => rows.OrderBy(v => v.IsRunning ? 0 : 1).ThenBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => rows.OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase),
        };
        var appRows = Sorted(all.Where(v => !v.IsWindows)).ToList();
        var windowsRows = Sorted(all.Where(v => v.IsWindows)).ToList();
        List<ServiceRow> wanted = [apps, .. appRows, windows, .. windowsRows];
        if (!wanted.SequenceEqual(Rows))
        {
            Rows.Clear();
            foreach (var row in wanted) Rows.Add(row);
        }
        Filter(filter);
    }

    static int StartOrder(ServiceStart start) => start switch
    {
        ServiceStart.Boot or ServiceStart.System => 0,
        ServiceStart.Automatic => 1,
        ServiceStart.AutomaticDelayed => 2,
        ServiceStart.Manual => 3,
        _ => 4,
    };

    static ServiceVm Updated(ServiceVm vm, ServiceInfo info)
    {
        if (vm.Info != info) vm.Update(info);
        return vm;
    }

    public void Filter(string text)
    {
        filter = text.Trim();
        // A search or filter looks into the folded Windows section as well.
        windows.Searching = filter.Length > 0 || ActiveFilters > 0;
        var windowsOpen = windows.Expanded || filter.Length > 0 || ActiveFilters > 0;
        foreach (var vm in byName.Values)
            vm.Matches(filter, (!vm.IsWindows || windowsOpen) && Passes(vm));
        // The counts say how many each section shows.
        apps.Count = all.Count(v => !v.IsWindows && Passes(v) && v.Matches(filter));
        windows.Count = all.Count(v => v.IsWindows && Passes(v) && v.Matches(filter));
    }

    bool Passes(ServiceVm vm) =>
        (StartTypes.Count == 0 || StartTypes.Contains(vm.Info.Start))
        && (OnlyRunning == OnlyStopped || (OnlyRunning ? vm.Info.State != ServiceState.Stopped : vm.Info.State == ServiceState.Stopped));

    public void Toggle(ServiceHeader header)
    {
        header.Expanded = !header.Expanded;
        Filter(filter);
    }
}
