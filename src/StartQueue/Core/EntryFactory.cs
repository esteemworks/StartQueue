namespace StartQueue.Core;

/// <summary>Builds entries for things the user adds by hand: files from the context menu or the picker.</summary>
public static class EntryFactory
{
    public static Entry FromPath(string path)
    {
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            // Copy, never move: the shortcut the user right-clicked stays where it is.
            var copy = Paths.UniqueFile(Paths.Shortcuts, Path.GetFileName(path));
            File.Copy(path, copy);
            var target = Shortcut.Resolve(copy).Target;
            return new Entry
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Kind = EntryKind.Shortcut,
                Target = copy,
                ResolvedTarget = target,
                ProcessName = CommandLine.ProcessNameOf(target),
            };
        }

        return new Entry
        {
            Name = DescribeFile(path),
            Kind = EntryKind.Program,
            Target = path,
            ResolvedTarget = path,
            ProcessName = CommandLine.ProcessNameOf(path),
        };
    }

    public static Entry FromStartApp(string name, string id)
    {
        if (id.Contains('!'))
            return new Entry { Name = name, Kind = EntryKind.PackagedApp, Target = id, ProcessName = "" };

        if (File.Exists(id))
        {
            var entry = FromPath(id);
            if (entry.Kind == EntryKind.Program) entry.Name = name;
            return entry;
        }

        // Desktop apps listed by a known-folder path; start them through the shell.
        return new Entry
        {
            Name = name,
            Kind = EntryKind.Program,
            Target = "explorer.exe",
            Arguments = $"shell:AppsFolder\\{id}",
        };
    }

    public static bool SameApp(Entry existing, Entry candidate) =>
        existing.Kind == candidate.Kind &&
        (candidate.Kind == EntryKind.Shortcut
            ? !string.IsNullOrEmpty(candidate.ResolvedTarget) && string.Equals(existing.ResolvedTarget, candidate.ResolvedTarget, StringComparison.OrdinalIgnoreCase)
            : string.Equals(existing.Target, candidate.Target, StringComparison.OrdinalIgnoreCase) &&
              string.Equals(existing.Arguments, candidate.Arguments, StringComparison.OrdinalIgnoreCase));

    static string DescribeFile(string path)
    {
        try
        {
            var description = FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(description) && description.Length <= 60) return description.Trim();
        }
        catch
        {
            // Fall back to the file name.
        }
        return Path.GetFileNameWithoutExtension(path);
    }
}
