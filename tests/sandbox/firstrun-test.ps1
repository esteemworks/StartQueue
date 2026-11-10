# Runs inside Windows Sandbox: the first start of a new install (STANDARD.md, "First start"). StartQueue changes
# nothing in Windows before it is switched on: the introduction (Welcome, Take over, Switch on), "Look around first" with
# the preview, a sign-in meanwhile, an entry unticked that stays with Windows. Then a full setup: machine-wide entries are
# taken over and given back through the admin helper, without a UAC prompt. Last, an update of 1.6.0 over a list in use:
# no introduction, everything as before.
# The sandbox user is an administrator without UAC; StartQueue runs with a restricted token (runas /trustlevel:0x20000,
# Administrators for deny only), as for a user without admin rights or a normal process under UAC. The texts are looked
# for in the sandbox's language, German or English.
# run-firstrun-test.ps1 maps this script (as test.ps1), SandboxKit.ps1, setup.exe and old\ (the setup of 1.6.0) to C:\sq.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$ae = [System.Windows.Automation.AutomationElement]
$types = [System.Windows.Automation.ControlType]
$run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runLm = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
$startup = [Environment]::GetFolderPath('Startup')
$data = "$env:APPDATA\StartQueue"
$configFile = "$data\config.json"
$log = "$data\StartQueue.log"
$exe = "$env:LOCALAPPDATA\Programs\StartQueue\StartQueue.exe"
$de = (Get-UICulture).Name -like 'de*'
function L([string]$English, [string]$German) { if ($de) { $German } else { $English } }
Write-Note "Language of the sandbox: $((Get-UICulture).Name)"

function Wait-Until([scriptblock]$Condition, [int]$Seconds = 30) {
    for ($i = 0; $i -lt $Seconds * 2; $i++) { if (& $Condition) { return $true }; Start-Sleep -Milliseconds 500 }
    return $false
}
function Config { try { Get-Content $configFile -Raw -ErrorAction Stop | ConvertFrom-Json } catch { $null } }
function RunValue([string]$Name, [string]$Key = $run) { (Get-ItemProperty $Key -ErrorAction SilentlyContinue).$Name }
function Lines([string]$Pattern) { @(Select-String -Path $log -Pattern $Pattern -ErrorAction SilentlyContinue).Count }
function Previewed { @((Config).entries | Where-Object stillInWindows | ForEach-Object { $_.origin.name }) }
function Describe-List { ((Config).entries | ForEach-Object { "$($_.origin.name)$(if ($_.stillInWindows) { '*' })" }) -join ', ' }
# The name an entry shows, as StartQueue reads it from the program (calc.exe is "Windows Calculator").
function Shown([string]$Origin) { ((Config).entries | Where-Object { $_.origin.name -eq $Origin } | Select-Object -First 1).name }

function Add-Shortcut([string]$Path, [string]$Target) {
    $link = (New-Object -ComObject WScript.Shell).CreateShortcut($Path)
    $link.TargetPath = $Target
    $link.Save()
}

# What a PC brings along: three entries in the user's Run key, a shortcut in the startup folder, one for all users.
function Set-Autostart {
    Set-ItemProperty $run -Name TestCloud -Value '"C:\Windows\System32\notepad.exe" --background'
    Set-ItemProperty $run -Name TestChat -Value '"C:\Windows\System32\charmap.exe"'
    Set-ItemProperty $run -Name TestGames -Value '"C:\Windows\System32\calc.exe"'
    Set-ItemProperty $runLm -Name TestPrinter -Value '"C:\Windows\System32\write.exe"'
    Add-Shortcut "$startup\TestNotes.lnk" 'C:\Windows\System32\notepad.exe'
}

function Stop-App { Get-Process StartQueue -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep 2 }

