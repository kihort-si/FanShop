using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Application = System.Windows.Application;

namespace FanShop.Services
{
    public class UpdateService
    {
        private const string GITHUB_API_URL = "https://api.github.com/repos/kihort-si/FanShop/releases/latest";
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FanShop", "1.0"));
        }

        public async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                var currentVersion = GetCurrentVersion();
                var latestRelease = await GetLatestReleaseInfoAsync();

                if (latestRelease == null)
                    return false;

                var latestVersionString = latestRelease.TagName.StartsWith("v")
                    ? latestRelease.TagName.Substring(1)
                    : latestRelease.TagName;

                if (!Version.TryParse(latestVersionString, out var latestVersion))
                    return false;

                return latestVersion > currentVersion;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при проверке обновлений: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateAsync()
        {
            try
            {
                var latestRelease = await GetLatestReleaseInfoAsync();
                if (latestRelease == null || latestRelease.Assets.Length == 0)
                    return false;

                string downloadUrl = null;
                foreach (var asset in latestRelease.Assets)
                {
                    if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.BrowserDownloadUrl;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    return false;

                var appPath = Process.GetCurrentProcess().MainModule.FileName;
                var tempFilePath = Path.Combine(Path.GetDirectoryName(appPath), "update.exe");

                await DownloadFileAsync(downloadUrl, tempFilePath);

                CreateUpdateScript(appPath, tempFilePath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при обновлении: {ex.Message}");
                return false;
            }
        }

        public void ExecuteUpdate()
        {
            string updaterPath = Path.Combine(
                Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                "updater.bat");

            if (File.Exists(updaterPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                Application.Current.Shutdown();
            }
        }

        private Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        private async Task<ReleaseInfo> GetLatestReleaseInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GITHUB_API_URL);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<ReleaseInfo>(response, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при получении информации о релизе: {ex.Message}");
                return null;
            }
        }

        private async Task DownloadFileAsync(string url, string destinationPath)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
        }

        private void CreateUpdateScript(string appPath, string updateFilePath)
        {
            var updaterPath = Path.Combine(Path.GetDirectoryName(appPath), "updater.bat");
            var appFileName = Path.GetFileName(appPath);

            var scriptContent = $@"@echo off
timeout /t 2 /nobreak > nul
echo Обновление FanShop...
if exist backup.exe del backup.exe
copy ""{appFileName}"" backup.exe
del ""{appFileName}""
copy update.exe ""{appFileName}""
del update.exe
start """" ""{appFileName}""
del ""%~f0""
";

            File.WriteAllText(updaterPath, scriptContent);
        }
    }

    public class ReleaseInfo
    {
        public string TagName { get; set; }
        public ReleaseAsset[] Assets { get; set; }
    }

    public class ReleaseAsset
    {
        public string Name { get; set; }
        public string BrowserDownloadUrl { get; set; }
    }
}