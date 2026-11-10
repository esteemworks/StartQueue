; StartQueue setup: the family's setup (EsteemWorks.Kit\setup\EsteemWorks.iss) with StartQueue's own parts.
; One setup.exe for devices with and without admin rights. Installs per user (no UAC prompt) into
; %LOCALAPPDATA%\Programs\StartQueue, with its own .NET. Its page asks "Basic, without admin rights" (chosen) or "Full,
; with admin rights": one UAC prompt sets up the admin helper task, the Windows 11 context menu and the event log source,
; and after that StartQueue asks Windows nothing more. Everything else is done by StartQueue.exe itself (--setup,
; --uninstall); a new installation (--setup --new) changes nothing in Windows until the user switches it on.
; Built by build.ps1 -Setup, which passes SourceDir (the self-contained app).

#define AppName "StartQueue"
; Never changed since 1.0, so updates and the uninstaller keep finding the installation.
#define AppId "{{6E4A7C2B-3D51-4F0A-9B8E-2C7D1A5F9E31}"
#define AppIcon "..\assets\StartQueue.ico"
#define BasicEn "StartQueue arranges everything that starts when you sign in. Apps set up for all users of the PC are still started by Windows."
#define BasicDe "StartQueue ordnet alles, was bei deiner Anmeldung startet. Apps, die für alle Benutzer des PCs eingetragen sind, startet weiter Windows."
#define FullEn "Also: take over apps for all users too, start apps as administrator, StartQueue in the new context menu of Windows 11."
#define FullDe "Dazu kommt: auch Apps für alle Benutzer übernehmen, Apps als Administrator starten, StartQueue im neuen Kontextmenü von Windows 11."
#define FinishedEn "StartQueue has not changed anything yet. When you open it, it shows you what it found in the Windows autostart, and you decide what it takes over and when it starts."
#define FinishedDe "StartQueue hat noch nichts verändert. Beim ersten Öffnen zeigt es dir, was es im Windows-Autostart gefunden hat, und du entscheidest, was es übernimmt und wann es startet."
; Drawn by make-images.ps1, in the sizes Inno Setup picks from for 100 to 250 percent scaling.
#define WizardImages "images\large-164.png,images\large-192.png,images\large-246.png,images\large-273.png,images\large-328.png,images\large-355.png,images\large-410.png"
#define WizardSmallImages "images\small-55.png,images\small-64.png,images\small-83.png,images\small-92.png,images\small-110.png,images\small-119.png,images\small-138.png"
#define AppSectionsFile SourcePath + "StartQueue.sections.iss"
#define AppCodeFile SourcePath + "StartQueue.code.iss"
#define AppPrepareHook
; Looks for new versions on GitHub once a day (a ticked task per user); after an update back to the tray quietly.
#define Updates
#include "..\..\EsteemWorks.Kit\setup\EsteemWorks.iss"
