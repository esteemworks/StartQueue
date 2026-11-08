using System.Text.RegularExpressions;

namespace StartQueue.Core;

/// <summary>A start parameter that keeps an app's window away: in the background or minimized.</summary>
public sealed record StartSwitch(string Text, WindowMode Mode);

/// <summary>Why an entry's window comes up the way it does.</summary>
public enum WindowSource
{
    /// <summary>Nothing special: the app starts as it is.</summary>
    None,
    /// <summary>A start parameter of the program, which StartQueue can set or remove.</summary>
    Switch,
    /// <summary>A start parameter inside a shortcut or scheduled task, which stays as it is.</summary>
    FixedSwitch,
    /// <summary>The shortcut itself starts the app minimized.</summary>
    Shortcut,
    /// <summary>StartQueue minimizes or closes the window after the start.</summary>
    StartQueue,
    /// <summary>The app showed no window at its last start (a service or a notification area app).</summary>
    NoWindow,
}

public sealed record StartWindowState(WindowMode Mode, WindowSource Source, string Switch = "");

/// <summary>
/// Start parameters for a start without window. Apps put them into their own autostart entries ("-background",
/// "--silent", "/minimize" …): StartQueue recognizes them there, knows them for the programs listed below and
/// learns more from every autostart entry it takes over. For apps without a known parameter StartQueue handles the
/// window itself after the start. Parameters are not tried out: unknown ones may do something else entirely.
/// </summary>
public static class StartSwitches
{
    /// <summary>Recognized anywhere in a command line; which mode each means, see <see cref="ModeOf"/>.</summary>
    static readonly string[] Tokens =
    [
        "-background", "/background", "--background", "-silent", "/silent", "--silent", "-hidden", "/hidden", "--hidden",
        "--headless", "--launch-hidden", "--start-minimized", "-minimized", "/minimized", "--minimized", "--minimize-to-tray",
        "--minimized-mode", "/minimize", "-runinbk", "--autostarted", "--autorun", "--run-as-service", "--win-session-start",
        "--no-startup-window", "-tray", "/tray", "--tray", "-WindowStyle Hidden",
    ];

