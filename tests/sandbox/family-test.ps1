# Runs inside Windows Sandbox: StartQueue and DynaTones together, the family's autostart. StartQueue takes DynaTones
# over quietly and first in the order, tells the family that it starts it, and DynaTones' own switch (here through
# its StartWithWindows policy) turns StartQueue's entry on and off over the pipe instead of writing the Run key
# again, so neither undoes the other. Uninstalling StartQueue gives DynaTones its own start back.
# run-family-test.ps1 maps this script (as test.ps1), SandboxKit.ps1, StartQueue's setup.exe and DynaTones' setup
# (in dt\) to C:\sq. Results go to C:\sq\results.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$starts = 'HKCU:\Software\EsteemWorks\Apps\StartQueue\Starts'
$policy = 'HKCU:\Software\Policies\EsteemWorks\DynaTones'
$sqLog = "$env:APPDATA\StartQueue\StartQueue.log"
$dtExe = "$env:LOCALAPPDATA\Programs\DynaTones\DynaTones.exe"

function Run-Value { (Get-ItemProperty $run -ErrorAction SilentlyContinue).DynaTones }
function Starts-Value { (Get-ItemProperty $starts -ErrorAction SilentlyContinue).DynaTones }
function Entry { (Get-Content "$env:APPDATA\StartQueue\config.json" -Raw | ConvertFrom-Json).Entries | Where-Object { $_.Name -eq 'DynaTones' } }
function Wait-Until([scriptblock]$Condition, [int]$Seconds = 30) {
    for ($i = 0; $i -lt $Seconds * 2; $i++) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 500 }
    return $false
}
function Lines([string]$Pattern) { @(Select-String -Path $sqLog -Pattern $Pattern -ErrorAction SilentlyContinue).Count }
# DynaTones applies its StartWithWindows policy when it starts.
function Restart-DynaTones {
    Get-Process DynaTones -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 1
    Start-Process $dtExe -ArgumentList '--autostart' | Out-Null
}

# 1. StartQueue first, then DynaTones: DynaTones' setup writes its Run value, StartQueue takes it over.
$sqExe = Install-App
Test-Check 'StartQueue runs' (Wait-Until { Get-Process StartQueue -ErrorAction SilentlyContinue })
$dtSetup = Get-ChildItem "$PSScriptRoot\dt" -Filter 'DynaTones-Setup-*.exe' | Select-Object -First 1
(Start-Process $dtSetup.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru).WaitForExit()
Test-Check 'DynaTones installed' (Test-Path $dtExe)

Test-Check 'DynaTones is in StartQueue''s list' (Wait-Until { Entry } 60)
$entry = Entry
Test-Check 'its Run value is gone' (Wait-Until { -not (Run-Value) })
$first = ((Get-Content "$env:APPDATA\StartQueue\config.json" -Raw | ConvertFrom-Json).Entries | Select-Object -First 1).Name
Test-Check 'it starts first' ($first -eq 'DynaTones') $first
Test-Check 'it is not marked new' (-not $entry.IsNew)
Test-Check 'StartQueue tells the family it starts DynaTones' (Wait-Until { (Starts-Value) -eq 1 })
Test-Check 'no "re-registered" for a family app' ((Lines 'Removed re-registered .*dynatones') -eq 0)

# 2. DynaTones' switch off (policy StartWithWindows = 0): the entry goes off, no Run value is written.
New-Item $policy -Force | Out-Null
Set-ItemProperty $policy -Name StartWithWindows -Value 0 -Type DWord
Restart-DynaTones
Test-Check 'switch off reaches StartQueue' (Wait-Until { (Starts-Value) -eq 0 })
Test-Check 'the entry is off' (-not (Entry).Enabled)
Test-Check 'no Run value after switching off' (-not (Run-Value))

# 3. And on again (policy = 1): the entry goes on, still no Run value, no ping-pong.
Set-ItemProperty $policy -Name StartWithWindows -Value 1 -Type DWord
Restart-DynaTones
Test-Check 'switch on reaches StartQueue' (Wait-Until { (Starts-Value) -eq 1 })
Test-Check 'the entry is on' ((Entry).Enabled)
Start-Sleep 5
Test-Check 'no Run value after switching on' (-not (Run-Value))

# 4. A start with the same policy changes nothing: DynaTones sees that StartQueue starts it.
$before = Lines 'family app DynaTones'
Restart-DynaTones
Start-Sleep 10
Test-Check 'a further start changes nothing' ((Lines 'family app DynaTones') -eq $before) "$before -> $(Lines 'family app DynaTones')"
Test-Check 'still no "re-registered"' ((Lines 'Removed re-registered .*dynatones') -eq 0)
Remove-Item $policy -Recurse -Force

# 5. Uninstalling StartQueue gives DynaTones its own start back.
Uninstall-App
Test-Check 'DynaTones'' Run value is back' (Wait-Until { Run-Value } 20) "$(Run-Value)"
Test-Check 'the family no longer hears that StartQueue starts it' (-not (Test-Path $starts))

Copy-Item "$env:APPDATA\DynaTones\DynaTones.log" $Kit.Results -ErrorAction SilentlyContinue
Complete-SandboxTest
