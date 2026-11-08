namespace StartQueue.Core;

/// <summary>
/// StartQueue's own ids in the Windows event log (Application, source "StartQueue"), next to the family's in
/// <see cref="KitEvents"/>. Only what an admin acts on goes there; the README lists them.
/// </summary>
static class Events
{
    // 1100s: things set up or changed in Windows on purpose
    public const int PreparedSignInSetUp = 1100;
    public const int PreparedSignInUndone = 1101;
    public const int ServiceChanged = 1102;
    public const int WindowsDelayChanged = 1103;

    // 2100s: the Windows autostart put right
    public const int EntryTakenOver = 2100;
    public const int EntryReturned = 2101;
    public const int ReRegisteredRemoved = 2102;

    // 3100s: something did not work
    public const int StartFailed = 3100;
    public const int TakeOverFailed = 3101;
    public const int ReturnFailed = 3102;
    // 3103 (the list could not be read) was StartQueue's own until 1.5.2; now the family's KitEvents.SettingsUnreadable
    // (2004). Not given out again.
    public const int SaveFailed = 3104;
    public const int ServiceChangeFailed = 3105;
    public const int PreparedSignInFailed = 3106;
    public const int ShutdownFailed = 3107;
}