    /// <summary>Parameters seen in the autostart entries these programs write themselves.</summary>
    static readonly Dictionary<string, string> Library = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RtkAudUService64.exe"] = "-background",
        ["OneDrive.exe"] = "/background",
        ["steam.exe"] = "-silent",
        ["EpicGamesLauncher.exe"] = "-silent",
        ["SignalRgbLauncher.exe"] = "--silent",
        ["WaveLink.exe"] = "-runinbk",
        ["Battle.net.exe"] = "--autostarted",
        ["Snap Camera.exe"] = "--minimized-mode",
        ["DUC40.exe"] = "/minimize",
        ["LM Studio.exe"] = "--run-as-service",
        ["iCUE Launcher.exe"] = "--autorun",
        ["msedge.exe"] = "--no-startup-window",
        ["obs64.exe"] = "--minimize-to-tray",
        ["Discord.exe"] = "--start-minimized",
    };

    static Dictionary<string, string> learned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Takes in what the configuration has learned, plus parameters already in its entries.</summary>
    public static void Learn(Config config)
    {
        var all = new Dictionary<string, string>(config.LearnedSwitches, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in config.Entries.Where(e => e.Kind == EntryKind.Program))
            if (Find(entry.Arguments) is { } found && FileOf(entry) is { Length: > 0 } file)
                all.TryAdd(file, found.Text);
        learned = all;
    }

    /// <summary>The window parameter in a command line, if any.</summary>
    public static StartSwitch? Find(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) return null;
        foreach (var token in Tokens)
            if (Regex.IsMatch(arguments, $@"(^|[\s""']){Regex.Escape(token)}(?=$|[\s""'=])", RegexOptions.IgnoreCase))
                return new StartSwitch(token, ModeOf(token));
        return null;
    }

    /// <summary>"…tray" goes to the notification area, so no window; other "minim…" ones leave a minimized window.</summary>
    static WindowMode ModeOf(string token) =>
        token.Contains("minim", StringComparison.OrdinalIgnoreCase) && !token.Contains("tray", StringComparison.OrdinalIgnoreCase)
            ? WindowMode.Minimized
            : WindowMode.Background;

    /// <summary>The parameter this program is known to take for a start without window.</summary>
    public static StartSwitch? Known(Entry entry)
    {
        var file = FileOf(entry);
        if (file.Length == 0) return null;
        var text = learned.GetValueOrDefault(file) ?? Library.GetValueOrDefault(file);
        return text == null ? null : Find(text) ?? new StartSwitch(text, WindowMode.Background);
    }

    /// <summary>How the entry's window comes up at sign-in, and why.</summary>
    public static StartWindowState Describe(Entry entry)
    {
        switch (entry.Kind)
        {
            // Own start parameters count as well: added ones on top, replacing ones instead.
            case EntryKind.Program when Find(StartCommands.Arguments(entry, entry.Arguments)) is { } own:
                return new StartWindowState(own.Mode, WindowSource.Switch, own.Text);
            case EntryKind.Shortcut:
                var link = LinkOf(entry.Target);
                if (Find(StartCommands.Arguments(entry, link.Arguments)) is { } inLink) return new StartWindowState(inLink.Mode, WindowSource.FixedSwitch, inLink.Text);
                // Started directly, the shortcut's "minimized" no longer applies.
                if (link.Minimized && !StartCommands.HasOwn(entry)) return new StartWindowState(WindowMode.Minimized, WindowSource.Shortcut);
                break;
            case EntryKind.ScheduledTask when Find(StartCommands.Arguments(entry, TaskArguments(entry.Target))) is { } inTask:
                return new StartWindowState(inTask.Mode, WindowSource.FixedSwitch, inTask.Text);
        }

        var chosen = entry.Window ?? (entry.Origin?.Kind == OriginKind.StartupTask ? WindowMode.Background : WindowMode.Normal);
        if (chosen != WindowMode.Normal) return new StartWindowState(chosen, WindowSource.StartQueue);
        return entry.HasWindow == false ? new StartWindowState(WindowMode.Normal, WindowSource.NoWindow) : new StartWindowState(WindowMode.Normal, WindowSource.None);
    }

    /// <summary>The arguments with <paramref name="text"/> added at the end, or taken out.</summary>
    public static string With(string arguments, string text, bool present)
    {
        var without = Regex.Replace(arguments ?? "", $@"(^|\s){Regex.Escape(text)}(?=$|\s)", "", RegexOptions.IgnoreCase).Trim();
        return present ? (without.Length == 0 ? text : $"{without} {text}") : without;
    }

    static string FileOf(Entry entry) => entry.Kind switch
    {
        EntryKind.PackagedApp => "",
        _ when entry.ResolvedTarget.Length > 0 => Path.GetFileName(entry.ResolvedTarget),
        EntryKind.Program => Path.GetFileName(Environment.ExpandEnvironmentVariables(entry.Target)),
        _ => "",
    };

    // ---------- Reading shortcuts and tasks, kept for a while: the window reads them often ----------

    sealed record Link(string Arguments, bool Minimized, DateTime Written);

    static readonly Dictionary<string, Link> Links = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, (string Arguments, DateTime Read)> Tasks = new(StringComparer.OrdinalIgnoreCase);

    static Link LinkOf(string path)
    {
        try
        {
            var written = File.GetLastWriteTimeUtc(path);
            lock (Links)
                if (Links.TryGetValue(path, out var known) && known.Written == written) return known;
            var link = new Link(Shortcut.Resolve(path).Arguments, Shortcut.StartsMinimized(path), written);
            lock (Links) Links[path] = link;
            return link;
        }
        catch
        {
            return new Link("", false, default);
        }
    }

    static string TaskArguments(string path)
    {
        lock (Tasks)
            if (Tasks.TryGetValue(path, out var known) && DateTime.Now - known.Read < TimeSpan.FromMinutes(5)) return known.Arguments;
        var arguments = TaskAutostart.Get(path)?.Arguments ?? "";
        lock (Tasks) Tasks[path] = (arguments, DateTime.Now);
        return arguments;
    }
}
