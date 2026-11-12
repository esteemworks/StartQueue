using System.Globalization;

namespace StartQueue;

/// <summary>
/// All user-facing text, in the Windows display language: German if Windows is German, English otherwise. The
/// language itself is EsteemWorks.Kit's (<see cref="EsteemWorks.Kit.Strings"/>), so the texts of the family's building blocks
/// follow the same choice.
/// </summary>
public static class Strings
{
    static Core.LanguageChoice choice;

    /// <summary>
    /// Chosen in the settings; System follows the Windows display language. Also sets the UI culture, so the
    /// texts built into WPF (the cut/copy/paste menu of text fields) match. Date and number formats keep the
    /// Windows regional format.
    /// </summary>
    public static Core.LanguageChoice Choice
    {
        get => choice;
        set
        {
            choice = value;
            EsteemWorks.Kit.Strings.Choice = value switch
            {
                Core.LanguageChoice.German => "de",
                Core.LanguageChoice.English => "en",
                _ => null,
            };
        }
    }

    public static bool IsGerman => EsteemWorks.Kit.Strings.German;

    public static string T(string english, string german) => EsteemWorks.Kit.Strings.T(english, german);

    public static string F(string format, params object?[] args) => EsteemWorks.Kit.Strings.F(format, args);

    // ---------- Header ----------
    public static string Scan => T("Scan", "Prüfen");
    public static string ScanNothingNew => T("Checked everything, nothing new.", "Alles geprüft, nichts Neues.");
    public static string ScanTakenOverOne => T("Taken over: {0}", "Übernommen: {0}");
    public static string ScanTakenOver => T("Taken over: {0}", "Übernommen: {0}");
    public static string ScanNewInWindows => T("New in the Windows autostart: {0}", "Neu im Windows-Autostart: {0}");
    public static string ScanNewTask => T("New sign-in task: {0}", "Neue Anmelde-Aufgabe: {0}");
    public static string ScanNewTasks => T("New sign-in tasks: {0}", "Neue Anmelde-Aufgaben: {0}");
    public static string ScanNewService => T("New service: {0}", "Neuer Dienst: {0}");
    public static string ScanNewServices => T("New services: {0}", "Neue Dienste: {0}");
    public static string ScanAndMore => T("{0} and {1} more", "{0} und {1} weitere");
    public static string ScanTip => T("Check everything now: new autostart entries and sign-in tasks, the Windows autostart list and the services (F5)", "Alles jetzt prüfen: neue Autostart-Einträge und Anmelde-Aufgaben, die Windows-Autostart-Liste und die Dienste (F5)");
    public static string AddApp => T("Add app", "App hinzufügen");
    public static string AddAppTip => T("Add a program, shortcut or Store app (Ctrl+N)", "Programm, Verknüpfung oder Store-App hinzufügen (Strg+N)");
    public static string AddProgramMenu => T("Program or shortcut…", "Programm oder Verknüpfung …");
    public static string AddStartAppMenu => T("App from the Start menu…", "App aus dem Startmenü …");
    public static string CtrlN => T("Ctrl+N", "Strg+N");
    public static string TabOrder => T("Startup order", "Startreihenfolge");
    public static string TabKept => T("Kept in Windows", "In Windows belassen");

    public static string Summary => T("{0} of {1} apps start when you sign in", "{0} von {1} Apps starten bei der Anmeldung");
    public static string SummaryLastRun => T("Last run {0:g}: {1} apps, the last one {2:0} s after the start", "Letzter Durchlauf {0:g}: {1} Apps, die letzte {2:0} s nach dem Start");
    public static string SummaryLastSignIn => T("Last sign-in {0:g}: {1} apps, the last one {2:0} s after sign-in", "Letzte Anmeldung {0:g}: {1} Apps, die letzte {2:0} s nach der Anmeldung");
    public static string SummaryFailed => T(", {0} failed", ", {0} fehlgeschlagen");
    public static string StatusOneNew => T("1 new entry", "1 neuer Eintrag");
    public static string StatusNew => T("{0} new entries", "{0} neue Einträge");
    public static string StatusOneLeft => T("1 entry still in Windows", "1 Eintrag noch in Windows");
    public static string StatusLeft => T("{0} entries still in Windows", "{0} Einträge noch in Windows");
    public static string StatusNewTip => T("Show the first new entry", "Zum ersten neuen Eintrag");
    public static string StatusLeftTip => T("Show the Windows autostart", "Zum Windows-Autostart");

    // ---------- List ----------
    public static string OrderHint => T(
        "Apps start from top to bottom when you sign in. Drag a row or press Alt+Up/Down to reorder. Drop apps from the Start menu or Explorer here to add them.",
        "Die Apps starten nach der Anmeldung von oben nach unten. Zum Umsortieren eine Zeile ziehen oder Alt+Pfeil drücken. Apps aus dem Startmenü oder Explorer hierher ziehen, um sie hinzuzufügen.");
    public static string MarkAllSeen => T("Mark all as seen", "Alle als gesehen markieren");
    public static string RunSequence => T("Start sequence now", "Reihenfolge jetzt starten");
    public static string RunSequenceTip => T("Starts every enabled app that is not running yet, in this order", "Startet alle eingeschalteten Apps, die noch nicht laufen, in dieser Reihenfolge");
    public static string DragTip => T("Drag to reorder", "Ziehen zum Umsortieren");
    public static string StartAtLogon => T("Start at logon", "Bei Anmeldung starten");
    public static string NewChip => T("NEW", "NEU");
    public static string NewChipTip => T("Added itself to the Windows autostart. Select it to mark it as seen.", "Hat sich selbst in den Windows-Autostart eingetragen. Zum Markieren als gesehen auswählen.");
    public static string DelayTip => T("Waits this long after the previous app", "Wartet so lange nach der vorherigen App");
    public static string WaitsChip => T("waits", "wartet");
    public static string WaitsTip => T("The next app starts once this one runs", "Die nächste App startet erst, wenn diese läuft");
    public static string AdminChip => T("admin", "Admin");
    public static string AdminTip => T("Starts as administrator", "Startet als Administrator");
    public static string StartNow => T("Start now", "Jetzt starten");
    public static string EmptyTitle => T("Nothing starts yet", "Noch startet nichts");
    public static string EmptyText => T(
        "Add an app, drag one here from the Start menu, or right-click a program in Explorer and choose Add to StartQueue.",
        "Füge eine App hinzu, ziehe sie aus dem Startmenü hierher oder wähle im Explorer per Rechtsklick „Zu StartQueue hinzufügen“.");

    // ---------- Details ----------
    public static string NoSelection => T("Select an app to change when and how it starts.", "Wähle eine App aus, um festzulegen, wann und wie sie startet.");
    public static string SectionStartup => T("STARTUP", "START");
    public static string Delay => T("Delay before start", "Verzögerung vor dem Start");
    public static string DelayUnit => T("seconds after the previous app", "Sekunden nach der vorherigen App");
    public static string DelayName => T("Delay before start in seconds", "Verzögerung vor dem Start in Sekunden");
    public static string WaitUntilRunning => T("Wait until it runs", "Warten, bis sie läuft");
    public static string WaitHelp => T("The next app starts only once this one is running, at most 60 seconds later.", "Die nächste App startet erst, wenn diese läuft, spätestens nach 60 Sekunden.");
    public static string RunElevated => T("Run as administrator", "Als Administrator ausführen");
    public static string RunElevatedHelp => T("Starts with admin rights through the StartQueue task, without a UAC prompt.", "Startet mit Adminrechten über die StartQueue-Aufgabe, ohne UAC-Abfrage.");
    public static string SectionWhat => T("WHAT STARTS", "WAS STARTET");
    public static string SectionParameters => T("START PARAMETERS", "STARTPARAMETER");
    public static string ParamHowItWorks => T("How it works", "So funktioniert's");
    public static string ParamAsBefore => T("As before", "Wie bisher");
    public static string ParamAdd => T("Add", "Ergänzen");
    public static string ParamReplace => T("Replace", "Ersetzen");
    public static string ParamAsBeforeTip => T("The app starts with the parameters it brings.", "Die App startet mit den Parametern, die sie mitbringt.");
    public static string ParamAddTip => T("Your parameters come after the app's own ones, which stay as they are.", "Deine Parameter kommen hinter die der App, die unverändert bleiben.");
    public static string ParamReplaceTip => T("Your parameters are used instead of the app's own ones. The field starts with the ones in use now.", "Deine Parameter gelten statt der App-eigenen. Das Feld beginnt mit denen, die jetzt gelten.");
    public static string ParamCurrent => T("Parameters", "Parameter");
    public static string ParamCurrentFixed => T("The app's own parameters, they stay", "Parameter der App, bleiben erhalten");
    public static string ParamExtra => T("Added", "Zusätzlich");
    public static string ParamOwn => T("Parameters instead", "Parameter stattdessen");
    public static string ParamRuns => T("Starts with", "Startet mit");
    public static string ParamNone => T("none", "keine");
    public static string ParamHelp => T("Own start parameters for this app. Click for how they work.", "Eigene Startparameter für diese App. Klick für die Erklärung.");
    public static string ParamHelpDirect => T("Own start parameters for this app. With them, StartQueue starts the program behind the shortcut or task directly. Click for how they work.",
        "Eigene Startparameter für diese App. Damit startet StartQueue das Programm hinter der Verknüpfung oder Aufgabe direkt. Klick für die Erklärung.");
    public static string ParamInfoTitle => T("Start parameters", "Startparameter");

