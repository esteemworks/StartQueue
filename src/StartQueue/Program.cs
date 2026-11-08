using StartQueue.Core;

namespace StartQueue;

/// <summary>
/// The entry point. At sign-in the startup order begins here, before WPF and its resources are even loaded:
/// they can take most of a minute while the PC is busy with signing in.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Who the app is, before anything logs: data folder, log, message pipe and icon come from here.
        KitApp.Init("StartQueue", Palette.Ember);
        // Updates from the GitHub releases, once a day when the user left it on (Settings, About, Updates).
        KitUpdate.Configure("esteemworks/StartQueue");
        // Restorable settings: an older config.json is taken over (after a backup) before the early start or anything
        // else reads it; export, import and the backups under About (Backup and restore).
        RestorableSettings.Configure();
        AppIcons.SetMark(UI.Mark.Paint);
        // What nobody catches goes to the log and the event log (3001), for the command lines too.
        KitDiagnostics.CatchUnhandled();
        if (args.Length > 0 && args[0].Equals("--autostart", StringComparison.OrdinalIgnoreCase)) EarlyStart.Begin();
        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }
}
