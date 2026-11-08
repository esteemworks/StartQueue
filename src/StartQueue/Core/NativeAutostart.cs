using Microsoft.Win32;

namespace StartQueue.Core;

/// <summary>An entry found in the Windows autostart.</summary>
public sealed record NativeItem(
    NativeOrigin Origin,
    string DisplayName,
    bool Enabled,
    EntryKind Kind,
    string Target,
    string Arguments,
    string ResolvedTarget,
    string ProcessName,
    string PackageExecutable = "",
    bool RunElevated = false);

/// <summary>Reads, takes over and restores the places Windows starts programs from at logon.</summary>
public static class NativeAutostart
{
    public const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";
    static readonly byte[] DisabledFlag = { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

    public static List<NativeItem> Scan()
    {
        var items = new List<NativeItem>();
        Guard("HKCU Run", () => ScanRun(items, "HKCU"));
        Guard("HKLM Run", () => ScanRun(items, "HKLM"));
        Guard("HKLM32 Run", () => ScanRun(items, "HKLM32"));
        Guard("user startup folder", () => ScanFolder(items, "User"));
        Guard("common startup folder", () => ScanFolder(items, "Common"));
        Guard("startup tasks", () => items.AddRange(PackagedApps.ScanStartupTasks()));
        return items;
    }

    public static Entry TakeOver(NativeItem item)
    {
        var origin = item.Origin;
        var entry = new Entry
        {
            Name = item.DisplayName,
            Kind = item.Kind,
            Target = item.Target,
            Arguments = item.Arguments,
            ResolvedTarget = item.ResolvedTarget,
            ProcessName = item.ProcessName,
            PackageExecutable = item.PackageExecutable,
            RunElevated = item.RunElevated,
            Enabled = item.Enabled,
            Origin = origin,
        };

        switch (origin.Kind)
        {
            case OriginKind.RunKey:
            {
                var (hive, view, approved) = RunLocation(origin.Location);
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key = root.OpenSubKey(RunPath, true))
                    key?.DeleteValue(origin.Name, false);
                DeleteApproved(hive, approved, origin.Name);
                break;
            }
            case OriginKind.StartupFolder:
            {
                var moved = Paths.UniqueFile(Paths.Shortcuts, Path.GetFileName(origin.Data));
                File.Move(origin.Data, moved);
                entry.Target = moved;
                DeleteApproved(FolderHive(origin.Location), "StartupFolder", origin.Name);
                break;
            }
            case OriginKind.StartupTask:
                PackagedApps.SetTaskState(origin.Name, PackagedApps.StateDisabledByUser);
                break;
        }

        origin.TakenOverAt = DateTime.Now;
        return entry;
    }