    public static (string Title, string Text)[] ParamInfoSections =>
    [
        (T("What they are", "Was das ist"), T(
            "Start parameters are the words after the program on the command line, for example \"--silent\" or \"/minimize\". Each app decides which it understands; the same word can mean something else in another app or nothing at all. Look in the app's help or documentation which ones it takes.",
            "Startparameter sind die Wörter hinter dem Programm in der Befehlszeile, zum Beispiel „--silent“ oder „/minimize“. Welche eine App versteht, entscheidet sie selbst; dasselbe Wort kann in einer anderen App etwas anderes oder gar nichts bewirken. Welche sie annimmt, steht in ihrer Hilfe oder Dokumentation.")),
        (T("As before", "Wie bisher"), T(
            "The app starts as it was taken over or added: with the parameters from its autostart entry, its shortcut or its task.",
            "Die App startet so, wie sie übernommen oder hinzugefügt wurde: mit den Parametern aus ihrem Autostart-Eintrag, ihrer Verknüpfung oder ihrer Aufgabe.")),
        (T("Add", "Ergänzen"), T(
            "The app's own parameters stay untouched and cannot be changed here. Yours are put after them, separated by a space. Good for one more switch without risking what the app needs.",
            "Die Parameter der App bleiben unverändert und lassen sich hier nicht bearbeiten. Deine werden mit einem Leerzeichen dahinter gesetzt. Gut für einen zusätzlichen Schalter, ohne zu gefährden, was die App braucht.")),
        (T("Replace", "Ersetzen"), T(
            "Your line is used instead of the app's own parameters. It starts with the parameters in use now, so you can take something out, add something or write the whole line anew. Back to \"As before\" restores the app's own ones; your line is kept for later.",
            "Deine Zeile gilt statt der Parameter der App. Sie beginnt mit denen, die jetzt gelten: Du kannst etwas herausnehmen, ergänzen oder die ganze Zeile neu schreiben. Zurück auf „Wie bisher“ gelten wieder die der App; deine Zeile bleibt für später gespeichert.")),
        (T("How to write them", "Wie man sie schreibt"), T(
            "Separate parameters with spaces. Put values with spaces in double quotes, for example --profile \"Work 2\". \"Starts with\" shows the whole line as it will run. Whether the app accepts it shows at the next start; \"Start now\" tries it at once.",
            "Parameter durch Leerzeichen trennen. Werte mit Leerzeichen in doppelte Anführungszeichen setzen, zum Beispiel --profile \"Arbeit 2\". „Startet mit“ zeigt die ganze Zeile, wie sie ausgeführt wird. Ob die App sie annimmt, zeigt der nächste Start; „Jetzt starten“ probiert es sofort aus.")),
        (T("Shortcuts and tasks", "Verknüpfungen und Aufgaben"), T(
            "A shortcut or scheduled task takes no parameters from outside. With own parameters StartQueue therefore starts the program behind it directly, in its folder. Settings that only the shortcut or task had, such as \"run minimized\" or the rights of the task, no longer apply then; \"Run as administrator\" still works.",
            "Eine Verknüpfung oder geplante Aufgabe nimmt keine Parameter von außen an. Mit eigenen Parametern startet StartQueue deshalb das Programm dahinter direkt, in dessen Ordner. Einstellungen, die nur die Verknüpfung oder Aufgabe hatte, etwa „minimiert ausführen“ oder die Rechte der Aufgabe, gelten dann nicht mehr; „Als Administrator ausführen“ funktioniert weiterhin.")),
        (T("Window at sign-in", "Fenster beim Start"), T(
            "A known window parameter such as \"--silent\" also counts in your own parameters. Choosing a window mode sets or takes out the known parameter in the line that is in use.",
            "Ein bekannter Fensterparameter wie „--silent“ zählt auch in deinen eigenen Parametern. Die Wahl des Fenstermodus setzt oder entfernt den bekannten Parameter in der Zeile, die gerade gilt.")),
    ];
    public static string Name => T("Name", "Name");
    public static string Program => T("Program", "Programm");
    public static string ChooseProgram => T("Choose program", "Programm auswählen");
    public static string Arguments => T("Arguments", "Argumente");
    public static string StartIn => T("Start in folder", "Starten in");
    public static string StartInHelp => T("Empty means the program's own folder.", "Leer bedeutet: Ordner des Programms.");
    public static string Starts => T("Starts", "Startet");
    public static string ProcessName => T("Process name", "Prozessname");
    public static string ProcessNameHelp => T("Used to tell whether the app already runs, so it is not started twice. Without .exe.", "Daran erkennt StartQueue, ob die App schon läuft, damit sie nicht doppelt startet. Ohne .exe.");
    public static string SectionLastLogon => T("LAST LOGON", "LETZTE ANMELDUNG");
    public static string SectionOrigin => T("ORIGIN", "HERKUNFT");
    public static string ShowFile => T("Show file", "Datei anzeigen");
    public static string ReturnToWindows => T("Return to Windows", "An Windows zurückgeben");
    public static string ReturnToWindowsTip => T("Puts the entry back into the Windows autostart and leaves it there", "Legt den Eintrag zurück in den Windows-Autostart und lässt ihn dort");
    public static string Remove => T("Remove", "Entfernen");
    public static string RemoveTip => T("Removes the app from StartQueue (Del)", "Entfernt die App aus StartQueue (Entf)");

    // The kind of an entry, a quiet line below its name in the details (no capitals line above the title, design system).
    public static string KindProgram => T("Program", "Programm");
    public static string KindShortcut => T("Shortcut", "Verknüpfung");
    public static string KindStoreApp => T("Store app", "Store-App");
    public static string StoreApp => T("Store app", "Store-App");
    public static string Shortcut => T("Shortcut", "Verknüpfung");
    public static string AddedByYou => T("Added by you on {0:g}.", "Von dir hinzugefügt am {0:g}.");
    public static string TakenOver => T("{0}. Taken over on {1:g}.", "{0}. Übernommen am {1:g}.");
    public static string NewNotice => T(
        "Added itself to the Windows autostart on {0:g}. StartQueue moved it here and starts it at logon. Keep it, turn it off or remove it.",
        "Hat sich am {0:g} selbst in den Windows-Autostart eingetragen. StartQueue hat sie hierher geholt und startet sie bei der Anmeldung. Behalten, ausschalten oder entfernen.");
    public static string LastFailedShort => T("failed", "Fehler");
    public static string NotStartedLastLogon => T("Not started at the last logon.", "Bei der letzten Anmeldung nicht gestartet.");
    public static string FailedAfterLogon => T("Failed {0:0.#} s after logon: {1}", "Fehlgeschlagen {0:0.#} s nach der Anmeldung: {1}");
    public static string StatusAfterLogon => T("{0}, {1:0.#} s after logon.", "{0}, {1:0.#} s nach der Anmeldung.");

    // ---------- Kept in Windows ----------
    public static string KeptHint => T(
        "These entries stay in the Windows autostart on purpose. Windows starts them itself, before StartQueue.",
        "Diese Einträge bleiben bewusst im Windows-Autostart. Windows startet sie selbst, vor StartQueue.");
    public static string MoveIntoStartQueue => T("Move into StartQueue", "In StartQueue übernehmen");
    public static string KeptEmpty => T("Nothing is kept in Windows. Everything starts through StartQueue.", "Nichts ist in Windows belassen. Alles startet über StartQueue.");
    public static string KeptAbsent => T("Not in the Windows autostart at the moment", "Derzeit nicht im Windows-Autostart");

    // ---------- Dialogs ----------
    public static string AddDialogTitle => T("Add a program or shortcut", "Programm oder Verknüpfung hinzufügen");
    public static string AddDialogFilter => T("Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd|All files|*.*", "Programme und Verknüpfungen|*.exe;*.lnk;*.bat;*.cmd|Alle Dateien|*.*");
    public static string ProgramFilter => T("Programs|*.exe;*.bat;*.cmd|All files|*.*", "Programme|*.exe;*.bat;*.cmd|Alle Dateien|*.*");
    public static string RemoveCaption => T("Remove app", "App entfernen");
    public static string RemoveQuestion => T("Remove \"{0}\" from StartQueue? It will no longer start when you sign in.", "„{0}“ aus StartQueue entfernen? Die App startet dann nicht mehr bei der Anmeldung.");
    public static string RemoveComesBack => T(
        "\n\nIf the app registers itself in the Windows autostart again, it comes back as a new entry. To keep it from starting, turning it off is enough.",
        "\n\nTrägt sich die App erneut in den Windows-Autostart ein, kommt sie als neuer Eintrag zurück. Damit sie nicht startet, genügt es, sie auszuschalten.");
    public static string ReturnQuestion => T(
        "Give \"{0}\" back to the Windows autostart?\n\nWindows then starts it itself, outside your order. You find it under Kept in Windows and can move it back any time.",
        "„{0}“ an den Windows-Autostart zurückgeben?\n\nWindows startet die App dann selbst, außerhalb deiner Reihenfolge. Du findest sie unter „In Windows belassen“ und kannst sie jederzeit zurückholen.");
    public static string ReturnFailed => T("Could not return to Windows", "Zurückgeben nicht möglich");

    // ---------- App picker ----------
    public static string PickerWindowTitle => T("Add an app from the Start menu", "App aus dem Startmenü hinzufügen");
    public static string PickerTitle => T("Add an app", "App hinzufügen");
    public static string PickerHint => T("All apps of your Start menu, including Store apps.", "Alle Apps deines Startmenüs, auch Store-Apps.");
    public static string SearchApps => T("Search apps", "Apps suchen");
    public static string Cancel => T("Cancel", "Abbrechen");
    public static string Add => T("Add", "Hinzufügen");
    public static string LoadingApps => T("Loading apps…", "Apps werden geladen …");

    // ---------- Tray and notifications ----------
    public static string TrayScan => T("Scan Windows autostart", "Windows-Autostart prüfen");
    public static string TrayDataFolder => T("Open data folder", "Datenordner öffnen");
    public static string TrayShutDownPrepared => T("Shut down, prepare apps", "Herunterfahren, Apps vorbereiten");
    public static string TrayRestartPrepared => T("Restart, prepare apps", "Neu starten, Apps vorbereiten");

    // ---------- Shutting down with prepared apps ----------
    public static string PreparedShutDownTitle => T("Shut down and prepare the apps?", "Herunterfahren und Apps vorbereiten?");
    public static string PreparedRestartTitle => T("Restart and prepare the apps?", "Neu starten und Apps vorbereiten?");
    public static string PreparedBody => T(
        "At the next start Windows signs you in by itself and locks the screen right away. Your apps start while the lock screen shows, so they are there when you unlock.\n\nThe PC is signed in but locked meanwhile, as after Windows+L. Messengers may show you as online before you sit down.",
        "Beim nächsten Start meldet Windows dich selbst an und sperrt sofort den Bildschirm. Deine Apps starten, während der Sperrbildschirm zu sehen ist, und sind beim Entsperren schon da.\n\nDer PC ist in der Zeit angemeldet, aber gesperrt, wie nach Windows+L. Messenger zeigen dich womöglich als online, bevor du am Rechner sitzt.");
    public static string PreparedNotePossible => T(
        "Windows does this only if \"Use my sign-in info to automatically finish setting up after an update\" is on, under Settings > Accounts > Sign-in options. After the next start StartQueue tells you whether it worked.",
        "Windows macht das nur, wenn „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ an ist, unter Einstellungen > Konten > Anmeldeoptionen. Nach dem nächsten Start sagt dir StartQueue, ob es geklappt hat.");
    public static string PreparedNoteTurnedOff => T(
        "Switched off in Windows: turn on \"Use my sign-in info to automatically finish setting up after an update\" under Settings > Accounts > Sign-in options. Otherwise this is a normal shutdown.",
        "In Windows ausgeschaltet: Schalte „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ ein, unter Einstellungen > Konten > Anmeldeoptionen. Sonst ist das ein normales Herunterfahren.");
    public static string PreparedNotePasswordless => T(
        "Probably not possible right now: this PC only allows Windows Hello sign-in, so Windows has no password to sign you in with. Under Settings > Accounts > Sign-in options, switch off the option that only allows Windows Hello sign-in and turn on \"Use my sign-in info to automatically finish setting up after an update\". Otherwise this is a normal shutdown.",
        "Im Moment wohl nicht möglich: Dieser PC lässt nur die Anmeldung mit Windows Hello zu, Windows hat also kein Kennwort, um dich anzumelden. Schalte unter Einstellungen > Konten > Anmeldeoptionen die Option aus, die nur Windows Hello zulässt, und „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ ein. Sonst ist das ein normales Herunterfahren.");
    public static string PreparedNotePolicy => T(
        "Your organization switched this off for this PC. This is a normal shutdown then.",
        "Deine Organisation hat das für diesen PC ausgeschaltet. Das ist dann ein normales Herunterfahren.");
    public static string PreparedNoteHello => T(
        "Your organization requires Windows Hello on this PC and has switched off sign-in with a password. Windows then cannot sign you in by itself, so this is a normal shutdown.",
        "Deine Organisation verlangt auf diesem PC Windows Hello und hat die Anmeldung mit Kennwort ausgeschaltet. Windows kann dich dann nicht selbst anmelden, das ist also ein normales Herunterfahren.");
    public static string PreparedNoteBitLocker => T(
        "This PC is managed by an organization; Windows signs in by itself there only with BitLocker on, and it is off. This is a normal shutdown then.",
        "Dieser PC wird von einer Organisation verwaltet. Dort meldet Windows nur mit eingeschaltetem BitLocker selbst an, und BitLocker ist aus. Das ist dann ein normales Herunterfahren.");
    public static string PreparedNoteSetUp => T(
        "Not set up yet: under Settings > Startup at sign-in, \"Prepare apps when switching on\" sets it up with one confirmation. Otherwise this is a normal shutdown.",
        "Noch nicht eingerichtet: In den Einstellungen unter „Start bei der Anmeldung“ richtet „Apps beim Einschalten vorbereiten“ das mit einer Bestätigung ein. Sonst ist das ein normales Herunterfahren.");

