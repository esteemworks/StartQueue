using System.Text.Json;

namespace StartQueue.Core;

/// <summary>
/// Changes that need administrator rights, handed to the admin helper (the scheduled task <see cref="Elevation.ElevatedTaskName"/>
/// that a full setup registers) instead of a UAC prompt: set up fully, StartQueue asks Windows nothing more. A request
/// is one of StartQueue's own command lines (<see cref="Commands"/>); the helper runs the waiting ones first thing and
/// writes each exit code back, which the caller waits for. Without the helper (Basis) callers ask through UAC as before.
/// </summary>
public static class AdminHelper
{
    /// <summary>The only command lines the helper takes, each checking its own arguments.</summary>
    public static readonly IReadOnlySet<string> Commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "--restore", "--prepared-signin", "--service",
    };

    sealed record Request(Guid Id, string[] Args);

    static string RequestsFile => Path.Combine(Paths.Data, "admin-requests.json");
    static string ResultsFile => Path.Combine(Paths.Data, "admin-results.json");

    /// <summary>Whether changes go through the helper here: set up fully, and not elevated already.</summary>
    public static bool Available => Elevation.AdminMode && !Elevation.IsElevated;

    /// <summary>
    /// Hands one command line to the helper and waits for its exit code. Null when the helper could not be started or
    /// gave no answer within <paramref name="wait"/> (60 s by default).
    /// </summary>
    public static int? Run(string[] args, TimeSpan? wait = null)
    {
        if (args.Length == 0 || !Commands.Contains(args[0])) throw new ArgumentException($"Not a command for the admin helper: {string.Join(' ', args)}");
        var id = Guid.NewGuid();
        Change<Request>(RequestsFile, list => list.Add(new Request(id, args)));
        if (!Elevation.RunElevatedTask())
        {
            Change<Request>(RequestsFile, list => list.RemoveAll(r => r.Id == id));
            return null;
        }
        var until = DateTime.UtcNow + (wait ?? TimeSpan.FromSeconds(60));
        while (DateTime.UtcNow < until)
        {
            Thread.Sleep(200);
            int? code = null;
            Change<KeyValuePair<Guid, int>>(ResultsFile, list =>
            {
                var index = list.FindIndex(r => r.Key == id);
                if (index < 0) return;
                code = list[index].Value;
                list.RemoveAt(index);
            });
            if (code != null) return code;
        }
        Log.Warning($"The admin helper gave no answer to {args[0]} in time");
        return null;
    }

    /// <summary>In the helper: runs every waiting request with <paramref name="execute"/> and writes its exit code back.</summary>
    public static void RunAll(Func<string[], int> execute)
    {
        var requests = new List<Request>();
        Change<Request>(RequestsFile, list =>
        {
            requests.AddRange(list);
            list.Clear();
        });
        foreach (var request in requests)
        {
            int code;
            try
            {
                code = request.Args.Length > 0 && Commands.Contains(request.Args[0]) ? execute(request.Args) : 87;
            }
            catch (Exception ex)
            {
                Log.Warning($"The admin helper could not run {request.Args.FirstOrDefault()}: {ex.Message}");
                code = 1;
            }
            Log.Info($"Admin helper: {request.Args[0]} done ({code})");
            Change<KeyValuePair<Guid, int>>(ResultsFile, list => list.Add(new(request.Id, code)));
        }
    }

    /// <summary>A read-modify-write of one of the two files under the config lock, which both processes share.</summary>
    static void Change<T>(string file, Action<List<T>> change)
    {
        using var _ = ConfigStore.Lock();
        List<T> list;
        try
        {
            list = File.Exists(file) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(file), Json.Options) ?? new() : new();
        }
        catch
        {
            list = new();
        }
        var before = list.Count;
        change(list);
        if (list.Count == 0)
        {
            if (File.Exists(file)) File.Delete(file);
        }
        else if (list.Count != before)
        {
            File.WriteAllText(file, JsonSerializer.Serialize(list, Json.Options));
        }
    }
}
