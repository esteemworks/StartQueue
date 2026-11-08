<#
.SYNOPSIS
    Builds StartQueue into out\app: the app, the context menu DLL and the package manifest.
.NOTES
    Needs the .NET 10 SDK and Visual Studio Build Tools with the C++ workload.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    # Brings its own .NET, for devices where the .NET 10 Desktop Runtime cannot be installed (no admin rights).
    [switch]$SelfContained,
    # Also builds out\setup\StartQueue-Setup-<version>.exe (Inno Setup); always self-contained.
    [switch]$Setup,
    # Takes the compiled kit in kit\ even when the kit's sources are next to this repository, as anyone building the
    # public repository does. Releases are built this way, so what is published is what the repository builds.
    [switch]$KitPackage
)

$ErrorActionPreference = 'Stop'
if ($Setup) { $SelfContained = [switch]::new($true) }
$root = $PSScriptRoot
$out = Join-Path $root 'out\app'
$obj = Join-Path $root 'out\obj'
$project = Join-Path $root 'src\StartQueue\StartQueue.csproj'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out, $obj | Out-Null

# Icons and logos are drawn by the app itself; create them once if missing: the .ico, the package logos and the
# tile as PNG up to 2048 px.
if (-not (Test-Path (Join-Path $root 'assets\StartQueue.ico')) -or -not (Test-Path (Join-Path $root 'assets\png\StartQueue-2048.png'))) {
    Write-Host 'Drawing icons'
    dotnet build $project -c $Configuration -o (Join-Path $root 'out\bootstrap') -v quiet
    if ($LASTEXITCODE) { throw 'Bootstrap build failed' }
    # Not -Wait: Windows PowerShell would also wait for anything the app starts.
    (Start-Process (Join-Path $root 'out\bootstrap\StartQueue.exe') -ArgumentList '--write-assets', "`"$(Join-Path $root 'assets')`"" -PassThru).WaitForExit()
}

Write-Host 'Building app'
$kitArgs = @()
if ($KitPackage) {
    # An empty path does not exist, so the project takes the package (StartQueue.csproj)
    $kitArgs = @('-p:EsteemWorksKitSource=')
    Write-Host "Kit: the package in kit\ ($(([xml](Get-Content (Join-Path $root 'kit\EsteemWorks.Kit.props'))).Project.PropertyGroup.EsteemWorksKitVersion))"
}
dotnet publish $project -c $Configuration -r win-x64 --self-contained $($SelfContained.IsPresent.ToString().ToLower()) -o $out -v quiet @kitArgs
if ($LASTEXITCODE) { throw 'App build failed' }

# The license texts of .NET go along with it, as its MIT license asks; "Privacy and notices" in the app points here.
$dotnetDir = Split-Path (Get-Command dotnet).Source
$notices = @(
    'StartQueue uses the following components by third parties.'
    ''
    '==== .NET runtime and Windows Presentation Foundation (MIT license) ===='
    ''
    (Get-Content (Join-Path $dotnetDir 'LICENSE.txt') -Raw)
    ''
    '==== Third-party notices of .NET ===='
    ''
    (Get-Content (Join-Path $dotnetDir 'ThirdPartyNotices.txt') -Raw)
)
$notices -join "`r`n" | Set-Content (Join-Path $out 'THIRD-PARTY-NOTICES.txt') -Encoding utf8

Write-Host 'Building context menu extension'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio Build Tools with C++ not found' }
$script = Join-Path $obj 'build-shell.cmd'
@"
@echo off
set "PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer;%PATH%"
call "$vs\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1
cl /nologo /std:c++17 /O2 /EHsc /W4 /LD /DUNICODE /D_UNICODE "$root\shell\StartQueueShell.cpp" /Fo"$obj\\" /link /DEF:"$root\shell\StartQueueShell.def" shlwapi.lib ole32.lib shell32.lib /OUT:"$out\StartQueueShell.dll" || exit /b 1
"@ | Set-Content $script -Encoding ascii
cmd /c "`"$script`""
if ($LASTEXITCODE) { throw 'Context menu build failed' }
Remove-Item (Join-Path $out 'StartQueueShell.exp'), (Join-Path $out 'StartQueueShell.lib') -ErrorAction SilentlyContinue

Write-Host 'Packing and signing the context menu package'
# Explorer only loads context menus from signed packages. The package holds just the manifest and logos;
# the DLL and the app stay in the install folder (ExternalLocation).
$kit = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" | Sort-Object FullName | Select-Object -Last 1
if (-not $kit) { throw 'Windows SDK (makeappx.exe) not found' }
$makeappx = $kit.FullName
$signtool = Join-Path $kit.DirectoryName 'signtool.exe'

$staging = Join-Path $obj 'package'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $staging 'Assets') | Out-Null
Copy-Item (Join-Path $root 'package\AppxManifest.xml') $staging
Copy-Item (Join-Path $root 'assets\*.png') (Join-Path $staging 'Assets')

# Own signing certificate, created once; the private key stays in the current user's store.
$cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq 'CN=StartQueue' -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
    Sort-Object NotAfter | Select-Object -Last 1
if (-not $cert) {
    Write-Host 'Creating signing certificate CN=StartQueue'
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=StartQueue' -FriendlyName 'StartQueue package signing' `
        -CertStoreLocation Cert:\CurrentUser\My -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(10) `
        -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
}

# Sign the files the package points to as well, like other context menu packages do.
& $signtool sign /q /fd SHA256 /sha1 $cert.Thumbprint /s My (Join-Path $out 'StartQueueShell.dll') (Join-Path $out 'StartQueue.exe')
if ($LASTEXITCODE) { throw 'signtool failed for the app files' }

$msix = Join-Path $out 'StartQueue.msix'
& $makeappx pack /d $staging /p $msix /nv /o | Out-Null
if ($LASTEXITCODE) { throw 'makeappx failed' }
& $signtool sign /q /fd SHA256 /sha1 $cert.Thumbprint /s My $msix
if ($LASTEXITCODE) { throw 'signtool failed' }
Export-Certificate -Cert $cert -FilePath (Join-Path $out 'StartQueue.cer') | Out-Null

Copy-Item (Join-Path $root 'install.ps1'), (Join-Path $root 'uninstall.ps1') $out
Write-Host "Done: $out"

if ($Setup) {
    Write-Host 'Building setup'
    $iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found (winget install JRSoftware.InnoSetup)' }
    # Public signing, when it is set up on this PC (the kit's tools\sign.ps1, environment variables): the program files
    # get the real signature instead of the own certificate above, and Inno Setup signs the setup and its uninstaller
    # through the same script. The context menu package keeps its own certificate, which its manifest names.
    # Without it the setup is built as before and one line says so.
    $sign = Join-Path $root '..\EsteemWorks.Kit\tools\sign.ps1'
    $signing = @()
    if ((Test-Path $sign) -and (& $sign -Check)) {
        & $sign -Description 'StartQueue' -Files (@('StartQueue.exe', 'StartQueue.dll', 'StartQueueShell.dll', 'EsteemWorks.Kit.dll') |
            ForEach-Object { Join-Path $out $_ } | Where-Object { Test-Path $_ })
        $signing = @('/DSign', "/Sesteemworks=powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$((Resolve-Path $sign).Path)`" -Description StartQueue -Files `$f")
    }
    elseif (Test-Path $sign) { & $sign }
    & $iscc /Q "/DSourceDir=$out" @signing (Join-Path $root 'installer\StartQueue.iss')
    if ($LASTEXITCODE) { throw 'Setup build failed' }
    Get-ChildItem (Join-Path $root 'out\setup') -Filter 'StartQueue-Setup-*.exe' | Sort-Object LastWriteTime | Select-Object -Last 1 |
        ForEach-Object { Write-Host "Setup: $($_.FullName) ($([Math]::Round($_.Length / 1MB)) MB)" }
}