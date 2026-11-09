using System.Text.Json;
using System.Text.Json.Serialization;

namespace StartQueue.Core;

public enum EntryKind { Program, Shortcut, PackagedApp, ScheduledTask }

public enum OriginKind { RunKey, StartupFolder, StartupTask, ScheduledTask }

/// <summary>
/// The app's window after a start at sign-in. Background closes it, for apps that keep running in the
/// notification area; if the app quits instead, StartQueue starts it again minimized and remembers that.
/// </summary>
public enum WindowMode { Normal, Minimized, Background }

/// <summary>Why an entry stays in the Windows autostart.</summary>
public enum KeepReason { Unknown, PartOfWindows, PartOfDriver, ReturnedByUser, NotifyOnly }

public sealed class KeepNote
{
    public string Key { get; set; } = "";
    public KeepReason Reason { get; set; }
    public DateTime At { get; set; } = DateTime.Now;
}

/// <summary>Where an entry lived in the Windows autostart before StartQueue took it over.</summary>
public sealed class NativeOrigin
{
    public OriginKind Kind { get; set; }

    /// <summary>HKCU, HKLM, HKLM32 for Run keys; User or Common for startup folders; Package for startup tasks.</summary>
    public string Location { get; set; } = "";

    /// <summary>Run value name, startup folder file name, or "PackageFamilyName!TaskId".</summary>
    public string Name { get; set; } = "";

    /// <summary>Run command line, original startup folder path, or AUMID of a startup task.</summary>
    public string Data { get; set; } = "";

    public bool ExpandString { get; set; }
    public bool WasEnabled { get; set; } = true;
    public DateTime TakenOverAt { get; set; } = DateTime.Now;

    /// <summary>Scheduled tasks only: changing the task needed admin rights.</summary>
    public bool AdminOnly { get; set; }

    [JsonIgnore] public string Key => MakeKey(Kind, Location, Name);
    [JsonIgnore] public bool NeedsAdmin => Location is "HKLM" or "HKLM32" or "Common" || AdminOnly;

    public static string MakeKey(OriginKind kind, string location, string name) =>
        $"{kind}:{location}:{name}".ToLowerInvariant();

    public string Describe() => Kind switch
    {
        OriginKind.RunKey => Strings.F(Strings.OriginRun, Location, Name),
        OriginKind.StartupFolder => Strings.F(Location == "Common" ? Strings.OriginFolderAll : Strings.OriginFolderUser, Name),
        OriginKind.ScheduledTask => Strings.F(Strings.OriginScheduledTask, Name),
        _ => Strings.F(Strings.OriginTask, Name.Split('!').Last())
    };
}

/// <summary>
/// A profile as StartQueue 1.5 and before kept it in config.json. Since 1.6 when a profile applies is the family's
/// (EsteemWorks.Kit <see cref="FamilyProfiles"/>) and which entries start in it is <see cref="Config.ProfileEntries"/>;
/// these stay as they were, untouched, so an older version still runs after going back to it (<see cref="Profiles.MoveIn"/>).
/// </summary>
public sealed class LegacyProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";

    /// <summary>One of <see cref="ProfileColors.All"/>; empty: picked by its place in the list.</summary>
    public string? Color { get; set; }

    /// <summary>Days it applies on; empty: every day.</summary>
    public List<DayOfWeek> Days { get; set; } = new();

    /// <summary>"HH:mm"; both empty: all day. From later than To: across midnight.</summary>
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    /// <summary>What applies on its days outside its hours: the profiles further down, Standard, or one chosen profile.</summary>
    public OutsideHours Outside { get; set; }

    /// <summary>The profile for <see cref="OutsideHours.Profile"/>.</summary>
    public Guid? OutsideProfile { get; set; }

    /// <summary>Skipped from this moment on; null: from when the pause was set.</summary>
    public DateTime? PauseFrom { get; set; }

    /// <summary>Applies again from this moment on (exclusive end of the pause).</summary>
    public DateTime? PauseUntil { get; set; }

    /// <summary>Skipped until resumed by hand (from <see cref="PauseFrom"/> on, if set).</summary>
    public bool PausedIndefinitely { get; set; }

    /// <summary>Version 1.3 and 1.4: skipped up to and including this day. Read, then moved to <see cref="PauseUntil"/>.</summary>
    public DateTime? PausedUntil { get; set; }

    /// <summary>Entries that start in this profile.</summary>
    public List<Guid> On { get; set; } = new();
}

