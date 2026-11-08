using System.Runtime.InteropServices;
using System.Windows;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>
/// Reads what the shell hands over on a drop: plain files from Explorer and the desktop, or shell items
/// from the Start menu, where Store apps have no file at all and come as an item of the Apps folder.
/// </summary>
public static class ShellDrop
{
    const string IdListFormat = "Shell IDList Array";
    const string AppsFolderPrefix = "::{4234D49B-0245-4DF3-B780-3893943456E1}\\";
    const uint DesktopAbsoluteParsing = 0x80028000;
    const uint NormalDisplay = 0;

    public static bool CanRead(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(IdListFormat);

    /// <summary>Name and target of each dropped item: a file path, or an AUMID / Apps folder id.</summary>
    public static List<(string Name, string Id)> Read(IDataObject data)
    {
        var items = new List<(string, string)>();
        try
        {
            if (data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                items.AddRange(files.Select(f => (Path.GetFileNameWithoutExtension(f), f)));
                return items;
            }
            var raw = data.GetDataPresent(IdListFormat) ? data.GetData(IdListFormat) : null;
            if (raw is MemoryStream stream) items.AddRange(ReadIdList(stream.ToArray()));
            else if (raw != null) Log.Debug($"Shell item list came as {raw.GetType().FullName}", "drop");
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading a drop failed: {ex.Message}");
        }
        return items;
    }

    static IEnumerable<(string, string)> ReadIdList(byte[] data)
    {
        var result = new List<(string, string)>();
        var pinned = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var start = pinned.AddrOfPinnedObject();
            var count = BitConverter.ToInt32(data, 0);
            var parent = start + BitConverter.ToInt32(data, 4);
            for (var i = 1; i <= count; i++)
            {
                var absolute = ILCombine(parent, start + BitConverter.ToInt32(data, 4 + 4 * i));
                try
                {
                    var parsing = Name(absolute, DesktopAbsoluteParsing);
                    var display = Name(absolute, NormalDisplay);
                    if (string.IsNullOrEmpty(parsing)) continue;
                    var id = parsing.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
                        ? parsing[AppsFolderPrefix.Length..]
                        : parsing;
                    result.Add((string.IsNullOrEmpty(display) ? id : display, id));
                }
                finally
                {
                    ILFree(absolute);
                }
            }
        }
        finally
        {
            pinned.Free();
        }
        return result;
    }

    static string Name(IntPtr idList, uint kind)
    {
        if (SHGetNameFromIDList(idList, kind, out var name) != 0) return "";
        try
        {
            return Marshal.PtrToStringUni(name) ?? "";
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    [DllImport("shell32.dll")]
    static extern IntPtr ILCombine(IntPtr parent, IntPtr child);

    [DllImport("shell32.dll")]
    static extern void ILFree(IntPtr idList);

    [DllImport("shell32.dll")]
    static extern int SHGetNameFromIDList(IntPtr idList, uint kind, out IntPtr name);
}
