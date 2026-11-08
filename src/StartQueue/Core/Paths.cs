namespace StartQueue.Core;

public static class Paths
{
    /// <summary>%APPDATA%\StartQueue (see <see cref="KitApp.DataFolder"/>).</summary>
    public static string Data => KitApp.DataFolder;

    public static string Config => Path.Combine(Data, "config.json");
    public static string LastRun => Path.Combine(Data, "last-run.json");
    public static string PendingElevated => Path.Combine(Data, "pending-elevated.json");
    public static string LockFile => Path.Combine(Data, "config.lock");
    public static string LogFile => KitApp.LogFile;
    public static string Shortcuts => Path.Combine(Data, "shortcuts");

    public static string Exe => KitApp.Exe;

    public static string UniqueFile(string folder, string fileName)
    {
        Directory.CreateDirectory(folder);
        var candidate = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (var i = 2; File.Exists(candidate); i++)
            candidate = Path.Combine(folder, $"{stem} ({i}){ext}");
        return candidate;
    }

    public static bool IsInside(string path, string folder) =>
        !string.IsNullOrEmpty(path) &&
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