    /// <summary>Puts an entry back where it came from, with its current on/off state.</summary>
    public static void Restore(Entry entry)
    {
        var origin = entry.Origin ?? throw new InvalidOperationException("This entry did not come from the Windows autostart.");
        switch (origin.Kind)
        {
            case OriginKind.RunKey:
            {
                var (hive, view, approved) = RunLocation(origin.Location);
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key = root.CreateSubKey(RunPath, true))
                    key.SetValue(origin.Name, origin.Data, origin.ExpandString ? RegistryValueKind.ExpandString : RegistryValueKind.String);
                SetApproved(hive, approved, origin.Name, entry.Enabled);
                break;
            }
            case OriginKind.StartupFolder:
            {
                var folder = Folder(origin.Location);
                var destination = Paths.UniqueFile(folder, origin.Name);
                if (File.Exists(entry.Target)) File.Move(entry.Target, destination);
                SetApproved(FolderHive(origin.Location), "StartupFolder", Path.GetFileName(destination), entry.Enabled);
                break;
            }
            case OriginKind.StartupTask:
                PackagedApps.SetTaskState(origin.Name, entry.Enabled ? PackagedApps.StateEnabled : PackagedApps.StateDisabledByUser);
                break;
            case OriginKind.ScheduledTask:
                TaskAutostart.SetLogonTrigger(origin.Name, entry.Enabled);
                break;
        }
    }

    static void ScanRun(List<NativeItem> items, string location)
    {
        var (hive, view, approved) = RunLocation(location);
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(RunPath);
        if (key == null) return;

        foreach (var name in key.GetValueNames())
        {
            if (string.IsNullOrEmpty(name)) continue;
            if (key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is not string raw ||
                string.IsNullOrWhiteSpace(raw)) continue;

            var enabled = IsApproved(hive, approved, name);
            var (file, args) = CommandLine.Split(raw);
            var origin = new NativeOrigin
            {
                Kind = OriginKind.RunKey,
                Location = location,
                Name = name,
                Data = raw,
                ExpandString = key.GetValueKind(name) == RegistryValueKind.ExpandString,
                WasEnabled = enabled,
            };
            items.Add(new NativeItem(origin, FriendlyName(file, name), enabled, EntryKind.Program,
                file, args, file, CommandLine.ProcessNameOf(file)));
        }
    }

    static void ScanFolder(List<NativeItem> items, string location)
    {
        var folder = Folder(location);
        if (!Directory.Exists(folder)) return;

        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;

            var enabled = IsApproved(FolderHive(location), "StartupFolder", fileName);
            var isLink = Path.GetExtension(file).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
            var resolved = isLink ? Shortcut.Resolve(file).Target : file;
            var origin = new NativeOrigin
            {
                Kind = OriginKind.StartupFolder,
                Location = location,
                Name = fileName,
                Data = file,
                WasEnabled = enabled,
            };
            items.Add(new NativeItem(origin, Path.GetFileNameWithoutExtension(file), enabled,
                isLink ? EntryKind.Shortcut : EntryKind.Program, file, "", resolved, CommandLine.ProcessNameOf(resolved),
                RunElevated: isLink && Shortcut.RunsAsAdmin(file)));
        }
    }

    static string FriendlyName(string file, string fallback)
    {
        try
        {
            if (File.Exists(file))
            {
                var description = FileVersionInfo.GetVersionInfo(file).FileDescription;
                if (!string.IsNullOrWhiteSpace(description) && description.Length <= 60) return description.Trim();
            }
        }
        catch
        {
            // Fall back to the value name.
        }
        return fallback;
    }

    static (RegistryHive Hive, RegistryView View, string Approved) RunLocation(string location) => location switch
    {
        "HKCU" => (RegistryHive.CurrentUser, RegistryView.Default, "Run"),
        "HKLM" => (RegistryHive.LocalMachine, RegistryView.Registry64, "Run"),
        "HKLM32" => (RegistryHive.LocalMachine, RegistryView.Registry32, "Run32"),
        _ => throw new ArgumentOutOfRangeException(nameof(location), location, null),
    };

    public static string Folder(string location) => Environment.GetFolderPath(
        location == "Common" ? Environment.SpecialFolder.CommonStartup : Environment.SpecialFolder.Startup);

    static RegistryHive FolderHive(string location) =>
        location == "Common" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;

    static bool IsApproved(RegistryHive hive, string sub, string name)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(ApprovedBase + sub);
        return key?.GetValue(name) is not byte[] { Length: > 0 } flags || flags[0] % 2 == 0;
    }

    static void DeleteApproved(RegistryHive hive, string sub, string name)
    {
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.OpenSubKey(ApprovedBase + sub, true);
        key?.DeleteValue(name, false);
    }

    static void SetApproved(RegistryHive hive, string sub, string name, bool enabled)
    {
        if (enabled)
        {
            DeleteApproved(hive, sub, name);
            return;
        }
        using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        using var key = root.CreateSubKey(ApprovedBase + sub, true);
        key.SetValue(name, DisabledFlag, RegistryValueKind.Binary);
    }

    static void Guard(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Warning($"Scanning {what} failed: {ex.Message}");
        }
    }
}
