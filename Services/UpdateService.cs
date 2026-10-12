using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using FanShop.Services.Updates;
using FanShop.Updates;

namespace FanShop.Services;

public sealed class UpdateService : IDisposable
{
    private const string ReleaseUrl = "https://api.github.com/repos/kihort-si/FanShop/releases/latest";
    private readonly HttpClient _client;
    private ReleaseInfo? _release;
    public UpdateService() : this(new HttpClient()) { }
    public UpdateService(HttpClient client)
    {
        _client = client; _client.Timeout = TimeSpan.FromMinutes(15);
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FanShop", GetAppVersion()));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }
    public async Task<bool> CheckForUpdatesAsync()
    {
        if (!OperatingSystem.IsWindows() || UpdateBootstrap.IsHealthCheck) return false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var json = await _client.GetStringAsync(ReleaseUrl, timeout.Token);
            _release = JsonSerializer.Deserialize<ReleaseInfo>(json, UpdateManifest.JsonOptions);
            if (_release is null || _release.Draft || _release.Prerelease) return false;
            var version = _release.Tag_Name.TrimStart('v', 'V');
            return Version.TryParse(version, out var latest) && latest > Assembly.GetExecutingAssembly().GetName().Version
                && !UpdateState.IsCoolingDown(version) && SelectPackage(_release, version) is not null;
        }
        catch (Exception ex) { UpdateState.Log("Проверка обновлений не завершена; приложение продолжит работу.", ex); return false; }
    }
    public static (ReleaseAsset Zip, ReleaseAsset Checksum)? SelectPackage(ReleaseInfo release, string version)
    {
        var name = "FanShop" + version + ".zip";
        var zip = release.Assets.SingleOrDefault(a => a.Name == name);
        var checksum = release.Assets.SingleOrDefault(a => a.Name == name + ".sha256");
        return zip is null || checksum is null ? null : (zip, checksum);
    }
    public async Task<bool> UpdateAsync(IProgress<int>? progress = null)
    {
        if (!OperatingSystem.IsWindows() || _release is null) return false;
        string? work = null;
        try
        {
            var version = _release.Tag_Name.TrimStart('v', 'V');
            var assets = SelectPackage(_release, version) ?? throw new InvalidDataException("Не найдены ZIP и его контрольная сумма.");
            Directory.CreateDirectory(UpdateState.Root);
            work = Path.Combine(UpdateState.Root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var checksumText = await _client.GetStringAsync(ValidateDownloadUrl(assets.Checksum.Browser_Download_Url), timeout.Token);
            var checksum = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (checksum is null || checksum.Length != 64 || !checksum.All(Uri.IsHexDigit)) throw new InvalidDataException("Некорректная контрольная сумма ZIP.");
            var archive = Path.Combine(work, "package.zip");
            await DownloadAsync(ValidateDownloadUrl(assets.Zip.Browser_Download_Url), archive, progress, timeout.Token);
            var payload = Path.Combine(work, "payload");
            await Task.Run(() => UpdatePackage.Extract(archive, payload, version, checksum), timeout.Token);
            progress?.Report(95);
            var install = Path.GetFullPath(AppContext.BaseDirectory);
            var target = Path.Combine(install, ".fanshop-write-probe-" + Guid.NewGuid().ToString("N"));
            var needsElevation = false;
            try { using (File.Create(target)) { } File.Delete(target); }
            catch (UnauthorizedAccessException) { needsElevation = true; }
            using var parent = Process.GetCurrentProcess();
            var request = new UpdateRequest(install, payload, version, parent.Id, parent.StartTime.ToUniversalTime().Ticks,
                UpdateState.UserArguments(Environment.GetCommandLineArgs().Skip(1).ToArray()), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
                System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value);
            var requestPath = Path.Combine(work, "request.json"); AtomicFiles.WriteJson(requestPath, request);
            var start = new ProcessStartInfo(Path.Combine(payload, "FanShop.exe"))
            { UseShellExecute = needsElevation, WorkingDirectory = payload, CreateNoWindow = true };
            if (needsElevation) start.Verb = "runas";
            start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(requestPath);
            using var installer = Process.Start(start) ?? throw new IOException("Не удалось запустить установщик.");
            var readyPath = Path.Combine(work, "ready.json");
            var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
            var ready = false;
            try
            {
                while (!installer.HasExited && DateTimeOffset.UtcNow < deadline)
                {
                    if (File.Exists(readyPath))
                    {
                        var signal = JsonSerializer.Deserialize<HealthAcknowledgment>(File.ReadAllText(readyPath), UpdateManifest.JsonOptions);
                        if (signal?.Token == request.HealthToken && signal.ProcessId == installer.Id && signal.Version == version) { ready = true; break; }
                    }
                    await Task.Delay(100, timeout.Token);
                }
            }
            finally
            {
                if (!ready)
                {
                    if (!installer.HasExited) { installer.Kill(true); await installer.WaitForExitAsync(); }
                    WindowsUpdateHost.ClearRecoveryIfOwned(install, payload);
                }
            }
            if (!ready) throw new IOException("Установщик не подтвердил готовность. Приложение продолжит работу.");
            UpdateState.Log($"Пакет {version} проверен; установщик {installer.Id} ожидает штатного завершения процесса {parent.Id}.");
            return true;
        }
        catch (Exception ex)
        {
            UpdateState.Log("Обновление не запущено; установленная версия и данные сохранены.", ex);
            if (work is not null)
                try { Directory.Delete(work, true); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { UpdateState.Log("Временная загрузка не удалена.", cleanup); }
            return false;
        }
    }
    private static Uri ValidateDownloadUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/kihort-si/FanShop/releases/download/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Адрес пакета не относится к релизам FanShop.");
        return uri;
    }
    private async Task DownloadAsync(Uri url, string destination, IProgress<int>? progress, CancellationToken token)
    {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        const long max = 512 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > max) throw new IOException("ZIP обновления слишком велик.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920]; long total = 0; int read; var previousProgress = -1;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            total += read; if (total > max) throw new IOException("ZIP обновления слишком велик."); await output.WriteAsync(buffer.AsMemory(0, read), token);
            if (response.Content.Headers.ContentLength is > 0)
            {
                var percent = (int)(total * 90 / response.Content.Headers.ContentLength.Value);
                if (percent != previousProgress) { previousProgress = percent; progress?.Report(percent); }
            }
        }
        await output.FlushAsync(token);
    }
    public static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
    public void Dispose() => _client.Dispose();
}
public sealed class ReleaseInfo
{
    public string Tag_Name { get; set; } = "";
    public ReleaseAsset[] Assets { get; set; } = [];
    public bool Draft { get; set; }
    public bool Prerelease { get; set; }
}
public sealed class ReleaseAsset
{
    public string Name { get; set; } = "";
    public string Browser_Download_Url { get; set; } = "";
}
