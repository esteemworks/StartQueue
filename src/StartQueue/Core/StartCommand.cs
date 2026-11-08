namespace StartQueue.Core;

/// <summary>A program with its start parameters.</summary>
public sealed record StartCommand(string Program, string Arguments, string WorkingDirectory);

/// <summary>
/// Own start parameters. "Add" puts them after the parameters the app brings, which stay as they are; "Replace"
/// uses them instead. A shortcut or scheduled task cannot take parameters from outside, so with own parameters
/// StartQueue starts the program behind it directly.
/// </summary>
public static class StartCommands
{
    /// <summary>What the entry starts by itself, before own parameters. Null when there is no program to name.</summary>
    public static StartCommand? Original(Entry entry)
    {
        try
        {
            switch (entry.Kind)
            {
                case EntryKind.Program:
                    return new StartCommand(Environment.ExpandEnvironmentVariables(entry.Target), entry.Arguments ?? "", entry.WorkingDirectory);
                case EntryKind.Shortcut:
                    var (target, arguments) = Shortcut.Resolve(entry.Target);
                    return string.IsNullOrEmpty(target) ? null : new StartCommand(target, arguments ?? "", "");
                case EntryKind.ScheduledTask:
                    return TaskAutostart.Get(entry.Target) is { Exe.Length: > 0 } task
                        ? new StartCommand(Environment.ExpandEnvironmentVariables(task.Exe), task.Arguments ?? "", task.WorkingDirectory)
                        : null;
                case EntryKind.PackagedApp:
                    return new StartCommand(entry.Target, entry.Arguments ?? "", "");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"Reading what \"{entry.Name}\" starts failed: {ex.Message}");
        }
        return null;
    }

    /// <summary>The parameters the entry starts with, own ones included.</summary>
    public static string Arguments(Entry entry, string original) => entry.Parameters switch
    {
        ParameterMode.Add when !string.IsNullOrWhiteSpace(entry.ExtraArguments) => $"{original} {entry.ExtraArguments.Trim()}".Trim(),
        ParameterMode.Replace => entry.OwnArguments.Trim(),
        _ => original,
    };

    /// <summary>Whether StartQueue has to start the program itself instead of the shortcut or task.</summary>
    public static bool HasOwn(Entry entry) => entry.Parameters switch
    {
        ParameterMode.Add => !string.IsNullOrWhiteSpace(entry.ExtraArguments),
        ParameterMode.Replace => true,
        _ => false,
    };

    /// <summary>The whole command line the entry starts with, for showing.</summary>
    public static string Describe(Entry entry)
    {
        if (Original(entry) is not { } original) return "";
        var arguments = Arguments(entry, original.Arguments);
        var program = original.Program.Contains(' ') ? $"\"{original.Program}\"" : original.Program;
        return arguments.Length == 0 ? program : $"{program} {arguments}";
    }
}