    // ---------- Setting up prepared shutdowns ----------
    public static string SettingsPrepared => T("Prepare apps when switching on", "Apps beim Einschalten vorbereiten");
    public static string SettingsPreparedHelp => T(
        "For \"Shut down, prepare apps\" in the menu of the StartQueue icon: at the next start Windows signs you in by itself and locks the screen, so your apps start before you sit down.\n\nSetting it up switches on \"Use my sign-in info to automatically finish setting up after an update\" for you. If this PC only allows Windows Hello sign-in, that option is switched off, since Windows needs your password to sign in by itself; Windows Hello keeps working. Can be undone here.",
        "Für „Herunterfahren, Apps vorbereiten“ im Menü am StartQueue-Symbol: Beim nächsten Start meldet Windows dich selbst an und sperrt den Bildschirm, deine Apps starten also, bevor du am Rechner sitzt.\n\nEinrichten schaltet für dich „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ ein. Lässt dieser PC nur Windows Hello zu, wird das ausgeschaltet, denn Windows braucht dein Kennwort, um dich selbst anzumelden. Windows Hello funktioniert weiter. Lässt sich hier rückgängig machen.") + AsksOnce;
    /// <summary>Set up fully the admin helper does it and Windows asks nothing; in Basic it asks once.</summary>
    static string AsksOnce => Core.Elevation.AdminMode ? "" : T(" Windows asks for your confirmation once.", " Windows fragt einmal nach deiner Bestätigung.");
    public static string PreparedSetUpButton => T("Set up", "Einrichten");
    public static string PreparedUndoButton => T("Undo", "Rückgängig");
    public static string PreparedStateReady => T(
        "Set up. Shut down with the power button at the top right or with \"Shut down, prepare apps\" in the menu of the StartQueue icon.",
        "Eingerichtet. Fahr mit dem Ein/Aus-Knopf oben rechts oder über „Herunterfahren, Apps vorbereiten“ im Menü am StartQueue-Symbol herunter.");
    public static string PreparedStateUnknown => T(
        "Not set up by StartQueue; whether Windows signs you in by itself is not clear.",
        "Nicht von StartQueue eingerichtet. Ob Windows dich selbst anmeldet, ist nicht eindeutig.");
    public static string PreparedStateTurnedOff => T(
        "Off: Windows does not sign you in by itself.",
        "Aus: Windows meldet dich nicht selbst an.");
    public static string PreparedStatePasswordless => T(
        "Off: this PC only allows Windows Hello sign-in, so Windows has no password to sign you in with.",
        "Aus: Dieser PC lässt nur Windows Hello zu, Windows hat also kein Kennwort, um dich anzumelden.");
    public static string PreparedStateNeedsAdmin => T(
        "Setting it up needs an administrator of this PC.",
        "Zum Einrichten braucht es einen Administrator dieses PCs.");
    public static string PreparedStatePolicy => T(
        "Not possible: your organization switched it off for this PC.",
        "Nicht möglich: Deine Organisation hat das für diesen PC ausgeschaltet.");
    public static string PreparedStateHelloRequired => T(
        "Not possible: your organization requires Windows Hello and has switched off sign-in with a password.",
        "Nicht möglich: Deine Organisation verlangt Windows Hello und hat die Anmeldung mit Kennwort ausgeschaltet.");
    public static string PreparedStateNeedsBitLocker => T(
        "Not possible: on a PC managed by an organization Windows needs BitLocker for this, and it is off.",
        "Nicht möglich: Auf einem von einer Organisation verwalteten PC braucht Windows dafür BitLocker, und das ist aus.");
    public static string PreparedSetUpTitle => T("Prepare apps when switching on?", "Apps beim Einschalten vorbereiten?");
    public static string PreparedSetUpBody => T(
        "StartQueue switches on \"Use my sign-in info to automatically finish setting up after an update\" for you. Then \"Shut down, prepare apps\" in the menu of the StartQueue icon works.",
        "StartQueue schaltet für dich „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ ein. Dann funktioniert „Herunterfahren, Apps vorbereiten“ im Menü am StartQueue-Symbol.");
    public static string PreparedSetUpNote => T(
        "Windows keeps your sign-in for this, protected by Windows, and uses it after Windows updates too. While the apps are prepared the PC is signed in but locked, as after Windows+L.",
        "Windows hebt deine Anmeldung dafür geschützt auf und nutzt sie auch nach Windows-Updates. Während die Apps vorbereitet werden, ist der PC angemeldet, aber gesperrt, wie nach Windows+L.") + AsksOnce;
    public static string PreparedSetUpNotePasswordless => T(
        "This PC only allows Windows Hello sign-in. Windows needs your password to sign you in by itself, so that option is switched off: the lock screen then offers the password as well, and Windows Hello keeps working. Make sure you know the password of your account. Windows asks for confirmation.",
        "Dieser PC lässt nur Windows Hello zu. Windows braucht dein Kennwort, um dich selbst anzumelden, deshalb wird das ausgeschaltet: Der Sperrbildschirm bietet dann auch das Kennwort an, Windows Hello funktioniert weiter. Du solltest das Kennwort deines Kontos kennen. Windows fragt nach einer Bestätigung.");
    public static string PreparedUndoTitle => T("Undo the setup?", "Einrichtung rückgängig machen?");
    public static string PreparedUndoBody => T(
        "StartQueue puts both Windows settings back as they were. \"Shut down, prepare apps\" is then a normal shutdown.",
        "StartQueue stellt beide Windows-Einstellungen wieder so ein wie vorher. „Herunterfahren, Apps vorbereiten“ ist dann ein normales Herunterfahren.");
    public static string PreparedSetUpFailed => T("Could not set it up", "Einrichten nicht möglich");
    public static string PreparedSetUpFailedText => T(
        "Windows did not allow the change, or its prompt was cancelled. Details are in the log.",
        "Windows hat die Änderung nicht zugelassen, oder seine Abfrage wurde abgebrochen. Einzelheiten stehen im Protokoll.");
    public static string PreparedShutDown => T("Shut down", "Herunterfahren");
    public static string PreparedRestart => T("Restart", "Neu starten");
    public static string PreparedFailed => T("Could not shut down", "Herunterfahren nicht möglich");
    public static string PreparedWorkedTitle => T("Your apps were prepared", "Deine Apps waren vorbereitet");
    public static string PreparedWorkedText => T(
        "Windows signed you in at {0:t} by itself, and your apps started before you unlocked.",
        "Windows hat dich um {0:t} selbst angemeldet, deine Apps sind vor dem Entsperren gestartet.");
    public static string PreparedNotWorkedTitle => T("Your apps were not prepared", "Deine Apps wurden nicht vorbereitet");
    public static string PreparedNotWorkedText => T(
        "Windows did not sign you in by itself this time. ",
        "Windows hat dich diesmal nicht selbst angemeldet. ");
    public static string PreparedNotWorkedPossible => T(
        "Check \"Use my sign-in info to automatically finish setting up after an update\" under Settings > Accounts > Sign-in options.",
        "Prüfe „Meine Anmeldeinformationen verwenden, um die Einrichtung nach einem Update automatisch abzuschließen“ unter Einstellungen > Konten > Anmeldeoptionen.");
    public static string TrayOneNew => T("1 new autostart entry", "1 neuer Autostart-Eintrag");
    public static string TrayNew => T("{0} new autostart entries", "{0} neue Autostart-Einträge");
    // The status line in the tray menu's head and the tooltip.
    public static string TrayNotOn => T("Not switched on yet", "Noch nicht eingeschaltet");
    public static string TrayOn => T("On, {0} apps at sign-in", "Eingeschaltet, {0} Apps bei der Anmeldung");
    public static string TrayOnProfile => T("On, {0} apps in \"{1}\"", "Eingeschaltet, {0} Apps in „{1}“");
    public static string TrayOnOne => T("On, 1 app at sign-in", "Eingeschaltet, 1 App bei der Anmeldung");
    public static string TrayOnProfileOne => T("On, 1 app in \"{0}\"", "Eingeschaltet, 1 App in „{0}“");
    public static string NotifyNewTitle => T("New autostart entry", "Neuer Autostart-Eintrag");
    public static string NotifyNewText => T(
        "\"{0}\" added itself to the Windows autostart. StartQueue took it over and starts it at the end of your list.",
        "„{0}“ hat sich in den Windows-Autostart eingetragen. StartQueue hat die App übernommen und startet sie am Ende deiner Liste.");
    public static string NotifyManyTitle => T("{0} new autostart entries", "{0} neue Autostart-Einträge");
    public static string NotifyManyText => T(
        "{0} added themselves to the Windows autostart. StartQueue took them over.",
        "{0} haben sich in den Windows-Autostart eingetragen. StartQueue hat sie übernommen.");
    public static string ScanFailed => T("Scan failed", "Prüfung fehlgeschlagen");
    public static string CouldNotAdd => T("Could not add", "Hinzufügen nicht möglich");
    public static string FileMissing => T("The file does not exist.", "Die Datei gibt es nicht.");
    public static string AlreadyListedTitle => T("Already in StartQueue", "Schon in StartQueue");
    public static string AlreadyListedText => T("\"{0}\" is already in your list.", "„{0}“ ist schon in deiner Liste.");
    public static string CouldNotStart => T("Could not start {0}", "{0} ließ sich nicht starten");
    public static string CouldNotSave => T("Could not save", "Speichern nicht möglich");

    public static string NotifyKeptTitle => T("New autostart entry left in Windows", "Neuer Autostart-Eintrag in Windows belassen");
    public static string NotifyKeptText => T(
        "{0} added itself to the Windows autostart. It stays there; under Kept in Windows you can move it into StartQueue.",
        "{0} hat sich in den Windows-Autostart eingetragen. Der Eintrag bleibt dort, unter „In Windows belassen“ kannst du ihn übernehmen.");
    public static string NotifySequenceTitle => T("Startup sequence done", "Start abgeschlossen");
    public static string NotifySequenceText => T("{0} apps handled in {1:0} s.", "{0} Apps in {1:0} s abgearbeitet.");
    public static string NotifySequenceFailed => T(" {0} failed: {1}", " {0} fehlgeschlagen: {1}");

