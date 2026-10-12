using System.Text.Json;

namespace FanShop.Updates;

public sealed record UpdateRequest(string InstallDirectory, string PayloadDirectory, string Version, int ParentProcessId,
    long ParentStartUtcTicks, string[] OriginalArguments, string HealthToken, string? UserSid = null)
{
    public static UpdateRequest Read(string path)
    {
        if (new FileInfo(path).Length > 64 * 1024) throw new InvalidDataException("Слишком большой запрос обновления.");
        var request = JsonSerializer.Deserialize<UpdateRequest>(File.ReadAllText(path), UpdateManifest.JsonOptions)
            ?? throw new InvalidDataException("Некорректный запрос обновления.");
        if (!System.Version.TryParse(request.Version, out _) || request.ParentProcessId <= 0 || request.OriginalArguments is null
            || request.OriginalArguments.Length > 100 || request.HealthToken.Length != 64 || !request.HealthToken.All(Uri.IsHexDigit))
            throw new InvalidDataException("Некорректные параметры обновления.");
        return request;
    }
}

public interface IUpdateHost
{
    Task WaitForParentAsync(UpdateRequest request, CancellationToken cancellationToken);
    void EnsureNoOtherInstances(string installDirectory);
    void RegisterRecovery(string requestPath, string payloadDirectory);
    void SignalReady(string requestPath, UpdateRequest request);
    void ClearRecovery(string installDirectory);
    Task<bool> StartAndVerifyAsync(UpdateRequest request, string requestPath, CancellationToken cancellationToken);
    void RestartPrevious(UpdateRequest request);
    void RecordFailure(string version, string reason);
    void Log(string message, Exception? exception = null);
}

public sealed class UpdateRunner(IUpdateHost host)
{
    public async Task<bool> RunAsync(string requestPath, bool recover = false, CancellationToken cancellationToken = default)
    {
        var request = UpdateRequest.Read(requestPath);
        var work = Path.GetDirectoryName(Path.GetFullPath(requestPath))!;
        var parentExited = false;
        try
        {
            using var transaction = new InstallationTransaction(request.InstallDirectory, work);
            if (recover)
            {
                await host.WaitForParentAsync(request, cancellationToken); parentExited = true;
                host.EnsureNoOtherInstances(request.InstallDirectory);
                if (File.Exists(Path.Combine(request.InstallDirectory, InstallationTransaction.JournalName))) transaction.Rollback();
                host.ClearRecovery(request.InstallDirectory); host.RestartPrevious(request); return true;
            }
            var manifest = UpdateManifest.Read(request.PayloadDirectory);
            if (!System.Version.Parse(manifest.Version).Equals(System.Version.Parse(request.Version))) throw new InvalidDataException("Версия запроса и пакета не совпадает.");
            manifest.Verify(request.PayloadDirectory);
            host.RegisterRecovery(requestPath, request.PayloadDirectory);
            host.SignalReady(requestPath, request);
            await host.WaitForParentAsync(request, cancellationToken); parentExited = true;
            host.EnsureNoOtherInstances(request.InstallDirectory);
            try
            {
                var removed = transaction.Apply(request.PayloadDirectory, manifest);
                host.Log($"Установлены файлы версии {manifest.Version}; удалено устаревших: {removed.Count}.");
                if (!await host.StartAndVerifyAsync(request, requestPath, cancellationToken))
                    throw new IOException("Новая версия не подтвердила успешный запуск.");
                transaction.Commit();
                try { host.ClearRecovery(request.InstallDirectory); } catch (Exception ex) { host.Log("Не удалось убрать RunOnce после завершённого обновления.", ex); }
                host.Log("Обновление завершено и запуск новой версии подтверждён."); return true;
            }
            catch
            {
                transaction.Rollback(); throw;
            }
        }
        catch (Exception ex)
        {
            host.Log("Обновление не завершено.", ex);
            try { host.RecordFailure(request.Version, ex.Message); } catch (Exception stateError) { host.Log("Не сохранено состояние неудачного обновления.", stateError); }
            if (!File.Exists(Path.Combine(request.InstallDirectory, InstallationTransaction.JournalName)))
            {
                try
                {
                    // Do not restart into another installer's active transaction or create a second GUI instance.
                    using var check = new InstallationTransaction(request.InstallDirectory, work);
                    host.ClearRecovery(request.InstallDirectory);
                    AtomicFiles.WriteJson(Path.Combine(work, "completed.json"), new { Success = false, Version = request.Version, Time = DateTimeOffset.UtcNow });
                    if (parentExited) { host.EnsureNoOtherInstances(request.InstallDirectory); host.RestartPrevious(request); }
                }
                catch (Exception restartError) { host.Log("Повторный запуск отложен; проверьте журнал обновлений.", restartError); }
            }
            return false;
        }
    }
}
