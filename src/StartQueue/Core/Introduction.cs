namespace StartQueue.Core;

/// <summary>
/// The first start of a new install (<see cref="Config.FirstStart"/>, STANDARD.md, "First start"): StartQueue
/// changes nothing in Windows before the user switches it on. Until then the sweep only shows what it would take over
/// (<see cref="Entry.StillInWindows"/>); switching on leaves the entries the user unticked with Windows and lets the next
/// sweep take over the rest, with everything set for them meanwhile.
/// </summary>
public static class Introduction
{
    /// <summary>The user's entries found in the Windows autostart, in the order shown.</summary>
    public static List<Entry> Found(Config config) => config.Entries.Where(e => e.StillInWindows).ToList();

    /// <summary>
    /// Switches StartQueue on: the entries in <paramref name="leave"/> stay with Windows (on the keep list, so no sweep
    /// takes them later), new autostart entries are handled as <paramref name="newEntries"/> says from now on. The sweep
    /// that follows takes over every other entry still with Windows.
    /// </summary>
    public static void SwitchOn(Config config, IReadOnlyCollection<Guid> leave, NewEntryMode newEntries)
    {
        foreach (var entry in config.Entries.Where(e => e.StillInWindows && leave.Contains(e.Id)).ToList())
        {
            config.Entries.Remove(entry);
            config.RemoveFromProfiles(entry.Id);
            if (entry.Origin != null) config.Keep(entry.Origin.Key, KeepReason.LeftWithWindows);
        }
        config.Settings.NewEntries = newEntries;
        config.FirstStart = FirstStart.Done;
        Log.Info($"Switched on: {config.Entries.Count(e => e.StillInWindows)} entries to take over, {leave.Count} left with Windows, new entries: {newEntries}");
    }

    /// <summary>
    /// After switching on: scheduled tasks chosen in the preview have their sign-in start switched off now (the sweep
    /// itself never touches tasks). Returns the ones only an administrator may change, for the admin helper.
    /// </summary>
    public static List<string> TakeOverTasks()
    {
        var needAdmin = new List<string>();
        foreach (var path in ConfigStore.Load().Entries.Where(e => e.StillInWindows && e.Origin?.Kind == OriginKind.ScheduledTask).Select(e => e.Origin!.Name))
        {
            try
            {
                TaskTakeOver.Apply(path, adminOnly: false);
            }
            catch (UnauthorizedAccessException)
            {
                needAdmin.Add(path);
            }
            catch (Exception ex)
            {
                Log.Event(Events.TakeOverFailed, LogLevel.Warning, $"Taking over task {path} failed: {ex.Message}");
            }
        }
        return needAdmin;
    }
}
