using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace StartQueue.Core;

/// <summary>Start types as services.msc names them; Boot and System belong to drivers and stay as they are.</summary>
public enum ServiceStart { AutomaticDelayed, Automatic, Manual, Disabled, Boot, System }

public enum ServiceState { Stopped = 1, StartPending, StopPending, Running, ContinuePending, PausePending, Paused }

public enum ServiceAction { AutomaticDelayed, Automatic, Manual, Disabled, Start, Stop, Restart }

public sealed record ServiceInfo(
    string Name,
    string DisplayName,
    string Description,
    ServiceState State,
    ServiceStart Start,
    bool TriggerStart,
    string Account,
    string ImagePath,
    bool PerUser,
    bool CanStop,
    bool IsWindows,
    bool Essential,
    string Company);

/// <summary>
/// The Windows services, shown and changed the way services.msc does it. StartQueue takes none of them over:
/// Windows keeps starting them. Changes need administrator rights; without them each change asks through UAC, as
/// services.msc does. The silent admin task is not used on purpose, so no program under the user's account can
/// change services through StartQueue without that question.
/// </summary>
public static class Services
{
    const string ServicesKey = @"SYSTEM\CurrentControlSet\Services";

    public static List<ServiceInfo> List()
    {
        var result = new List<ServiceInfo>();
        var scm = OpenSCManager(null, null, ScManagerConnect | ScManagerEnumerate);
        if (scm == IntPtr.Zero) throw new Win32Exception();
        try
        {
            uint resume = 0;
            EnumServicesStatusEx(scm, 0, ServiceWin32, ServiceStateAll, IntPtr.Zero, 0, out var needed, out _, ref resume, null);
            var buffer = Marshal.AllocHGlobal((int)needed + 4096);
            try
            {
                resume = 0;
                if (!EnumServicesStatusEx(scm, 0, ServiceWin32, ServiceStateAll, buffer, needed + 4096, out _, out var count, ref resume, null))
                    throw new Win32Exception();
                var size = Marshal.SizeOf<EnumServiceStatusProcess>();
                for (var i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<EnumServiceStatusProcess>(buffer + i * size);
                    var name = Marshal.PtrToStringUni(item.ServiceName) ?? "";
                    var status = item.Status;
                    result.Add(Read(name, Marshal.PtrToStringUni(item.DisplayName) ?? name, (ServiceState)status.CurrentState,
                        (status.ServiceType & 0xC0) != 0, (status.ControlsAccepted & 1) != 0));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
        return result.OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Start type, description and program come from the registry, where services.msc reads them too.</summary>
    static ServiceInfo Read(string name, string displayName, ServiceState state, bool perUser, bool canStop)
    {
        // A few services lock their key or parts of it for users; they are listed with what can be read.
        RegistryKey? key = null;
        try { key = Registry.LocalMachine.OpenSubKey($@"{ServicesKey}\{name}"); } catch { }
        using var _ = key;
        var start = key?.GetValue("Start") is int s ? s : 3;
        var delayed = key?.GetValue("DelayedAutostart") is int d && d == 1;
        bool trigger;
        try { using var triggers = key?.OpenSubKey("TriggerInfo"); trigger = triggers?.SubKeyCount > 0; } catch { trigger = false; }
        var type = start switch
        {
            0 => ServiceStart.Boot,
            1 => ServiceStart.System,
            2 => delayed ? ServiceStart.AutomaticDelayed : ServiceStart.Automatic,
            4 => ServiceStart.Disabled,
            _ => ServiceStart.Manual,
        };
        var imagePath = key?.GetValue("ImagePath", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var file = ProgramFile(key, imagePath);
        var company = CompanyOf(file);
        // Part of Windows: a program by Microsoft in the Windows folder or in a folder of a Windows part ("Windows Defender",
        // "Windows Media Player", "Windows Identity Foundation" …), or a per-user service.
        // A file locked for users (Defender) gives no publisher; then its place in a Microsoft folder of Windows decides.
        var isWindows = perUser || company.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
            (file.StartsWith(WindowsFolder, StringComparison.OrdinalIgnoreCase) ||
             file.Split('\\').Any(part => part.StartsWith("Windows ", StringComparison.OrdinalIgnoreCase)))
            || company.Length == 0 && file.Contains(@"\Microsoft\Windows ", StringComparison.OrdinalIgnoreCase);
        var errorControl = key?.GetValue("ErrorControl") is int e ? e : 1;
        var essential = isWindows && (errorControl >= 2 || Essentials.Contains(name));
        return new ServiceInfo(name, Indirect(displayName) is { Length: > 0 } shown ? shown : name, Indirect(key?.GetValue("Description") as string ?? ""), state, type, trigger,
            key?.GetValue("ObjectName") as string ?? "", imagePath, perUser, canStop, isWindows, essential, company);
    }

    static readonly string WindowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\";

    /// <summary>Services Windows does not run properly without: stopping or disabling them can make it unusable.</summary>
    static readonly HashSet<string> Essentials = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs", "RpcEptMapper", "DcomLaunch", "LSM", "Power", "BrokerInfrastructure", "SamSs", "EventLog", "PlugPlay", "ProfSvc",
        "Schedule", "CoreMessagingRegistrar", "SystemEventsBroker", "gpsvc", "mpssvc", "WinDefend", "BFE", "Winmgmt", "CryptSvc",
        "Dhcp", "Dnscache", "nsi", "AudioSrv", "AudioEndpointBuilder", "Themes", "UserManager", "StateRepository", "TimeBrokerSvc",
        "wscsvc", "SecurityHealthService", "LanmanWorkstation", "EventSystem", "SENS", "ShellHWDetection", "Wcmsvc", "WinHttpAutoProxySvc",
        "AppXSvc", "ClipSVC", "camsvc", "KeyIso", "VaultSvc", "TrustedInstaller", "wuauserv", "TokenBroker", "TextInputManagementService",
    };

    /// <summary>The program file behind the service; for services in svchost, their DLL.</summary>
    static string ProgramFile(RegistryKey? key, string imagePath)
    {
        var line = Environment.ExpandEnvironmentVariables(imagePath).Trim();
        if (line.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase)) line = WindowsFolder + line[12..];
        string file;
        if (line.StartsWith('"'))
        {
            var close = line.IndexOf('"', 1);
            file = close > 0 ? line[1..close] : line.Trim('"');
        }
        else
        {
            var end = line.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            file = end > 0 ? line[..(end + 4)] : line.Split(' ')[0];
        }
        if (Path.GetFileName(file).Equals("svchost.exe", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var parameters = key?.OpenSubKey("Parameters");
                if ((parameters?.GetValue("ServiceDll") ?? key?.GetValue("ServiceDll")) is string dll) file = Environment.ExpandEnvironmentVariables(dll);
            }
            catch
            {
                // Locked: svchost itself still tells it is part of Windows.
            }
        }
        return file;
    }

    static string CompanyOf(string file)
    {
        try
        {
            return File.Exists(file) ? FileVersionInfo.GetVersionInfo(file).CompanyName ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>"@%SystemRoot%\system32\x.dll,-101" as the text it points to.</summary>
    static string Indirect(string text)
    {
        // Some descriptions come with XML entities, as services.msc shows them decoded.
        if (!text.StartsWith('@')) return text.Replace("&amp;", "&");
        var buffer = new StringBuilder(1024);
        // Not readable without administrator rights for a few services: then no text rather than the reference.
        return SHLoadIndirectString(text, buffer, buffer.Capacity, IntPtr.Zero) == 0 ? buffer.ToString() : "";
    }

    /// <summary>Running services that depend on this one; services.msc stops them along with it.</summary>
    public static List<string> RunningDependents(string name)
    {
        var names = new List<string>();
        WithService(name, ServiceEnumerateDependents, service =>
        {
            EnumDependentServices(service, 1, IntPtr.Zero, 0, out var needed, out _);
            if (needed == 0) return;
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!EnumDependentServices(service, 1, buffer, needed, out _, out var count)) return;
                var size = Marshal.SizeOf<EnumServiceStatus>();
                for (var i = 0; i < count; i++)
                {
                    var item = Marshal.PtrToStructure<EnumServiceStatus>(buffer + i * size);
                    names.Add(Marshal.PtrToStringUni(item.DisplayName) ?? Marshal.PtrToStringUni(item.ServiceName) ?? "");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        });
        return names;
    }

    // ---------- Changing ----------

    /// <summary>
    /// Carries out the change: in this process when it has administrator rights, otherwise in a copy of StartQueue
    /// started through UAC. Returns null when it worked, or what went wrong.
    /// </summary>
    public static async Task<string?> ChangeAsync(string name, ServiceAction action)
    {
        if (Elevation.IsElevated)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Apply(name, action);
                    return null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"--service {action} \"{name}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
            })!;
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? null : new Win32Exception(process.ExitCode).Message;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Strings.ServiceCancelled;
        }
    }

    /// <summary>The "--service" mode, run with administrator rights. Exit code: 0, or the Windows error.</summary>
    public static int ApplyFromCommandLine(string action, string name)
    {
        try
        {
            if (!Enum.TryParse<ServiceAction>(action, true, out var parsed)) return 87;
            Apply(name, parsed);
            return 0;
        }
        catch (Win32Exception ex)
        {
            Log.Event(Events.ServiceChangeFailed, LogLevel.Warning, $"Changing service {name} ({action}) failed: {ex.Message}");
            return ex.NativeErrorCode;
        }
        catch (Exception ex)
        {
            Log.Event(Events.ServiceChangeFailed, LogLevel.Warning, $"Changing service {name} ({action}) failed: {ex.Message}");
            return 1;
        }
    }

    static void Apply(string name, ServiceAction action)
    {
        Log.Event(Events.ServiceChanged, LogLevel.Info, $"Service {name}: {action}");
        switch (action)
        {
            case ServiceAction.AutomaticDelayed or ServiceAction.Automatic or ServiceAction.Manual or ServiceAction.Disabled:
                WithService(name, ServiceChangeConfig, service =>
                {
                    var start = action switch { ServiceAction.Manual => 3u, ServiceAction.Disabled => 4u, _ => 2u };
                    if (!ChangeServiceConfig(service, NoChange, start, NoChange, null, null, IntPtr.Zero, null, null, null, null))
                        throw new Win32Exception();
                    var info = new DelayedAutoStartInfo { Delayed = action == ServiceAction.AutomaticDelayed ? 1 : 0 };
                    if (start == 2 && !ChangeServiceConfig2(service, 3, ref info)) throw new Win32Exception();
                });
                break;
            case ServiceAction.Start:
                Start(name);
                break;
            case ServiceAction.Stop:
                Stop(name);
                break;
            case ServiceAction.Restart:
                Stop(name);
                Start(name);
                break;
        }
    }

    static void Start(string name) => WithService(name, ServiceStartRight | ServiceQueryStatus, service =>
    {
        // 1056: already running, which is what was asked for.
        if (!StartService(service, 0, null) && Marshal.GetLastWin32Error() != 1056) throw new Win32Exception();
        WaitFor(service, ServiceState.Running);
    });

    /// <summary>Stops the running services that depend on it first, as services.msc does.</summary>
    static void Stop(string name)
    {
        var dependents = new List<string>();
        WithService(name, ServiceEnumerateDependents, service =>
        {
            EnumDependentServices(service, 1, IntPtr.Zero, 0, out var needed, out _);
            if (needed == 0) return;
            var buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (!EnumDependentServices(service, 1, buffer, needed, out _, out var count)) return;
                var size = Marshal.SizeOf<EnumServiceStatus>();
                for (var i = 0; i < count; i++)
                    dependents.Add(Marshal.PtrToStringUni(Marshal.PtrToStructure<EnumServiceStatus>(buffer + i * size).ServiceName) ?? "");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        });
        // Windows lists them in the order they have to stop.
        foreach (var dependent in dependents.Where(d => d.Length > 0)) StopOne(dependent);
        StopOne(name);
    }

    static void StopOne(string name) => WithService(name, ServiceStopRight | ServiceQueryStatus, service =>
    {
        // 1062: not running, which is what was asked for.
        if (!ControlService(service, 1, out _) && Marshal.GetLastWin32Error() != 1062) throw new Win32Exception();
        WaitFor(service, ServiceState.Stopped);
    });

    static void WaitFor(IntPtr service, ServiceState wanted)
    {
        var until = DateTime.Now.AddSeconds(30);
        while (DateTime.Now < until)
        {
            if (QueryServiceStatus(service, out var status) && (ServiceState)status.CurrentState == wanted) return;
            Thread.Sleep(250);
        }
        throw new TimeoutException(Strings.ServiceTimeout);
    }

    static void WithService(string name, uint access, Action<IntPtr> use)
    {
        var scm = OpenSCManager(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero) throw new Win32Exception();
        try
        {
            var service = OpenService(scm, name, access);
            if (service == IntPtr.Zero) throw new Win32Exception();
            try
            {
                use(service);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    // ---------- Windows API ----------

    const uint ScManagerConnect = 0x1, ScManagerEnumerate = 0x4;
    const uint ServiceWin32 = 0x30, ServiceStateAll = 3, NoChange = 0xFFFFFFFF;
    const uint ServiceQueryStatus = 0x4, ServiceChangeConfig = 0x2, ServiceEnumerateDependents = 0x8, ServiceStartRight = 0x10, ServiceStopRight = 0x20;

    [StructLayout(LayoutKind.Sequential)]
    struct ServiceStatus
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode, CheckPoint, WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ServiceStatusProcess
    {
        public uint ServiceType, CurrentState, ControlsAccepted, Win32ExitCode, ServiceSpecificExitCode, CheckPoint, WaitHint, ProcessId, ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EnumServiceStatusProcess
    {
        public IntPtr ServiceName, DisplayName;
        public ServiceStatusProcess Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EnumServiceStatus
    {
        public IntPtr ServiceName, DisplayName;
        public ServiceStatus Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DelayedAutoStartInfo
    {
        public int Delayed;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr OpenSCManager(string? machine, string? database, uint access);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr OpenService(IntPtr scm, string name, uint access);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool EnumServicesStatusEx(IntPtr scm, int infoLevel, uint serviceType, uint serviceState, IntPtr buffer, uint bufferSize,
        out uint bytesNeeded, out uint servicesReturned, ref uint resumeHandle, string? groupName);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool EnumDependentServices(IntPtr service, uint state, IntPtr buffer, uint bufferSize, out uint bytesNeeded, out uint servicesReturned);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool ChangeServiceConfig(IntPtr service, uint type, uint start, uint errorControl, string? binaryPath, string? loadOrderGroup,
        IntPtr tagId, string? dependencies, string? account, string? password, string? displayName);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ChangeServiceConfig2(IntPtr service, uint infoLevel, ref DelayedAutoStartInfo info);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool StartService(IntPtr service, uint argCount, string[]? args);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool ControlService(IntPtr service, uint control, out ServiceStatus status);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int SHLoadIndirectString(string source, StringBuilder buffer, int size, IntPtr reserved);
}
