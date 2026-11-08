# Runs inside Windows Sandbox: StartQueue starts at sign-in in exactly one way, the scheduled task or the Run key,
# never both. The case behind it: an older setup ran elevated, so the sign-in task belongs to the administrators
# and the user may only read it; a later setup without admin rights (every update) could not replace it and added the
# Run key, and StartQueue started twice.
# The sandbox user is an administrator without UAC, so the setup without admin rights runs with a restricted token
# (runas /trustlevel:0x20000: Administrators for deny only, as a normal process of an administrator under UAC).
# run-autostart-test.ps1 maps this script (as test.ps1), SandboxKit.ps1 and the setup to C:\sq.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$log = "$env:APPDATA\StartQueue\StartQueue.log"
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$readOnly = "D:(A;;FA;;;BA)(A;;FA;;;SY)(A;;FR;;;$sid)"

function Get-SchedulerFolder { $service = New-Object -ComObject Schedule.Service; $service.Connect(); $service.GetFolder('\StartQueue') }
function Get-LogonTask { try { (Get-SchedulerFolder).GetTask('Logon') } catch { $null } }
function Get-RunValue { (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).StartQueue }
function Test-UserWritable { $task = Get-LogonTask; $task -and ($task.GetSecurityDescriptor(4) -match "\(A;[^;]*;FA;;;$([regex]::Escape($sid))\)") }
function Get-SetupCount { @(Select-String -Path $log -Pattern 'Setup done' -ErrorAction SilentlyContinue).Count }
function Get-LastSignInLine { (Select-String -Path $log -Pattern 'Sign-in: ' -ErrorAction SilentlyContinue | Select-Object -Last 1).Line }

# As an older setup left it: registered elevated, the user may only read it.
function Set-ReadOnlyTask([string]$Command) {
    $xml = (Get-LogonTask).Xml
    if ($Command) { $xml = $xml -replace '<Command>[^<]*</Command>', "<Command>$Command</Command>" }
    try {
        $folder = Get-SchedulerFolder
        $folder.DeleteTask('Logon', 0)
        $task = $folder.RegisterTask('Logon', $xml, 6, $null, $null, 3, $null)
        $task.SetSecurityDescriptor($readOnly, 0)
        Write-Note "prepared read-only task: $((Get-LogonTask).GetSecurityDescriptor(4))"
    }
    catch {
        Write-Note "preparing the read-only task failed: $($_.Exception.Message)"
    }
}

# StartQueue's own setup step (what setup.exe runs), without admin rights. Waits for its "Setup done".
function Invoke-RestrictedSetup {
    $before = Get-SetupCount
    Start-Process runas.exe -ArgumentList '/trustlevel:0x20000', "`"$exe --setup`"" -WindowStyle Hidden
    for ($i = 0; $i -lt 120 -and (Get-SetupCount) -eq $before; $i++) { Start-Sleep -Milliseconds 500 }
    Test-Check 'setup without admin rights ran' ((Get-SetupCount) -gt $before)
    Write-Note (Get-LastSignInLine)
}

# 0. The restricted token really has no admin rights.
$groups = Join-Path $Kit.Results 'restricted-groups.txt'
Start-Process runas.exe -ArgumentList '/trustlevel:0x20000', "`"cmd /c whoami /groups > $groups`"" -WindowStyle Hidden
for ($i = 0; $i -lt 40 -and -not (Test-Path $groups); $i++) { Start-Sleep -Milliseconds 500 }
Start-Sleep 1
$admins = Select-String -Path $groups -Pattern 'S-1-5-32-544' -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check 'restricted token: Administrators for deny only' ($admins -and $admins.Line -match 'deny|ablehn|verweiger') "$($admins.Line)"

# 1. A new setup (elevated here): task, no Run key, and the user may replace the task.
$exe = Install-App
Test-Check 'sign-in task registered' ([bool](Get-LogonTask))
Test-Check 'no Run key next to it' (-not (Get-RunValue))
Test-Check 'task registered elevated is writable for the user' (Test-UserWritable) (Get-LogonTask).GetSecurityDescriptor(4)

# 2. As found on a real PC: read-only task and the Run key from the update before. An update keeps the task and
#    removes the Run key.
Set-ReadOnlyTask
Set-ItemProperty $runKey -Name StartQueue -Value "`"$exe`" --autostart"
Test-Check 'prepared: task read-only for the user' (-not (Test-UserWritable))
Invoke-RestrictedSetup
Test-Check 'update could not replace it and kept it' ((Get-LastSignInLine) -match 'kept as it is')
Test-Check 'task still there' ([bool](Get-LogonTask))
Test-Check 'the Run key from before is gone' (-not (Get-RunValue)) "$(Get-RunValue)"

# 3. A setup with the admin features repairs it: registered again, now writable for the user.
$signIns = @(Select-String -Path $log -Pattern 'Sign-in: ' -ErrorAction SilentlyContinue).Count
(Start-Process $exe -ArgumentList '--setup-admin' -PassThru).WaitForExit(60000) | Out-Null
for ($i = 0; $i -lt 60 -and @(Select-String -Path $log -Pattern 'Sign-in: ' -ErrorAction SilentlyContinue).Count -eq $signIns; $i++) { Start-Sleep -Milliseconds 500 }
Start-Sleep 1
Write-Note (Get-LastSignInLine)
Test-Check 'admin part makes the task writable for the user' (Test-UserWritable)
Test-Check 'still no Run key' (-not (Get-RunValue))

# 4. From then on an update without admin rights replaces the task itself.
Invoke-RestrictedSetup
Test-Check 'update replaced the task' ((Get-LastSignInLine) -match 'Sign-in: scheduled task$')
Test-Check 'no Run key after the update' (-not (Get-RunValue))

# 5. A read-only task that starts something else (another folder): the Run key takes over, and the log says the task
#    is left. That task cannot start StartQueue, so there is still one start.
Set-ReadOnlyTask -Command 'C:\Old\StartQueue.exe'
Invoke-RestrictedSetup
Test-Check 'Run key for a task that does not start this StartQueue' ([bool](Get-RunValue))
Test-Check 'log names the task that is left' ((Get-LastSignInLine) -match 'is left')

# 6. Uninstalling removes both (elevated here, so the read-only task goes too).
Uninstall-App
Test-Check 'no task after uninstall' (-not (Get-LogonTask))
Test-Check 'no Run key after uninstall' (-not (Get-RunValue))

Complete-SandboxTest