# StartQueue with the restricted token, as without admin rights. -Keep hands the command to the running one.
function Start-Restricted([string]$Arguments = '--show', [switch]$Keep) {
    if (-not $Keep) { Stop-App }
    Start-Process runas.exe -ArgumentList '/trustlevel:0x20000', "`"$exe $Arguments`"" -WindowStyle Hidden
}

function Find-Window {
    for ($i = 0; $i -lt 30; $i++) {
        $w = $ae::RootElement.FindFirst('Children', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, 'StartQueue')))
        if ($w) { return $w }
        Start-Sleep 1
    }
}

function Find([string]$Name, $Type = $null, $Root = $null, [int]$Tries = 10) {
    $scope = if ($Root) { $Root } else { Find-Window }
    if (-not $scope) { return $null }
    $condition = New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $Name)
    if ($Type) {
        $condition = New-Object System.Windows.Automation.AndCondition($condition, (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $Type)))
    }
    for ($i = 0; $i -lt $Tries; $i++) {
        $found = $scope.FindFirst('Descendants', $condition)
        if ($found) { return $found }
        Start-Sleep -Milliseconds 500
    }
}

# Something whose name starts with this, as the tab "Startup order  4".
function Find-Like([string]$Start) {
    $w = Find-Window
    if (-not $w) { return $null }
    $w.FindAll('Descendants', [System.Windows.Automation.Condition]::TrueCondition) | Where-Object { $_.Current.Name -like "$Start*" } | Select-Object -First 1
}

function Click([string]$Name, $Root = $null) {
    $button = Find $Name $types::Button $Root
    if (-not $button) { Write-Note "no button '$Name'"; return $false }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep 2
    return $true
}

function Has-Text([string]$Part) {
    $w = Find-Window
    if (-not $w) { return $false }
    $texts = $w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $types::Text)))
    return [bool]($texts | Where-Object { $_.Current.Name -like "*$Part*" } | Select-Object -First 1)
}

function Shots([string]$Name) {
    foreach ($theme in 'light', 'dark') {
        $folder = "$out\shots\$Name-$theme"
        Start-Process $exe -ArgumentList '--snapshot', "`"$folder`"", 'startqueue', $theme, $(if ($de) { 'german' } else { 'english' }), 'x2'
        Wait-Until { Test-Path "$folder\order-low.png" } 150 | Out-Null
        Start-Sleep 2
    }
}

# The steps are named as in the kit's introduction.
$stepWelcome = L 'Step 1 of 3: Welcome, current step' 'Schritt 1 von 3: Willkommen, aktueller Schritt'
$stepTakeOver = L 'Step 2 of 3: Take over, current step' 'Schritt 2 von 3: Übernehmen, aktueller Schritt'
$stepSwitchOn = L 'Step 3 of 3: Switch on, current step' 'Schritt 3 von 3: Einschalten, aktueller Schritt'
$tabOrder = L 'Startup order' 'Startreihenfolge'

# The sandbox account has no UAC token split: everything runs elevated and nothing would ever ask. With this the setup,
# the uninstaller and StartQueue act as an administrator under UAC does, and every start that would ask Windows is
# logged ("Elevated start: ..."), so they can be counted: Basic 0, Full 1 to set up and 1 to uninstall, in use 0.
$env:ESTEEMWORKS_ASSUME_STANDARD = '1'
function Elevated-Starts { @(Select-String -Path $log -Pattern 'Elevated start: ' -ErrorAction SilentlyContinue | ForEach-Object { $_.Line }) }

# ================= A. Basis, without admin rights =================
Set-Autostart
$installed = Install-App
$asked = @(Elevated-Starts)
Test-Check 'A: Basic in the setup asks Windows nothing' ($asked.Count -eq 0) ($asked -join ' | ')
Start-Restricted
Test-Check 'A: StartQueue made its list' (Wait-Until { (Config) -and (Previewed).Count -ge 4 } 60) (Describe-List)
$c = Config
Test-Check 'A: a new install starts with the introduction' ($c.firstStart -eq 'Welcome') $c.firstStart
Test-Check 'A: the setup marked the new installation' (Test-Path "$data\not-switched-on")
$shown = Previewed
Test-Check 'A: the user''s entries show, still with Windows' (@('TestCloud', 'TestChat', 'TestGames', 'TestNotes.lnk' | Where-Object { $shown -notcontains $_ }).Count -eq 0) (Describe-List)
Test-Check 'A: the entry for all users is not in the list without admin rights' (-not ($c.entries | Where-Object { $_.origin.name -eq 'TestPrinter' }))
Test-Check 'A: nothing taken out of the Run key' ((RunValue TestCloud) -and (RunValue TestChat) -and (RunValue TestGames))
Test-Check 'A: the startup shortcut is still there' (Test-Path "$startup\TestNotes.lnk")

Test-Check 'A: window open' ($null -ne (Find-Window))
Test-Check 'A: step line in place of the tabs' ($null -ne (Find $stepWelcome))
Test-Check 'A: no tabs during the introduction' ($null -eq (Find-Like "$tabOrder "))
Save-Screen 'a-1-welcome'

