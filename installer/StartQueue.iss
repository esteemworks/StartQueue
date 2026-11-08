; StartQueue setup: the family's setup (EsteemWorks.Kit\setup\EsteemWorks.iss) with StartQueue's own parts.
; One setup.exe for devices with and without admin rights. Installs per user (no UAC prompt) into
; %LOCALAPPDATA%\Programs\StartQueue, with its own .NET. Administrators can tick "admin features": one UAC prompt
; sets up the elevated helper task, the Windows 11 context menu and the event log source. Everything else is done by
; StartQueue.exe itself (--setup, --uninstall). Built by build.ps1 -Setup, which passes SourceDir (the self-contained app).

#define AppName "StartQueue"
; Never changed since 1.0, so updates and the uninstaller keep finding the installation.
#define AppId "{{6E4A7C2B-3D51-4F0A-9B8E-2C7D1A5F9E31}"
#define AppIcon "..\assets\StartQueue.ico"
#define AdminFeaturesEn "Set up admin features: machine-wide autostart entries, starts as administrator without a UAC prompt, context menu in the Windows 11 menu (asks once for confirmation)"
#define AdminFeaturesDe "Adminfunktionen einrichten: systemweite Autostart-Einträge, Starts als Administrator ohne UAC-Abfrage, Kontextmenü im Windows-11-Menü (fragt einmal nach Bestätigung)"
; Drawn by make-images.ps1, in the sizes Inno Setup picks from for 100 to 250 percent scaling.
#define WizardImages "images\large-164.png,images\large-192.png,images\large-246.png,images\large-273.png,images\large-328.png,images\large-355.png,images\large-410.png"
#define WizardSmallImages "images\small-55.png,images\small-64.png,images\small-83.png,images\small-92.png,images\small-110.png,images\small-119.png,images\small-138.png"
#define AppSectionsFile SourcePath + "StartQueue.sections.iss"
#define AppCodeFile SourcePath + "StartQueue.code.iss"
#define AppPrepareHook
; Looks for new versions on GitHub once a day (a ticked task per user); after an update back to the tray quietly.
#define Updates
#include "..\..\EsteemWorks.Kit\setup\EsteemWorks.iss"
