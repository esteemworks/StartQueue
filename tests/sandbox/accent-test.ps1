# Runs inside Windows Sandbox: the accent color row of the settings, built by EsteemWorks.Kit's AccentRow (STANDARD.md
# section 3, "Appearance follows the profile"). The four segments in the family's order, Profile chosen on a fresh
# install, a click on Sand saved and kept after a restart; then every accent drawn in light and dark (--snapshot) for a
# look at the pictures in C:\sq\results\shots.
# run-accent-test.ps1 maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\sq.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [System.Windows.Automation.AutomationElement]

function Find-Window {
    for ($i = 0; $i -lt 20; $i++) {
        $w = $ae::RootElement.FindFirst('Children', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, 'StartQueue')))
        if ($w) { return $w }
        Start-Sleep 1
    }
}

function Open-Settings($w) {
    foreach ($name in 'Einstellungen', 'Settings') {
        $gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $name)))
        if ($gear) { $gear.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 3; return $true }
    }
    return $false
}

# The segments of the accent row: the radio buttons whose names are the row's, in the order they stand.
function Get-AccentSegments($w) {
    $radios = $w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, [System.Windows.Automation.ControlType]::RadioButton)))
    @($radios | Where-Object { $_.Current.Name -in 'StartQueue', 'Profil', 'Profile', 'Windows', 'Sand' } |
        Sort-Object { $_.Current.BoundingRectangle.Left })
}

function Get-SavedAccent { (Get-Content $configFile -Raw | ConvertFrom-Json).settings.accent }

function Start-Window {
    Get-Process StartQueue -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2
    Start-Process $exe -ArgumentList '--show'
    Start-Sleep 8
    $w = Find-Window
    if ($w) { [void](Open-Settings $w) }
    return $w
}

$exe = Install-App
$configFile = "$env:APPDATA\StartQueue\config.json"
for ($i = 0; $i -lt 40 -and -not (Test-Path $configFile); $i++) { Start-Sleep 1 }
Start-Sleep 3

$w = Start-Window
Test-Check 'window open with the settings' ($null -ne $w)
$segments = Get-AccentSegments $w
$names = ($segments | ForEach-Object { $_.Current.Name }) -join ', '
Test-Check 'four segments in the order StartQueue, Profile, Windows, Sand' ($names -in 'StartQueue, Profil, Windows, Sand', 'StartQueue, Profile, Windows, Sand') $names
$selected = @($segments | Where-Object { $_.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } | ForEach-Object { $_.Current.Name })
Test-Check 'a fresh install has Profile chosen' (($selected -join ',') -in 'Profil', 'Profile') ($selected -join ',')
Test-Check 'every segment has a tooltip' (@($segments | Where-Object { -not $_.Current.HelpText }).Count -eq 0) (($segments | ForEach-Object { $_.Current.HelpText }) -join ' | ')

$sand = $segments | Where-Object { $_.Current.Name -eq 'Sand' }
if ($sand) {
    $sand.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep 2
}
Test-Check 'a click on Sand is saved' ((Get-SavedAccent) -eq 'Sand') (Get-SavedAccent)
Save-Screen 'sand-chosen'

$w = Start-Window
$segments = Get-AccentSegments $w
$selected = @($segments | Where-Object { $_.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Current.IsSelected } | ForEach-Object { $_.Current.Name })
Test-Check 'Sand is still chosen after a restart' (($selected -join ',') -eq 'Sand') ($selected -join ',')

# Every accent in light and dark, drawn from the running app into PNG files; the saved choice comes back afterwards.
foreach ($accent in 'StartQueue', 'Profile', 'Windows', 'Sand') {
    foreach ($theme in 'light', 'dark') {
        $folder = "$out\shots\$accent-$theme"
        Start-Process $exe -ArgumentList '--snapshot', "`"$folder`"", $accent, $theme, 'german', 'x2'
        for ($i = 0; $i -lt 120 -and -not (Test-Path "$folder\order-low.png"); $i++) { Start-Sleep 1 }
        Start-Sleep 2
        Test-Check "snapshot $accent $theme" ((Test-Path "$folder\settings.png") -and (Test-Path "$folder\order.png"))
    }
}
Test-Check 'the saved accent is unchanged by the snapshots' ((Get-SavedAccent) -eq 'Sand') (Get-SavedAccent)

Complete-SandboxTest
