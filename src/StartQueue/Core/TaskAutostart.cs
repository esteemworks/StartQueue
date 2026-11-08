using System.Runtime.InteropServices;

namespace StartQueue.Core;

/// <summary>Why a scheduled task cannot be taken over.</summary>
public enum TaskBlock { None, SeveralActions, StoredPassword, NoProgram }

/// <param name="OtherTriggers">Starts besides sign-in (daily, at startup …); they stay with Windows either way.</param>
/// <param name="Repeat">How often the sign-in start repeats afterwards, as text; StartQueue takes the repetition over.</param>
/// <param name="RepeatMinutes">The same in minutes; 0 if it does not repeat.</param>
/// <param name="RepeatForMinutes">How long the repetition goes on; 0 for indefinitely.</param>
public sealed record TaskInfo(
    string Path,
    string Name,
    string Folder,
    string Exe,
    string Arguments,
    string WorkingDirectory,
    bool Elevated,
    TaskBlock Block,
    string OtherTriggers,
    string Repeat,
    int RepeatMinutes = 0,
    int RepeatForMinutes = 0);

/// <summary>
/// Scheduled tasks that start at sign-in. StartQueue takes one over by switching off only its sign-in
/// trigger: the task itself stays as it is (rights, conditions, settings) and StartQueue runs it on demand at
/// its place in the order. Giving it back switches the trigger on again. Windows' own tasks are left alone.
/// </summary>
public static class TaskAutostart
{
    const int LogonTrigger = 9;
    const int ExecAction = 0;
    const int TaskUpdate = 4;
    const int HighestRunLevel = 1;
    const int PasswordLogon = 1;
    const int InteractiveOrPassword = 6;
    const int AccessDenied = unchecked((int)0x80070005);

    static readonly Dictionary<int, (string English, string German)> TriggerNames = new()
    {
        [0] = ("on an event", "bei Ereignis"),
        [1] = ("once", "einmalig"),
        [2] = ("daily", "täglich"),
        [3] = ("weekly", "wöchentlich"),
        [4] = ("monthly", "monatlich"),
        [5] = ("monthly", "monatlich"),
        [6] = ("when idle", "bei Leerlauf"),
        [7] = ("when created", "bei Erstellung"),
        [8] = ("at startup", "beim Systemstart"),
        [11] = ("on session change", "bei Sitzungswechsel"),
    };