public sealed class Entry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public EntryKind Kind { get; set; }

    /// <summary>Program path, shortcut path in the StartQueue folder, or AUMID.</summary>
    public string Target { get; set; } = "";

    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";

    /// <summary>Executable the entry ends up running, used for the icon. Empty for Store apps.</summary>
    public string ResolvedTarget { get; set; } = "";

    /// <summary>Store apps: the package-relative program their startup task runs, started inside the package.</summary>
    public string PackageExecutable { get; set; } = "";

    /// <summary>Process name without .exe, used to tell whether the app already runs.</summary>
    public string ProcessName { get; set; } = "";

    public bool Enabled { get; set; } = true;
    public int DelaySeconds { get; set; }
    public bool WaitUntilRunning { get; set; }
    public bool RunElevated { get; set; }

    /// <summary>How the window comes up when the sign-in sequence starts the app. Null: from where it came from.</summary>
    public WindowMode? Window { get; set; }

    /// <summary>Seen at the last start at sign-in: whether the app showed a window at all. Null: not seen yet.</summary>
    public bool? HasWindow { get; set; }

    /// <summary>Seen at sign-in: the app quits when its window is closed, so "background" cannot work for it.</summary>
    public bool QuitsOnClose { get; set; }

    /// <summary>Started again every this many minutes after the sign-in start; 0: once.</summary>
    public int RepeatMinutes { get; set; }

    /// <summary>For how long the repetition goes on, in minutes; 0: as long as StartQueue runs.</summary>
    public int RepeatForMinutes { get; set; }

    /// <summary>Own start parameters: none, added to the ones the app brings, or in their place.</summary>
    public ParameterMode Parameters { get; set; }

    /// <summary>Added after the app's own start parameters (<see cref="ParameterMode.Add"/>).</summary>
    public string ExtraArguments { get; set; } = "";

    /// <summary>Used instead of the app's own start parameters (<see cref="ParameterMode.Replace"/>).</summary>
    public string OwnArguments { get; set; } = "";

    public bool IsNew { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.Now;
    public NativeOrigin? Origin { get; set; }

    public Entry Clone() => JsonSerializer.Deserialize<Entry>(JsonSerializer.Serialize(this, Json.Options), Json.Options)!;
}

public sealed class Config
{
    /// <summary>Today's shape of config.json. Raise it only with a step in <see cref="RestorableSettings.Migrations"/>.</summary>
    public const int Schema = 1;

    public int Version { get; set; } = Schema;
    public List<Entry> Entries { get; set; } = new();

    /// <summary>Origin keys of autostart entries StartQueue leaves in Windows.</summary>
    public List<string> KeepInWindows { get; set; } = new()
    {
        NativeOrigin.MakeKey(OriginKind.RunKey, "HKLM", "SecurityHealth"),
        NativeOrigin.MakeKey(OriginKind.RunKey, "HKLM", "RtkAudUService"),
    };

    /// <summary>How each entry came onto the keep list, so the app can say why it stays in Windows.</summary>
    public List<KeepNote> KeepNotes { get; set; } = new();

    /// <summary>Entries you chose to take over that the admin task still has to move; they are not "new".</summary>
    public List<string> ExpectedTakeOvers { get; set; } = new();

    public bool InitialImportDone { get; set; }

    public Settings Settings { get; set; } = new();

    /// <summary>
    /// The profiles of version 1.5 and before, kept for going back to it; read only by <see cref="Profiles.MoveIn"/>.
    /// Today's profiles are the family's (<see cref="Profiles.All"/>).
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<LegacyProfile> LegacyProfiles { get; set; } = new();

    /// <summary>
    /// Which entries start in each of the family's profiles, by profile id. At sign-in the profile that applies decides
    /// which entries start; if none does, the entries' own switches do ("Standard").
    /// </summary>
    public Dictionary<Guid, List<Guid>> ProfileEntries { get; set; } = new();

    /// <summary>Start parameters for a start without window, by program file, learned from autostart entries.</summary>
    public Dictionary<string, string> LearnedSwitches { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Puts a new entry into every profile the way it is switched on or off itself.</summary>
    public void AddToProfiles(Entry entry)
    {
        if (!entry.Enabled) return;
        foreach (var profile in Profiles.All)
        {
            var on = Profiles.On(this, profile.Id);
            if (!on.Contains(entry.Id)) on.Add(entry.Id);
        }
    }

    /// <summary>Takes an entry out of every profile.</summary>
    public void RemoveFromProfiles(Guid entry)
    {
        foreach (var on in ProfileEntries.Values) on.Remove(entry);
    }

    /// <summary>The preset exceptions: components that belong to Windows or a driver and start best early.</summary>
    static readonly Dictionary<string, KeepReason> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        [NativeOrigin.MakeKey(OriginKind.RunKey, "HKLM", "SecurityHealth")] = KeepReason.PartOfWindows,
        [NativeOrigin.MakeKey(OriginKind.RunKey, "HKLM", "RtkAudUService")] = KeepReason.PartOfDriver,
    };

