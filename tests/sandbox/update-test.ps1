# Runs inside Windows Sandbox: installs StartQueue with the setup's default tasks, lets it find a newer version in a
# local feed (the stand-in for the GitHub release) and checks the whole update: the task "look for updates" is on by
# default, the app downloads and checks the setup, installs it quietly and comes back to the tray with --updated,
# without a window. Results go to C:\sq\results.
# run-update-test.ps1 maps this script (as test.ps1), SandboxKit.ps1, setup.exe (the older version) and upd\ (the
# newer setup) to C:\sq. Modelled on DynaTones' update-test.ps1.
. "$PSScriptRoot\SandboxKit.ps1"
Start-SandboxTest

# The feed as GitHub would give it, with the address and digest of the newer setup in this sandbox.
$newer = Get-ChildItem 'C:\sq\upd' -Filter 'StartQueue-Setup-*.exe' | Select-Object -First 1
$hash = (Get-FileHash $newer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
$version = [regex]::Match($newer.Name, 'Setup-(.+)\.exe').Groups[1].Value
$feed = 'C:\sq\upd\release.json'
@{
    tag_name = "v$version"; draft = $false; prerelease = $false; html_url = 'https://github.com/esteemworks/StartQueue/releases'
    assets = @(@{ name = $newer.Name; size = $newer.Length; browser_download_url = $newer.FullName; digest = "sha256:$hash" })
} | ConvertTo-Json -Depth 4 | Set-Content $feed -Encoding utf8
Write-Note "Feed: v$version, sha256 $hash"
$env:STARTQUEUE_UPDATE_FEED = $feed
[Environment]::SetEnvironmentVariable('STARTQUEUE_UPDATE_FEED', $feed, 'User')

# Installing by itself, so the test needs no click; the setup's task decides whether it looks at all.
$data = "$env:APPDATA\StartQueue"
New-Item -ItemType Directory $data -Force | Out-Null
'{ "autoInstall": true }' | Set-Content "$data\updates.json" -Encoding utf8

$exe = Install-App
$log = "$data\StartQueue.log"
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{6E4A7C2B-3D51-4F0A-9B8E-2C7D1A5F9E31}_is1'
$before = (Get-ItemProperty $key).DisplayVersion
Write-Note "Installed: $before"
$settings = Get-Content "$data\updates.json" -Raw | ConvertFrom-Json
Test-Check 'the setup turned the update check on by default' ($settings.check -eq $true)

# Settings the user changed, which the update has to keep (STANDARD.md, "Restorable"): a gap, a start delay and a
# profile, written while the old version runs, as its window would. Its file watcher reads them in.
$configFile = "$data\config.json"
for ($i = 0; $i -lt 40 -and -not (Test-Path $configFile); $i++) { Start-Sleep 1 }
$config = Get-Content $configFile -Raw | ConvertFrom-Json
$config.settings.gapSeconds = 7
$config.settings.startDelaySeconds = 4
$config.profiles = @([pscustomobject]@{ id = [guid]::NewGuid().ToString(); name = 'Survives the update'; color = 'ozean'; days = @('Monday'); from = ''; to = ''; outside = 'Next'; on = @() })
$config | ConvertTo-Json -Depth 8 | Set-Content $configFile -Encoding utf8
Write-Note "Settings changed before the update: gap 7 s, start delay 4 s, profile 'Survives the update'"

# The first look comes three minutes after the start. Heartbeat every 15 s, at most seven minutes.
$deadline = (Get-Date).AddMinutes(7)
$after = $before
while ((Get-Date) -lt $deadline) {
    Start-Sleep 15
    $after = (Get-ItemProperty $key -ErrorAction SilentlyContinue).DisplayVersion
    $last = (Get-Content $log -Tail 1 -ErrorAction SilentlyContinue)
    Write-Live "     ... version $after | $last"
    if ($after -and $after -ne $before -and (Get-Process StartQueue -ErrorAction SilentlyContinue)) { break }
}
Test-Check "updated to the newer version" ($after -like "$version*") "$before -> $after"
Start-Sleep 10

$lines = Get-Content $log -ErrorAction SilentlyContinue
Test-Check 'update found in the log' ([bool]($lines | Select-String -Pattern "Update found: StartQueue $version" -Quiet))
Test-Check 'installing in the log' ([bool]($lines | Select-String -Pattern "Installing StartQueue $version; the setup's log" -SimpleMatch -Quiet))
Test-Check 'came back as the new version' ([bool]($lines | Select-String -Pattern "StartQueue $version started.*\(--updated\)" -Quiet))
# The admin tier stays as the first installation set it up: the quiet update must not ask for admin rights.
Test-Check 'the update did not set up the admin tier again' (-not (Select-String -Path "$data\update-setup.log" -Pattern '--setup --admin' -SimpleMatch -Quiet)) ((Select-String -Path "$data\update-setup.log" -Pattern 'Parameters:' -SimpleMatch | ForEach-Object { $_.Line.Trim() }) -join ' | ')
Test-Check 'said it was updated' ([bool]($lines | Select-String -Pattern "was updated to $version" -Quiet))
Test-Check 'the setup log was kept' (Test-Path "$data\update-setup.log")

$process = Get-Process StartQueue -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check 'StartQueue runs again' ([bool]$process)
if ($process) {
    $line = (Get-CimInstance Win32_Process -Filter "ProcessId = $($process.Id)").CommandLine
    Test-Check 'started with --updated' ($line -match '--updated') $line
    Test-Check 'no window opened after the update' ($process.MainWindowHandle -eq 0)
}
$settings = Get-Content "$data\updates.json" -Raw | ConvertFrom-Json
Test-Check 'the settings kept the check on' ($settings.check -eq $true -and $settings.autoInstall -eq $true)
Test-Check 'About says it was updated' ($settings.lastResult -match [regex]::Escape($version)) $settings.lastResult
Write-Note "Last result: $($settings.lastResult)"

# The changed settings came through, and the old version backed them up before it installed the new one.
$config = Get-Content $configFile -Raw | ConvertFrom-Json
Test-Check 'the changed settings survived the update' ($config.settings.gapSeconds -eq 7 -and $config.settings.startDelaySeconds -eq 4 -and @($config.profiles | Where-Object name -eq 'Survives the update').Count -eq 1) "gap $($config.settings.gapSeconds), delay $($config.settings.startDelaySeconds), profiles $(@($config.profiles).name -join ', ')"
if ([version]$version -ge [version]'1.6.0') {
    # From 1.6 the profiles live in the family's file, taken over from config.json at the first start
    $shared = "$env:APPDATA\EsteemWorks\profiles.json"
    Test-Check "the profile moved to the family's profiles.json" ((Test-Path $shared) -and ((Get-Content $shared -Raw) -match 'Survives the update')) $shared
}
$backup = Get-ChildItem "$data\backups" -Filter "* update-$version.zip" -ErrorAction SilentlyContinue | Select-Object -First 1
Test-Check "a backup 'update-$version' is in backups" ([bool]$backup) ((Get-ChildItem "$data\backups" -ErrorAction SilentlyContinue).Name -join ', ')
if ($backup) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($backup.FullName)
    try {
        $names = @($zip.Entries.FullName)
        $reader = New-Object IO.StreamReader ($zip.GetEntry('esteemworks-settings.json').Open())
        $manifest = $reader.ReadToEnd() | ConvertFrom-Json
        $reader.Close()
        $saved = New-Object IO.StreamReader ($zip.GetEntry('config.json').Open())
        $savedConfig = $saved.ReadToEnd() | ConvertFrom-Json
        $saved.Close()
    } finally { $zip.Dispose() }
    Test-Check 'the backup holds config.json and its manifest' ($names -contains 'config.json' -and $manifest.app -eq 'StartQueue' -and $manifest.reason -eq "update-$version") "$($names -join ', '); $($manifest | ConvertTo-Json -Compress)"
    Test-Check 'the backup is the state from before the update, with the changes' ($savedConfig.settings.gapSeconds -eq 7 -and $manifest.appVersion -eq ($before -replace '^(\d+\.\d+\.\d+).*', '$1'))
}

# A second look finds nothing newer.
(Start-Process $exe -ArgumentList '--snapshot', "`"$($Kit.Results)\about`"" -WindowStyle Hidden -PassThru).WaitForExit()
$lines | Select-String -Pattern 'Update|update' | ForEach-Object { Write-Note $_.Line }
Complete-SandboxTest
