; StartQueue's own sections, included by the family's setup (see StartQueue.iss). Shutting down with prepared apps
; is set up in StartQueue's settings only: the setup asks nothing a new user cannot judge yet.

[InstallDelete]
; Left over from installs with install.ps1.
Type: files; Name: "{app}\install.ps1"
Type: files; Name: "{app}\uninstall.ps1"
Type: files; Name: "{app}\StartQueue.pdb"