Test-Check 'A: Next leads to Take over' ((Click (L 'Next' 'Weiter')) -and $null -ne (Find $stepTakeOver))
$gamesName = Shown 'TestGames'
Test-Check 'A: every found entry has its switch' ($null -ne (Find $gamesName $types::CheckBox) -and $null -ne (Find (Shown 'TestCloud') $types::CheckBox)) "TestGames shows as '$gamesName'"
Test-Check 'A: the entry for all users is named apart' (Has-Text 'TestPrinter')
Test-Check 'A: without admin rights it says who can take it over' (Has-Text (L 'needs an administrator' 'braucht einen Administrator'))
Save-Screen 'a-2-takeover'

Test-Check 'A: Look around first' (Click (L 'Look around first' 'Erst umsehen'))
Test-Check 'A: looking around is saved' (Wait-Until { (Config).firstStart -eq 'LookingAround' } 10) (Config).firstStart
Test-Check 'A: preview bar with Continue setup' ($null -ne (Find (L 'Continue setup' 'Einrichtung fortsetzen')))
Test-Check 'A: the tabs are back' ($null -ne (Find-Like "$tabOrder "))
Test-Check 'A: the header says it is a preview' (Has-Text (L 'Nothing is taken over yet' 'Noch ist nichts übernommen'))
Test-Check 'A: rows say "still with Windows"' (Has-Text (L 'still with Windows' 'noch bei Windows'))

# A sign-in while looking around: StartQueue starts nothing and takes nothing.
Start-Restricted '--autostart'
Start-Sleep 12
Test-Check 'A: at sign-in nothing is started by StartQueue' ((Lines 'not switched on yet') -ge 1)
Test-Check 'A: and nothing taken' ((RunValue TestCloud) -and (RunValue TestChat) -and (Test-Path "$startup\TestNotes.lnk"))
Start-Restricted '--show' -Keep
Test-Check 'A: the window opens as the preview again' ($null -ne (Find (L 'Continue setup' 'Einrichtung fortsetzen')))
Save-Screen 'a-3-preview'
Shots 'a-preview'

# Back into the introduction (the kit's frame starts it again at its first step after a restart).
Test-Check 'A: Continue setup leads back into the introduction' ((Click (L 'Continue setup' 'Einrichtung fortsetzen')) -and $null -ne (Find $stepWelcome))
Test-Check 'A: back in the introduction is saved' (Wait-Until { (Config).firstStart -eq 'Welcome' } 10) (Config).firstStart
# One entry stays with Windows: its switch off in Take over.
[void](Click (L 'Next' 'Weiter'))
$games = Find $gamesName $types::CheckBox
if ($games) { $games.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 1 }
Test-Check 'A: TestGames unticked' ($games -and $games.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Current.ToggleState -eq 'Off')
[void](Click (L 'Next' 'Weiter'))
Test-Check 'A: the summary names the entry left with Windows' (Has-Text (L '1 app you leave with Windows' '1 App, die du Windows lässt'))
Test-Check 'A: and the one for all users' (Has-Text (L '1 app for all users of this PC' '1 App für alle Benutzer dieses PCs'))
Test-Check 'A: set up without admin rights' (Has-Text (L 'Without admin rights' 'Ohne Adminrechte'))
Save-Screen 'a-4-ready'
Test-Check 'A: Switch StartQueue on' (Click (L 'Switch StartQueue on' 'StartQueue einschalten'))
Test-Check 'A: switched on' (Wait-Until { $x = Config; $x -and -not $x.firstStart } 20)
Test-Check 'A: chosen entries taken out of the Run key' (Wait-Until { -not (RunValue TestCloud) -and -not (RunValue TestChat) } 30) "Cloud: $(RunValue TestCloud) Chat: $(RunValue TestChat)"
Test-Check 'A: the startup shortcut moved to StartQueue' (Wait-Until { -not (Test-Path "$startup\TestNotes.lnk") } 20)
Test-Check 'A: the unticked entry stays in the Run key' ([bool](RunValue TestGames))
$c = Config
$note = $c.keepNotes | Where-Object { $_.key -match 'testgames' }
Test-Check 'A: and on the keep list, left by the user' ($note.reason -eq 'LeftWithWindows') ($note | ConvertTo-Json -Compress)
Test-Check 'A: nothing marked "still with Windows" after switching on' (-not ($c.entries | Where-Object stillInWindows)) (Describe-List)
Test-Check 'A: the entry for all users stays with Windows' ([bool](RunValue TestPrinter $runLm))
Test-Check 'A: switching on is logged' ((Lines 'StartQueue was switched on') -ge 1)
Test-Check 'A: the family''s mark is gone' (-not (Test-Path "$data\not-switched-on"))
Save-Screen 'a-5-on'

