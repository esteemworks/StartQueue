# Saved with a byte order mark: Windows PowerShell 5.1 in the sandbox reads it as UTF-8 only then ("Zurück", "hinzufügen").
# Runs inside Windows Sandbox: the family's profiles (EsteemWorks.Kit FamilyProfiles). StartQueue starts on the
# config.json of 1.5.2 (tests\fixtures) and moves its profiles to %APPDATA%\EsteemWorks\profiles.json; another app
# (here this script, writing the file as DynaTones would) chooses a profile by hand and StartQueue follows; the
# window's "Back to the schedule" ends it for all; a profile another app offers is taken over under "Add profile".
# run-profiles-test.ps1 maps this script (as test.ps1), SandboxKit.ps1, setup.exe and fx\ (the fixtures) to C:\sq.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [System.Windows.Automation.AutomationElement]

$data = "$env:APPDATA\StartQueue"
$family = "$env:APPDATA\EsteemWorks\profiles.json"
$log = "$data\StartQueue.log"
New-Item -ItemType Directory -Force $data | Out-Null
Copy-Item 'C:\sq\fx\config-v1.json' "$data\config.json"
$legacy = @((Get-Content "$data\config.json" -Raw | ConvertFrom-Json).profiles)
Write-Note "config.json of 1.5.2 with $($legacy.Count) profiles: $(($legacy | ForEach-Object name) -join ', ')"

$exe = Install-App
for ($i = 0; $i -lt 40 -and -not (Test-Path $family); $i++) { Start-Sleep 1 }
Start-Sleep 2
$f = Get-Content $family -Raw | ConvertFrom-Json
Test-Check 'the profiles of 1.5 moved to the family''s file' (@($f.profiles).Count -eq $legacy.Count -and @($f.profiles | Where-Object { $legacy.id -contains $_.id }).Count -eq $legacy.Count) (($f.profiles | ForEach-Object name) -join ', ')
Test-Check 'they are StartQueue''s, offered to no other app' (@($f.profiles | Where-Object { ($_.apps -join ',') -ne 'StartQueue' -or $_.shared }).Count -eq 0)
Test-Check 'the file remembers them as moved' (@($legacy | Where-Object { $f.imported -contains $_.id }).Count -eq $legacy.Count)
$c = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
Test-Check 'the old profiles stay in config.json, for going back' (@($c.profiles).Count -eq $legacy.Count)
$first = $legacy[0]
Test-Check 'which apps start in a profile stayed in config.json' (@($c.profileEntries.($first.id)).Count -eq @($first.on).Count) "$(@($c.profileEntries.($first.id)).Count) of $(@($first.on).Count)"
Test-Check 'the move is in the log' ([bool](Select-String $log -Pattern 'Moved \d+ profiles' -Quiet))

