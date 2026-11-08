{ StartQueue's own code, included by the family's setup before PrepareToInstall (see StartQueue.iss). }

{ Versions before the setup (1.0 and 1.1) do not know --prepare-update: stop StartQueue here, remove the context menu
  package and release the context menu DLL that Explorer keeps loaded. Newer versions do this with --prepare-update. }
function AppPrepareToInstall(Exe: String): Boolean;
var
  Dll, Version: String;
  ResultCode: Integer;
begin
  Result := False;
  Dll := ExpandConstant('{app}\StartQueueShell.dll');
  if GetVersionNumbersString(Exe, Version) and ((Copy(Version, 1, 4) = '1.0.') or (Copy(Version, 1, 4) = '1.1.')) then
  begin
    Exec('powershell.exe', '-NoProfile -ExecutionPolicy Bypass -Command "Get-Process StartQueue -EA 0 | Stop-Process -Force; ' +
      'Get-AppxPackage -Name StartQueue.ContextMenu | Remove-AppxPackage; ' +
      'Get-Process dllhost -EA 0 | Where-Object { try { $_.Modules.FileName -contains ''' + Dll + ''' } catch { $false } } | Stop-Process -Force -EA 0"',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Result := True;
  end;
end;
