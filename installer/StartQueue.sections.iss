; StartQueue's own sections, included by the family's setup (see StartQueue.iss).

[CustomMessages]
de.PrepareApps=Herunterfahren mit vorbereiteten Apps einrichten: Nach „Herunterfahren, Apps vorbereiten“ meldet Windows dich beim nächsten Start selbst an und sperrt sofort, deine Apps sind beim Entsperren schon da. Lässt der PC nur Windows Hello zu, wird das ausgeschaltet. Fragt nach Bestätigung, lässt sich in den Einstellungen rückgängig machen
en.PrepareApps=Set up shutting down with prepared apps: after "Shut down, prepare apps" Windows signs you in by itself at the next start and locks right away, so your apps are there when you unlock. If the PC only allows Windows Hello sign-in, that is switched off. Asks for confirmation, can be undone in the settings

[Tasks]
Name: "prepareapps"; Description: "{cm:PrepareApps}"; GroupDescription: "{cm:AdminGroup}"; Flags: unchecked; Check: UserIsAdmin

[InstallDelete]
; Left over from installs with install.ps1.
Type: files; Name: "{app}\install.ps1"
Type: files; Name: "{app}\uninstall.ps1"
Type: files; Name: "{app}\StartQueue.pdb"

[Run]
; As the signed-in user, so the setting is made for this account; asks through UAC itself.
Filename: "{app}\StartQueue.exe"; Parameters: "--setup-prepared"; StatusMsg: "{cm:SettingUp}"; Flags: waituntilterminated runasoriginaluser; Tasks: prepareapps; Check: not IsUpdateRun
