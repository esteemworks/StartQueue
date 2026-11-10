using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace StartQueue.Core;

/// <summary>What Windows allows for a shutdown that prepares the apps.</summary>
public enum PreparedSignInState
{
    /// <summary>Nothing speaks against it; whether it works shows at the next start.</summary>
    Possible,

    /// <summary>"Use my sign-in info to automatically finish setting up" is switched off.</summary>
    TurnedOff,

    /// <summary>Only Windows Hello sign-in is allowed on this device: Windows has no password to sign in with.</summary>
    Passwordless,

    /// <summary>Switched off by a policy of the organization.</summary>
    BlockedByPolicy,

    /// <summary>The organization requires Windows Hello (for Business) and has switched off password sign-in.</summary>
    HelloRequired,

    /// <summary>A device joined to a domain or Microsoft Entra: Windows signs in by itself only with BitLocker on.</summary>
    NeedsBitLocker,
}

/// <summary>What "Set up" can do on this device.</summary>
public enum PreparedSetupState
{
    /// <summary>On: switched on explicitly, nothing in the way.</summary>
    Ready,

    /// <summary>One click with a UAC prompt switches it on.</summary>
    CanSetUp,

    /// <summary>Would be possible, but setting it up needs an administrator.</summary>
    NeedsAdmin,

    /// <summary>The organization decides: StartQueue does not touch its policies.</summary>
    Managed,
}

/// <summary>
/// Shutting down or restarting so that the apps are ready at the next start. This uses Windows' own automatic restart
/// sign-on: after "shutdown /sg" (or a restart with "/g") Windows signs the last user in at the next start and locks
/// right away. The autostart, and with it StartQueue, runs behind the lock screen, so the apps are up when the user
/// unlocks. StartQueue leaves a note before shutting down and checks at the next sign-in whether it worked.
/// </summary>
public static class PreparedSignIn
{
    static string Marker => Path.Combine(Paths.Data, "prepared-shutdown.txt");

    /// <summary>A note older than this belongs to a shutdown long ago and says nothing about this sign-in.</summary>
    static readonly TimeSpan MarkerLifetime = TimeSpan.FromDays(30);

    const string PolicyKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    const string PasswordlessKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device";
    const string PasswordlessValue = "DevicePasswordLessBuildVersion";
    const string OptOutValue = "OptOut";
    const string PasswordProvider = "{60b78e88-ead8-445c-9cfd-0b87f74ea6cd}";

    static string UserKey(string sid) => $@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\UserARSO\{sid}";

    static string? CurrentSid => WindowsIdentity.GetCurrent().User?.Value;

    public static PreparedSignInState State()
    {
        try
        {
            if (ReadInt(PolicyKey, "DisableAutomaticRestartSignOn") == 1) return PreparedSignInState.BlockedByPolicy;
            if (HelloRequiredByOrganization()) return PreparedSignInState.HelloRequired;
            if (IsJoined() && !BitLockerOn()) return PreparedSignInState.NeedsBitLocker;
            if (CurrentSid is { } sid && ReadInt(UserKey(sid), OptOutValue) == 1) return PreparedSignInState.TurnedOff;
            if (ReadInt(PasswordlessKey, PasswordlessValue) == 2) return PreparedSignInState.Passwordless;
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading the automatic sign-in settings failed: {ex.Message}");
        }
        return PreparedSignInState.Possible;
    }

    public static PreparedSetupState SetupState()
    {
        var state = State();
        if (state is PreparedSignInState.BlockedByPolicy or PreparedSignInState.HelloRequired or PreparedSignInState.NeedsBitLocker)
            return PreparedSetupState.Managed;
        if (state == PreparedSignInState.Possible && CurrentSid is { } sid && ReadInt(UserKey(sid), OptOutValue) == 0) return PreparedSetupState.Ready;
        // Set up fully the admin helper writes the two values, whoever is signed in; otherwise an administrator, once.
        return Setup.UserIsAdmin || Elevation.AdminMode ? PreparedSetupState.CanSetUp : PreparedSetupState.NeedsAdmin;
    }

