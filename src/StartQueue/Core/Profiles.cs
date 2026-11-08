namespace StartQueue.Core;

/// <summary>
/// StartQueue's side of the family's profiles (EsteemWorks.Kit <see cref="FamilyProfiles"/>): when a profile applies is
/// in the family's file, shared with the other apps; which entries start in it is StartQueue's own, in config.json by
/// profile id (<see cref="Config.ProfileEntries"/>). No profile applying means "Standard": the entries' own switches.
/// </summary>
public static class Profiles
{
    /// <summary>StartQueue's name in a profile's apps.</summary>
    public const string App = "StartQueue";

    /// <summary>The profiles StartQueue uses, in order of precedence.</summary>
    public static List<FamilyProfile> All => FamilyProfiles.Visible(App);

    public static FamilyProfile? Find(Guid? id) => id is { } wanted ? All.FirstOrDefault(p => p.Id == wanted) : null;

    /// <summary>The profile that applies at <paramref name="at"/>, by its schedule or chosen by hand; null: Standard.</summary>
    public static FamilyProfile? Active(DateTime at) => FamilyProfiles.Active(App, at);

    /// <summary>The switch by hand that decides now; null when the schedule does.</summary>
    public static ProfileSwitch? Manual(DateTime at) => FamilyProfiles.ManualFor(App, at);

    /// <summary>The entries that start in a profile, the list itself to change. Made on first use.</summary>
    public static List<Guid> On(Config config, Guid profile)
    {
        if (!config.ProfileEntries.TryGetValue(profile, out var on))
            config.ProfileEntries[profile] = on = [.. LegacyOn(config, profile)];
        return on;
    }

    /// <summary>
    /// A profile's list of 1.5 and before, for a profile config.json has no list for yet: right after the move, or
    /// after going back to an older version, which saves only its own lists.
    /// </summary>
    static IReadOnlyCollection<Guid> LegacyOn(Config config, Guid profile) =>
        config.LegacyProfiles.FirstOrDefault(p => p.Id == profile)?.On ?? [];

    /// <summary>Whether the entry starts in this profile (null: Standard).</summary>
    public static bool IsOn(Config config, Entry entry, FamilyProfile? profile) =>
        profile == null
            ? entry.Enabled
            : (config.ProfileEntries.TryGetValue(profile.Id, out var on) ? on : LegacyOn(config, profile.Id)).Contains(entry.Id);

    /// <summary>Whether the entry starts in Standard or in any profile.</summary>
    public static bool StartsAnywhere(Config config, Entry entry) => entry.Enabled || All.Any(p => IsOn(config, entry, p));

    // ---------- Texts, from the family's ----------

    public static string DescribeCondition(FamilyProfile profile) => FamilyProfiles.DescribeCondition(profile);

    /// <summary>What decides the profile, as one phrase: "Mo bis Fr, 07:00 bis 18:00, sonst Standard, pausiert bis 14.11."</summary>
    public static string Describe(FamilyProfile profile, DateTime at) => FamilyProfiles.Describe(All, profile, at);

    public static string DescribePause(FamilyProfile profile, DateTime at) => FamilyProfiles.DescribePause(profile, at);

    public static bool IsPaused(FamilyProfile profile, DateTime at) => FamilyProfiles.IsPaused(profile, at);

    public static bool HasHours(FamilyProfile profile) => FamilyProfiles.HasHours(profile);

    // ---------- From config.json to the family's file ----------

    /// <summary>
    /// Moves the profiles of 1.5 and before to the family's file, each once (the file remembers which), with
    /// StartQueue as the app that uses them; the lists of what starts stay in config.json by profile id. The old
    /// profiles stay in config.json as they were, so an older version still runs after going back to it. In every
    /// process, before the early start reads which profile applies.
    /// </summary>
    public static void MoveIn()
    {
        var config = ConfigStore.Load();
        if (config.LegacyProfiles.Count == 0) return;
        var family = FamilyProfiles.Load();
        // A file that cannot be read is never written over; the next start tries again.
        if (FamilyProfiles.Problem != null) return;
        var moving = config.LegacyProfiles.Where(p => !family.Imported.Contains(p.Id)).ToList();
        if (moving.Count > 0)
        {
            FamilyProfiles.Update(f =>
            {
                foreach (var old in moving)
                {
                    if (f.Profiles.All(p => p.Id != old.Id)) f.Profiles.Add(Convert(config.LegacyProfiles, old));
                    f.Imported.Add(old.Id);
                }
            });
            Log.Info($"Moved {moving.Count} profiles to the family's profiles (EsteemWorks\\profiles.json)");
        }
        var ids = All.Select(p => p.Id).ToHashSet();
        ConfigStore.Update(c =>
        {
            foreach (var old in c.LegacyProfiles.Where(p => ids.Contains(p.Id) && !c.ProfileEntries.ContainsKey(p.Id)))
                c.ProfileEntries[old.Id] = [.. old.On];
        });
    }

    /// <summary>The same profile in the family's shape: the color it showed, the pause of 1.3 and 1.4 as a span.</summary>
    public static FamilyProfile Convert(IReadOnlyList<LegacyProfile> all, LegacyProfile old) => new()
    {
        Id = old.Id,
        Name = old.Name,
        // An earlier color name as today's; none chosen: the one its place in the list gave it.
        Color = ProfileColors.Find(old.Color)?.Id ?? FamilyProfiles.ColorOrder[Math.Max(0, all.ToList().IndexOf(old)) % FamilyProfiles.ColorOrder.Count],
        Days = [.. old.Days],
        From = old.From,
        To = old.To,
        Outside = old.Outside switch
        {
            OutsideHours.Standard => ProfileOutside.Standard,
            OutsideHours.Profile => ProfileOutside.Profile,
            _ => ProfileOutside.Next,
        },
        OutsideProfile = old.OutsideProfile,
        PauseFrom = old.PauseFrom,
        PauseUntil = old.PauseUntil ?? old.PausedUntil?.Date.AddDays(1),
        PausedIndefinitely = old.PausedIndefinitely,
        Apps = [App],
    };
}
