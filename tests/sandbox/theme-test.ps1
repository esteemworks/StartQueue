# Runs inside Windows Sandbox: the window's colors when the color mode of Windows changes while it is open, and the
# saved choice (light, dark, like Windows) at every start (STANDARD.md section 1). The settings are opened through UI
# Automation; pictures before and after each switch go to C:\sq\results, with the colors of the page and of a field
# measured in them. Modelled on DynaTones' theme-test.ps1.
# run-theme-test.ps1 maps this script (as test.ps1), SandboxKit.ps1 and setup.exe to C:\sq.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest
$out = $Kit.Results
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
$ae = [System.Windows.Automation.AutomationElement]

function Find-Window {
    for ($i = 0; $i -lt 20; $i++) {
        $w = $ae::RootElement.FindFirst('Children', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, 'StartQueue')))
        if ($w) { return $w }
        Start-Sleep 1
    }
}

# Opens the settings with the gear (a toggle, "Einstellungen" or "Settings").
function Open-Settings($w) {
    foreach ($name in 'Einstellungen', 'Settings') {
        $gear = $w.FindFirst('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::NameProperty, $name)))
        if ($gear) { $gear.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 3; return $true }
    }
    return $false
}

# The first drop-down or text box that lies wholly in the window below the header; the settings page is scrolled down
# step by step until one does (the first screen has only buttons).
function Find-Field($w) {
    $r = $w.Current.BoundingRectangle
    $inside = {
        param($e)
        $c = $e.Current.BoundingRectangle
        -not $e.Current.IsOffscreen -and $c.Width -gt 40 -and $c.Height -gt 10 -and
            $c.Left -ge $r.Left + 8 -and $c.Right -le $r.Right - 24 -and $c.Top -ge $r.Top + 180 -and $c.Bottom -le $r.Bottom - 8
    }
    $kinds = [System.Windows.Automation.ControlType]::ComboBox, [System.Windows.Automation.ControlType]::Edit
    $scroller = @($w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::IsScrollPatternAvailableProperty, $true))) |
        Where-Object { $_.Current.BoundingRectangle.Height -gt 300 }) | Select-Object -First 1
    for ($step = 0; $step -le 20; $step++) {
        foreach ($kind in $kinds) {
            $all = $w.FindAll('Descendants', (New-Object System.Windows.Automation.PropertyCondition($ae::ControlTypeProperty, $kind)))
            $field = @($all | Where-Object { & $inside $_ }) | Select-Object -First 1
            if ($field) { return $field }
        }
        if (-not $scroller) { return $null }
        $pattern = $scroller.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
        if ($pattern.Current.VerticalScrollPercent -ge 100) { return $null }
        $pattern.ScrollVertical([System.Windows.Automation.ScrollAmount]::LargeIncrement)
        Start-Sleep -Milliseconds 400
    }
}

# Measured in a picture of the window: the page low on the left, and the middle line of the first visible field
# (drop-down or text box; the median over its width, so a letter or the arrow does not decide it).
function Measure-Window($name) {
    $w = Find-Window
    $r = $w.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save("$out\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    $field = Find-Field $w
    if ($field) {
        # The picture again, now with the field scrolled into view.
        $g.Dispose(); $bmp.Dispose()
        $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
        $bmp.Save("$out\$name.png", [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $brightness = { param($p) ($p.R + $p.G + $p.B) / 3 }
    $fieldLevel = -1; $fieldText = 'no field'
    if ($field) {
        $c = $field.Current.BoundingRectangle
        $y = [int]($c.Y - $r.Y + $c.Height / 2)
        $levels = for ($x = [int]($c.X - $r.X + 6); $x -lt [int]($c.X - $r.X + $c.Width - 6); $x += 3) { & $brightness $bmp.GetPixel($x, $y) }
        $fieldLevel = ($levels | Sort-Object)[[int]($levels.Count / 2)]
        $fieldText = "field $([int]$fieldLevel) ($($field.Current.ControlType.ProgrammaticName))"
    }
    $page = $bmp.GetPixel(20, [int]($r.Height - 40))
    $g.Dispose(); $bmp.Dispose()
    $pageLevel = & $brightness $page
    return [pscustomobject]@{ PageLight = ($pageLevel -gt 128); FieldLight = ($fieldLevel -gt 128); FieldDark = ($fieldLevel -ge 0 -and $fieldLevel -le 128); HasField = [bool]$field; Text = "page $($page.R),$($page.G),$($page.B), $fieldText" }
}

function Set-SavedTheme($theme) {
    Get-Process StartQueue -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2
    $c = Get-Content $configFile -Raw | ConvertFrom-Json
    $c.settings.theme = $theme
    $c | ConvertTo-Json -Depth 10 | Set-Content $configFile -Encoding utf8
}

function Start-Window {
    Start-Process $exe
    Start-Sleep 8
    $w = Find-Window
    if ($w) { [void](Open-Settings $w) }
    return $w
}

$exe = Install-App
$data = "$env:APPDATA\StartQueue"
$configFile = "$data\config.json"
for ($i = 0; $i -lt 40 -and -not (Test-Path $configFile); $i++) { Start-Sleep 1 }
Start-Sleep 3

# Like Windows: Windows dark, the window opened, the settings shown, then Windows switched while it stays open.
Set-SavedTheme 'System'
Set-WindowsColorMode dark
$w = Start-Window
Test-Check 'window open with the settings' ($null -ne $w)
$m = Measure-Window '1-like-windows-dark'
Test-Check 'a field was found to measure' $m.HasField $m.Text
Test-Check 'like Windows, Windows dark: page and field dark' (-not $m.PageLight -and $m.FieldDark) $m.Text
Set-WindowsColorMode light
$m = Measure-Window '2-switched-to-light'
Test-Check 'switched to light while open: page and field light' ($m.PageLight -and $m.FieldLight) $m.Text
Set-WindowsColorMode dark
$m = Measure-Window '3-switched-to-dark'
Test-Check 'switched back to dark while open: page and field dark' (-not $m.PageLight -and $m.FieldDark) $m.Text

# The saved choice holds from the start, whatever Windows does.
Set-SavedTheme 'Light'
Set-WindowsColorMode dark
[void](Start-Window)
$m = Measure-Window '4-light-windows-dark'
Test-Check 'saved light, Windows dark, after a restart: page and field light' ($m.PageLight -and $m.FieldLight) $m.Text
Set-WindowsColorMode light
Set-WindowsColorMode dark
$m = Measure-Window '5-light-after-a-switch'
Test-Check 'saved light stays light when Windows switches while open' ($m.PageLight -and $m.FieldLight) $m.Text

Set-SavedTheme 'Dark'
Set-WindowsColorMode light
[void](Start-Window)
$m = Measure-Window '6-dark-windows-light'
Test-Check 'saved dark, Windows light, after a restart: page and field dark' (-not $m.PageLight -and $m.FieldDark) $m.Text

Complete-SandboxTest