    // ---------- Settings ----------
    public static string TabSettings => T("Settings", "Einstellungen");
    public static string AddToStartQueue => T("Add to StartQueue", "Zu StartQueue hinzufügen");
    public static string UninstallLeftText => T("These entries could not go back to the Windows autostart, because that needs admin rights:\n\n{0}\n\nYour list stays in {1}.", "Diese Einträge konnten nicht in den Windows-Autostart zurück, weil das Adminrechte braucht:\n\n{0}\n\nDeine Liste bleibt in {1}.");
    public static string SetUpAdmin => T("Set up fully", "Vollständig einrichten");
    public static string SetUpAdminTip => T("Windows asks once for your confirmation, then StartQueue starts again and asks nothing more.",
        "Windows fragt einmal nach deiner Bestätigung, dann startet StartQueue neu und fragt danach nichts mehr.");
    public static string SetupSignInTask => T("Starts at sign-in through a scheduled task", "Start bei der Anmeldung über eine geplante Aufgabe");
    public static string SetupSignInRun => T("Starts at sign-in through the user's Run entry", "Start bei der Anmeldung über den Run-Eintrag des Benutzers");
    public static string SetupSignInNone => T("No start at sign-in set up, run the setup again", "Kein Start bei der Anmeldung eingerichtet, Setup erneut ausführen");
    public static string SetupMenuModern => T("context menu in the Windows 11 menu", "Kontextmenü im Windows-11-Menü");
    public static string SetupMenuClassic => T("context menu under \"Show more options\"", "Kontextmenü unter „Weitere Optionen anzeigen“");
    public static string SetupMenuNone => T("no context menu", "kein Kontextmenü");
    public static string SettingsAppearance => T("General", "Allgemein");
    public static string SettingsAppearanceHelp => T("How this window looks and speaks.", "Wie dieses Fenster aussieht und spricht.");
    public static string SettingsTheme => T("Color mode of this window", "Farbmodus dieses Fensters");
    public static string SettingsThemeHelp => T("Only this window; Windows itself you set under Color mode", "Nur dieses Fenster, Windows selbst stellst du unter Farbmodus ein");
    public static string SettingsNewModeRow => T("When an app adds itself", "Wenn sich eine App einträgt");
    public static string SettingsScanShort => T("Also finds new startup tasks of Store apps", "Findet auch neue Startaufgaben von Store-Apps");
    public static string ThemeSystem => T("Like Windows", "Wie Windows");
    public static string ThemeLight => T("Light", "Hell");
    public static string ThemeDark => T("Dark", "Dunkel");
    public static string SettingsLanguage => T("Language", "Sprache");
    public static string LanguageSystem => T("Like Windows", "Wie Windows");
    public static string LanguageHelp => T("Also applies to the \"Add to StartQueue\" entry in the Explorer context menu", "Gilt auch für den Eintrag „Zu StartQueue hinzufügen“ im Kontextmenü des Explorers");
    public static string SettingsNewEntries => T("New autostart entries", "Neue Autostart-Einträge");
    public static string SettingsNewMode => T("When an app adds itself to the Windows autostart", "Wenn sich eine App in den Windows-Autostart einträgt");
    public static string NewModeEnabled => T("Take it over and start it", "Übernehmen und starten");
    public static string NewModeDisabled => T("Take it over, but switched off", "Übernehmen, aber ausgeschaltet");
    public static string NewModeNotify => T("Only tell me and leave it in Windows", "Nur melden und in Windows belassen");
    public static string SettingsNotifyNew => T("Show a notification", "Benachrichtigung anzeigen");
    public static string SettingsNotifyNewHelp => T("The red dot on the tray icon and the NEW mark appear either way.", "Der rote Punkt am Tray-Symbol und die Markierung NEU erscheinen in jedem Fall.");
    public static string SettingsStoreApps => T("Manage Store apps too", "Store-Apps mitverwalten");
    public static string SettingsStoreAppsHelp => T("Off: the startup tasks of Store apps stay with Windows", "Aus: Die Startaufgaben von Store-Apps bleiben bei Windows");
    public static string SettingsSequence => T("Startup at sign-in", "Start bei der Anmeldung");
    public static string SettingsSequenceHelp => T("Applies to the whole list. Each app also has its own delay.", "Gilt für die ganze Liste. Jede App hat zusätzlich eine eigene Verzögerung.");
    public static string SettingsStartDelay => T("Wait before the first app", "Wartezeit vor der ersten App");
    public static string SettingsStartDelayHelp => T("Gives Windows time to finish signing you in", "Gibt Windows Zeit, die Anmeldung abzuschließen");
    public static string SettingsGap => T("Gap between apps", "Abstand zwischen den Apps");
    public static string SettingsGapHelp => T("Added to each app's own delay", "Kommt zur eigenen Verzögerung jeder App hinzu");
    public static string SettingsWaitLimit => T("Longest wait for \"Wait until it runs\"", "Höchstens warten bei „Warten, bis sie läuft“");
    public static string SettingsWaitLimitHelp => T("After that the next app starts, even if this one is not running yet", "Danach startet die nächste App, auch wenn diese noch nicht läuft");
    public static string Seconds => T("seconds", "Sekunden");
    public static string SettingsNotifySequence => T("Show a summary when done", "Zusammenfassung nach dem Start anzeigen");
    public static string SettingsNotifySequenceHelp => T("A notification once every app has been started", "Eine Benachrichtigung, sobald alle Apps gestartet sind");
    public static string SettingsScanInterval => T("Extra full scan", "Zusätzliche vollständige Prüfung");
    public static string SettingsScanHelp => T("Changes to Run keys and startup folders are noticed right away anyway. The full scan also finds new Store app startup tasks.",
        "Änderungen an Run-Schlüsseln und Autostart-Ordnern bemerkt StartQueue ohnehin sofort. Die vollständige Prüfung findet zusätzlich neue Startaufgaben von Store-Apps.");
    public static string EveryMinutes => T("every {0} minutes", "alle {0} Minuten");
    public static string Never => T("never", "nie");
    public static string LegalTitle => T("Privacy and notices", "Datenschutz und Hinweise");

    /// <summary>The sections of "Privacy and notices": what is read and stored, how StartQueue was made, license, liability.</summary>
    public static (string Title, string Text)[] LegalSections(string dataFolder) =>
    [
        (T("Privacy", "Datenschutz"), F(T(
            "StartQueue works on this device. It collects no usage data, needs no account and passes nothing on to the publisher or anyone else. Its only connection is the update check, when it is on: once a day it asks GitHub for the latest version and sends nothing about you or your PC.\n\n" +
            "It reads what the job needs: the autostart entries of Windows (registry, startup folders, Task Scheduler, Store apps), the installed apps, the running processes and, for the suggestions in \"Add app\", how often Windows counted an app being started. That count is read when the dialog opens and not stored.\n\n" +
            "Stored are your list, the settings, the last start, a log and the last five backups of the settings, in the folder {0}. They contain app names and program paths, which can include your user name. When the profiles apply is kept in the EsteemWorks folder next to it, shared with the other EsteemWorks apps, without app names or paths. The folder stays when StartQueue is uninstalled, so a new installation finds your list again, and can be deleted at any time.",
            "StartQueue arbeitet auf diesem Gerät. Es sammelt keine Nutzungsdaten, braucht kein Konto und gibt nichts an den Herausgeber oder an Dritte weiter. Die einzige Verbindung ist die Update-Suche, wenn sie an ist: Einmal am Tag fragt sie GitHub nach der neuesten Version und sendet nichts über dich oder deinen PC.\n\n" +
            "Gelesen wird, was die Aufgabe braucht: die Autostart-Einträge von Windows (Registrierung, Autostart-Ordner, Aufgabenplanung, Store-Apps), die installierten Apps, die laufenden Prozesse und für die Vorschläge in „App hinzufügen“, wie oft Windows den Start einer App gezählt hat. Diese Zählung wird beim Öffnen gelesen und nicht gespeichert.\n\n" +
            "Gespeichert werden deine Liste, die Einstellungen, der letzte Start, ein Protokoll und die letzten fünf Sicherungen der Einstellungen, im Ordner {0}. Darin stehen App-Namen und Programmpfade, die deinen Benutzernamen enthalten können. Wann die Profile gelten, steht im Ordner EsteemWorks daneben, gemeinsam mit den anderen EsteemWorks-Apps, ohne App-Namen und Pfade. Der Ordner bleibt bei der Deinstallation erhalten, damit eine neue Installation deine Liste wiederfindet, und lässt sich jederzeit löschen."), dataFolder)),
        (T("Made with the help of AI", "Mit Hilfe von KI entwickelt"), T(
            "Large parts of the code, texts and graphics of StartQueue were created with Claude by Anthropic, directed by the publisher, who tests the app on his own devices. The app itself contains no AI, makes no automated decisions about people and sends no data to AI services.",
            "Große Teile von Code, Texten und Grafiken von StartQueue sind mit Claude von Anthropic entstanden, unter Anleitung des Herausgebers, der die App auf seinen eigenen Geräten testet. Die App selbst enthält keine KI, trifft keine automatisierten Entscheidungen über Personen und sendet keine Daten an KI-Dienste.")),
        (T("What StartQueue changes in Windows", "Was StartQueue an Windows ändert"), T(
            "StartQueue switches off the Windows autostart entries it takes over and starts those apps itself. \"Give back\" restores an entry, uninstalling restores all of them. Depending on your choices it also sets up tasks in Task Scheduler (start at sign-in, admin features), an entry in the Explorer context menu and the Windows startup delay.",
            "StartQueue schaltet die Autostart-Einträge von Windows ab, die es übernimmt, und startet diese Apps selbst. „Zurückgeben“ stellt einen Eintrag wieder her, die Deinstallation stellt alle wieder her. Je nach Auswahl richtet es außerdem Aufgaben in der Aufgabenplanung ein (Start bei der Anmeldung, Adminfunktionen), einen Eintrag im Kontextmenü des Explorers und die Windows-Startverzögerung.")),
        (T("Publisher and license", "Herausgeber und Lizenz"), T(
            "© 2026 Leon Tüpker. StartQueue is a private project, free of charge and without advertising. All rights reserved.",
            "© 2026 Leon Tüpker. StartQueue ist ein privates Projekt, kostenlos und ohne Werbung. Alle Rechte vorbehalten.")),
        (T("Liability", "Haftung"), T(
            "StartQueue is provided as is, without warranty. Before larger changes to the autostart, a Windows restore point is a good idea. As far as the law allows, the publisher is not liable for damage arising from its use; liability for intent, gross negligence and injury to life, body or health remains unaffected.",
            "StartQueue wird ohne Gewähr bereitgestellt. Vor größeren Änderungen am Autostart ist ein Wiederherstellungspunkt von Windows sinnvoll. Soweit gesetzlich zulässig, haftet der Herausgeber nicht für Schäden aus der Nutzung; die Haftung für Vorsatz, grobe Fahrlässigkeit und die Verletzung von Leben, Körper oder Gesundheit bleibt unberührt.")),
        (T("Third-party components", "Komponenten von Dritten"), T(
            "StartQueue contains the .NET runtime and Windows Presentation Foundation by Microsoft under the MIT license, Copyright (c) .NET Foundation and Contributors. The full license texts are in THIRD-PARTY-NOTICES.txt in the installation folder. The setup was built with Inno Setup by Jordan Russell and Martijn Laan.",
            "StartQueue enthält die .NET-Laufzeit und Windows Presentation Foundation von Microsoft unter der MIT-Lizenz, Copyright (c) .NET Foundation and Contributors. Die vollständigen Lizenztexte stehen in THIRD-PARTY-NOTICES.txt im Installationsordner. Der Installer wurde mit Inno Setup von Jordan Russell und Martijn Laan erstellt.")),
    ];

    // ---------- Window chrome and navigation ----------
    public static string Close => T("Close", "Schließen");

