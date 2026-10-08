using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FanShop.Updates;

namespace FanShop.Services.Updates;

public sealed record FailedUpdate(string Version, DateTimeOffset Time, string Reason);
public static class UpdateState
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FanShop", "Updates");
    public static void Log(string message, Exception? exception = null)
    {
        try { Directory.CreateDirectory(Root); File.AppendAllText(Path.Combine(Root, "update.log"), $"{DateTimeOffset.Now:O} {message} {exception}\n"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { System.Diagnostics.Debug.WriteLine(ex); }
    }
    public static void RecordFailure(string version, string reason)
    {
        Directory.CreateDirectory(Root);
        AtomicFiles.WriteJson(Path.Combine(Root, "failed-update.json"), new FailedUpdate(version, DateTimeOffset.UtcNow, reason));
    }
    public static bool IsCoolingDown(string version)
    {
        try
        {
            var path = Path.Combine(Root, "failed-update.json");
            if (!File.Exists(path)) return false;
            var failed = JsonSerializer.Deserialize<FailedUpdate>(File.ReadAllText(path), UpdateManifest.JsonOptions);
            return failed?.Version == version && DateTimeOffset.UtcNow - failed.Time < TimeSpan.FromHours(6);
        }
        catch (Exception ex) when (ex is IOException or JsonException) { Log("Не прочитано состояние неудачного обновления.", ex); return false; }
    }
    public static string RecoveryName(string install) => "FanShopUpdate-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(install).ToUpperInvariant())))[..16];
    public static string[] UserArguments(string[] arguments)
    {
        var output = new List<string>();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is "--update-healthcheck") { i++; continue; }
            if (arguments[i] is "--apply-update" or "--recover-update" or "--write-update-manifest" or "--verify-update-package" or "--verify-update-archive") break;
            output.Add(arguments[i]);
        }
        return output.ToArray();
    }
}
