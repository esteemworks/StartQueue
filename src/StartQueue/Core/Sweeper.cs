namespace StartQueue.Core;

public sealed record SweepResult(
    List<Entry> Added,
    int Refreshed,
    bool MachineScanNeeded,
    List<NativeItem> Kept,
    int Leftovers,
    List<string> NewlyKept);

/// <summary>
/// Keeps the Windows autostart clean: everything found there that is not on the keep list moves into
/// StartQueue. Entries that come back (an app re-registers itself after an update) are matched to their
/// existing StartQueue entry and removed again silently; only unknown ones count as new.
/// </summary>
public static class Sweeper
{
    public static SweepResult Run(bool elevated)
    {
        var items = NativeAutostart.Scan();
        var added = new List<Entry>();
        var kept = new List<NativeItem>();
        var newlyKept = new List<string>();
        var refreshed = 0;
        var machineScanNeeded = false;
        var leftovers = 0;

        ConfigStore.Update(config =>
        {
            var keep = new HashSet<string>(config.KeepInWindows, StringComparer.OrdinalIgnoreCase);
            var importing = !config.InitialImportDone;
            var settings = config.Settings;

            foreach (var item in items)
            {
                if (IsSelf(item)) continue;
                if (keep.Contains(item.Origin.Key))
                {
                    kept.Add(item);
                    continue;
                }
                if (item.Origin.Kind == OriginKind.StartupTask && !settings.ManageStoreApps) continue;

                var match = config.Entries.FirstOrDefault(e => e.Origin?.Key == item.Origin.Key)
                            ?? config.Entries.FirstOrDefault(e => SameTarget(e, item));
                var family = FamilyApps.Of(item);

                // A family app installed for every user starts for every user from there; taking it into one
                // user's list would take it from the others.
                if (family != null && item.Origin.NeedsAdmin) continue;

                // "Only tell me": unknown entries stay in Windows, on the keep list, and are reported once.
                // Family apps are no strangers: they come in quietly.
                if (match == null && family == null && !importing && settings.NewEntries == NewEntryMode.NotifyOnly)
                {
                    config.Keep(item.Origin.Key, KeepReason.NotifyOnly);
                    keep.Add(item.Origin.Key);
                    kept.Add(item);
                    newlyKept.Add(item.DisplayName);
                    Log.Info($"Left new {item.Origin.Key} in Windows (notify only)");
                    continue;
                }

                if (item.Origin.NeedsAdmin && !elevated)
                {
                    // In user mode machine-wide entries simply stay with Windows; that is not a problem to report.
                    if (!Elevation.AdminMode) continue;
                    machineScanNeeded = true;
                    leftovers++;
                    continue;
                }

                Entry taken;
                try
                {
                    taken = NativeAutostart.TakeOver(item);
                }
                catch (Exception ex)
                {
                    leftovers++;
                    Log.Event(Events.TakeOverFailed, LogLevel.Warning, $"Taking over {item.Origin.Key} failed: {ex.Message}");
                    continue;
                }

                if (match != null)
                {
                    if (match.Kind == EntryKind.Shortcut && !string.Equals(match.Target, taken.Target, StringComparison.OrdinalIgnoreCase)
                        && Paths.IsInside(match.Target, Paths.Shortcuts) && File.Exists(match.Target))
                        File.Delete(match.Target);

                    match.Origin = taken.Origin;
                    match.Target = taken.Target;
                    match.Arguments = taken.Arguments;
                    if (!string.IsNullOrEmpty(taken.ResolvedTarget)) match.ResolvedTarget = taken.ResolvedTarget;
                    refreshed++;
                    if (family != null)
                    {
                        // A family app writes its Run value when it is asked to start with Windows: its own
                        // switch, its setup, or a policy. That is a wish, not an app sneaking back in.
                        if (!FamilyApps.StartsAtSignIn(config, match))
                        {
                            match.Enabled = true;
                            config.AddToProfiles(match);
                        }
                        Log.Info($"The family app {family.Name} asked to start at sign-in; it starts through StartQueue");
                    }
                    else Log.Event(Events.ReRegisteredRemoved, LogLevel.Info, $"Removed re-registered {item.Origin.Key} again, kept as \"{match.Name}\"");
                }
                else
                {
                    var expected = config.ExpectedTakeOvers.RemoveAll(k => string.Equals(k, item.Origin.Key, StringComparison.OrdinalIgnoreCase)) > 0;
                    taken.IsNew = !importing && !expected && family == null;
                    if (!importing && family == null && settings.NewEntries == NewEntryMode.TakeOverDisabled) taken.Enabled = false;
                    // New apps start last among the enabled ones, ahead of everything switched off. A family app
                    // starts first: its wallpaper, colors or helpers are what the other apps come up into.
                    var position = family != null ? 0
                        : importing || !taken.Enabled
                        ? config.Entries.Count
                        : config.Entries.FindLastIndex(e => e.Enabled) + 1;
                    config.Entries.Insert(position, taken);
                    config.AddToProfiles(taken);
                    // An app that adds itself with a window parameter teaches StartQueue that parameter.
                    if (taken.Kind == EntryKind.Program && StartSwitches.Find(taken.Arguments) is { } found)
                        config.LearnedSwitches[Path.GetFileName(taken.ResolvedTarget.Length > 0 ? taken.ResolvedTarget : taken.Target)] = found.Text;
                    added.Add(taken);
                    Log.Event(Events.EntryTakenOver, LogLevel.Info, $"Took over {item.Origin.Key} as \"{taken.Name}\" ({(taken.Enabled ? "on" : "off")}{(taken.IsNew ? ", new" : "")}{(family != null ? ", family app" : "")})");
                }
            }

            // Taken-over scheduled tasks: an installer or update may switch their sign-in start back on.
            foreach (var entry in config.Entries.Where(e => e.Origin?.Kind == OriginKind.ScheduledTask))
            {
                var path = entry.Origin!.Name;
                if (!TaskAutostart.StartsAtLogon(path)) continue;
                try
                {
                    TaskAutostart.SetLogonTrigger(path, false);
                    refreshed++;
                    Log.Info($"Switched the sign-in start of task {path} off again");
                }
                catch (UnauthorizedAccessException)
                {
                    if (Elevation.AdminMode && !elevated) machineScanNeeded = true;
                }
                catch (Exception ex)
                {
                    Log.Warning($"Task {path}: {ex.Message}");
                }
            }

            if (importing && !machineScanNeeded) config.InitialImportDone = true;
        });

        return new SweepResult(added, refreshed, machineScanNeeded, kept, leftovers, newlyKept);
    }

    /// <summary>StartQueue's own logon entry (user mode installs a Run value when no task is allowed).</summary>
    public static bool IsSelf(NativeItem item)
    {
        try
        {
            return item.Kind == EntryKind.Program && item.Target.Length > 0 &&
                   string.Equals(Path.GetFullPath(Environment.ExpandEnvironmentVariables(item.Target)), Paths.Exe, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    static bool SameTarget(Entry entry, NativeItem item)
    {
        if (entry.Kind != item.Kind) return false;
        return entry.Kind switch
        {
            EntryKind.PackagedApp => string.Equals(entry.Target, item.Target, StringComparison.OrdinalIgnoreCase),
            EntryKind.Program => string.Equals(entry.Target, item.Target, StringComparison.OrdinalIgnoreCase)
                                 && string.Equals(entry.Arguments, item.Arguments, StringComparison.OrdinalIgnoreCase),
            _ => !string.IsNullOrEmpty(item.ResolvedTarget)
                 && string.Equals(entry.ResolvedTarget, item.ResolvedTarget, StringComparison.OrdinalIgnoreCase),
        };
    }
}
