using System.IO;
using EsteemWorks.Kit;
using StartQueue.Core;
using Xunit;

namespace StartQueue.Tests;

/// <summary>
/// The first start of a new install (STANDARD.md, "First start"): StartQueue changes nothing in Windows before
/// it is switched on, and an update of an existing install never sees the introduction.
/// </summary>
// One data folder per process (STARTQUEUE_DATA): the classes that use it run one after the other.
[Collection("Data folder")]
public class IntroductionTests : IDisposable
{
    readonly string temp = Path.Combine(Path.GetTempPath(), "startqueue-intro-" + Guid.NewGuid().ToString("N"));
    string Data => Path.Combine(temp, "data");
    string ConfigFile => Path.Combine(Data, "config.json");

    public IntroductionTests()
    {
        Directory.CreateDirectory(Data);
        Environment.SetEnvironmentVariable("STARTQUEUE_DATA", Data);
        KitApp.Init("StartQueue", Palette.Ember);
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

    static Entry Found(string name) => new()
    {
        Name = name,
        Kind = EntryKind.Program,
        Target = $@"C:\Apps\{name}.exe",
        StillInWindows = true,
        Origin = new NativeOrigin { Kind = OriginKind.RunKey, Location = "HKCU", Name = name, Data = $@"C:\Apps\{name}.exe" },
    };

    [Fact]
    public void A_new_install_starts_with_the_introduction()
    {
        RestorableSettings.Configure();
        // The setup marks a new installation (--setup --new).
        KitIntro.MarkNew();
        var config = ConfigStore.Load();
        Assert.Equal(FirstStart.Welcome, config.FirstStart);
        Assert.False(config.SwitchedOn);
        // Saved once, it stays a new install until switched on.
        ConfigStore.Update(c => c.Settings.GapSeconds = 2);
        Assert.Equal(FirstStart.Welcome, ConfigStore.Load().FirstStart);
        Assert.Contains("\"firstStart\": \"Welcome\"", File.ReadAllText(ConfigFile));
    }

    [Fact]
    public void Without_the_setup_s_mark_a_missing_list_counts_as_switched_on()
    {
        // One per device, or a setup of before: as before, StartQueue takes over at once.
        RestorableSettings.Configure();
        Assert.True(ConfigStore.Load().SwitchedOn);
    }

    [Fact]
    public void A_setup_over_a_list_that_stayed_marks_nothing()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "config-v1.json"), ConfigFile);
        RestorableSettings.Configure();
        KitIntro.MarkNew();
        Assert.False(KitIntro.Pending);
        Assert.True(ConfigStore.Load().SwitchedOn);
    }

    [Fact]
    public void An_update_of_an_existing_install_never_sees_it_and_writes_nothing_new()
    {
        File.Copy(Path.Combine(AppContext.BaseDirectory, "fixtures", "config-v1.json"), ConfigFile);
        RestorableSettings.Configure();
        Assert.True(ConfigStore.Load().SwitchedOn);
        ConfigStore.Update(c => c.Settings.GapSeconds = 3);
        var text = File.ReadAllText(ConfigFile);
        Assert.DoesNotContain("firstStart", text);
        Assert.DoesNotContain("stillInWindows", text);
    }

    [Fact]
    public void Switching_on_leaves_what_was_unticked_with_Windows()
    {
        RestorableSettings.Configure();
        KitIntro.MarkNew();
        var cloud = Found("Cloud");
        var chat = Found("Chat");
        var games = Found("Games");
        ConfigStore.Update(c => c.Entries.AddRange([cloud, chat, games]));

        var config = ConfigStore.Update(c => Introduction.SwitchOn(c, [games.Id], NewEntryMode.NotifyOnly));

        Assert.True(config.SwitchedOn);
        Assert.Equal(NewEntryMode.NotifyOnly, config.Settings.NewEntries);
        Assert.Equal(["Cloud", "Chat"], config.Entries.Select(e => e.Name));
        // Still marked until the sweep has taken them over.
        Assert.All(config.Entries, e => Assert.True(e.StillInWindows));
        Assert.Contains(games.Origin!.Key, config.KeepInWindows);
        Assert.Equal(KeepReason.LeftWithWindows, config.NoteFor(games.Origin.Key).Reason);
        Assert.DoesNotContain("firstStart", File.ReadAllText(ConfigFile));
    }

    [Fact]
    public void Nothing_starts_through_StartQueue_before_it_is_switched_on()
    {
        var config = new Config { FirstStart = FirstStart.LookingAround };
        var added = new Entry { Name = "Added by hand" };
        var found = Found("Cloud");
        config.Entries.AddRange([added, found]);
        Assert.False(Launcher.StartsBy(config, added, null));
        Assert.False(Launcher.StartsBy(config, found, null));

        config.FirstStart = FirstStart.Done;
        Assert.True(Launcher.StartsBy(config, added, null));
        // Windows still starts it until the sweep has taken it over.
        Assert.False(Launcher.StartsBy(config, found, null));
    }
}
