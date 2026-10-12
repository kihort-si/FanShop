using System.Diagnostics;
using FanShop.Updates;

namespace FanShop.Services.Updates;

public static class UpdateBootstrap
{
    private static string? _healthRequest;
    public static bool IsHealthCheck => _healthRequest is not null;
    public static int? RunCommand(string[] arguments)
    {
        try
        {
            if (arguments.FirstOrDefault() == "--write-update-manifest")
            {
                if (arguments.Length != 3) throw new ArgumentException("--write-update-manifest <publish-directory> <previous-zip-or-none>");
                var manifest = UpdateManifest.Build(arguments[1], UpdateService.GetAppVersion(), arguments[2] == "none" ? null : arguments[2]);
                manifest.Write(arguments[1]); manifest.Verify(arguments[1]); return 0;
            }
            if (arguments.FirstOrDefault() == "--verify-update-package")
            {
                if (arguments.Length != 2) throw new ArgumentException("--verify-update-package <publish-directory>");
                UpdateManifest.Read(arguments[1]).Verify(arguments[1]); return 0;
            }
            if (arguments.FirstOrDefault() == "--verify-update-archive")
            {
                if (arguments.Length != 2) throw new ArgumentException("--verify-update-archive <zip>");
                var temp = Path.Combine(Path.GetTempPath(), "FanShop-package-verification-" + Guid.NewGuid().ToString("N"));
                try { UpdatePackage.Extract(arguments[1], temp, UpdateService.GetAppVersion(), UpdateManifest.HashFile(arguments[1])); }
                finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
                return 0;
            }
            if (arguments.FirstOrDefault() is "--apply-update" or "--recover-update")
            {
                if (!OperatingSystem.IsWindows() || arguments.Length > 2) throw new ArgumentException("Некорректный запуск установщика.");
                var requestPath = arguments.Length == 2 ? arguments[1] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "request.json"));
                var request = UpdateRequest.Read(requestPath);
                if (request.UserSid is not null && request.UserSid != System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value)
                    throw new IOException("Обновление должно выполняться под той же учётной записью Windows, чтобы сохранить профиль и данные пользователя.");
                if (!Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(
                        Path.GetFullPath(request.PayloadDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Установщик должен запускаться из проверенного пакета.");
                if (arguments[0] == "--apply-update" && request.Version != UpdateService.GetAppVersion()) throw new IOException("Версия установщика не совпадает с запросом.");
                var success = new UpdateRunner(new WindowsUpdateHost()).RunAsync(requestPath, arguments[0] == "--recover-update").GetAwaiter().GetResult();
                return success ? 0 : 1;
            }
            var healthIndex = Array.IndexOf(arguments, "--update-healthcheck");
            if (healthIndex >= 0)
            {
                if (healthIndex + 1 >= arguments.Length) throw new ArgumentException("Нет запроса проверки запуска.");
                var path = Path.GetFullPath(arguments[healthIndex + 1]);
                var request = UpdateRequest.Read(path);
                if (!Path.GetFullPath(request.InstallDirectory).TrimEnd(Path.DirectorySeparatorChar).Equals(
                        Path.GetFullPath(AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                    || request.Version != UpdateService.GetAppVersion()) throw new IOException("Запрос подтверждения относится к другой установке.");
                _healthRequest = path;
            }
            if (arguments.Contains("--update-probe-fail")) return 42;
            if (arguments.Contains("--update-probe-hang"))
            {
                if (IsHealthCheck)
                {
                    AtomicFiles.WriteJson(Path.Combine(Path.GetDirectoryName(_healthRequest)!, "probe-started.json"), new { ProcessId = Environment.ProcessId });
                    Thread.Sleep(TimeSpan.FromSeconds(60)); // CI deliberately kills this startup to exercise crash recovery.
                }
                return 0;
            }
            if (arguments.Contains("--update-health-probe"))
            {
                ConfirmHealthy();
                // The CI probe remains alive until the installer commits, just like the real application.
                var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
                while (IsHealthCheck && File.Exists(Path.Combine(AppContext.BaseDirectory, InstallationTransaction.JournalName)) && DateTimeOffset.UtcNow < deadline)
                    Thread.Sleep(100);
                return 0;
            }
            if (!IsHealthCheck && OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory, InstallationTransaction.JournalName)))
            {
                var journal = System.Text.Json.JsonSerializer.Deserialize<InstallationJournal>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, InstallationTransaction.JournalName)), UpdateManifest.JsonOptions)
                    ?? throw new IOException("Не прочитан журнал восстановления.");
                var requestPath = Path.Combine(journal.WorkDirectory, "request.json");
                var request = UpdateRequest.Read(requestPath);
                // Wait for this process too; it may still hold installed assemblies while recovery starts.
                using var current = Process.GetCurrentProcess();
                AtomicFiles.WriteJson(requestPath, request with { ParentProcessId = current.Id, ParentStartUtcTicks = current.StartTime.ToUniversalTime().Ticks });
                var info = new ProcessStartInfo(Path.Combine(request.PayloadDirectory, "FanShop.exe")) { UseShellExecute = false, WorkingDirectory = request.PayloadDirectory };
                info.ArgumentList.Add("--recover-update"); info.ArgumentList.Add(requestPath);
                using var recovery = Process.Start(info) ?? throw new IOException("Не удалось запустить восстановление.");
                return 0;
            }
            return null;
        }
        catch (Exception ex) { UpdateState.Log("Ошибка загрузчика обновлений.", ex); Console.Error.WriteLine(ex.Message); return 1; }
    }
    public static void ConfirmHealthy()
    {
        if (_healthRequest is null) return;
        var request = UpdateRequest.Read(_healthRequest);
        AtomicFiles.WriteJson(Path.Combine(Path.GetDirectoryName(_healthRequest)!, "healthy.json"),
            new HealthAcknowledgment(request.HealthToken, UpdateService.GetAppVersion(), Environment.ProcessId));
    }
    public static async Task CleanLegacyFilesAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            await Task.Run(() =>
            {
                UpdateCache.Cleanup(UpdateState.Root);
                if (IsHealthCheck) return;
                var removed = LegacyPackageCleanup.Run(AppContext.BaseDirectory, Path.Combine(UpdateState.Root, "legacy-backup"),
                    ex => UpdateState.Log("Не удалён старый файл; очистка будет повторена.", ex));
                if (removed.Count > 0) UpdateState.Log($"Первый переход: перенесено в резервную копию устаревших файлов: {removed.Count}.");
            });
        }
        catch (Exception ex) { UpdateState.Log("Очистка старой поставки отложена.", ex); }
    }
}