    /// <summary>Enabled tasks with an active sign-in trigger, outside Windows' and StartQueue's own folders.</summary>
    public static List<TaskInfo> List()
    {
        var tasks = new List<TaskInfo>();
        try
        {
            Walk(Service().GetFolder("\\"), tasks);
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading scheduled tasks failed: {ex.Message}");
        }
        return tasks.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static TaskInfo? Get(string path)
    {
        try
        {
            var (folder, name) = Split(path);
            dynamic task = Service().GetFolder(folder).GetTask(name);
            return Describe(task, folder);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>True while the task would start by itself at sign-in.</summary>
    public static bool StartsAtLogon(string path)
    {
        try
        {
            var (folder, name) = Split(path);
            dynamic task = Service().GetFolder(folder).GetTask(name);
            if (!(bool)task.Enabled) return false;
            foreach (dynamic trigger in task.Definition.Triggers)
                if ((int)trigger.Type == LogonTrigger && (bool)trigger.Enabled) return true;
        }
        catch
        {
            // Task gone or not readable.
        }
        return false;
    }

    /// <summary>Switches the task's sign-in triggers on or off. Throws UnauthorizedAccessException if that needs admin rights.</summary>
    public static void SetLogonTrigger(string path, bool enabled)
    {
        try
        {
            var (folder, name) = Split(path);
            dynamic taskFolder = Service().GetFolder(folder);
            dynamic task = taskFolder.GetTask(name);
            dynamic definition = task.Definition;
            foreach (dynamic trigger in definition.Triggers)
                if ((int)trigger.Type == LogonTrigger) trigger.Enabled = enabled;
            taskFolder.RegisterTaskDefinition(name, definition, TaskUpdate, null, null, (int)definition.Principal.LogonType, null);
        }
        catch (COMException ex) when (ex.HResult == AccessDenied)
        {
            throw new UnauthorizedAccessException(ex.Message, ex);
        }
    }

    /// <summary>Runs the task now, the way "Run" in Task Scheduler does.</summary>
    public static void Run(string path)
    {
        try
        {
            var (folder, name) = Split(path);
            dynamic task = Service().GetFolder(folder).GetTask(name);
            task.Run(null);
        }
        catch (COMException ex) when (ex.HResult == AccessDenied)
        {
            throw new UnauthorizedAccessException(ex.Message, ex);
        }
    }

    public static Entry ToEntry(TaskInfo task, bool adminOnly) => new()
    {
        Name = task.Name,
        Kind = EntryKind.ScheduledTask,
        Target = task.Path,
        ResolvedTarget = task.Exe,
        ProcessName = CommandLine.ProcessNameOf(task.Exe),
        RepeatMinutes = task.RepeatMinutes,
        RepeatForMinutes = task.RepeatForMinutes,
        Origin = new NativeOrigin
        {
            Kind = OriginKind.ScheduledTask,
            Location = "Task",
            Name = task.Path,
            Data = task.Path,
            AdminOnly = adminOnly,
        },
    };

    public static string KeyOf(string path) => NativeOrigin.MakeKey(OriginKind.ScheduledTask, "Task", path);

    // ---------- Reading ----------

    static void Walk(dynamic folder, List<TaskInfo> tasks)
    {
        string path = folder.Path;
        if (path.StartsWith(@"\Microsoft", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(@"\StartQueue", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            foreach (dynamic task in folder.GetTasks(1))
            {
                try
                {
                    if (!(bool)task.Enabled) continue;
                    var info = Describe(task, path);
                    if (info != null && StartsAtLogonNow(task)) tasks.Add(info);
                }
                catch
                {
                    // A task we may not read; skip it.
                }
            }
        }
        catch
        {
            // A folder we may not read.
        }

        try
        {
            foreach (dynamic sub in folder.GetFolders(0)) Walk(sub, tasks);
        }
        catch
        {
            // Subfolders we may not read.
        }
    }

    static bool StartsAtLogonNow(dynamic task)
    {
        foreach (dynamic trigger in task.Definition.Triggers)
            if ((int)trigger.Type == LogonTrigger && (bool)trigger.Enabled) return true;
        return false;
    }

    static TaskInfo? Describe(dynamic task, string folder)
    {
        dynamic definition = task.Definition;
        var german = Strings.IsGerman;

        var others = new List<string>();
        var repeat = "";
        var repeatMinutes = 0;
        var repeatForMinutes = 0;
        foreach (dynamic trigger in definition.Triggers)
        {
            int type = trigger.Type;
            if (!(bool)trigger.Enabled) continue;
            if (type == LogonTrigger)
            {
                string? interval = trigger.Repetition.Interval;
                if (!string.IsNullOrEmpty(interval))
                {
                    repeat = FormatInterval(interval);
                    repeatMinutes = Minutes(interval);
                    repeatForMinutes = Minutes((string?)trigger.Repetition.Duration);
                }
                continue;
            }
            var label = TriggerNames.TryGetValue(type, out var names) ? (german ? names.German : names.English) : type.ToString();
            if (!others.Contains(label)) others.Add(label);
        }

        var exe = "";
        var arguments = "";
        var workingDirectory = "";
        var execActions = 0;
        foreach (dynamic action in definition.Actions)
        {
            if ((int)action.Type != ExecAction) continue;
            execActions++;
            if (execActions > 1) continue;
            exe = Environment.ExpandEnvironmentVariables(((string?)action.Path ?? "").Trim('"'));
            arguments = (string?)action.Arguments ?? "";
            workingDirectory = (string?)action.WorkingDirectory ?? "";
        }

        int logonType = definition.Principal.LogonType;
        var block = execActions > 1 ? TaskBlock.SeveralActions
            : execActions == 0 ? TaskBlock.NoProgram
            : logonType is PasswordLogon or InteractiveOrPassword ? TaskBlock.StoredPassword
            : TaskBlock.None;

        var path = (folder.EndsWith('\\') ? folder : folder + "\\") + (string)task.Name;
        return new TaskInfo(path, (string)task.Name, folder, exe, arguments, workingDirectory,
            (int)definition.Principal.RunLevel == HighestRunLevel, block, string.Join(", ", others), repeat, repeatMinutes, repeatForMinutes);
    }

    /// <summary>"PT3H30M" as 210; empty or unreadable as 0.</summary>
    static int Minutes(string? iso)
    {
        try
        {
            return string.IsNullOrEmpty(iso) ? 0 : (int)Math.Round(System.Xml.XmlConvert.ToTimeSpan(iso).TotalMinutes);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>"PT3H30M" as "3 h 30 min".</summary>
    static string FormatInterval(string iso)
    {
        try
        {
            var span = System.Xml.XmlConvert.ToTimeSpan(iso);
            var parts = new List<string>();
            if (span.Days > 0) parts.Add($"{span.Days} d");
            if (span.Hours > 0) parts.Add($"{span.Hours} h");
            if (span.Minutes > 0) parts.Add($"{span.Minutes} min");
            return parts.Count > 0 ? string.Join(" ", parts) : iso;
        }
        catch
        {
            return iso;
        }
    }

    static dynamic Service()
    {
        var type = Type.GetTypeFromProgID("Schedule.Service") ?? throw new InvalidOperationException("Task Scheduler is not available.");
        dynamic service = Activator.CreateInstance(type)!;
        service.Connect();
        return service;
    }

    static (string Folder, string Name) Split(string path)
    {
        var cut = path.LastIndexOf('\\');
        var folder = cut <= 0 ? "\\" : path[..cut];
        return (folder, path[(cut + 1)..]);
    }
}