    public void Keep(string key, KeepReason reason)
    {
        if (!KeepInWindows.Contains(key, StringComparer.OrdinalIgnoreCase)) KeepInWindows.Add(key);
        KeepNotes.RemoveAll(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
        KeepNotes.Add(new KeepNote { Key = key, Reason = reason });
    }

    public void Unkeep(string key)
    {
        KeepInWindows.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        KeepNotes.RemoveAll(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public KeepNote NoteFor(string key) =>
        KeepNotes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.OrdinalIgnoreCase))
        ?? new KeepNote { Key = key, Reason = Presets.GetValueOrDefault(key, KeepReason.Unknown), At = DateTime.MinValue };
}

/// <summary>
/// StartQueue: the app's own deep orange with a gradient. Windows: the accent color set in Windows. Profile: the color
/// of the profile the window shows, the default. Sand: the family's quiet beige, the same in every EsteemWorks app.
/// The names are in config.json and on the command line, so new ones only ever come at the end and none is renamed.
/// The theme itself (System, Light, Dark) is EsteemWorks.Kit's <see cref="ThemeChoice"/>, with the same names.
/// </summary>
public enum AccentChoice { StartQueue, Windows, Profile, Sand }

public static class AccentChoices
{
    /// <summary>The same choice in EsteemWorks.Kit's terms: StartQueue's colors are its brand, a profile is what the window shows.</summary>
    public static EsteemWorks.Kit.AccentChoice ToKit(this AccentChoice accent) => accent switch
    {
        AccentChoice.Windows => EsteemWorks.Kit.AccentChoice.Windows,
        AccentChoice.Profile => EsteemWorks.Kit.AccentChoice.Shown,
        AccentChoice.Sand => EsteemWorks.Kit.AccentChoice.Sand,
        _ => EsteemWorks.Kit.AccentChoice.Brand,
    };

    /// <summary>The kit's choice (from the row <c>AccentRow</c> builds) in StartQueue's terms.</summary>
    public static AccentChoice FromKit(this EsteemWorks.Kit.AccentChoice accent) => accent switch
    {
        EsteemWorks.Kit.AccentChoice.Windows => AccentChoice.Windows,
        EsteemWorks.Kit.AccentChoice.Shown => AccentChoice.Profile,
        EsteemWorks.Kit.AccentChoice.Sand => AccentChoice.Sand,
        _ => AccentChoice.StartQueue,
    };
}

public enum ParameterMode { AsBefore, Add, Replace }

public enum OutsideHours { Next, Standard, Profile }

public enum LanguageChoice { System, German, English }

public enum NewEntryMode { TakeOverEnabled, TakeOverDisabled, NotifyOnly }

public sealed class Settings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;
    public AccentChoice Accent { get; set; } = AccentChoice.Profile;
    public LanguageChoice Language { get; set; } = LanguageChoice.System;

    /// <summary>What happens when an app writes itself into the Windows autostart.</summary>
    public NewEntryMode NewEntries { get; set; } = NewEntryMode.TakeOverEnabled;

    public bool NotifyNewEntries { get; set; } = true;

    /// <summary>Take over the startup tasks of Store apps too.</summary>
    public bool ManageStoreApps { get; set; } = true;

    /// <summary>Wait after sign-in before the first app starts.</summary>
    public int StartDelaySeconds { get; set; }

    /// <summary>Added to every app's own delay.</summary>
    public int GapSeconds { get; set; }

    /// <summary>Longest wait for apps set to "wait until it runs".</summary>
    public int WaitLimitSeconds { get; set; } = 60;

    public bool NotifyAfterSequence { get; set; }

    /// <summary>
    /// Set when StartQueue switched on the automatic sign-in for prepared shutdowns: the Windows Hello only value and
    /// the opt-out value from before, each a number or "-" for none, so "Undo" puts back exactly that.
    /// </summary>
    public string? PreparedUndo { get; set; }

    /// <summary>Extra full scan besides watching the registry and folders; 0 means never.</summary>
    public int ScanIntervalMinutes { get; set; } = 10;
}

/// <summary>What happened to an app in the sign-in sequence. Stored as a fact; the text is made when shown, in the current language.</summary>
public enum RunOutcome { Started, StartedAsAdmin, WasRunning, Failed, AdminTaskMissing }

public sealed class LastRunItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public double Seconds { get; set; }
    public RunOutcome Outcome { get; set; }

    /// <summary>With "wait until it runs": waited this many seconds without seeing the app run.</summary>
    public int? NotDetectedAfter { get; set; }

    /// <summary>The error Windows reported when the start failed.</summary>
    public string Error { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool Failed => Outcome is RunOutcome.Failed or RunOutcome.AdminTaskMissing;
}

/// <summary>The scheduled task StartQueue uses for admin starts is gone (install.ps1 sets it up).</summary>
public sealed class AdminTaskMissingException() : InvalidOperationException("The StartQueue admin task is missing.");

public sealed class LastRun
{
    public DateTime StartedAt { get; set; }

    /// <summary>Started at sign-in: StartedAt is the sign-in time. Otherwise by hand, from that moment.</summary>
    public bool AtSignIn { get; set; }

    public List<LastRunItem> Items { get; set; } = new();
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };
}
