<#
.SYNOPSIS
    Tests StartQueue together with DynaTones in Windows Sandbox: the family's autostart (family-test.ps1).
.DESCRIPTION
    Uses the family's sandbox runner (EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1, next to this repository):
    StartQueue's setup from .\build.ps1 -Setup and DynaTones' setup from ..\DynaTones\out\setup go to C:\sq in a
    fresh sandbox without network. Never brings a window to the front.
.EXAMPLE
    .\tests\sandbox\run-family-test.ps1 -WorkFolder $env:TEMP\sq-family
#>
param(
    [Parameter(Mandatory)] [string]$WorkFolder,
    [string]$DynaTonesSetupFolder,
    [int]$TimeoutMinutes = 15,
    [switch]$Keep
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$setup = Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if (-not $setup) { throw 'Build the setup first: .\build.ps1 -Setup' }
if (-not $DynaTonesSetupFolder) { $DynaTonesSetupFolder = Join-Path $root '..\DynaTones\out\setup' }
if (-not (Get-ChildItem $DynaTonesSetupFolder -Filter 'DynaTones-Setup-*.exe' -ErrorAction SilentlyContinue)) {
    throw "No DynaTones setup in $DynaTonesSetupFolder; build it there with .\build.ps1 -Setup"
}
$runner = Join-Path $root '..\EsteemWorks.Kit\tools\sandbox\Invoke-SandboxTest.ps1'
& $runner -App StartQueue -Short sq -Setup $setup.FullName -TestScript (Join-Path $PSScriptRoot 'family-test.ps1') `
    -WorkFolder $WorkFolder -Extra @{ dt = (Resolve-Path $DynaTonesSetupFolder).Path } -TimeoutMinutes $TimeoutMinutes -Keep:$Keep