    // ---------- Services ----------
    public static string TabServices => T("Services", "Dienste");
    public static string ServicesSearch => T("Search services", "Dienste suchen");
    public static string ServicesHintShort => T("Only managed here, Windows starts them", "Nur verwaltet, Windows startet sie");
    public static string ServicesHint => T(
        "The same settings as in services.msc. StartQueue takes no service over: Windows keeps starting them.",
        "Dieselben Einstellungen wie in services.msc. StartQueue übernimmt keinen Dienst: Windows startet sie weiterhin.") + (Core.Elevation.AdminMode
        ? T(" Set up fully, a change asks nothing more.", " Vollständig eingerichtet, fragt eine Änderung nichts mehr.")
        : T(" Every change needs administrator rights and asks through Windows (UAC), just like services.msc.",
            " Jede Änderung braucht Administratorrechte und fragt über Windows nach (UAC), genau wie services.msc."));
    public static string ServicesRefresh => T("Refresh", "Aktualisieren");
    public static string ServicesSort => T("Sort", "Sortieren");
    public static string ServicesSortName => T("By name", "Nach Name");
    public static string ServicesSortStart => T("By startup type", "Nach Starttyp");
    public static string ServicesSortState => T("By state", "Nach Zustand");
    public static string ServicesFilter => T("Filter", "Filter");
    public static string ServicesFilterCount => T("Filter ({0})", "Filter ({0})");
    public static string ServicesFilterTip => T("Show only some startup types or states. Several can be ticked; none ticked in a group shows all of it.",
        "Nur bestimmte Starttypen oder Zustände zeigen. Mehrere lassen sich ankreuzen; ist in einer Gruppe nichts angekreuzt, zeigt sie alles.");
    public static string ServicesFilterState => T("State", "Zustand");
    public static string ServicesFilterReset => T("Show all", "Alle zeigen");
    public static string ServicesRefreshTip => T("Read state and start type of all services again.", "Zustand und Starttyp aller Dienste neu lesen.");
    public static string ServicesLoading => T("Reading services …", "Dienste werden gelesen …");
    public static string ServicesAppsSection => T("SERVICES OF APPS", "DIENSTE VON APPS");
    public static string ServicesAppsSectionTip => T("Installed with programs, drivers or tools. Usually safe to set to manual if you do not need them at every start.",
        "Mit Programmen, Treibern oder Werkzeugen installiert. Meist gefahrlos auf Manuell zu stellen, wenn du sie nicht bei jedem Start brauchst.");
    public static string ServicesWindowsSection => T("WINDOWS SERVICES", "WINDOWS-DIENSTE");
    public static string ServicesWindowsSectionTip => T("Part of Windows: a program by Microsoft in the Windows folder. Changes here can affect sign-in, network, sound, updates or security.",
        "Teil von Windows: ein Programm von Microsoft im Windows-Ordner. Änderungen hier können Anmeldung, Netzwerk, Ton, Updates oder Sicherheit beeinträchtigen.");
    public static string ServicesShow => T("Show", "Einblenden");
    public static string ServicesHide => T("Hide", "Ausblenden");
    public static string ServiceTipName => T("Service name: {0}", "Dienstname: {0}");
    public static string ServiceTipProgram => T("Program: {0}", "Programm: {0}");
    public static string ServiceTipAccount => T("Runs as: {0}", "Läuft als: {0}");
    public static string ServiceTipCompany => T("Publisher: {0}", "Herausgeber: {0}");
    public static string ServiceRunning => T("Running", "Wird ausgeführt");
    public static string ServiceStopped => T("Stopped", "Beendet");
    public static string ServicePaused => T("Paused", "Angehalten");
    public static string ServicePending => T("Changing …", "Wechselt …");
    public static string ServiceWindowsChip => T("Windows", "Windows");
    public static string ServiceWindowsTip => T("Part of Windows. Before a change StartQueue says what can happen.", "Teil von Windows. Vor einer Änderung sagt StartQueue, was passieren kann.");
    public static string ServiceEssentialChip => T("Essential", "Wichtig");
    public static string ServiceEssentialTip => T("Windows does not run properly without this service.", "Ohne diesen Dienst läuft Windows nicht richtig.");
    public static string StartDelayed => T("Automatic (delayed start)", "Automatisch (verzögert)");
    public static string StartAutomatic => T("Automatic", "Automatisch");
    public static string StartManual => T("Manual", "Manuell");
    public static string StartDisabled => T("Disabled", "Deaktiviert");
    public static string ServiceStartType => T("Startup type", "Starttyp");
    public static string ServiceStartTypeTip => T(
        "Automatic: starts with Windows. Delayed: shortly after sign-in, so the desktop is ready sooner. Manual: when a program or Windows needs it. Disabled: never.",
        "Automatisch: startet mit Windows. Verzögert: kurz nach der Anmeldung, damit der Desktop schneller bereit ist. Manuell: wenn ein Programm oder Windows ihn braucht. Deaktiviert: nie.");
    public static string ServiceTriggerTip => T("Windows also starts this service on an event, for example when a device is connected (\"Trigger Start\" in services.msc).",
        "Windows startet diesen Dienst zusätzlich bei einem Ereignis, zum Beispiel wenn ein Gerät angeschlossen wird („Start durch Auslösen“ in services.msc).");
    public static string ServiceStart => T("Start", "Starten");
    public static string ServiceStop => T("Stop", "Beenden");
    public static string ServiceRestart => T("Restart", "Neu starten");
    public static string ServiceDoStop => T("stop it", "ihn beenden");
    public static string ServiceDoRestart => T("restart it", "ihn neu starten");
    public static string ServiceDoStartType => T("set its startup type to \"{0}\"", "seinen Starttyp auf „{0}“ stellen");
    public static string ServiceConfirmTitle => T("Change \"{0}\"?", "„{0}“ ändern?");
    public static string ServiceConfirmBody => T("You are about to {1}.", "Du willst {1}.");
    public static string ServiceWindowsTitle => T("Change Windows service \"{0}\"?", "Windows-Dienst „{0}“ ändern?");
    public static string ServiceWindowsBody => T(
        "\"{0}\" is part of Windows and you are about to {1}. Features that rely on it may stop working, for example network, sound, printing, updates or apps from the Store. If something goes wrong, set the previous startup type again here or in services.msc.",
        "„{0}“ ist Teil von Windows und du willst {1}. Funktionen, die darauf aufbauen, können ausfallen, zum Beispiel Netzwerk, Ton, Drucken, Updates oder Apps aus dem Store. Geht etwas schief, stell den vorherigen Starttyp hier oder in services.msc wieder ein.");
    public static string ServiceEssentialBody => T(
        "\"{0}\" is essential for Windows and you are about to {1}. Windows may stop responding, sign-in may fail or security protection may switch off. In the worst case only safe mode or a restore point helps. Only go on if you know exactly why.",
        "„{0}“ ist für Windows wichtig und du willst {1}. Windows kann hängen bleiben, die Anmeldung fehlschlagen oder der Schutz vor Schadsoftware ausfallen. Im schlimmsten Fall hilft nur noch der abgesicherte Modus oder ein Wiederherstellungspunkt. Mach nur weiter, wenn du genau weißt, warum.");
    public static string ServiceDependents => T("These running services stop along with it: {0}", "Diese laufenden Dienste werden mit beendet: {0}");
    public static string ServiceChangeAnyway => T("Change", "Ändern");
    public static string ServiceFailedTitle => T("\"{0}\" could not be changed", "„{0}“ ließ sich nicht ändern");
    public static string AdminHelperNoAnswer => T("StartQueue's admin helper did not answer. Nothing was changed; try again in a moment.",
        "Der Admin-Helfer von StartQueue hat nicht geantwortet. Es wurde nichts geändert; versuch es gleich noch einmal.");
    public static string ServiceCancelled => T("The Windows prompt was cancelled. Nothing was changed.", "Die Windows-Abfrage wurde abgebrochen. Es wurde nichts geändert.");
    public static string ServiceTimeout => T("The service did not respond within 30 seconds.", "Der Dienst hat nicht innerhalb von 30 Sekunden reagiert.");
    public static string TabWindows => T("Windows autostart", "Windows-Autostart");
    public static string AllSeen => T("Mark all seen", "Alle gesehen");
    public static string StartAllNow => T("Start all now", "Jetzt alle starten");
    public static string MoreActions => T("More actions", "Weitere Aktionen");

    // ---------- Row menu ----------
    public static string MenuStart => T("Start now", "Jetzt starten");
    public static string MenuTurnOff => T("Turn off", "Ausschalten");
    public static string MenuTurnOn => T("Turn on", "Einschalten");
    public static string MenuUp => T("Move up", "Nach oben");
    public static string MenuDown => T("Move down", "Nach unten");
    public static string MenuTop => T("Move to top", "An den Anfang");
    public static string MenuEnd => T("Move to end", "Ans Ende");
    public static string KeyEnter => T("Enter", "Enter");
    public static string KeySpace => T("Space", "Leertaste");
    public static string KeyAltUp => T("Alt+Up", "Alt+↑");
    public static string KeyAltDown => T("Alt+Down", "Alt+↓");
    public static string KeyDel => T("Del", "Entf");

    // ---------- Details ----------
    public static string DelayShort => T("Delay", "Verzögerung");
    public static string SecondsShort => T("s", "s");
    public static string WaitHelpShort => T("The next app starts only after it.", "Die nächste App startet erst danach.");
    public static string RunElevatedHelpAdmin => T("Without a UAC prompt, through the StartQueue task.", "Ohne UAC-Abfrage, über die StartQueue-Aufgabe.");
    public static string RunElevatedHelpUser => T("Only available when StartQueue is set up with admin rights.", "Nur verfügbar, wenn StartQueue mit Adminrechten eingerichtet ist.");
    public static string EditDetails => T("Edit", "Bearbeiten");
    public static string DoneEditing => T("Done", "Fertig");

    // ---------- In-app dialogs ----------
    public static string RemoveTitle => T("Remove \"{0}\"?", "„{0}“ entfernen?");
    public static string RemoveBody => T("{0} will no longer start when you sign in.", "{0} startet dann nicht mehr bei der Anmeldung.");
    public static string RemoveNote => T("If the app adds itself to the Windows autostart again, it shows up as a new entry. To keep it from starting, turning it off is enough.",
        "Trägt sich die App wieder in den Windows-Autostart ein, erscheint sie erneut als neuer Eintrag. Soll sie nur nicht starten, genügt Ausschalten.");
    public static string ReturnTitle => T("Give \"{0}\" back to Windows?", "„{0}“ an Windows zurückgeben?");
    public static string ReturnBody => T("Windows then starts it itself, outside your order.", "Windows startet die App dann selbst, außerhalb deiner Reihenfolge.");
    public static string ReturnNote => T("You find it under Windows autostart and can take it back any time.", "Du findest sie unter „Windows-Autostart“ und kannst sie jederzeit zurückholen.");
    public static string ReturnConfirm => T("Give back", "Zurückgeben");
    public static string Ok => T("OK", "OK");
    public static string RemovedText => T("\"{0}\" removed.", "„{0}“ entfernt.");
    public static string Undo => T("Undo", "Rückgängig");

