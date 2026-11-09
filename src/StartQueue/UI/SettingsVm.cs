using System.ComponentModel;
using StartQueue.Core;

namespace StartQueue.UI;

/// <summary>
/// The settings page. Choices are exposed as one bool per option so segmented buttons and radio cards bind
/// directly; every change is saved at once through the tray host.
/// </summary>
public sealed class SettingsVm : INotifyPropertyChanged
{
    readonly TrayHost host;

    public SettingsVm(TrayHost host) => this.host = host;

    public event PropertyChangedEventHandler? PropertyChanged;

    Settings Current => host.Config.Settings;

    public string ModeTitle => Elevation.AdminMode ? Strings.ModeAdminTitle : Strings.ModeUserTitle;
    public string ModeText => Elevation.AdminMode ? Strings.ModeAdminText : Strings.ModeUserText;

    public bool WindowsDelayOff => WindowsDelay.IsOff;

    /// <summary>Set up by StartQueue: the button undoes it. Otherwise it sets it up, where that is possible.</summary>
    public bool PreparedUndoable => Current.PreparedUndo != null;

    public string PreparedText => PreparedSignIn.Describe(PreparedSignIn.SetupState(), PreparedSignIn.State());

    public string PreparedButton => PreparedUndoable ? Strings.PreparedUndoButton : Strings.PreparedSetUpButton;

    public bool PreparedCanAct => PreparedUndoable || PreparedSignIn.SetupState() == PreparedSetupState.CanSetUp;

    public void RefreshPrepared()
    {
        foreach (var name in new[] { nameof(PreparedText), nameof(PreparedButton), nameof(PreparedCanAct) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Set up without them, but the user is an administrator: one click adds them.</summary>
    public bool CanSetUpAdmin => !Elevation.AdminMode && Setup.UserIsAdmin;

    /// <summary>How StartQueue starts at sign-in and where its context menu entry is, on this device.</summary>
    public string SetupStateText
    {
        get
        {
            var state = Setup.Current();
            var signIn = state.LogonTask ? Strings.SetupSignInTask : state.RunKeyStart ? Strings.SetupSignInRun : Strings.SetupSignInNone;
            var menu = state.ModernMenu ? Strings.SetupMenuModern : state.ClassicMenu ? Strings.SetupMenuClassic : Strings.SetupMenuNone;
            return $"{signIn}, {menu}.";
        }
    }

    public bool ThemeSystem { get => Current.Theme == ThemeChoice.System; set => Pick(value, s => s.Theme = ThemeChoice.System); }
    public bool ThemeLight { get => Current.Theme == ThemeChoice.Light; set => Pick(value, s => s.Theme = ThemeChoice.Light); }
    public bool ThemeDark { get => Current.Theme == ThemeChoice.Dark; set => Pick(value, s => s.Theme = ThemeChoice.Dark); }

    /// <summary>The choice of the accent row (built by the kit's <c>AccentRow</c> in MainWindow), saved and applied.</summary>
    public void PickAccent(EsteemWorks.Kit.AccentChoice accent) => Change(s => s.Accent = accent.FromKit());

    /// <summary>The language drop-down: Like Windows, English, Deutsch.</summary>
    public int LanguageIndex
    {
        get => Current.Language switch { LanguageChoice.English => 1, LanguageChoice.German => 2, _ => 0 };
        set => Change(s => s.Language = value switch { 1 => LanguageChoice.English, 2 => LanguageChoice.German, _ => LanguageChoice.System });
    }

    public bool ModeEnabled { get => Current.NewEntries == NewEntryMode.TakeOverEnabled; set => Pick(value, s => s.NewEntries = NewEntryMode.TakeOverEnabled); }
    public bool ModeDisabled { get => Current.NewEntries == NewEntryMode.TakeOverDisabled; set => Pick(value, s => s.NewEntries = NewEntryMode.TakeOverDisabled); }
    public bool ModeNotify { get => Current.NewEntries == NewEntryMode.NotifyOnly; set => Pick(value, s => s.NewEntries = NewEntryMode.NotifyOnly); }

    public bool Scan5 { get => Current.ScanIntervalMinutes == 5; set => Pick(value, s => s.ScanIntervalMinutes = 5); }
    public bool Scan10 { get => Current.ScanIntervalMinutes == 10; set => Pick(value, s => s.ScanIntervalMinutes = 10); }
    public bool Scan30 { get => Current.ScanIntervalMinutes == 30; set => Pick(value, s => s.ScanIntervalMinutes = 30); }
    public bool Scan60 { get => Current.ScanIntervalMinutes == 60; set => Pick(value, s => s.ScanIntervalMinutes = 60); }
    public bool ScanNever { get => Current.ScanIntervalMinutes <= 0; set => Pick(value, s => s.ScanIntervalMinutes = 0); }

    public bool NotifyNewEntries
    {
        get => Current.NotifyNewEntries;
        set => Change(s => s.NotifyNewEntries = value);
    }

    public bool ManageStoreApps
    {
        get => Current.ManageStoreApps;
        set => Change(s => s.ManageStoreApps = value);
    }

    public int StartDelaySeconds
    {
        get => Current.StartDelaySeconds;
        set => Change(s => s.StartDelaySeconds = Math.Clamp(value, 0, 600));
    }

    public int GapSeconds
    {
        get => Current.GapSeconds;
        set => Change(s => s.GapSeconds = Math.Clamp(value, 0, 120));
    }

    public int WaitLimitSeconds
    {
        get => Current.WaitLimitSeconds;
        set => Change(s => s.WaitLimitSeconds = Math.Clamp(value, 5, 600));
    }

    public bool NotifyAfterSequence
    {
        get => Current.NotifyAfterSequence;
        set => Change(s => s.NotifyAfterSequence = value);
    }


    // Radio buttons also report the option they leave (false); only the chosen one counts.
    void Pick(bool chosen, Action<Settings> change)
    {
        if (chosen) Change(change);
    }

    void Change(Action<Settings> change)
    {
        host.UpdateSettings(change);
        // Clamped values and the other options of a group show what was actually saved.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
