using System.Text.Json;
using System.Text.Json.Nodes;

namespace StartQueue.Core;

/// <summary>
/// StartQueue's part of the family's restorable settings (EsteemWorks.Kit <see cref="KitSettings"/>, STANDARD.md,
/// section 3): which files are its settings and how an older shape of config.json is taken over. The list and the
/// settings are config.json; the startup folder shortcuts it took over live in shortcuts, and entries point there.
/// last-run.json is what happened at the last sign-in, not a setting.
/// </summary>
public static class RestorableSettings
{
    public static readonly string[] Files = ["config.json", "shortcuts"];

    /// <summary>
    /// One step per older version of config.json, on the JSON: [1] = doc => { ... } turns a version 1 file into
    /// version 2. Empty while <see cref="Config.Schema"/> is 1. The pause of 1.3 and 1.4 is still read by the model
    /// itself (<see cref="LegacyProfile.PausedUntil"/>); new shapes only through a step here, with a sample file of the
    /// version before in tests\fixtures.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, Action<JsonObject>> Migrations = new Dictionary<int, Action<JsonObject>>();

    /// <summary>Right after KitApp.Init, before anything reads the config, in every process (the admin helper too).</summary>
    public static void Configure()
    {
        // The admin helper writes the same files: an import waits for it and the other way round.
        KitSettings.Lock = ConfigStore.Lock;
        try
        {
            KitSettings.Configure(Config.Schema, Files, Migrations);
        }
        catch (Exception ex)
        {
            // Never the reason StartQueue does not start at sign-in. The files are known by now, so About still offers
            // export and import; taking over an older shape is tried again at the next start. One way here: the kit's
            // lock across processes was made by the admin helper, which a process with normal rights may not open.
            Log.Warning($"Taking over the settings did not run: {ex.Message}");
        }
        // Profiles of 1.5 and before move to the family's file before the early start asks which one applies.
        try
        {
            Profiles.MoveIn();
        }
        catch (Exception ex)
        {
            Log.Warning($"Moving the profiles to the family's profiles did not run: {ex.Message}");
        }
    }
}

/// <summary>
/// The config is shared by the tray app (normal rights) and the elevated helper. Every change is a
/// read-modify-write under a file lock, so neither side overwrites the other.
/// </summary>
public static class ConfigStore
{
    public static Config Load()
    {
        using var _ = Lock();
        return LoadUnlocked();
    }

    public static Config Update(Action<Config> change)
    {
        using var _ = Lock();
        var config = LoadUnlocked();
        var before = JsonSerializer.Serialize(config, Json.Options);
        change(config);
        // Unchanged: no write, so the file watcher does not make the window reload for nothing.
        if (JsonSerializer.Serialize(config, Json.Options) != before)
        {
            SaveUnlocked(config);
            FamilyApps.Publish(config);
        }
        return config;
    }

    public static IDisposable Lock()
    {
        Directory.CreateDirectory(Paths.Data);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return new FileStream(Paths.LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (attempt < 200)
            {
                Thread.Sleep(50);
            }
        }
    }

    static Config LoadUnlocked()
    {
        if (!File.Exists(Paths.Config)) return new Config();
        try
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(Paths.Config), Json.Options) ?? new Config();
        }
        catch (Exception ex)
        {
            // Never replaced silently with an empty list: the kit keeps a copy under backups, logs it and shows it
            // under About with the last backup to go back to; nothing is saved over it meanwhile. Once per process,
            // the window reads the config often.
            if (KitSettings.Problem == null) KitSettings.ReportUnreadable(Paths.Config, ex);
            throw;
        }
    }

    static void SaveUnlocked(Config config)
    {
        var temp = Paths.Config + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, Json.Options));
        File.Move(temp, Paths.Config, true);
    }
}

public static class LastRunStore
{
    public static LastRun? Load()
    {
        try
        {
            return File.Exists(Paths.LastRun)
                ? JsonSerializer.Deserialize<LastRun>(File.ReadAllText(Paths.LastRun), Json.Options)
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(LastRun run)
    {
        try
        {
            Directory.CreateDirectory(Paths.Data);
            var temp = Paths.LastRun + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(run, Json.Options));
            File.Move(temp, Paths.LastRun, true);
        }
        catch (Exception ex)
        {
            Log.Warning($"Could not save last run: {ex.Message}");
        }
    }
}

/// <summary>Entries the tray app hands to the elevated helper to start with admin rights.</summary>
public static class PendingElevated
{
    public static void Add(Guid id)
    {
        using var _ = ConfigStore.Lock();
        var ids = Read();
        if (!ids.Contains(id)) ids.Add(id);
        File.WriteAllText(Paths.PendingElevated, JsonSerializer.Serialize(ids, Json.Options));
    }

    public static List<Guid> TakeAll()
    {
        using var _ = ConfigStore.Lock();
        var ids = Read();
        if (File.Exists(Paths.PendingElevated)) File.Delete(Paths.PendingElevated);
        return ids;
    }

    static List<Guid> Read()
    {
        try
        {
            return File.Exists(Paths.PendingElevated)
                ? JsonSerializer.Deserialize<List<Guid>>(File.ReadAllText(Paths.PendingElevated), Json.Options) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }
}

/// <summary>Scheduled tasks the tray app asked the elevated helper to take over (tasks only an admin may change).</summary>
public static class PendingTaskTakeOvers
{
    static string File => Path.Combine(Paths.Data, "pending-tasks.json");

    public static void Add(string taskPath)
    {
        using var _ = ConfigStore.Lock();
        var paths = Read();
        if (!paths.Contains(taskPath, StringComparer.OrdinalIgnoreCase)) paths.Add(taskPath);
        System.IO.File.WriteAllText(File, JsonSerializer.Serialize(paths, Json.Options));
    }

    public static List<string> TakeAll()
    {
        using var _ = ConfigStore.Lock();
        var paths = Read();
        if (System.IO.File.Exists(File)) System.IO.File.Delete(File);
        return paths;
    }

    static List<string> Read()
    {
        try
        {
            return System.IO.File.Exists(File)
                ? JsonSerializer.Deserialize<List<string>>(System.IO.File.ReadAllText(File), Json.Options) ?? new()
                : new();
        }
        catch
        {
            return new();
        }
    }
}

public static class TaskTakeOver
{
    /// <summary>Switches off the task's sign-in start and adds it to the list. Runs in the tray app or the admin helper.</summary>
    public static Entry? Apply(string taskPath, bool adminOnly)
    {
        var task = TaskAutostart.Get(taskPath);
        if (task == null || task.Block != TaskBlock.None) return null;
        TaskAutostart.SetLogonTrigger(taskPath, false);
        var entry = TaskAutostart.ToEntry(task, adminOnly);
        var known = false;
        ConfigStore.Update(c =>
        {
            known = c.Entries.Any(e => string.Equals(e.Origin?.Key, entry.Origin!.Key, StringComparison.OrdinalIgnoreCase));
            if (known) return;
            c.Unkeep(entry.Origin!.Key);
            c.Entries.Insert(c.Entries.FindLastIndex(e => e.Enabled) + 1, entry);
            c.AddToProfiles(entry);
        });
        Log.Event(Events.EntryTakenOver, LogLevel.Info, known ? $"Switched the sign-in start of task {taskPath} off again (already in the list)" : $"Took over scheduled task {taskPath}");
        return entry;
    }
}