    // ---------- Windows autostart view ----------
    public static string WindowsHint => T("As of the last scan, {0:t}", "Stand der letzten Prüfung: {0:t}");
    public static string WindowsHintTip => T("What Windows still starts by itself at sign-in.", "Was Windows bei der Anmeldung noch selbst startet.");
    public static string OrderLabel => T("ORDER OF THE APPS", "REIHENFOLGE DER APPS");
    public static string TakeOverAll => T("Take over all", "Alle übernehmen");
    public static string TakeOverAllEntriesTip => T("Takes over every entry of this section that can be taken over.", "Übernimmt alle Einträge dieses Abschnitts, die sich übernehmen lassen.");
    public static string TakeOverAllTasksTip => T("Takes over every scheduled task of this section that can be taken over.", "Übernimmt alle geplanten Aufgaben dieses Abschnitts, die sich übernehmen lassen.");
    public static string TakeOver => T("Take over", "Übernehmen");
    public static string NeedsAdmin => T("Needs admin rights", "Adminrechte nötig");
    public static string SectionStartsItself => T("STARTED BY WINDOWS", "STARTET WINDOWS SELBST");
    public static string SectionLogonTasks => T("SCHEDULED TASKS AT SIGN-IN", "GEPLANTE AUFGABEN BEI DER ANMELDUNG");
    public static string InfoOnly => T("FOR INFORMATION ONLY", "NUR ZUR INFORMATION");
    public static string TasksHint => T("StartQueue does not change scheduled tasks. Many of them belong to Windows itself.", "StartQueue verändert geplante Aufgaben nicht. Viele davon gehören zu Windows selbst.");
    public static string StatusKept => T("kept on purpose", "bewusst belassen");
    public static string StatusStore => T("not managed", "nicht verwaltet");
    public static string StatusMachine => T("machine-wide", "systemweit");
    public static string StatusLocked => T("admin rights only", "nur mit Adminrechten");
    public static string StatusPending => T("being taken over", "wird übernommen");
    public static string WindowsEmpty => T("Windows starts nothing by itself anymore. Everything runs through StartQueue.", "Windows startet nichts mehr selbst. Alles läuft über StartQueue.");
    public static string NoLogonTasks => T("No scheduled tasks start at sign-in.", "Keine geplanten Aufgaben starten bei der Anmeldung.");
    public static string Scanning => T("Reading the Windows autostart…", "Windows-Autostart wird gelesen …");

    // ---------- Settings page ----------
    public static string SetupFullAdds => T("Fully set up, StartQueue also manages machine-wide entries and starts apps as administrator",
        "Vollständig verwaltet StartQueue auch systemweite Einträge und startet Apps als Administrator");
    public static string SetupFullGives => T("machine-wide entries and starts as administrator too; Windows asks nothing more",
        "auch systemweite Einträge und Starts als Administrator; Windows fragt nichts mehr");
    public static string ModeAdminTitle => T("Set up fully, with admin rights", "Vollständig eingerichtet, mit Adminrechten");
    public static string ModeAdminText => T("StartQueue also manages machine-wide entries and starts apps as administrator when you want. Windows asks nothing more.",
        "StartQueue verwaltet auch systemweite Einträge und startet Apps auf Wunsch als Administrator. Windows fragt nichts mehr.");
    public static string ModeUserTitle => T("Set up as Basic, without admin rights", "Als Basis eingerichtet, ohne Adminrechte");
    public static string ModeUserText => T("StartQueue fully manages your own entries. Machine-wide entries stay with Windows, starting as administrator is not available.",
        "Eigene Einträge verwaltet StartQueue vollständig. Systemweite Einträge bleiben bei Windows, Starts als Administrator sind nicht verfügbar.");
    public static string ModeEnabledHelp => T("The app goes to the end of the apps that are on", "Die App kommt ans Ende der eingeschalteten Apps");
    public static string ModeDisabledHelp => T("It is in the list but starts only once you turn it on", "Sie steht in der Liste, startet aber erst, wenn du sie einschaltest");
    public static string ModeNotifyHelp => T("Windows keeps starting it; you take it over under Windows autostart", "Windows startet sie weiter selbst; übernehmen kannst du sie unter „Windows-Autostart“");
    public static string NotifyHelpShort => T("The red dot on the tray icon and NEW always appear", "Roter Punkt am Tray-Symbol und NEU erscheinen immer");
    public static string Minutes => T("{0} min", "{0} min");

    // ---------- Add app dialog ----------
    public static string SearchPlaceholder => T("Search apps", "Apps durchsuchen");
    public static string SectionSuggestions => T("SUGGESTIONS", "VORSCHLÄGE");
    public static string SuggestionsHint => T("not in StartQueue yet", "noch nicht in StartQueue");
    public static string SectionAllApps => T("ALL APPS IN THE START MENU", "ALLE APPS IM STARTMENÜ");
    public static string ReasonRunning => T("running now", "läuft gerade");
    public static string ReasonOften => T("used often", "oft genutzt");
    public static string ReasonCanStart => T("can start itself", "kann selbst starten");
    public static string ReasonRecent => T("installed recently", "zuletzt installiert");
    public static string Added => T("Added", "Hinzugefügt");
    public static string AlreadyListed => T("already in StartQueue", "schon in StartQueue");
    public static string BrowseFile => T("Choose a program or shortcut…", "Programm oder Verknüpfung auswählen …");
    public static string NoSuggestions => T("No suggestions right now.", "Gerade keine Vorschläge.");

    // ---------- Launcher and origins ----------
    public static string WasRunning => T("was already running", "lief schon");
    public static string Started => T("started", "gestartet");
    public static string StartedAsAdmin => T("started as administrator", "als Administrator gestartet");
    public static string NotDetected => T(", not detected within {0} s", ", nach {0} s nicht erkannt");
    public static string AdminTaskMissing => T("The StartQueue admin task is missing. Run install.ps1 again.", "Die StartQueue-Adminaufgabe fehlt. install.ps1 erneut ausführen.");
    public static string OriginRun => T("Registry Run key ({0}), value \"{1}\"", "Run-Schlüssel der Registry ({0}), Wert „{1}“");
    public static string OriginFolderAll => T("All users startup folder, \"{0}\"", "Autostart-Ordner aller Benutzer, „{0}“");
    public static string OriginFolderUser => T("Your startup folder, \"{0}\"", "Dein Autostart-Ordner, „{0}“");
    public static string OriginTask => T("Store app startup task \"{0}\"", "Startaufgabe der Store-App „{0}“");
    public static string OriginScheduledTask => T("Scheduled task \"{0}\", its sign-in start switched off", "Geplante Aufgabe „{0}“, Anmeldestart abgeschaltet");

    // ---------- Why something stays in Windows ----------
    public static string KeepWindows => T("part of Windows", "Teil von Windows");
    public static string KeepWindowsTip => T("Belongs to Windows itself. Windows starts it early, before your apps; moving it would bring nothing.",
        "Gehört zu Windows selbst. Windows startet es früh, vor deinen Apps; verschieben brächte nichts.");
    public static string KeepDriver => T("part of a driver", "Teil eines Treibers");
    public static string KeepDriverTip => T("Belongs to a hardware driver and should run before the apps that use the device.",
        "Gehört zu einem Gerätetreiber und sollte laufen, bevor Apps das Gerät nutzen.");
    public static string KeepReturned => T("given back by you", "von dir zurückgegeben");
    public static string KeepReturnedTip => T("You gave it back to Windows on {0:g}.", "Du hast es am {0:g} an Windows zurückgegeben.");
    public static string KeepLeft => T("left with Windows by you", "von dir bei Windows gelassen");
    public static string KeepLeftTip => T("You left it with Windows on {0:g}, before switching StartQueue on.",
        "Du hast es am {0:g} bei Windows gelassen, bevor du StartQueue eingeschaltet hast.");
    public static string KeepNotify => T("only reported", "nur gemeldet");
    public static string KeepNotifyTip => T("Added itself on {0:g}. Your setting \"Only tell me\" leaves new entries in Windows.",
        "Hat sich am {0:g} eingetragen. Deine Einstellung „Nur melden“ lässt neue Einträge in Windows.");
    public static string KeepUnknown => T("exception", "Ausnahme");
    public static string KeepUnknownTip => T("On StartQueue's exception list.", "Steht auf der Ausnahmeliste von StartQueue.");

    // ---------- Scheduled tasks ----------
    public static string KindTask => T("Scheduled task", "Geplante Aufgabe");
    public static string TaskSubtitle => T("Scheduled task {0}", "Geplante Aufgabe {0}");
    public static string TasksHintTakeOver => T("These start at sign-in too. Taking one over switches off only its sign-in start; StartQueue then runs the task at its place in your order, with all its settings.",
        "Starten ebenfalls bei der Anmeldung. Übernehmen schaltet nur ihren Anmeldestart ab; StartQueue führt die Aufgabe dann an ihrer Stelle in deiner Reihenfolge aus, mit allen Einstellungen.");
    public static string TaskOnlyLogon => T("sign-in only", "nur Anmeldung");
    public static string TaskOtherTriggers => T("also {0}", "auch {0}");
    public static string TaskOtherTriggersTip => T("The task also starts {0}. That stays with Windows; StartQueue only takes over the start at sign-in.",
        "Die Aufgabe startet auch {0}. Das bleibt bei Windows; StartQueue übernimmt nur den Start bei der Anmeldung.");
    public static string TaskRepeats => T("every {0}", "alle {0}");
    public static string TaskRepeatsTip => T("After sign-in the task repeats every {0}. StartQueue takes that over: it starts the task at its place in your order and then every {0}.",
        "Nach der Anmeldung wiederholt sich die Aufgabe alle {0}. StartQueue übernimmt das: Es startet die Aufgabe an ihrer Stelle in deiner Reihenfolge und danach alle {0}.");
    // ---------- Windows startup delay ----------
    public static string WindowsDelayOff => T("Switch off Windows' startup delay", "Windows-Startverzögerung abschalten");
    public static string WindowsDelayHelp => T("For what Windows still starts by itself, and for StartQueue where it starts through the Run key",
        "Für alles, was Windows noch selbst startet, und für StartQueue, wo es über den Run-Eintrag startet");
    public static string WindowsDelayOffTitle => T("Switch off Windows' startup delay?", "Windows-Startverzögerung abschalten?");
    public static string WindowsDelayOffBody => T("After sign-in Windows waits a few seconds before its autostart and then starts the entries one by one once the system is idle. Switched off, they start right away.",
        "Nach der Anmeldung wartet Windows einige Sekunden mit seinem Autostart und startet die Einträge dann nacheinander, sobald das System ruhig ist. Abgeschaltet starten sie sofort.");
    public static string WindowsDelayOffNote => T("Applies to your user only and needs no admin rights. The two registry values are not officially documented. In the first seconds after sign-in the desktop may respond a little slower. Takes effect at the next sign-in; switch it on again here at any time.",
        "Gilt nur für deinen Benutzer und braucht keine Adminrechte. Die zwei Registry-Werte sind nicht offiziell dokumentiert. In den ersten Sekunden nach der Anmeldung kann der Desktop etwas zäher reagieren. Wirkt ab der nächsten Anmeldung und lässt sich hier jederzeit wieder einschalten.");
    public static string WindowsDelayOffConfirm => T("Switch off", "Abschalten");
    public static string WindowsDelayOnTitle => T("Use Windows' startup delay again?", "Windows-Startverzögerung wieder verwenden?");
    public static string WindowsDelayOnBody => T("StartQueue removes its two values, and Windows delays its autostart as it does by default.",
        "StartQueue entfernt seine zwei Werte, und Windows verzögert seinen Autostart wieder wie vorgesehen.");
    public static string WindowsDelayOnConfirm => T("Switch on", "Wieder einschalten");

