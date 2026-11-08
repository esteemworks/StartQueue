using System.Runtime.InteropServices;

namespace StartQueue.Core;

public static class CommandLine
{
    /// <summary>
    /// Splits a Run key command line into program and arguments. Handles quoted paths and unquoted
    /// paths with spaces ("C:\Program Files\App\app.exe --flag") by probing which prefix exists.
    /// </summary>
    public static (string File, string Arguments) Split(string commandLine)
    {
        var cmd = Environment.ExpandEnvironmentVariables(commandLine.Trim());
        if (cmd.StartsWith('"'))
        {
            var end = cmd.IndexOf('"', 1);
            if (end > 0) return (cmd[1..end], cmd[(end + 1)..].Trim());
        }

        var parts = cmd.Split(' ');
        for (var i = 1; i <= parts.Length; i++)
        {
            var candidate = string.Join(' ', parts.Take(i));
            var rest = string.Join(' ', parts.Skip(i)).Trim();
            if (File.Exists(candidate)) return (candidate, rest);
            if (!candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(candidate + ".exe"))
                return (candidate + ".exe", rest);
        }
        return (parts[0], string.Join(' ', parts.Skip(1)).Trim());
    }

    public static string ProcessNameOf(string file) =>
        string.IsNullOrWhiteSpace(file) ? "" : Path.GetFileNameWithoutExtension(file);
}

public static class Shortcut
{
    /// <summary>True if the shortcut has "Run as administrator" ticked (SLDF_RUNAS_USER in the link header).</summary>
    public static bool RunsAsAdmin(string lnkPath)
    {
        try
        {
            using var file = File.OpenRead(lnkPath);
            var header = new byte[0x18];
            return file.Read(header, 0, header.Length) == header.Length && (header[0x15] & 0x20) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>True if the shortcut's "Run" setting is "Minimized" (ShowCommand in the link header).</summary>
    public static bool StartsMinimized(string lnkPath)
    {
        try
        {
            using var file = File.OpenRead(lnkPath);
            var header = new byte[0x40];
            if (file.Read(header, 0, header.Length) != header.Length) return false;
            var show = BitConverter.ToInt32(header, 0x3C);
            return show is 2 or 6 or 7; // SW_SHOWMINIMIZED, SW_MINIMIZE, SW_SHOWMINNOACTIVE
        }
        catch
        {
            return false;
        }
    }

    public static (string Target, string Arguments) Resolve(string lnkPath)
    {
        object? shell = null, link = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) return ("", "");
            shell = Activator.CreateInstance(type);
            dynamic sh = shell!;
            link = sh.CreateShortcut(lnkPath);
            dynamic lnk = link!;
            return ((string?)lnk.TargetPath ?? "", (string?)lnk.Arguments ?? "");
        }
        catch (Exception ex)
        {
            Log.Debug($"Could not resolve shortcut {lnkPath}: {ex.Message}", "shortcut");
            return ("", "");
        }
        finally
        {
            if (link != null) Marshal.FinalReleaseComObject(link);
            if (shell != null) Marshal.FinalReleaseComObject(shell);
        }
    }
}
