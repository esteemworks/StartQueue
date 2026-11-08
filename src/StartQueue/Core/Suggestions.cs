using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace StartQueue.Core;

public enum SuggestReason { None, Running, OftenUsed, CanStartItself, RecentlyInstalled }

/// <summary>An app of the Start menu, as offered in the "add app" dialog.</summary>
public sealed class AppCandidate
{
    public string Name { get; init; } = "";

    /// <summary>AUMID for Store apps, otherwise the Apps folder id (often a path).</summary>
    public string Id { get; init; } = "";

    /// <summary>The Start menu shortcut of a desktop app, if there is one.</summary>
    public string LinkPath { get; init; } = "";

    public string ExePath { get; init; } = "";
    public bool Listed { get; set; }
    public SuggestReason Reason { get; set; }
    public int Score { get; set; }
}

/// <summary>
/// Suggests apps worth starting at sign-in. Only facts Windows records count: running now, how often Windows
/// counted the app being started (UserAssist), a Store app declaring a startup task that is off, and a recent
/// install. Apps already in StartQueue or started by Windows itself are never suggested.
/// </summary>
public static class Suggestions
{
    const string UserAssistKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist\{CEBFF5CD-ACE2-4F4F-9178-9926F41749EA}\Count";
    const int OftenUsedRuns = 5;
    static readonly TimeSpan RecentInstall = TimeSpan.FromDays(30);
    static readonly string WindowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    /// <summary>What Windows knows about the installed apps. Slow to read (seconds), so it is read ahead and kept a while.</summary>
    public sealed class AppFacts
    {
        internal Dictionary<string, (string? Path, string? Target, DateTime Created)> Links = new();
        internal Dictionary<string, PackageFact> Packages = new();
        internal HashSet<string> Running = new();
        internal Dictionary<string, int> Runs = new();
        internal List<(string Name, string Id)> Apps = new();
        internal HashSet<string> StartedByWindows = new();
        public DateTime At { get; } = DateTime.Now;
    }

    /// <summary>Reads all sources at the same time; none depends on another.</summary>
    public static AppFacts Gather()
    {
        var links = Task.Run(StartMenuLinks);
        var packages = Task.Run(PackageFacts);
        var running = Task.Run(RunningExecutables);
        var apps = Task.Run(PackagedApps.ListStartApps);
        var autostart = Task.Run(NativeAutostart.Scan);
        var runs = UserAssistRuns();
        Task.WaitAll(links, packages, running, apps, autostart);
        return new AppFacts
        {
            Links = links.Result,
            Packages = packages.Result,
            Running = running.Result,
            Runs = runs,
            Apps = apps.Result,
            StartedByWindows = new HashSet<string>(
                autostart.Result.SelectMany(i => new[] { i.Target, i.ResolvedTarget }).Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase),
        };
    }

    /// <summary>Quick: matches the facts against the current list.</summary>
    public static List<AppCandidate> Build(Config config, AppFacts facts)
    {
        var (links, packages, running, runs, startedByWindows) = (facts.Links, facts.Packages, facts.Running, facts.Runs, facts.StartedByWindows);
        var candidates = new List<AppCandidate>();
        foreach (var (name, id) in facts.Apps)
        {
            var packaged = id.Contains('!');
            links.TryGetValue(name, out var link);
            var exe = packaged ? "" : link.Target ?? (File.Exists(id) ? id : ExpandKnownFolder(id));
            var candidate = new AppCandidate { Name = name, Id = id, LinkPath = link.Path ?? "", ExePath = exe ?? "" };
            candidate.Listed = IsListed(config, candidate);

            if (!candidate.Listed && !startedByWindows.Contains(id) && !(exe != null && startedByWindows.Contains(exe)) && !IsSystemApp(candidate))
                Score(candidate, packaged ? packages.GetValueOrDefault(id.Split('!')[0]) : null, link, running, runs);
            candidates.Add(candidate);
        }
        return candidates;
    }

    static void Score(AppCandidate c, PackageFact? package, (string? Path, string? Target, DateTime Created) link,
        HashSet<string> running, Dictionary<string, int> runs)
    {
        var best = (Reason: SuggestReason.None, Points: 0);
        void Consider(SuggestReason reason, int points)
        {
            c.Score += points;
            if (points > best.Points) best = (reason, points);
        }

        var isRunning = package != null
            ? running.Any(p => p.StartsWith(package.Location, StringComparison.OrdinalIgnoreCase))
            : c.ExePath.Length > 0 && running.Contains(c.ExePath);
        if (isRunning) Consider(SuggestReason.Running, 40);

        var count = Math.Max(runs.GetValueOrDefault(c.Id), c.ExePath.Length > 0 ? runs.GetValueOrDefault(c.ExePath) : 0);
        if (count >= OftenUsedRuns) Consider(SuggestReason.OftenUsed, 20 + Math.Min(count, 100) / 5);

        if (package?.HasStartupTask == true) Consider(SuggestReason.CanStartItself, 25);

        var installed = package?.Installed ?? (link.Path != null ? link.Created : (DateTime?)null);
        if (installed != null && DateTime.Now - installed.Value < RecentInstall) Consider(SuggestReason.RecentlyInstalled, 15);

        c.Reason = best.Reason;
    }

