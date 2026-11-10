namespace StartQueue.Core;

public enum WindowsItemStatus
{
    /// <summary>On the keep list; <see cref="WindowsItem.Note"/> says why.</summary>
    Kept,

    /// <summary>A Store app's startup task while "manage Store apps" is off.</summary>
    StoreUnmanaged,

    /// <summary>Machine-wide; StartQueue can take it over through its admin task.</summary>
    MachineWide,

    /// <summary>Machine-wide in user mode: only someone with admin rights can change it.</summary>
    MachineLocked,

    /// <summary>Would be taken over by the next scan.</summary>
    Pending,
}

public sealed record WindowsItem(NativeItem Item, WindowsItemStatus Status, KeepNote? Note);

/// <param name="TasksBack">Tasks in StartQueue whose sign-in start an app switched on again; the next scan switches it off.</param>
public sealed record WindowsReport(List<WindowsItem> Items, List<TaskInfo> Tasks, DateTime At, int TasksBack = 0);

/// <summary>What Windows still starts by itself at sign-in, for the Windows autostart view.</summary>
public static class WindowsAutostart
{
    public static WindowsReport Analyze(Config config)
    {
        var keep = new HashSet<string>(config.KeepInWindows, StringComparer.OrdinalIgnoreCase);
        // Before switching on, what StartQueue would take over shows in the order already, marked "still with Windows".
        var previewed = new HashSet<string>(config.Entries.Where(e => e.StillInWindows && e.Origin != null).Select(e => e.Origin!.Key),
            StringComparer.OrdinalIgnoreCase);
        var items = new List<WindowsItem>();
        foreach (var item in NativeAutostart.Scan())
        {
            if (Sweeper.IsSelf(item)) continue;
            var key = item.Origin.Key;
            if (previewed.Contains(key)) continue;
            if (keep.Contains(key)) items.Add(new WindowsItem(item, WindowsItemStatus.Kept, config.NoteFor(key)));
            else if (item.Origin.Kind == OriginKind.StartupTask && !config.Settings.ManageStoreApps) items.Add(new WindowsItem(item, WindowsItemStatus.StoreUnmanaged, null));
            else if (item.Origin.NeedsAdmin && !Elevation.IsElevated)
                items.Add(new WindowsItem(item, Elevation.AdminMode ? WindowsItemStatus.MachineWide : WindowsItemStatus.MachineLocked, null));
            else items.Add(new WindowsItem(item, WindowsItemStatus.Pending, null));
        }
        // A task already in StartQueue is not offered again: an app may switch its sign-in start back on
        // (often when it starts), and taking it over a second time would add nothing.
        var ours = new HashSet<string>(config.Entries.Where(e => e.Origin?.Kind == OriginKind.ScheduledTask && !e.StillInWindows).Select(e => e.Origin!.Key),
            StringComparer.OrdinalIgnoreCase);
        var tasks = TaskAutostart.List();
        var back = tasks.RemoveAll(t => ours.Contains(TaskAutostart.KeyOf(t.Path)));
        tasks.RemoveAll(t => previewed.Contains(TaskAutostart.KeyOf(t.Path)));
        return new WindowsReport(items, tasks, DateTime.Now, back);
    }
}
