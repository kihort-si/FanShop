param(
    [Parameter(Mandatory = $true)][string]$PackageDirectory,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutputDirectory = 'artifacts'
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'The EXE installer must be built on Windows.' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid installer version.' }
$PackageDirectory = (Resolve-Path $PackageDirectory).Path
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory = (Resolve-Path $OutputDirectory).Path
$manifest = Get-Content (Join-Path $PackageDirectory 'fanshop-update-manifest.json') -Raw | ConvertFrom-Json
if ($manifest.Version -ne $Version) { throw 'Installer version does not match the verified package.' }
. (Join-Path $PSScriptRoot 'Invoke-FanShopCommand.ps1')
if ((Invoke-FanShopCommand (Join-Path $PackageDirectory 'FanShop.exe') @('--verify-update-package', $PackageDirectory)) -ne 0) { throw 'Installer source verification failed.' }

# Inno Setup is included in the GitHub-hosted Windows image; locate it without a third-party action.
$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if (-not $compiler) {
    foreach ($programs in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if (-not $programs) { continue }
        $candidate = Get-ChildItem -Path (Join-Path $programs 'Inno Setup*\ISCC.exe') -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($candidate) { $compiler = $candidate.FullName; break }
    }
}
if (-not $compiler) { throw 'ISCC.exe not found. Install Inno Setup 6.3 or newer on the Windows build runner.' }
$script = (Resolve-Path (Join-Path $PSScriptRoot '..\installer\FanShop.iss')).Path
& $compiler "/DAppVersion=$Version" "/DPublishDir=$PackageDirectory" "/DInstallerOutputDir=$OutputDirectory" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$setup = Join-Path $OutputDirectory "FanShopSetup-$Version.exe"
if (-not (Test-Path $setup)) { throw 'Inno Setup did not produce the expected installer.' }
"$((Get-FileHash $setup -Algorithm SHA256).Hash)  $([IO.Path]::GetFileName($setup))" | Set-Content "$setup.sha256" -Encoding utf8NoBOM
Write-Host "Installer ready: $setup"