    /// <summary>
    /// Password sign-in switched off by the organization: Windows Hello for Business only ("passwordless experience"),
    /// or the password sign-in hidden by policy. Windows then has no password to sign in with by itself, and these
    /// policies are the organization's to change, not StartQueue's.
    /// </summary>
    static bool HelloRequiredByOrganization()
    {
        if (ReadInt(@"SOFTWARE\Microsoft\PolicyManager\current\device\Authentication", "EnablePasswordlessExperience") == 1) return true;
        using var system = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\System");
        return system?.GetValue("ExcludedCredentialProviders") is string excluded &&
               excluded.Contains(PasswordProvider, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Joined to a domain or to Microsoft Entra: Windows treats the device as managed.</summary>
    static bool IsJoined()
    {
        using (var entra = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin\JoinInfo"))
            if (entra?.SubKeyCount > 0) return true;
        if (NetGetJoinInformation(null, out var name, out var status) != 0) return false;
        NetApiBufferFree(name);
        return status == NetSetupDomainName;
    }

    /// <summary>BitLocker protects the system drive; read without admin rights, the way Explorer shows it.</summary>
    static bool BitLockerOn()
    {
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type == null) return false;
            dynamic shell = Activator.CreateInstance(type)!;
            var drive = Path.GetPathRoot(Environment.SystemDirectory)!;
            var value = shell.NameSpace(drive).Self.ExtendedProperty("System.Volume.BitLockerProtection");
            return value is int protection && protection == 1;
        }
        catch
        {
            return false;
        }
    }

    static int? ReadInt(string key, string name)
    {
        using var k = Registry.LocalMachine.OpenSubKey(key);
        return k?.GetValue(name) is int value ? value : null;
    }

    // ---------- Setting it up ----------

    /// <summary>
    /// Switches the automatic sign-in on for this user: "Use my sign-in info to automatically finish setting up" on,
    /// and the Windows Hello only option off where it is on, since Windows needs a password to sign in by itself.
    /// One UAC prompt. The values before are kept in the settings, so it can be undone exactly.
    /// Returns false if the UAC prompt was cancelled or something failed.
    /// </summary>
    public static bool SetUp()
    {
        if (CurrentSid is not { } sid) return false;
        var passwordless = ReadInt(PasswordlessKey, PasswordlessValue);
        var optOut = ReadInt(UserKey(sid), OptOutValue);
        var undo = $"{Format(passwordless)} {Format(optOut)}";
        var newPasswordless = passwordless == 2 ? "0" : Format(passwordless);
        if (!Apply(sid, newPasswordless, "0")) return false;
        ConfigStore.Update(c => c.Settings.PreparedUndo ??= undo);
        Log.Event(Events.PreparedSignInSetUp, LogLevel.Info, $"Set up the automatic sign-in after a prepared shutdown (before: {undo})");
        return true;
    }

    /// <summary>Puts back what was there before <see cref="SetUp"/>.</summary>
    public static bool Undo()
    {
        var undo = ConfigStore.Load().Settings.PreparedUndo;
        if (CurrentSid is not { } sid || undo?.Split(' ') is not [var passwordless, var optOut]) return false;
        if (!Apply(sid, passwordless, optOut)) return false;
        ConfigStore.Update(c => c.Settings.PreparedUndo = null);
        Log.Event(Events.PreparedSignInUndone, LogLevel.Info, $"Undid the automatic sign-in setup ({undo})");
        return true;
    }

    static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";

    static bool Apply(string sid, string passwordless, string optOut)
    {
        var arguments = $"--prepared-signin {sid} {passwordless} {optOut}";
        if (Elevation.IsElevated) return ApplyFromCommandLine([.. arguments.Split(' ').Skip(1)]) == 0;
        // Set up fully: through the admin helper, without asking Windows. In Basis through UAC.
        if (AdminHelper.Available) return AdminHelper.Run(arguments.Split(' ')) == 0;
        return Elevation.RunSelfElevated(arguments);
    }

    /// <summary>
    /// Runs elevated: writes exactly these two values, nothing else. Arguments: user SID, the Windows Hello only
    /// value and the opt-out value, each a number or "-" to remove the value.
    /// </summary>
    public static int ApplyFromCommandLine(string[] args)
    {
        try
        {
            if (args is not [var sid, var passwordless, var optOut]) return 2;
            sid = new SecurityIdentifier(sid).Value;
            Write(PasswordlessKey, PasswordlessValue, passwordless);
            Write(UserKey(sid), OptOutValue, optOut);
            Log.Info($"Automatic sign-in: Windows Hello only {passwordless}, opt-out {optOut}");
            return 0;
        }
        catch (Exception ex)
        {
            Log.Event(Events.PreparedSignInFailed, LogLevel.Warning, $"Changing the automatic sign-in failed: {ex.Message}");
            return 1;
        }
    }

    static void Write(string key, string name, string value)
    {
        if (value == "-")
        {
            using var existing = Registry.LocalMachine.OpenSubKey(key, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }
        var number = int.Parse(value, CultureInfo.InvariantCulture);
        if (number is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(value), value, "Only 0, 1 or 2.");
        using var k = Registry.LocalMachine.CreateSubKey(key, writable: true);
        k.SetValue(name, number, RegistryValueKind.DWord);
    }

    /// <summary>One line for the settings.</summary>
    public static string Describe(PreparedSetupState setup, PreparedSignInState state) => setup switch
    {
        PreparedSetupState.Ready => Strings.PreparedStateReady,
        PreparedSetupState.NeedsAdmin => Strings.PreparedStateNeedsAdmin,
        PreparedSetupState.Managed => state switch
        {
            PreparedSignInState.HelloRequired => Strings.PreparedStateHelloRequired,
            PreparedSignInState.NeedsBitLocker => Strings.PreparedStateNeedsBitLocker,
            _ => Strings.PreparedStatePolicy,
        },
        _ => state switch
        {
            PreparedSignInState.Passwordless => Strings.PreparedStatePasswordless,
            PreparedSignInState.TurnedOff => Strings.PreparedStateTurnedOff,
            _ => Strings.PreparedStateUnknown,
        },
    };

    const int NetSetupDomainName = 3;

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    static extern int NetGetJoinInformation(string? server, out IntPtr name, out int status);

    [DllImport("netapi32.dll")]
    static extern int NetApiBufferFree(IntPtr buffer);

    /// <summary>Shuts down or restarts right away. Apps with unsaved work can still stop it, as with every shutdown.</summary>
    public static void ShutDown(bool restart)
    {
        Directory.CreateDirectory(Paths.Data);
        File.WriteAllText(Marker, DateTime.Now.ToString("o", CultureInfo.InvariantCulture));
        Log.Info(restart ? "Restarting, apps prepared at the next start" : "Shutting down, apps prepared at the next start");
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), restart ? "/g /t 0" : "/sg /t 0")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("shutdown.exe did not start.");
        process.WaitForExit(10000);
        if (process.HasExited && process.ExitCode != 0)
        {
            Forget();
            throw new InvalidOperationException($"shutdown.exe ended with code {process.ExitCode}.");
        }
    }

    /// <summary>StartQueue still runs a while after shutting down: an app stopped the shutdown, so the note goes.</summary>
    public static void Forget()
    {
        try
        {
            File.Delete(Marker);
        }
        catch
        {
            // Checked and removed at the next sign-in anyway.
        }
    }

    /// <summary>
    /// At sign-in: after a prepared shutdown, whether Windows signed in by itself, which shows in a session still
    /// locked when StartQueue started. Null if no prepared shutdown came before or Windows does not say.
    /// </summary>
    public static bool? CheckAtSignIn(bool? lockedAtStart)
    {
        DateTime written;
        try
        {
            if (!File.Exists(Marker)) return null;
            var text = File.ReadAllText(Marker).Trim();
            File.Delete(Marker);
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out written)) return null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading the note of the prepared shutdown failed: {ex.Message}");
            return null;
        }
        if (DateTime.Now - written > MarkerLifetime || lockedAtStart == null) return null;
        Log.Info(lockedAtStart == true
            ? "Prepared start worked: Windows signed in by itself, the apps started behind the lock screen"
            : "Prepared start did not work: the session was not locked when StartQueue started");
        return lockedAtStart;
    }
}