    static bool IsListed(Config config, AppCandidate c)
    {
        var name = Simplify(c.Name);
        return config.Entries.Any(e =>
            string.Equals(e.Target, c.Id, StringComparison.OrdinalIgnoreCase) ||
            (c.ExePath.Length > 0 && (string.Equals(e.ResolvedTarget, c.ExePath, StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(e.Target, c.ExePath, StringComparison.OrdinalIgnoreCase))) ||
            (e.Kind == EntryKind.Program && e.Arguments.EndsWith(c.Id, StringComparison.OrdinalIgnoreCase)) ||
            // Same app under another program (launcher vs. main exe): match the name, ignoring punctuation.
            (name.Length >= 3 && Simplify(e.Name) == name));
    }

    static string Simplify(string name) => new(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    static bool IsSystemApp(AppCandidate c)
    {
        if (c.ExePath.StartsWith(WindowsFolder, StringComparison.OrdinalIgnoreCase)) return true;
        if (c.Id.StartsWith("Microsoft.Windows.", StringComparison.OrdinalIgnoreCase) ||
            c.Id.StartsWith("windows.", StringComparison.OrdinalIgnoreCase) ||
            c.Id.StartsWith("MicrosoftWindows.", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(c.ExePath, Paths.Exe, StringComparison.OrdinalIgnoreCase)) return true;
        var name = c.Name.ToLowerInvariant();
        return name.Contains("uninstall") || name.Contains("deinstall") || name.Contains("readme") || name.Contains("help");
    }

    // ---------- Sources ----------

    static Dictionary<string, (string? Path, string? Target, DateTime Created)> StartMenuLinks()
    {
        var links = new Dictionary<string, (string?, string?, DateTime)>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                     Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                 })
        {
            if (!Directory.Exists(root)) continue;
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).ToList(); }
            catch { continue; }
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (links.ContainsKey(name)) continue;
                var target = Shortcut.Resolve(file).Target;
                links[name] = (file, string.IsNullOrEmpty(target) ? null : target, File.GetCreationTime(file));
            }
        }
        return links;
    }

    internal sealed record PackageFact(string Location, DateTime Installed, bool HasStartupTask);

    static Dictionary<string, PackageFact> PackageFacts()
    {
        var facts = new Dictionary<string, PackageFact>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var package in new PackageManager().FindPackagesForUser(string.Empty))
            {
                try
                {
                    if (package.IsFramework || package.IsResourcePackage) continue;
                    var location = package.InstalledLocation.Path;
                    var manifest = Path.Combine(location, "AppxManifest.xml");
                    var hasTask = File.Exists(manifest) &&
                                  File.ReadAllText(manifest).Contains("windows.startupTask", StringComparison.OrdinalIgnoreCase);
                    facts[package.Id.FamilyName] = new PackageFact(location, package.InstalledDate.LocalDateTime, hasTask);
                }
                catch
                {
                    // Some system packages hide their location.
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading packages failed: {ex.Message}");
        }
        return facts;
    }

    static HashSet<string> RunningExecutables()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var session = Process.GetCurrentProcess().SessionId;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.SessionId == session && process.MainModule?.FileName is { } file) paths.Add(file);
            }
            catch
            {
                // Elevated or protected processes do not reveal their path.
            }
            finally
            {
                process.Dispose();
            }
        }
        return paths;
    }

    /// <summary>How often Windows counted each app being started (UserAssist, names ROT13-encoded).</summary>
    static Dictionary<string, int> UserAssistRuns()
    {
        var runs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UserAssistKey);
            if (key == null) return runs;
            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is not byte[] { Length: >= 8 } data) continue;
                var name = Rot13(valueName);
                var count = BitConverter.ToInt32(data, 4);
                var expanded = ExpandKnownFolder(name) ?? name;
                runs[expanded] = Math.Max(runs.GetValueOrDefault(expanded), count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading app usage failed: {ex.Message}");
        }
        return runs;
    }

    static string Rot13(string text) => new(text.Select(ch => ch switch
    {
        >= 'a' and <= 'z' => (char)('a' + (ch - 'a' + 13) % 26),
        >= 'A' and <= 'Z' => (char)('A' + (ch - 'A' + 13) % 26),
        _ => ch,
    }).ToArray());

    /// <summary>Turns "{known folder GUID}\rest" into a real path, or returns null.</summary>
    static string? ExpandKnownFolder(string value)
    {
        if (value.Length < 39 || value[0] != '{' || value[37] != '}') return null;
        if (!Guid.TryParse(value[..38], out var folderId)) return null;
        if (SHGetKnownFolderPath(ref folderId, 0, IntPtr.Zero, out var pointer) != 0) return null;
        try
        {
            return Marshal.PtrToStringUni(pointer) + value[38..];
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath(ref Guid folderId, uint flags, IntPtr token, out IntPtr path);
}