# Another app writes the file, as DynaTones would: atomically, through a temporary file.
function Edit-Family([scriptblock]$change) {
    $j = Get-Content $family -Raw | ConvertFrom-Json
    & $change $j
    $temp = "$family.test"
    $j | ConvertTo-Json -Depth 10 | Set-Content $temp -Encoding utf8
    Move-Item $temp $family -Force
}
function Wait-Log([string]$pattern, [int]$seconds = 10) {
    for ($i = 0; $i -lt $seconds * 2; $i++) {
        if (Select-String $log -Pattern $pattern -Quiet) { return $true }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

# Work: paused until 2027 in the sample, so certainly not the one that applies now; by hand it applies all the same.
$byHand = $legacy[0]
Edit-Family { param($j) $j | Add-Member -Force -NotePropertyName manual -NotePropertyValue ([pscustomobject]@{ profile = $byHand.id; since = (Get-Date).ToString('o') }) }
Test-Check 'a profile chosen by hand in another app applies in StartQueue too' (Wait-Log ([regex]::Escape("Profile that applies now: `"$($byHand.name)`", chosen by hand")))

# The main window, not a hidden window of the same name: one on screen and of a window's size.
function Find-Window {
    for ($i = 0; $i -lt 20; $i++) {
        $w = @($ae::RootElement.FindAll('Children', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, 'StartQueue'))) |
            Where-Object { -not $_.Current.IsOffscreen -and $_.Current.BoundingRectangle.Width -gt 400 }) | Select-Object -First 1
        if ($w) { return $w }
        Start-Sleep 1
    }
}
function Names($w) { (@($w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button)))) | ForEach-Object { $_.Current.Name } | Where-Object { $_ } | Select-Object -First 30) -join ' | ' }
function Find-Named($w, [string]$name) {
    @($w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $name))) | Where-Object { -not $_.Current.IsOffscreen }) | Select-Object -First 1
}
function Press($element) { $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }

Start-Process $exe
Start-Sleep 8
$w = Find-Window
if (-not $w) {
    # What is on screen instead, for the result.
    foreach ($top in @($ae::RootElement.FindAll('Children', [System.Windows.Automation.Condition]::TrueCondition))) {
        $r = $top.Current.BoundingRectangle
        Write-Note ("top: '{0}' class {1} offscreen {2} {3}x{4} pid {5}" -f $top.Current.Name, $top.Current.ClassName, $top.Current.IsOffscreen, [int]$r.Width, [int]$r.Height, $top.Current.ProcessId)
    }
    Add-Type -AssemblyName System.Drawing
    $bmp = New-Object System.Drawing.Bitmap 1920, 1080
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen(0, 0, 0, 0, $bmp.Size)
    $bmp.Save("$($Kit.Results)\screen.png")
}
Test-Check 'window open' ($null -ne $w)
$back = Find-Named $w 'Zurück zum Zeitplan'
Test-Check 'the window offers the way back to the schedule' ($null -ne $back) $(if (-not $back -and $w) { Names $w })
if ($back) {
    Press $back
    Start-Sleep 2
    $f = Get-Content $family -Raw | ConvertFrom-Json
    Test-Check '"Back to the schedule" ends the switch by hand for every app' ($null -eq $f.manual)
}

# A profile DynaTones offers to all apps: StartQueue offers it under "Add profile" and takes it over, same id.
$offered = [guid]::NewGuid().ToString()
Edit-Family {
    param($j)
    $j.profiles += [pscustomobject]@{ id = $offered; name = 'Abend'; color = 'lavendel'; days = @(); from = '18:00'; to = '23:00'; outside = 'next'; pausedIndefinitely = $false; shared = $true; apps = @('DynaTones') }
}
Start-Sleep 2
$add = Find-Named $w 'Profil hinzufügen'
Test-Check 'the profile bar has "Add profile"' ($null -ne $add) $(if (-not $add -and $w) { Names $w })
if ($add) {
    Press $add
    Start-Sleep 2
    $combo = @($w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::ComboBox))) |
        Where-Object { -not $_.Current.IsOffscreen -and $_.Current.Name -eq 'Profil' }) | Select-Object -First 1
    Test-Check 'a new profile can be one another app offers' ($null -ne $combo)
    if ($combo) {
        $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
        Start-Sleep 1
        $item = @($combo.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem))) |
            Where-Object { $_.Current.Name -like '*Abend*' -or ($_.FindFirst('Descendants', [System.Windows.Automation.Condition]::TrueCondition).Current.Name -like '*Abend*') }) | Select-Object -First 1
        Test-Check 'the offered profile is listed with the app that offers it' ($null -ne $item) ((@($combo.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)))) | ForEach-Object { $_.Current.Name }) -join ' | ')
        if ($item) {
            $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
            $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
            Start-Sleep 1
            $name = @($w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, 'Name'))) | Where-Object { -not $_.Current.IsOffscreen -and $_.Current.ControlType -eq [System.Windows.Automation.ControlType]::Edit }) | Select-Object -First 1
            $nameValue = if ($name) { $name.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).Current.Value } else { '' }
            Test-Check 'choosing it fills in its name and schedule' ($nameValue -eq 'Abend') $nameValue
            Press (Find-Named $w 'Speichern')
            Start-Sleep 2
            $f = Get-Content $family -Raw | ConvertFrom-Json
            $taken = @($f.profiles | Where-Object id -eq $offered)
            Test-Check 'taken over: the same profile, now used by both apps, no copy' ($taken.Count -eq 1 -and $taken[0].apps -contains 'StartQueue' -and $taken[0].apps -contains 'DynaTones') (($taken | ForEach-Object { $_.apps -join ',' }) -join ' / ')
            $c = Get-Content "$data\config.json" -Raw | ConvertFrom-Json
            Test-Check 'StartQueue keeps which apps start in it in its own settings' ($null -ne $c.profileEntries.$offered)
            Test-Check 'the take-over is in the log' ([bool](Select-String $log -Pattern 'Took over the profile "Abend"' -Quiet))
        }
    }
}

Complete-SandboxTest
