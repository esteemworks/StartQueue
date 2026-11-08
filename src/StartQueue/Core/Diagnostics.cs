using System.Text.Json;

namespace StartQueue.Core;

/// <summary>
/// StartQueue's own files in the family's diagnostics package (About, Troubleshooting, or --diagnostics): the list and
/// the settings as they are, and the last run. The user's own folder and name are replaced by placeholders; nothing
/// else in there is more personal than the programs that start at sign-in.
/// </summary>
static class Diagnostics
{
    public static void AddToPackage()
    {
        // StartQueue signs in through a scheduled task, the Run key only where tasks are not allowed.
        KitDiagnostics.StartsAtSignIn = () => Setup.Current() is { } state && (state.LogonTask || state.RunKeyStart);
        KitDiagnostics.AddToPackage("startqueue.json", () => Anonymize(File.ReadAllText(Paths.Config)));
        KitDiagnostics.AddToPackage("last-run.json", () => Anonymize(JsonSerializer.Serialize(LastRunStore.Load(), Json.Options)));
        KitDiagnostics.AddToPackage("setup.txt", () => Setup.Describe() + Environment.NewLine);
    }

    static string Anonymize(string text)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (profile.Length > 0) text = text.Replace(profile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
            .Replace(profile.Replace(@"\", @"\\"), @"%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
        return Environment.UserName.Length > 2 ? text.Replace(Environment.UserName, "%USERNAME%", StringComparison.OrdinalIgnoreCase) : text;
    }
}
