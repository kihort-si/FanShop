using System.Diagnostics;
using System.Text.Json;
using FanShop.Updates;
using Microsoft.Win32;

namespace FanShop.Services.Updates;

public sealed class WindowsUpdateHost : IUpdateHost
{
    public async Task WaitForParentAsync(UpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var parent = Process.GetProcessById(request.ParentProcessId);
            if (parent.StartTime.ToUniversalTime().Ticks != request.ParentStartUtcTicks) return;
            await parent.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(60), cancellationToken);
        }
        catch (ArgumentException) { } // The parent already exited; never kill a process by image name.
    }
    public void EnsureNoOtherInstances(string installDirectory)
    {
        var executable = Path.GetFullPath(Path.Combine(installDirectory, "FanShop.exe"));
        foreach (var process in Process.GetProcessesByName("FanShop"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                string? path;
                try { path = process.MainModule?.FileName; }
                catch (InvalidOperationException) { continue; }
                catch (System.ComponentModel.Win32Exception ex)
                { throw new IOException("Не удалось проверить другой процесс FanShop; установка отложена, чтобы не менять файлы работающего приложения.", ex); }
                if (path is not null && Path.GetFullPath(path).Equals(executable, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("В этой папке запущен другой экземпляр FanShop. Обновление будет повторено при следующем запуске.");
            }
        }
    }
    public void RegisterRecovery(string requestPath, string payloadDirectory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        var request = UpdateRequest.Read(requestPath);
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce")
            ?? throw new IOException("Не удалось зарегистрировать восстановление обновления.");
        // '!' keeps the entry until the recovery command has finished (including after interrupted installation).
        var command = $"\"{Path.Combine(payloadDirectory, "FanShop.exe")}\" --recover-update";
        if (command.Length > 260) throw new IOException("Путь к установщику слишком длинный для Windows RunOnce.");
        key.SetValue("!" + UpdateState.RecoveryName(request.InstallDirectory), command);
    }
    public void ClearRecovery(string installDirectory)
    {
        ClearRecoveryIfOwned(installDirectory, AppContext.BaseDirectory);
    }
    public static void ClearRecoveryIfOwned(string installDirectory, string payloadDirectory)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce", true);
        var name = "!" + UpdateState.RecoveryName(installDirectory);
        var expected = $"\"{Path.Combine(payloadDirectory, "FanShop.exe")}\" --recover-update";
        if (string.Equals(key?.GetValue(name) as string, expected, StringComparison.OrdinalIgnoreCase)) key?.DeleteValue(name, false);
    }
    public void SignalReady(string requestPath, UpdateRequest request) => AtomicFiles.WriteJson(
        Path.Combine(Path.GetDirectoryName(requestPath)!, "ready.json"), new HealthAcknowledgment(request.HealthToken, request.Version, Environment.ProcessId));
    public async Task<bool> StartAndVerifyAsync(UpdateRequest request, string requestPath, CancellationToken cancellationToken)
    {
        var health = Path.Combine(Path.GetDirectoryName(requestPath)!, "healthy.json");
        if (File.Exists(health)) File.Delete(health);
        using var process = Start(request, requestPath);
        var confirmed = false;
        try
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
            while (DateTimeOffset.UtcNow < deadline && !process.HasExited)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(health))
                {
                    var acknowledgment = JsonSerializer.Deserialize<HealthAcknowledgment>(File.ReadAllText(health), UpdateManifest.JsonOptions);
                    if (acknowledgment is not null && acknowledgment.Token == request.HealthToken && acknowledgment.Version == request.Version
                        && acknowledgment.ProcessId == process.Id)
                    { confirmed = true; return true; }
                }
                await Task.Delay(200, cancellationToken);
            }
            return false;
        }
        finally
        {
            if (!confirmed && !process.HasExited)
            {
                process.CloseMainWindow();
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { process.Kill(true); await process.WaitForExitAsync(); }
            }
        }
    }
    private static Process Start(UpdateRequest request, string? healthRequest = null)
    {
        var info = new ProcessStartInfo(Path.Combine(request.InstallDirectory, "FanShop.exe"))
        { WorkingDirectory = request.InstallDirectory, UseShellExecute = false };
        foreach (var argument in request.OriginalArguments) info.ArgumentList.Add(argument);
        if (healthRequest is not null) { info.ArgumentList.Add("--update-healthcheck"); info.ArgumentList.Add(healthRequest); }
        return Process.Start(info) ?? throw new IOException("Не удалось запустить FanShop.");
    }
    public void RestartPrevious(UpdateRequest request) { using var process = Start(request); }
    public void RecordFailure(string version, string reason) => UpdateState.RecordFailure(version, reason);
    public void Log(string message, Exception? exception = null) => UpdateState.Log(message, exception);
}
public sealed record HealthAcknowledgment(string Token, string Version, int ProcessId);