Start-Restricted
Test-Check 'A: after a restart the normal window' ($null -ne (Find-Like "$tabOrder "))
Test-Check 'A: no step line any more' ($null -eq (Find $stepWelcome -Tries 2))

# Uninstalling gives every entry back.
Stop-App
Uninstall-App
$asked = @(Elevated-Starts)
Test-Check 'A: uninstalling Basic asks Windows nothing' ($asked.Count -eq 0) ($asked -join ' | ')
Test-Check 'A: uninstall gives the Run entries back' ((RunValue TestCloud) -and (RunValue TestChat))
Test-Check 'A: and the startup shortcut' (Test-Path "$startup\TestNotes.lnk")

# ================= B. Full setup, with the admin helper =================
Stop-App
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
$installed = Install-App -SetupArguments '/FULL'
$asked = @(Elevated-Starts)
Test-Check 'B: Full in the setup asks Windows exactly once' ($asked.Count -eq 1 -and $asked[0] -match '--setup-admin') ($asked -join ' | ')
Start-Restricted
Test-Check 'B: list made' (Wait-Until { (Config) -and (Previewed).Count -ge 5 } 60) (Describe-List)
$c = Config
Test-Check 'B: introduction again for a new install' ($c.firstStart -eq 'Welcome')
Test-Check 'B: set up fully, the entry for all users is in the list' ((Previewed) -contains 'TestPrinter') (Describe-List)
Test-Check 'B: and still with Windows' ([bool](RunValue TestPrinter $runLm))
[void](Click (L 'Next' 'Weiter'))
Test-Check 'B: its switch is in Take over' ($null -ne (Find (Shown 'TestPrinter') $types::CheckBox))
[void](Click (L 'Next' 'Weiter'))
Test-Check 'B: the summary says fully set up' (Has-Text (L 'Fully, with admin rights' 'Vollständig, mit Adminrechten'))
Save-Screen 'b-1-ready'
Test-Check 'B: Switch StartQueue on' (Click (L 'Switch StartQueue on' 'StartQueue einschalten'))
Test-Check 'B: the admin helper took the entry for all users over' (Wait-Until { -not (RunValue TestPrinter $runLm) } 60)
Test-Check 'B: and the user''s ones' (Wait-Until { -not (RunValue TestGames) -and -not (RunValue TestCloud) } 30)
Start-Sleep 3

# Giving the machine-wide entry back: through the admin helper, no UAC prompt.
$c = Config
$index = [array]::IndexOf(@($c.entries | ForEach-Object { $_.origin.name }), 'TestPrinter')
Write-Note "TestPrinter is entry $($index + 1) of $(@($c.entries).Count)"
$list = Find $tabOrder $types::List
$more = if ($list) { @($list.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, (L 'More actions' 'Weitere Aktionen'))))) } else { @() }
$consentBefore = @(Get-Process consent -ErrorAction SilentlyContinue).Count
if ($index -ge 0 -and $more.Count -gt $index) {
    $more[$index].GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep 1
    $item = $ae::RootElement.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, (L 'Return to Windows' 'An Windows zurückgeben'))))
    if ($item) { $item.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1 }
    [void](Click (L 'Give back' 'Zurückgeben'))
}
else { Write-Note "rows with More actions: $($more.Count)" }
Test-Check 'B: given back to Windows for all users' (Wait-Until { [bool](RunValue TestPrinter $runLm) } 60)
Test-Check 'B: through the admin helper' ((Lines 'Admin helper: --restore done \(0\)') -ge 1)
Test-Check 'B: no UAC prompt' (@(Get-Process consent -ErrorAction SilentlyContinue).Count -le $consentBefore)
Test-Check 'B: and out of the list' (-not ((Config).entries | Where-Object { $_.origin.name -eq 'TestPrinter' }))

