using Microsoft.Win32;

namespace StartQueue.Core;

/// <summary>
/// StartQueue as the starter of its siblings in the EsteemWorks family (DynaTones and those to come). A family app
/// writes its start at sign-in into the user's Run key like any other app; StartQueue takes it over quietly, early in
/// the order and without "new", and tells the family what it does with it, so the app's own switch shows the truth
/// and changes StartQueue's entry instead of writing the Run key again:
/// <list type="bullet">
/// <item>HKCU\Software\EsteemWorks\Apps\StartQueue\Starts holds one DWORD per family app in the list: 1 when it
/// starts at sign-in (Standard or a profile), 0 when it is in the list but off.</item>
/// <item>The pipe command "autostart" with the lines &lt;app&gt; and on|off switches the app's entry.</item>
/// <item>A family app that writes its Run value again asks to be started; that is no "re-registered" app.</item>
/// </list>
/// </summary>
public static class FamilyApps
{
    const string StartsKey = Family.KeyPath + @"\StartQueue\Starts";

    static IReadOnlyList<FamilyApp> siblings = [];
    static DateTime siblingsReadAt;

    /// <summary>The installed siblings, read again at most every 30 s.</summary>
    static IReadOnlyList<FamilyApp> Siblings
    {
        get
        {
            if (DateTime.UtcNow - siblingsReadAt > TimeSpan.FromSeconds(30))
            {
                siblings = Family.Siblings();
                siblingsReadAt = DateTime.UtcNow;
            }
            return siblings;
        }
    }

    public static FamilyApp? Of(Entry entry) =>
        entry.Kind == EntryKind.Program ? OfProgram(entry.Target) : null;

    public static FamilyApp? Of(NativeItem item) =>
        item.Kind == EntryKind.Program ? OfProgram(item.Target) : null;

    static FamilyApp? OfProgram(string target)
    {
        if (target.Length == 0) return null;
        try
        {
            var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(target));
            return Siblings.FirstOrDefault(a => string.Equals(Path.GetFullPath(a.Exe), full, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Whether the entry starts at sign-in in Standard or in any profile.</summary>
    public static bool StartsAtSignIn(Config config, Entry entry) =>
        Profiles.StartsAnywhere(config, entry);

    /// <summary>Switches a family app's entry on or off, as its own switch asked; adds it when it is missing.</summary>
    public static bool Set(Config config, FamilyApp app, bool on)
    {
        var entry = config.Entries.FirstOrDefault(e => Of(e)?.Name == app.Name);
        if (entry == null)
        {
            if (!on) return false;
            entry = New(app);
            config.Entries.Insert(0, entry);
            config.AddToProfiles(entry);
            Log.Info($"Added the family app {app.Name}, as it asked to start at sign-in");
            return true;
        }
        if (StartsAtSignIn(config, entry) == on) return false;
        entry.Enabled = on;
        if (on) config.AddToProfiles(entry);
        else config.RemoveFromProfiles(entry.Id);
        Log.Info($"The family app {app.Name} switched its start at sign-in {(on ? "on" : "off")}");
        return true;
    }

    /// <summary>An entry for a family app that asked through the pipe, as if taken over from its Run value, so
    /// uninstalling StartQueue gives the app its own start back.</summary>
    static Entry New(FamilyApp app) => new()
    {
        Name = app.Name,
        Kind = EntryKind.Program,
        Target = app.Exe,
        Arguments = "--autostart",
        ResolvedTarget = app.Exe,
        ProcessName = Path.GetFileNameWithoutExtension(app.Exe),
        Origin = new NativeOrigin
        {
            Kind = OriginKind.RunKey,
            Location = "HKCU",
            Name = app.Name,
            Data = $"\"{app.Exe}\" --autostart",
        },
    };

    /// <summary>Writes what StartQueue does with each family app in its list; called after every saved change.</summary>
    public static void Publish(Config config)
    {
        try
        {
            var states = config.Entries
                // One Windows still starts (before StartQueue is switched on) is not StartQueue's: the app's own switch
                // writes its Run value as without StartQueue.
                .Where(e => !e.StillInWindows)
                .Select(e => (App: Of(e), Entry: e))
                .Where(x => x.App != null)
                .GroupBy(x => x.App!.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Any(x => StartsAtSignIn(config, x.Entry)), StringComparer.OrdinalIgnoreCase);
            using var key = Registry.CurrentUser.CreateSubKey(StartsKey);
            foreach (var name in key.GetValueNames().Where(n => !states.ContainsKey(n))) key.DeleteValue(name, false);
            foreach (var (name, on) in states)
                if (key.GetValue(name) is not int current || current != (on ? 1 : 0)) key.SetValue(name, on ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Log.Warning($"Telling the family which apps StartQueue starts failed: {ex.Message}");
        }
    }
}
