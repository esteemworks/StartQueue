using System.Runtime.InteropServices;

namespace StartQueue.Core;

/// <summary>
/// Watches an app's windows after a start at sign-in. Where StartQueue handles the window itself (no start
/// parameter for it), it sends the window to the background or minimizes it; for every app it notes whether a
/// window came up at all, so the settings can say "shows no window" instead of "normal". Apps often show a splash
/// first, so it keeps watching a little after the first window.
/// </summary>
public static class StartWindow
{
    static readonly TimeSpan WaitForWindow = TimeSpan.FromSeconds(30);
    // Store apps take much longer for their first window at sign-in, often a minute.
    static readonly TimeSpan WaitForStoreWindow = TimeSpan.FromSeconds(90);
    static readonly TimeSpan KeepWatching = TimeSpan.FromSeconds(10);
    static readonly TimeSpan QuitCheck = TimeSpan.FromSeconds(4);

    /// <summary>Call right after starting the entry in the sign-in sequence; watches in the background for up to about 40 s, 100 s for Store apps.</summary>
    public static void Track(Entry entry, DateTime startedAt)
    {
        var name = ProcessNameOf(entry);
        // Elevated windows do not take orders from here; shared hosts (conhost …) are no sign of this app.
        if (name.Length == 0 || entry.RunElevated || Launcher.GenericHosts.Contains(name)) return;
        var state = StartSwitches.Describe(entry);
        var act = state.Source == WindowSource.StartQueue ? state.Mode : WindowMode.Normal;
        Task.Run(() => Watch(entry, name, startedAt, act));
    }

    static void Watch(Entry entry, string name, DateTime startedAt, WindowMode act)
    {
        try
        {
            var wait = entry.Kind == EntryKind.PackagedApp ? WaitForStoreWindow : WaitForWindow;
            var (pids, windows) = Handle(name, startedAt, act, wait);
            Remember(entry, windows > 0 ? true : Processes(name, startedAt).Any(IsAlive) ? false : null);
            if (act != WindowMode.Background || windows == 0) return;

            // Closing the window should leave the app running in the notification area. Some apps quit instead:
            // start those again, minimized, and minimize them from now on.
            Thread.Sleep(QuitCheck);
            if (pids.Any(IsAlive)) return;
            Log.Info($"\"{entry.Name}\" quit when its window was closed; starting it again minimized");
            ConfigStore.Update(c =>
            {
                var saved = c.Entries.FirstOrDefault(e => e.Id == entry.Id);
                if (saved == null) return;
                saved.Window = WindowMode.Minimized;
                saved.QuitsOnClose = true;
            });
            var again = DateTime.Now;
            Launcher.StartDirect(entry);
            Handle(name, again, WindowMode.Minimized, wait);
        }
        catch (Exception ex)
        {
            Log.Info($"Window of \"{entry.Name}\": {ex.Message}");
        }
    }

    /// <summary>Whether the app showed a window; null when it could not be told (it quit or never started).</summary>
    static void Remember(Entry entry, bool? hasWindow)
    {
        if (hasWindow == null || entry.HasWindow == hasWindow) return;
        ConfigStore.Update(c =>
        {
            var saved = c.Entries.FirstOrDefault(e => e.Id == entry.Id);
            if (saved != null) saved.HasWindow = hasWindow;
        });
    }

    /// <summary>
    /// Finds each main window of the app's processes started since <paramref name="since"/> and, unless the mode
    /// is Normal, closes or minimizes it. Returns the processes that had one and how many windows there were.
    /// </summary>
    static (HashSet<int> Pids, int Windows) Handle(string processName, DateTime since, WindowMode mode, TimeSpan wait)
    {
        var done = new HashSet<IntPtr>();
        var pids = new HashSet<int>();
        var deadline = DateTime.Now + wait;
        while (DateTime.Now < deadline)
        {
            var ours = Processes(processName, since);
            foreach (var window in MainWindows(ours))
            {
                if (!done.Add(window.Handle)) continue;
                pids.Add(window.ProcessId);
                if (mode == WindowMode.Background) PostMessage(window.Handle, WmClose, IntPtr.Zero, IntPtr.Zero);
                else if (mode == WindowMode.Minimized) ShowWindowAsync(window.Handle, ShowMinNoActive);
                // After the first window, watch a little longer for the real one behind a splash.
                if (done.Count == 1) deadline = DateTime.Now + KeepWatching;
            }
            // Only watching: the first window answers the question.
            if (mode == WindowMode.Normal && done.Count > 0) break;
            Thread.Sleep(400);
        }
        if (done.Count > 0 && mode != WindowMode.Normal)
            Log.Debug($"{(mode == WindowMode.Background ? "Sent to the background" : "Minimized")}: {processName} ({done.Count} window{(done.Count == 1 ? "" : "s")})", "window");
        return (pids, done.Count);
    }

    static string ProcessNameOf(Entry entry)
    {
        if (entry.ProcessName.Length > 0) return entry.ProcessName;
        if (entry.PackageExecutable.Length > 0) return Path.GetFileNameWithoutExtension(entry.PackageExecutable);
        return entry.ResolvedTarget.Length > 0 ? Path.GetFileNameWithoutExtension(entry.ResolvedTarget) : "";
    }

    static HashSet<int> Processes(string name, DateTime since)
    {
        var ids = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName(name))
        {
            using (process)
            {
                try
                {
                    if (process.StartTime >= since.AddSeconds(-5)) ids.Add(process.Id);
                }
                catch
                {
                    // Elevated or already gone.
                }
            }
        }
        return ids;
    }

    static bool IsAlive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Visible, titled top-level windows without an owner: what the user would see as the app's window.</summary>
    static List<(IntPtr Handle, int ProcessId)> MainWindows(HashSet<int> pids)
    {
        var found = new List<(IntPtr, int)>();
        if (pids.Count == 0) return found;
        EnumWindows((handle, _) =>
        {
            GetWindowThreadProcessId(handle, out var pid);
            if (!pids.Contains((int)pid) || !IsWindowVisible(handle) || GetWindow(handle, GwOwner) != IntPtr.Zero) return true;
            if ((GetWindowLongPtr(handle, GwlExStyle).ToInt64() & WsExToolWindow) != 0) return true;
            if (GetWindowTextLength(handle) == 0) return true;
            found.Add((handle, (int)pid));
            return true;
        }, IntPtr.Zero);
        return found;
    }

    const uint WmClose = 0x0010;
    const int ShowMinNoActive = 7;
    const uint GwOwner = 4;
    const int GwlExStyle = -20;
    const long WsExToolWindow = 0x80;

    delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr handle);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr handle, int command);
}
