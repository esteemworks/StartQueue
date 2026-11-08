using System.Diagnostics;
using System.Runtime.InteropServices;

namespace StartQueue.Core;

/// <summary>When the user signed in to this Windows session, so start times count from there.</summary>
public static class SignIn
{
    /// <summary>The sign-in time of the current session; null if Windows does not say.</summary>
    public static DateTime? Time()
    {
        try
        {
            if (!WTSQuerySessionInformationW(IntPtr.Zero, CurrentSession, WtsSessionInfo, out var buffer, out var bytes)) return null;
            try
            {
                // WTSINFOW: LogonTime is the fourth of the LARGE_INTEGER times, at byte 200.
                if (bytes < LogonTimeOffset + 8) return null;
                var fileTime = Marshal.ReadInt64(buffer, LogonTimeOffset);
                return fileTime > 0 ? DateTime.FromFileTime(fileTime) : null;
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>"12.3 s after sign-in", for the log.</summary>
    public static string Since(DateTime reference) => FormattableString.Invariant($"{(DateTime.Now - reference).TotalSeconds:0.0} s after sign-in");

    /// <summary>Whether the session is locked right now; null if Windows does not say.</summary>
    public static bool? IsLocked()
    {
        try
        {
            if (!WTSQuerySessionInformationW(IntPtr.Zero, CurrentSession, WtsSessionInfoEx, out var buffer, out var bytes)) return null;
            try
            {
                // WTSINFOEXW: Level, then (aligned to 8) SessionId, SessionState and SessionFlags at byte 16.
                if (bytes < SessionFlagsOffset + 4 || Marshal.ReadInt32(buffer) != 1) return null;
                return Marshal.ReadInt32(buffer, SessionFlagsOffset) switch
                {
                    0 => true,
                    1 => false,
                    _ => null,
                };
            }
            finally
            {
                WTSFreeMemory(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    const int CurrentSession = -1;
    const int WtsSessionInfo = 24;
    const int WtsSessionInfoEx = 25;
    const int LogonTimeOffset = 200;
    const int SessionFlagsOffset = 16;

    [DllImport("wtsapi32.dll", SetLastError = true)]
    static extern bool WTSQuerySessionInformationW(IntPtr server, int session, int infoClass, out IntPtr buffer, out int bytes);

    [DllImport("wtsapi32.dll")]
    static extern void WTSFreeMemory(IntPtr memory);
}

/// <summary>
/// At sign-in every second counts: the startup order begins as soon as the process runs, on its own thread, before
/// WPF loads its resources and StartQueue builds its tray icon and window. The tray takes over the result.
/// </summary>
public static class EarlyStart
{
    /// <summary>The single-instance mutex is Local\StartQueue.Tray, as in every version before.</summary>
    public const string InstanceName = "Tray";

    public static SingleInstance? Instance { get; private set; }
    public static bool IsFirst { get; private set; }
    public static Task<LastRun?>? Sequence { get; private set; }

    /// <summary>The moment start times count from: the sign-in, or the start of StartQueue if Windows does not say.</summary>
    public static DateTime Reference { get; private set; } = DateTime.Now;

    /// <summary>
    /// Whether the session was locked when StartQueue started at sign-in: then Windows signed in by itself, after a
    /// shutdown or restart that prepares the apps. Null if Windows does not say.
    /// </summary>
    public static bool? LockedAtStart { get; private set; }

    public static void Begin()
    {
        Instance = new SingleInstance(InstanceName);
        IsFirst = Instance.IsFirst;
        if (!IsFirst) return;
        LockedAtStart = SignIn.IsLocked();
        var signIn = SignIn.Time();
        Reference = signIn ?? Process.GetCurrentProcess().StartTime;
        var processStart = Process.GetCurrentProcess().StartTime;
        Log.Info(signIn == null
            ? "Sign-in start, sign-in time unknown"
            : FormattableString.Invariant($"Sign-in start: process started {(processStart - Reference).TotalSeconds:0.0} s after sign-in, first code of StartQueue {SignIn.Since(Reference)} (.NET took {(DateTime.Now - processStart).TotalSeconds:0.0} s to load)") +
              (LockedAtStart == true ? ", session still locked" : ""));
        var reference = Reference;
        Sequence = Task.Run<LastRun?>(() =>
        {
            try
            {
                var config = ConfigStore.Load();
                StartSwitches.Learn(config);
                return Launcher.RunSequence(config, reference, atSignIn: true);
            }
            catch (Exception ex)
            {
                // The tray starts the order itself then.
                Log.Warning($"Early start failed: {ex.Message}");
                return null;
            }
        });
    }
}
