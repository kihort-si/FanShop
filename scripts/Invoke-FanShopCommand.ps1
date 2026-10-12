function Invoke-FanShopCommand([string]$Executable, [string[]]$Parameters) {
    $info = [Diagnostics.ProcessStartInfo]::new([IO.Path]::GetFullPath($Executable))
    $info.UseShellExecute = $false
    $info.WorkingDirectory = Split-Path ([IO.Path]::GetFullPath($Executable))
    foreach ($argument in $Parameters) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    try {
        if (-not $process.WaitForExit(120000)) { $process.Kill($true); throw 'FanShop command timed out.' }
        return $process.ExitCode
    } finally { $process.Dispose() }
}
