using System.IO;
using System.IO.Compression;
using EsteemWorks.Kit;
using StartQueue.Core;
using Xunit;
using AccentChoice = StartQueue.Core.AccentChoice;

namespace StartQueue.Tests;

/// <summary>
/// Settings of every released version are taken over (STANDARD.md, section 3, "Restorable"): each file in
/// tests\fixtures is config.json as that version wrote it, and today's StartQueue has to read all of it.
/// </summary>
public class SettingsTests : IDisposable
{
    readonly string temp = Path.Combine(Path.GetTempPath(), "startqueue-settings-" + Guid.NewGuid().ToString("N"));
    string Data => Path.Combine(temp, "data");

    public SettingsTests()
    {
        Directory.CreateDirectory(Data);
        Environment.SetEnvironmentVariable("STARTQUEUE_DATA", Data);
        KitApp.Init("StartQueue", Palette.Ember);
        // Never the real family profiles of the PC the tests run on.
        FamilyProfiles.FilePath = Path.Combine(temp, "family", "profiles.json");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("STARTQUEUE_DATA", null);
        KitSettings.Lock = null;
        KitSettings.Reload = null;
        try
        {
            Directory.Delete(temp, true);
        }
        catch
        {
            // Temp.
        }
    }

    static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    [Fact]
    public void Every_released_version_has_a_sample_and_a_way_to_today()
    {
        var versions = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "fixtures"), "config-v*.json")
            .Select(f => int.Parse(Path.GetFileNameWithoutExtension(f)["config-v".Length..]))
            .ToList();
        Assert.Contains(Config.Schema, versions);
        foreach (var v in versions.Where(v => v < Config.Schema)) Assert.True(RestorableSettings.Migrations.ContainsKey(v), $"No migration from version {v}");
    }

    [Fact]
    public void The_config_of_1_5_2_is_read_with_everything_in_it()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        RestorableSettings.Configure();
        var config = ConfigStore.Load();

        Assert.Equal(Config.Schema, config.Version);
        Assert.Equal(5, config.Entries.Count);
        Assert.Equal([EntryKind.Program, EntryKind.Shortcut, EntryKind.ScheduledTask, EntryKind.PackagedApp, EntryKind.Program], config.Entries.Select(e => e.Kind));

        var discord = config.Entries[0];
        Assert.Equal(WindowMode.Background, discord.Window);
        Assert.Equal(5, discord.DelaySeconds);
        Assert.True(discord.IsNew);
        Assert.Equal(OriginKind.RunKey, discord.Origin!.Kind);

        var obs = config.Entries[1];
        Assert.True(obs.WaitUntilRunning);
        Assert.True(obs.QuitsOnClose);
        Assert.Equal(ParameterMode.Add, obs.Parameters);
        Assert.Equal("--minimize-to-tray", obs.ExtraArguments);

        var task = config.Entries[2];
        Assert.False(task.Enabled);
        Assert.Equal(240, task.RepeatMinutes);
        Assert.True(task.Origin!.AdminOnly);

        Assert.Equal(ParameterMode.Replace, config.Entries[3].Parameters);
        Assert.True(config.Entries[4].RunElevated);
        Assert.Null(config.Entries[4].Origin);

        Assert.Equal(ThemeChoice.Dark, config.Settings.Theme);
        Assert.Equal(AccentChoice.StartQueue, config.Settings.Accent);
        Assert.Equal(LanguageChoice.German, config.Settings.Language);
        Assert.Equal(NewEntryMode.TakeOverDisabled, config.Settings.NewEntries);
        Assert.Equal(90, config.Settings.WaitLimitSeconds);
        Assert.Equal("2 -", config.Settings.PreparedUndo);

        // The profiles moved to the family's file, for StartQueue; what starts in them stayed in config.json.
        Assert.Equal(2, Profiles.All.Count);
        var work = Profiles.All[0];
        Assert.Equal(config.LegacyProfiles[0].Id, work.Id);
        Assert.Equal([Profiles.App], work.Apps);
        Assert.Equal(ProfileOutside.Standard, work.Outside);
        Assert.Equal(new DateTime(2027, 1, 4), work.PauseUntil);
        Assert.Equal(3, Profiles.On(config, work.Id).Count);
        // And stayed in config.json as they were, for going back to 1.5.
        Assert.Equal(2, config.LegacyProfiles.Count);
        Assert.Equal(3, config.LegacyProfiles[0].On.Count);

        Assert.Contains("runkey:hkcu:onedrive", config.KeepInWindows);
        Assert.Equal(KeepReason.ReturnedByUser, config.KeepNotes[1].Reason);
        Assert.Equal("--minimize-to-tray", config.LearnedSwitches["obs64.exe"]);

        // Same version: nothing to take over, so no backup.
        Assert.Empty(KitSettings.Backups());
        Assert.Null(KitSettings.Problem);
    }

    [Fact]
    public void A_change_saved_today_keeps_the_rest_of_the_file()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        RestorableSettings.Configure();
        ConfigStore.Update(c => c.Settings.GapSeconds = 4);
        var config = ConfigStore.Load();
        Assert.Equal(4, config.Settings.GapSeconds);
        Assert.Equal(5, config.Entries.Count);
        Assert.Equal(new DateTime(2027, 1, 4), config.LegacyProfiles[0].PauseUntil);
    }

    [Fact]
    public void The_accent_is_kept_as_chosen_and_matches_the_kit()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        RestorableSettings.Configure();
        foreach (var accent in Enum.GetValues<AccentChoice>())
        {
            ConfigStore.Update(c => c.Settings.Accent = accent);
            Assert.Equal(accent, ConfigStore.Load().Settings.Accent);
            // The kit's row gives back what it was given.
            Assert.Equal(accent, accent.ToKit().FromKit());
        }
        // Saved by name, so 1.6 and earlier read the three they know as before.
        ConfigStore.Update(c => c.Settings.Accent = AccentChoice.Sand);
        Assert.Contains("\"accent\": \"Sand\"", File.ReadAllText(Path.Combine(Data, "config.json")));
        Assert.Equal(5, ConfigStore.Load().Entries.Count);
        Assert.Equal(AccentChoice.Profile, new Settings().Accent);
    }

    [Fact]
    public void Export_and_import_carry_the_list_and_the_shortcuts()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        Directory.CreateDirectory(Path.Combine(Data, "shortcuts"));
        File.WriteAllText(Path.Combine(Data, "shortcuts", "OBS Studio.lnk"), "shortcut");
        File.WriteAllText(Path.Combine(Data, "last-run.json"), "{}");
        RestorableSettings.Configure();

        var zip = Path.Combine(temp, "export.zip");
        KitSettings.Export(zip);
        using (var archive = ZipFile.OpenRead(zip))
        {
            var names = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("config.json", names);
            Assert.Contains("shortcuts/OBS Studio.lnk", names);
            // When the profiles apply, from the family's file.
            Assert.Contains("esteemworks/profiles.json", names);
            // What happened at the last sign-in is no setting.
            Assert.DoesNotContain("last-run.json", names);
        }

        ConfigStore.Update(c => c.Entries.Clear());
        File.Delete(Path.Combine(Data, "shortcuts", "OBS Studio.lnk"));
        var result = KitSettings.Import(zip);

        Assert.True(result.Ok, result.Message);
        Assert.Equal(5, ConfigStore.Load().Entries.Count);
        Assert.True(File.Exists(Path.Combine(Data, "shortcuts", "OBS Studio.lnk")));
        Assert.Single(KitSettings.Backups(), b => b.Reason == "import");
    }

    [Fact]
    public void Profiles_move_once_so_one_deleted_since_does_not_come_back()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        RestorableSettings.Configure();
        var first = Profiles.All[0];
        FamilyProfiles.Leave(first.Id, Profiles.App);

        // The next start, and one after going back to 1.5 and forward again: its profiles are still in config.json.
        RestorableSettings.Configure();

        Assert.Single(Profiles.All);
        Assert.DoesNotContain(Profiles.All, p => p.Id == first.Id);
    }

    [Fact]
    public void After_going_back_to_1_5_the_lists_of_what_starts_are_taken_from_its_profiles()
    {
        File.Copy(Fixture("config-v1.json"), Path.Combine(Data, "config.json"));
        RestorableSettings.Configure();
        var work = Profiles.All[0];
        // 1.5 saves only what it knows: its profiles, now with an app switched off there, and no list by profile id.
        ConfigStore.Update(c =>
        {
            c.ProfileEntries.Clear();
            c.LegacyProfiles[0].On.RemoveAt(0);
        });

        RestorableSettings.Configure();

        Assert.Equal(2, Profiles.On(ConfigStore.Load(), work.Id).Count);
    }

    [Fact]
    public void A_moved_profile_keeps_the_color_it_showed_and_the_pause_of_1_3()
    {
        var old = new LegacyProfile { Name = "Old", Color = "ozean", PausedUntil = new DateTime(2026, 11, 20) };
        var unnamed = new LegacyProfile { Name = "No color" };
        var moved = Profiles.Convert([old, unnamed], old);
        Assert.Equal("himmel", moved.Color);
        Assert.Equal(new DateTime(2026, 11, 21), moved.PauseUntil);
        // None chosen: the one its place gave it, the second color of the order.
        Assert.Equal("wald", Profiles.Convert([old, unnamed], unnamed).Color);
    }

    [Fact]
    public void An_unreadable_config_is_kept_and_reported_never_replaced()
    {
        var file = Path.Combine(Data, "config.json");
        File.WriteAllText(file, "{ \"version\": 1, \"entries\": [ {");
        RestorableSettings.Configure();

        Assert.NotNull(KitSettings.Problem);
        Assert.ThrowsAny<Exception>(() => ConfigStore.Load());
        Assert.Equal("{ \"version\": 1, \"entries\": [ {", File.ReadAllText(file));
        Assert.Single(Directory.GetFiles(KitSettings.BackupFolder, "* unreadable config.json"));
    }
}