# Shutting down with prepared apps, set up in the settings: through the admin helper too.
$gear = Find (L 'Settings' 'Einstellungen') $types::Button
if (-not $gear) { $gear = (Find-Window).FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, (L 'Settings' 'Einstellungen')))) }
if ($gear) { $gear.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 2 }
function By-Id([string]$Id) { (Find-Window).FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::AutomationIdProperty, $Id))) }
$prepared = By-Id 'PreparedButton'
Write-Note "prepared apps button: $(if ($prepared) { $prepared.Current.Name } else { 'not shown' })"
if ($prepared) {
    $prepared.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep 2
    $ok = By-Id 'ConfirmOk'
    if ($ok) { $ok.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
}
Test-Check 'B: prepared apps set up through the admin helper' (Wait-Until { (Lines 'Admin helper: --prepared-signin done \(0\)') -ge 1 } 60)
Save-Screen 'b-2-settings'

# A service change through the helper, as the services view hands it over; and nothing it does not know.
function Ask-Helper([string[]]$Arguments) {
    $id = [guid]::NewGuid().ToString()
    $json = '[{"id":"' + $id + '","args":[' + (($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ',') + ']}]'
    [IO.File]::WriteAllText("$data\admin-requests.json", $json)
    schtasks /run /tn '\StartQueue\Elevated' | Out-Null
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep 1
        $results = try { Get-Content "$data\admin-results.json" -Raw -ErrorAction Stop | ConvertFrom-Json } catch { @() }
        $hit = @($results) | Where-Object { $_.key -eq $id }
        if ($hit) { return $hit.value }
    }
}
$before = (Get-CimInstance Win32_Service -Filter "Name='Spooler'").StartMode
$code = Ask-Helper @('--service', 'Manual', 'Spooler')
$after = (Get-CimInstance Win32_Service -Filter "Name='Spooler'").StartMode
Test-Check 'B: a service change through the helper' ($code -eq 0 -and $after -eq 'Manual') "exit $code, $before -> $after"
$code = Ask-Helper @('--uninstall')
Test-Check 'B: the helper takes no other command line' ($code -eq 87) "exit $code"
$asked = @(Elevated-Starts)
Test-Check 'B: after the setup no more UAC prompts (giving back, prepared apps, service)' ($asked.Count -eq 1) ($asked -join ' | ')

# ================= C. Update of 1.6.0 with a list in use =================
Stop-App
Uninstall-App
$asked = @(Elevated-Starts)
Test-Check 'B: uninstalling Full asks Windows exactly once more' ($asked.Count -eq 2 -and $asked[1] -match '--uninstall-admin') ($asked -join ' | ')
Remove-Item Env:ESTEEMWORKS_ASSUME_STANDARD
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
Set-Autostart
$old = Get-ChildItem 'C:\sq\old' -Filter 'StartQueue-Setup-*.exe' | Select-Object -First 1
(Start-Process $old.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru).WaitForExit()
Start-Restricted
Test-Check 'C: 1.6.0 takes over at once, as before' (Wait-Until { -not (RunValue TestCloud) -and -not (RunValue TestChat) } 60)
Start-Sleep 3
$beforeUpdate = @((Config).entries).Count
Stop-App
$installed = Install-App
Start-Restricted
Start-Sleep 8
$c = Config
Test-Check 'C: no introduction after the update' (-not $c.firstStart) $c.firstStart
Test-Check 'C: the update marked nothing' (-not (Test-Path "$data\not-switched-on"))
Test-Check 'C: the list is kept' (@($c.entries).Count -eq $beforeUpdate) "$beforeUpdate -> $(@($c.entries).Count)"
Test-Check 'C: the window shows the order, no step line' ($null -ne (Find-Like "$tabOrder ") -and $null -eq (Find $stepWelcome -Tries 2))
Set-ItemProperty $run -Name TestLater -Value '"C:\Windows\System32\notepad.exe" --later'
Test-Check 'C: a new autostart entry is taken over as before' (Wait-Until { -not (RunValue TestLater) } 60)
Test-Check 'C: and marked new' ([bool]((Config).entries | Where-Object { $_.origin.name -eq 'TestLater' -and $_.isNew }))
Save-Screen 'c-1-updated'

# ================= D. --switch-on, as a deployment would =================
Stop-App
Uninstall-App
Remove-Item $data -Recurse -Force -ErrorAction SilentlyContinue
Set-Autostart
$installed = Install-App
Start-Restricted '--autostart'
Test-Check 'D: new installation, not switched on' (Wait-Until { (Config).firstStart -eq 'Welcome' -and (Previewed).Count -ge 4 } 60) (Describe-List)
Start-Restricted '--switch-on' -Keep
Test-Check 'D: --switch-on takes over what was found' (Wait-Until { $x = Config; $x -and -not $x.firstStart -and -not (RunValue TestCloud) -and -not (RunValue TestGames) } 60) "$((Config).firstStart) $(RunValue TestCloud)"
Test-Check 'D: and the mark is gone' (-not (Test-Path "$data\not-switched-on"))

Complete-SandboxTest
