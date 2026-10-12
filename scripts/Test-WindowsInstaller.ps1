param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [Parameter(Mandatory = $true)][string]$PackageDirectory
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Installer smoke tests require Windows.' }
$Installer = (Resolve-Path $Installer).Path
$PackageDirectory = (Resolve-Path $PackageDirectory).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ('FanShop-setup-test-' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $root 'installed app'
New-Item -ItemType Directory $root | Out-Null
. (Join-Path $PSScriptRoot 'Invoke-FanShopCommand.ps1')
try {
    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/TASKS=', "/DIR=$target", "/LOG=$root\setup.log")
    if ((Invoke-FanShopCommand $Installer $arguments) -ne 0) { throw 'Silent EXE installation failed.' }
    $manifest = Get-Content (Join-Path $PackageDirectory 'fanshop-update-manifest.json') -Raw | ConvertFrom-Json
    foreach ($file in $manifest.Files) {
        $path = Join-Path $target $file.Path
        if (-not (Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -ne $file.Size -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.Sha256) {
            throw "Installer did not install the expected file: $($file.Path)"
        }
    }
    if ((Get-FileHash (Join-Path $target 'fanshop-update-manifest.json')).Hash -ne (Get-FileHash (Join-Path $PackageDirectory 'fanshop-update-manifest.json')).Hash) { throw 'Installed manifest differs from the ZIP manifest.' }
    if ((Invoke-FanShopCommand (Join-Path $target 'FanShop.exe') @('--update-health-probe')) -ne 0) { throw 'Installed application runtime did not launch.' }
    $uninstaller = Join-Path $target 'unins000.exe'
    if (-not (Test-Path $uninstaller)) { throw 'Uninstaller missing.' }
    Set-Content (Join-Path $target 'personal-notes.txt') 'preserve user files' -Encoding utf8NoBOM
    if ((Invoke-FanShopCommand $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART')) -ne 0) { throw 'Silent uninstall failed.' }
    if (Test-Path (Join-Path $target 'FanShop.exe')) { throw 'Application remained after uninstall.' }
    if (-not (Test-Path (Join-Path $target 'personal-notes.txt'))) { throw 'Uninstaller removed a personal file.' }
    Write-Host 'EXE installer verified: all payload hashes, runtime startup, uninstall and personal-file preservation.'
} finally {
    if (Test-Path $root) { Remove-Item $root -Recurse -Force }
}
