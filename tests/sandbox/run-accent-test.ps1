<#
.SYNOPSIS
    Tests StartQueue's accent color row in Windows Sandbox (accent-test.ps1), then checks the window for screen readers
    and the keyboard in light and dark (Test-DevAccessibility).
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox, next to this repository): the newest setup in
    out\setup goes to C:\sq in a fresh sandbox without network. The window opens inside the sandbox only; nothing comes
    to the front on this PC. The pictures of every accent land in <WorkFolder>\results\shots.
.EXAMPLE
    .\tests\sandbox\run-accent-test.ps1 -WorkFolder $env:TEMP\sq-accent
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    [int]$TimeoutMinutes = 20,
    # Without the accessibility check, which takes a few minutes.
    [switch]$NoAccessibility
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$setup = Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $setup) { throw 'Build the setup first: .\build.ps1 -Setup' }
$sandbox = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox'
& (Join-Path $sandbox 'Invoke-SandboxTest.ps1') -App StartQueue -Short sq -Setup $setup.FullName -TestScript (Join-Path $PSScriptRoot 'accent-test.ps1') `
    -WorkFolder $WorkFolder -TimeoutMinutes $TimeoutMinutes -Keep:(-not $NoAccessibility)
if ($NoAccessibility) { return }
Import-Module (Join-Path $sandbox 'DevKit.psm1') -Force
try {
    $findings = Test-DevAccessibility -ProcessName StartQueue -Exe '%LOCALAPPDATA%\Programs\StartQueue\StartQueue.exe'
    $findings | Set-Content (Join-Path $WorkFolder 'accessibility.txt') -Encoding utf8
    $findings
}
finally {
    Stop-DevSandbox -Owner "StartQueue test $PID"
}
