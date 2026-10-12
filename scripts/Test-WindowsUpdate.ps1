param([Parameter(Mandatory = $true)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Native update smoke tests require Windows.' }
$PackageDirectory = (Resolve-Path $PackageDirectory).Path

. (Join-Path $PSScriptRoot 'Invoke-FanShopCommand.ps1')

$temp = Join-Path ([IO.Path]::GetTempPath()) ('FanShop-update-smoke-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $temp | Out-Null
try {
    foreach ($scenario in @('success', 'rollback', 'interruption')) {
        $fail = $scenario -ne 'success'
        $case = Join-Path $temp $scenario
        $install = Join-Path $case 'installed app'
        $work = Join-Path $case 'work'
        $payload = Join-Path $work 'payload'
        New-Item -ItemType Directory -Force $install, $payload | Out-Null
        Get-ChildItem -LiteralPath $PackageDirectory | Copy-Item -Destination $install -Recurse
        Get-ChildItem -LiteralPath $PackageDirectory | Copy-Item -Destination $payload -Recurse
        Set-Content (Join-Path $install 'version.marker') 'old' -Encoding utf8NoBOM
        Set-Content (Join-Path $install 'obsolete.dll') 'obsolete owned component' -Encoding utf8NoBOM
        if ((Invoke-FanShopCommand "$PackageDirectory/FanShop.exe" @('--write-update-manifest', $install, 'none')) -ne 0) { throw 'Old fixture manifest failed.' }
        Set-Content (Join-Path $install 'personal-notes.txt') 'preserve me' -Encoding utf8NoBOM
        Set-Content (Join-Path $install 'settings.json') '{"user":"preserve"}' -Encoding utf8NoBOM
        $oldManifest = [IO.File]::ReadAllText((Join-Path $install 'fanshop-update-manifest.json'))
        Set-Content (Join-Path $payload 'version.marker') 'new' -Encoding utf8NoBOM
        Set-Content (Join-Path $payload 'new-feature.txt') 'new' -Encoding utf8NoBOM
        if ((Invoke-FanShopCommand "$PackageDirectory/FanShop.exe" @('--write-update-manifest', $payload, 'none')) -ne 0) { throw 'New fixture manifest failed.' }
        $manifest = Get-Content (Join-Path $payload 'fanshop-update-manifest.json') -Raw | ConvertFrom-Json
        $request = @{
            InstallDirectory = $install; PayloadDirectory = $payload; Version = $manifest.Version
            ParentProcessId = [int]::MaxValue; ParentStartUtcTicks = 1
            OriginalArguments = @($(if ($scenario -eq 'interruption') { '--update-probe-hang' } elseif ($fail) { '--update-probe-fail' } else { '--update-health-probe' }))
            HealthToken = ('AB' * 32)
        }
        $requestPath = Join-Path $work 'request.json'
        $request | ConvertTo-Json -Depth 5 | Set-Content $requestPath -Encoding utf8NoBOM
        if ($scenario -eq 'interruption') {
            $info = [Diagnostics.ProcessStartInfo]::new("$payload/FanShop.exe")
            $info.UseShellExecute = $false
            $info.ArgumentList.Add('--apply-update'); $info.ArgumentList.Add($requestPath)
            $helper = [Diagnostics.Process]::Start($info)
            try {
                $deadline = [DateTime]::UtcNow.AddSeconds(30)
                while (-not (Test-Path (Join-Path $work 'probe-started.json')) -and -not $helper.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
                if (-not (Test-Path (Join-Path $work 'probe-started.json'))) { throw 'Interrupted startup probe did not begin.' }
                $childPid = (Get-Content (Join-Path $work 'probe-started.json') -Raw | ConvertFrom-Json).ProcessId
                $helper.Kill($true); $helper.WaitForExit()
                try { $child = [Diagnostics.Process]::GetProcessById($childPid); if (-not $child.WaitForExit(10000)) { throw 'Child process did not exit.' }; $child.Dispose() } catch [ArgumentException] { }
            } finally { if (-not $helper.HasExited) { $helper.Kill($true) }; $helper.Dispose() }
            if (-not (Test-Path (Join-Path $install '.fanshop-update-journal.json'))) { throw 'No recovery journal after forced interruption.' }
            $exit = Invoke-FanShopCommand "$payload/FanShop.exe" @('--recover-update', $requestPath)
        } else {
            $exit = Invoke-FanShopCommand "$payload/FanShop.exe" @('--apply-update', $requestPath)
        }
        if (-not (Test-Path (Join-Path $install 'personal-notes.txt')) -or -not (Test-Path (Join-Path $install 'settings.json'))) { throw 'User files were removed.' }
        if (Test-Path (Join-Path $install '.fanshop-update-journal.json')) { throw 'Update left an unfinished journal.' }
        if ($fail) {
            $expectedExit = if ($scenario -eq 'interruption') { 0 } else { 1 }
            if ($exit -ne $expectedExit -or (Get-Content (Join-Path $install 'version.marker')).Trim() -ne 'old' -or -not (Test-Path (Join-Path $install 'obsolete.dll')) -or (Test-Path (Join-Path $install 'new-feature.txt'))) { throw 'Rollback did not restore old files.' }
            if ([IO.File]::ReadAllText((Join-Path $install 'fanshop-update-manifest.json')) -ne $oldManifest) { throw 'Old manifest was not restored.' }
        } else {
            if ($exit -ne 0 -or (Get-Content (Join-Path $install 'version.marker')).Trim() -ne 'new' -or (Test-Path (Join-Path $install 'obsolete.dll')) -or -not (Test-Path (Join-Path $install 'new-feature.txt'))) { throw 'Success scenario failed.' }
            if (-not (Test-Path (Join-Path $work 'healthy.json'))) { throw 'No successful startup acknowledgment.' }
        }
        Write-Host "Native Windows updater verified: $scenario"
    }
} finally {
    Remove-Item $temp -Recurse -Force
}