    // ---------- Profiles ----------
    public static string ProfileBarLabel => T("PROFILE", "PROFIL");
    public static string ProfileBarTip => T("From left to right: at sign-in the first profile applies that matches and is not paused, otherwise none. The one selected here is whose switches the list shows and changes. Drag a profile to change the order.",
        "Von links nach rechts: Bei der Anmeldung gilt das erste Profil, das zutrifft und nicht pausiert ist, sonst keins. Das hier gewählte ist das, dessen Schalter die Liste zeigt und ändert. Zum Umsortieren ein Profil ziehen.");
    public static string ProfileAddShort => T("Profile", "Profil");
    public static string ProfilesIntro => T("One set of apps for every sign-in. With profiles a different set starts depending on the time, for example on workdays during working hours.",
        "Ein Satz Apps für jede Anmeldung. Mit Profilen startet je nach Zeit ein anderer, zum Beispiel an Werktagen während der Arbeitszeit.");
    public static string ProfileInProfile => T("in profile \"{0}\"", "im Profil „{0}“");
    public static string ProfileEarlier => T("Higher precedence (to the left)", "Höherer Vorrang (nach links)");
    public static string ProfileLater => T("Lower precedence (to the right)", "Niedrigerer Vorrang (nach rechts)");
    public static string ProfileDeleteTitle => T("Delete profile \"{0}\"?", "Profil „{0}“ löschen?");
    public static string ProfileDeleteBody => T("The apps stay in the list; only this profile and its switches go.", "Die Apps bleiben in der Liste, nur dieses Profil und seine Schalter fallen weg.");
    public static string ProfileStandard => T("No profile", "Ohne Profil");
    public static string ProfileStandardHelp => T("When no profile applies: every app with its own switch.", "Wenn kein Profil zutrifft: jede App mit ihrem eigenen Schalter.");
    public static string ProfileActiveNow => T("applies now", "gilt jetzt");
    public static string ProfilePausedUntil => T("paused up to and including {0:d}", "pausiert bis einschließlich {0:d}");
    public static string ProfileRange => T("{0} to {1}", "{0} bis {1}");
    public static string ProfileAlways => T("always", "immer");
    public static string ProfileAdd => T("Add profile", "Profil hinzufügen");
    public static string ProfileEdit => T("Edit", "Bearbeiten");
    public static string ProfileNewTitle => T("New profile", "Neues Profil");
    public static string ProfileEditTitle => T("Edit profile", "Profil bearbeiten");
    public static string ProfileName => T("Name", "Name");
    public static string ProfileColor => T("Color", "Farbe");
    public static string ProfileColorHelp => T("Marks the profile in the profile bar. With the accent color \"Profile\" in the settings, the whole window takes it on.", "Kennzeichnet das Profil in der Profilleiste. Mit der Akzentfarbe „Profil“ in den Einstellungen nimmt das ganze Fenster sie an.");
    public static string ProfileDays => T("Days", "Tage");
    public static string ProfileDaysHelp => T("None selected: every day.", "Keiner gewählt: jeden Tag.");
    public static string ProfileTimeLabel => T("Time", "Uhrzeit");
    public static string ProfileTimeTo => T("to", "bis");
    public static string ProfileTimeHelp => T("From when to when the profile applies on its days. Across midnight works too, for example 22:00 to 06:00. \"All day\": from 00:00 to 24:00.", "Von wann bis wann das Profil an seinen Tagen gilt. Über Mitternacht geht auch, zum Beispiel 22:00 bis 06:00. „Ganztägig“: von 00:00 bis 24:00.");
    public static string ProfileAllDay => T("All day", "Ganztägig");
    public static string ProfileAllDayShort => T("all day", "ganztägig");
    public static string ProfileTimeInvalid => T("Please enter times as HH:MM, for example 07:30.", "Uhrzeiten bitte als HH:MM, zum Beispiel 07:30.");
    public static string ProfileNameMissing => T("Please give the profile a name.", "Bitte gib dem Profil einen Namen.");
    public static string ProfileNewHelp => T("Starts with the apps switched on as in the profile the list shows now.", "Übernimmt die eingeschalteten Apps des Profils, das die Liste gerade zeigt.");
    public static string ProfileDelete => T("Delete", "Löschen");
    public static string Save => T("Save", "Speichern");
    public static string PauseTitle => T("Pause \"{0}\"", "„{0}“ pausieren");
    public static string PauseText => T("A paused profile is skipped; the next one in the order applies, or none. After the pause it applies again by itself.",
        "Ein pausiertes Profil wird übersprungen; dann gilt das nächste in der Reihenfolge oder keins. Nach der Pause gilt es von selbst wieder.");
    public static string PauseKindNow => T("From now", "Ab jetzt");
    public static string PauseKindPlanned => T("Period", "Zeitraum");
    public static string PauseKindForever => T("Until resumed", "Dauerhaft");
    public static string PauseKindNowTip => T("Paused from now until the moment you choose.", "Ab sofort pausiert, bis zum gewählten Zeitpunkt.");
    public static string PauseKindPlannedTip => T("Planned ahead, like an out-of-office reply: paused only within this period.", "Im Voraus geplant, wie eine Abwesenheitsnotiz: nur in diesem Zeitraum pausiert.");
    public static string PauseKindForeverTip => T("Paused from now until you resume it.", "Ab sofort pausiert, bis du es fortsetzt.");
    public static string PauseWhen => T("When", "Wann");
    public static string PauseFrom => T("From", "Von");
    public static string PauseUntil => T("To", "Bis");
    public static string PauseForeverText => T("The profile stays paused until you click \"Resume\".", "Das Profil bleibt pausiert, bis du auf „Fortsetzen“ klickst.");
    public static string PauseRemove => T("Remove pause", "Pause aufheben");
    public static string PauseMissing => T("Please choose the days and enter times as HH:MM.", "Bitte die Tage wählen und Uhrzeiten als HH:MM eingeben.");
    public static string PauseEndBeforeStart => T("The end has to be later than the start and than now.", "Das Ende muss nach dem Beginn und nach jetzt liegen.");
    public static string ProfilePauseEdit => T("Change pause", "Pause ändern");
    public static string PausedOpen => T("paused until resumed", "pausiert, bis du fortsetzt");
    public static string PausedUntilMoment => T("paused until {0}", "pausiert bis {0}");
    public static string PausePlanned => T("pause planned: {0} to {1}", "Pause geplant: {0} bis {1}");
    public static string PausePlannedOpen => T("pause planned from {0}", "Pause geplant ab {0}");
    public static string PauseMoment => T("{0:ddd} {0:d} {0:HH:mm}", "{0:ddd} {0:d} {0:HH:mm}");
    public static string PauseDayStart => T("{0:ddd} {0:d}", "{0:ddd} {0:d}");
    public static string PauseDayEnd => T("end of {0:ddd} {0:d}", "einschließlich {0:ddd} {0:d}");
    public static string ProfileOutside => T("Outside these hours", "Außerhalb der Uhrzeit");
    public static string ProfileOutsideHelp => T(
        "What applies on the profile's days outside its hours. \"Next in the order\": the profiles further right decide, then none if none applies. Or straight to no profile, or to one chosen profile, whatever its own days and times.",
        "Was an den Tagen des Profils außerhalb seiner Uhrzeit gilt. „Weiter in der Reihenfolge“: die Profile weiter rechts entscheiden, trifft keins zu, gilt keins. Oder direkt ohne Profil oder ein gewähltes Profil, unabhängig von dessen Tagen und Uhrzeiten.");
    public static string ProfileOutsideNext => T("Next in the order", "Weiter in der Reihenfolge");
    public static string ProfileOutsideStandard => T("No profile", "Ohne Profil");
    public static string ProfileOutsideProfile => T("Profile \"{0}\"", "Profil „{0}“");
    public static string ProfileOutsideStandardShort => T("otherwise no profile", "sonst ohne Profil");
    public static string ProfileOutsideProfileShort => T("otherwise \"{0}\"", "sonst „{0}“");
    public static string ProfileUseUntil => T("Use until {0}", "Verwenden bis {0}");
    public static string ProfileUseOpen => T("Use until I switch back", "Verwenden, bis ich zurückstelle");
    public static string ProfileBackToSchedule => T("Back to the schedule", "Zurück zum Zeitplan");
    public static string ProfileBackToScheduleTip => T("A profile was chosen by hand. From now on the profiles' days and times decide again, in every app that uses them.",
        "Ein Profil wurde von Hand gewählt. Ab jetzt entscheiden wieder Tage und Uhrzeiten der Profile, in jeder App, die sie nutzt.");
    public static string ProfileLeaveTitle => T("Remove \"{0}\" from StartQueue?", "„{0}“ aus StartQueue entfernen?");
    public static string ProfileLeaveBody => T("{0} keeps using it. Here the profile goes, with which apps start in it.", "{0} nutzt es weiter. Hier fällt das Profil weg, mit den Apps, die darin starten.");
    public static string ProfileLeave => T("Remove", "Entfernen");

    /// <summary>"18:00" today, otherwise with the day: "Mo 09.11. 18:00".</summary>
    public static string ProfileMoment(DateTime moment) =>
        moment.Date == DateTime.Today ? moment.ToString("HH:mm") : string.Format(System.Globalization.CultureInfo.CurrentUICulture, "{0:ddd} {0:d} {0:HH:mm}", moment);
    public static string ProfileOrderShort => T("Order", "Reihenfolge");
    public static string ProfileOrderTip => T("All profiles one below the other, to put them in order.", "Alle Profile untereinander, zum Ordnen.");
    public static string ProfileOrderTitle => T("Order of the profiles", "Reihenfolge der Profile");
    public static string ProfileOrderHelp => T(
        "At sign-in StartQueue goes from top to bottom: the first profile that applies and is not paused decides, otherwise none. Drag a profile or use the arrows; the ring marks the profile that applies now.",
        "Bei der Anmeldung geht StartQueue von oben nach unten: Das erste Profil, das zutrifft und nicht pausiert ist, entscheidet, sonst keins. Profil ziehen oder die Pfeile nutzen; der Ring zeigt das Profil, das gerade gilt.");
    public static string ProfileEarlierTip => T("Earlier in the order (takes precedence)", "Weiter nach vorn (hat Vorrang)");
    public static string ProfileLaterTip => T("Later in the order", "Weiter nach hinten");
    public static string SummaryProfile => T("{0} of {1} apps start in \"{2}\"", "{0} von {1} Apps starten in „{2}“");
    public static string SummaryAppliesNow => T(", which applies now", ", das gerade gilt");
    public static string SummaryOtherApplies => T(". \"{0}\" applies now", ". Gerade gilt „{0}“");

    public static string DayShort(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => T("Mon", "Mo"),
        DayOfWeek.Tuesday => T("Tue", "Di"),
        DayOfWeek.Wednesday => T("Wed", "Mi"),
        DayOfWeek.Thursday => T("Thu", "Do"),
        DayOfWeek.Friday => T("Fri", "Fr"),
        DayOfWeek.Saturday => T("Sat", "Sa"),
        _ => T("Sun", "So"),
    };

