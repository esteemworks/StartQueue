using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace StartQueue.Core;

/// <summary>Notices new autostart entries as they are written: Run keys and startup folders.</summary>
public sealed class Watcher : IDisposable
{
    const int NotifyName = 0x1;
    const int NotifyLastSet = 0x4;

    readonly Action changed;
    readonly System.Threading.Timer debounce;
    readonly ManualResetEvent stop = new(false);
    readonly List<FileSystemWatcher> folders = new();

    public Watcher(Action changed)
    {
        this.changed = changed;
        debounce = new System.Threading.Timer(_ => this.changed(), null, Timeout.Infinite, Timeout.Infinite);

        WatchKey(RegistryHive.CurrentUser, RegistryView.Default);
        WatchKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        WatchKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        WatchFolder(NativeAutostart.Folder("User"));
        WatchFolder(NativeAutostart.Folder("Common"));
    }

    void Trigger() => debounce.Change(3000, Timeout.Infinite);

    void WatchKey(RegistryHive hive, RegistryView view)
    {
        var thread = new Thread(() => Loop(hive, view)) { IsBackground = true, Name = $"Watch {hive} {view}" };
        thread.Start();
    }

    void Loop(RegistryHive hive, RegistryView view)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(NativeAutostart.RunPath, false);
            if (key == null) return;
            using var signal = new AutoResetEvent(false);
            while (true)
            {
                if (RegNotifyChangeKeyValue(key.Handle, false, NotifyName | NotifyLastSet, signal.SafeWaitHandle, true) != 0) return;
                if (WaitHandle.WaitAny(new WaitHandle[] { signal, stop }) == 1) return;
                Trigger();
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Watching {hive} Run failed: {ex.Message}");
        }
    }

    void WatchFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        var watcher = new FileSystemWatcher(folder) { IncludeSubdirectories = false, EnableRaisingEvents = true };
        watcher.Created += (_, _) => Trigger();
        watcher.Renamed += (_, _) => Trigger();
        folders.Add(watcher);
    }

    public void Dispose()
    {
        stop.Set();
        debounce.Dispose();
        foreach (var watcher in folders) watcher.Dispose();
    }

    [DllImport("advapi32.dll")]
    static extern int RegNotifyChangeKeyValue(SafeRegistryHandle key, bool watchSubtree, int filter, SafeWaitHandle signal, bool asynchronous);
}