    public static string Repeat => T("Repeat", "Wiederholen");
    public static string RepeatHelp => T("After the start at sign-in, every so many minutes, as long as StartQueue runs. 0: start once.",
        "Nach dem Start bei der Anmeldung alle so viele Minuten, solange StartQueue läuft. 0: nur einmal starten.");
    public static string RepeatName => T("Repeat every (minutes)", "Wiederholen alle (Minuten)");
    public static string MinutesShort => T("min", "Min.");
    public static string RepeatChip => T("every {0}", "alle {0}");
    public static string StartWindow => T("Window at sign-in", "Fenster beim Start");
    public static string WindowQuitsOnClose => T("\"{0}\" quits when its window is closed, so it cannot start in the background. StartQueue minimizes its window instead.",
        "„{0}“ beendet sich, wenn sein Fenster geschlossen wird, und kann deshalb nicht im Hintergrund starten. StartQueue minimiert sein Fenster stattdessen.");
    public static string StartWindowHelp => T("Background closes the window; the app keeps running in the notification area. If it quits instead, StartQueue starts it again minimized.",
        "Hintergrund schließt das Fenster, die App läuft im Infobereich weiter. Beendet sie sich dabei, startet StartQueue sie minimiert neu.");
    public static string WindowNormal => T("Normal", "Normal");
    public static string WindowMinimized => T("Minimized", "Minimiert");
    public static string WindowBackground => T("Background", "Hintergrund");
    public static string WindowViaSwitch => T("Through the start parameter \"{0}\". Normal takes it out.", "Über den Startparameter „{0}“. „Normal“ nimmt ihn heraus.");
    public static string WindowViaFixedSwitch => T("Through the start parameter \"{0}\" in the shortcut or task.", "Über den Startparameter „{0}“ in der Verknüpfung oder Aufgabe.");
    public static string WindowViaShortcut => T("The shortcut starts the app minimized.", "Die Verknüpfung startet die App minimiert.");
    public static string WindowNoWindow => T("The app showed no window at its last start, for example a service or an app in the notification area.",
        "Die App hat beim letzten Start kein Fenster gezeigt, zum Beispiel ein Dienst oder eine App im Infobereich.");
    public static string WindowViaStartQueueMinimized => T("StartQueue minimizes the window after the start.", "StartQueue minimiert das Fenster nach dem Start.");
    public static string WindowNormalKnown => T("Starts as it is. For the background StartQueue sets the start parameter \"{0}\".",
        "Startet, wie sie ist. Für den Hintergrund setzt StartQueue den Startparameter „{0}“.");
    public static string WindowNormalPlain => T("Starts as it is. With Minimized or Background StartQueue handles the window after the start.",
        "Startet, wie sie ist. Bei Minimiert oder Hintergrund kümmert sich StartQueue nach dem Start um das Fenster.");
    public static string WindowFactSwitch => T("Through the start parameter \"{0}\".", "Über den Startparameter „{0}“.");
    public static string WindowFactFixedSwitch => T("Through \"{0}\" in the shortcut or task.", "Über „{0}“ in der Verknüpfung oder Aufgabe.");
    public static string WindowFactShortcut => T("The shortcut starts it minimized.", "Die Verknüpfung startet sie minimiert.");
    public static string WindowFactNoWindow => T("Shows no window at its start.", "Zeigt beim Start kein Fenster.");
    public static string BackgroundChip => T("background", "Hintergrund");
    public static string MinimizedChip => T("minimized", "minimiert");
    public static string TaskSeveralActions => T("several programs", "mehrere Programme");
    public static string TaskSeveralActionsTip => T("The task runs several programs; StartQueue leaves it as it is.", "Die Aufgabe startet mehrere Programme; StartQueue lässt sie, wie sie ist.");
    public static string TaskPassword => T("stored password", "Kennwort hinterlegt");
    public static string TaskPasswordTip => T("The task runs with a stored password; changing it would need that password.", "Die Aufgabe läuft mit hinterlegtem Kennwort; ändern ginge nur mit diesem Kennwort.");
    public static string TaskNoProgram => T("no program", "kein Programm");
    public static string TaskNoProgramTip => T("The task does not start a program (for example it sends a message).", "Die Aufgabe startet kein Programm (sie sendet zum Beispiel eine Nachricht).");
    public static string TaskTakeOverFailed => T("Could not take over the task", "Aufgabe ließ sich nicht übernehmen");
    public static string TaskNeedsAdmin => T("Changing this task needs admin rights. StartQueue is set up without them on this device.",
        "Diese Aufgabe zu ändern braucht Adminrechte. StartQueue ist auf diesem Gerät ohne Adminrechte eingerichtet.");

    // ---------- First start: introduction and preview, before StartQueue is switched on ----------
    public static string IntroNotOn => T("Not switched on yet. Windows starts everything as before.", "Noch nicht eingeschaltet. Windows startet alles wie bisher.");
    public static string IntroStepWelcome => T("Welcome", "Willkommen");
    public static string IntroStepTakeOver => T("Take over", "Übernehmen");
    public static string IntroStepSwitchOn => T("Switch on", "Einschalten");

    public static string IntroWelcomeTitle => T("Your apps start after sign-in in your order.", "Deine Apps starten nach der Anmeldung in deiner Reihenfolge.");
    public static string IntroWelcomeText => T("StartQueue takes apps out of the Windows autostart and starts them itself: one after the other, with pauses, minimized or in the background, by day of the week if you like.",
        "StartQueue nimmt Apps aus dem Windows-Autostart und startet sie selbst: nacheinander, mit Pausen, minimiert oder im Hintergrund, auf Wunsch je nach Wochentag.");
    public static string IntroPointListTitle => T("One list for everything that starts with Windows", "Eine Liste für alles, was mit Windows startet");
    public static string IntroPointListText => T("Scheduled tasks and services of apps too.", "Auch geplante Aufgaben und Dienste von Apps.");
    public static string IntroPointFirstTitle => T("What you need first is there first", "Was du zuerst brauchst, ist zuerst da");
    public static string IntroPointFirstText => T("The rest waits for its turn.", "Der Rest wartet, bis er dran ist.");
    public static string IntroPointBackTitle => T("Everything can be given back", "Alles lässt sich zurückgeben");
    public static string IntroPointBackText => T("When you uninstall, every entry goes back where it was.", "Beim Deinstallieren kommt jeder Eintrag dorthin zurück, wo er war.");
    public static string IntroWelcomeFound => T("So far StartQueue has only looked and changed nothing. The autostart holds {0} entries.",
        "Bisher hat StartQueue nur nachgesehen und nichts geändert. Im Autostart stehen {0} Einträge.");
    public static string IntroWelcomeFoundOne => T("So far StartQueue has only looked and changed nothing. The autostart holds 1 entry.",
        "Bisher hat StartQueue nur nachgesehen und nichts geändert. Im Autostart steht 1 Eintrag.");

    public static string IntroTakeOverTitle => T("Which apps should StartQueue start?", "Welche Apps soll StartQueue starten?");
    public static string IntroTakeOverText => T("StartQueue takes the chosen apps out of the Windows autostart and starts them itself. Windows goes on starting all others as before.",
        "Ausgewählte Apps nimmt StartQueue aus dem Windows-Autostart und startet sie selbst. Alle anderen startet Windows weiter wie bisher.");
    public static string IntroAtSignIn => T("AT YOUR SIGN-IN ({0})", "BEI DEINER ANMELDUNG ({0})");
    public static string IntroChosen => T("{0} chosen", "{0} ausgewählt");
    public static string IntroNoneFound => T("Nothing starts with Windows for your account yet. You can add apps after switching on.",
        "Für dein Konto startet noch nichts mit Windows. Apps kannst du nach dem Einschalten hinzufügen.");
    public static string IntroForAllUsers => T("FOR ALL USERS OF THIS PC ({0})", "FÜR ALLE BENUTZER DIESES PCS ({0})");
    public static string IntroForAllUsersText => T("Windows goes on starting these. StartQueue can take them over once it is set up fully.",
        "Diese startet weiter Windows. Übernehmen kann StartQueue sie, wenn es vollständig eingerichtet ist.");
    public static string IntroForAllUsersNoAdmin => T("Windows goes on starting these. Taking them over needs an administrator of this PC.",
        "Diese startet weiter Windows. Sie zu übernehmen braucht einen Administrator dieses PCs.");
    public static string IntroSetUpFully => T("Set up fully", "Vollständig einrichten");
    public static string IntroSetUpFullyTip => T("Windows asks once for your confirmation, then StartQueue starts again and asks nothing more.",
        "Windows fragt einmal nach deiner Bestätigung, dann startet StartQueue neu und fragt danach nichts mehr.");
    public static string IntroLaterLabel => T("WHEN AN APP ADDS ITSELF LATER", "WENN SICH SPÄTER EINE APP EINTRÄGT");
    public static string IntroModeEnabled => T("Take over", "Übernehmen");
    public static string IntroModeEnabledHelp => T("StartQueue starts it too, you get a notification.", "StartQueue startet sie mit, du bekommst eine Meldung.");
    public static string IntroModeDisabled => T("Take over, but switched off", "Übernehmen, aber ausgeschaltet");
    public static string IntroModeDisabledHelp => T("It is in your list and starts once you switch it on.", "Steht in deiner Liste, startet erst, wenn du sie einschaltest.");
    public static string IntroModeNotify => T("Only tell me", "Nur melden");
    public static string IntroModeNotifyHelp => T("It stays in the Windows autostart, you get a notification.", "Bleibt im Windows-Autostart, du bekommst eine Meldung.");
    public static string IntroWhereRun => T("Registry", "Registrierung");
    public static string IntroWhereFolder => T("Startup folder", "Autostart-Ordner");
    public static string IntroWhereTask => T("Task", "Aufgabe");
    public static string IntroWhereStore => T("Store app", "Store-App");

    public static string IntroReadyTitle => T("Ready to switch on", "Bereit zum Einschalten");
    public static string IntroReadyText => T("This is how your PC starts from the next sign-in. You can change all of it afterwards under Startup order.",
        "So startet dein PC ab der nächsten Anmeldung. Ändern kannst du alles danach unter Startreihenfolge.");
    public static string IntroReadyStarts => T("StartQueue starts", "StartQueue startet");
    public static string IntroReadyNothing => T("Nothing yet; you add apps with \"Add app\".", "Noch nichts; Apps fügst du mit „App hinzufügen“ hinzu.");
    public static string IntroReadyMore => T("and {0} more", "und {0} weitere");
    public static string IntroReadyWindows => T("Windows keeps starting", "Windows startet weiter");
    public static string IntroReadyWindowsAll => T("{0} apps for all users of this PC", "{0} Apps für alle Benutzer dieses PCs");
    public static string IntroReadyWindowsAllOne => T("1 app for all users of this PC", "1 App für alle Benutzer dieses PCs");
    public static string IntroReadyWindowsLeft => T("{0} apps you leave with Windows", "{0} Apps, die du Windows lässt");
    public static string IntroReadyWindowsLeftOne => T("1 app you leave with Windows", "1 App, die du Windows lässt");
    public static string IntroReadyWindowsNone => T("Nothing of yours", "Nichts von dir");
    public static string IntroReadyNew => T("New autostart entries", "Neue Autostart-Einträge");
    public static string IntroReadyNewEnabled => T("Take over, you get a notification", "Übernehmen, du bekommst eine Meldung");
    public static string IntroReadyNewDisabled => T("Take over switched off, you get a notification", "Ausgeschaltet übernehmen, du bekommst eine Meldung");
    public static string IntroReadyNewNotify => T("Leave in Windows, you get a notification", "In Windows lassen, du bekommst eine Meldung");
    public static string IntroReadySetUp => T("Set up", "Eingerichtet");
    public static string IntroReadyBasis => T("Without admin rights", "Ohne Adminrechte");
    public static string IntroReadyFull => T("Fully, with admin rights", "Vollständig, mit Adminrechten");
    public static string IntroReadyGiveBack => T("Giving back works any time: when you uninstall, every entry goes back where it was.",
        "Zurückgeben geht jederzeit: Beim Deinstallieren kommt jeder Eintrag dorthin zurück, wo er war.");

    public static string PreviewSummary => T("Preview: {0} apps would start in this order. Nothing is taken over yet.",
        "Vorschau: {0} Apps würden in dieser Reihenfolge starten. Noch ist nichts übernommen.");
    public static string PreviewSummaryOne => T("Preview: 1 app would start. Nothing is taken over yet.", "Vorschau: 1 App würde starten. Noch ist nichts übernommen.");
    public static string StillInWindowsChip => T("still with Windows", "noch bei Windows");
    public static string StillInWindowsTip => T("Windows still starts this app. StartQueue takes it over with these settings when you switch it on.",
        "Diese App startet noch Windows. StartQueue übernimmt sie mit diesen Einstellungen, wenn du es einschaltest.");
}
