using System.Text.Json;

namespace FanShop.Updates;

public static class UpdateCache
{
    public static int Cleanup(string root, int keep = 2)
    {
        if (!Directory.Exists(root)) return 0;
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Каталог обновлений не должен быть ссылкой.");
        var completed = new List<DirectoryInfo>();
        foreach (var directory in new DirectoryInfo(root).EnumerateDirectories())
        {
            if (!Guid.TryParseExact(directory.Name, "N", out _) || (directory.Attributes & FileAttributes.ReparsePoint) != 0) continue;
            var marker = Path.Combine(directory.FullName, "completed.json");
            if (!File.Exists(marker) || new FileInfo(marker).Length > 4096) continue;
            var requestPath = Path.Combine(directory.FullName, "request.json");
            if (File.Exists(requestPath))
            {
                try
                {
                    var request = UpdateRequest.Read(requestPath);
                    if (File.Exists(Path.Combine(request.InstallDirectory, InstallationTransaction.JournalName))) continue;
                }
                catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException) { continue; }
            }
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(marker));
                if (!document.RootElement.TryGetProperty("Success", out var success) || success.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                    || !document.RootElement.TryGetProperty("Version", out var version) || !Version.TryParse(version.GetString(), out _)) continue;
                completed.Add(directory);
            }
            catch (JsonException) { }
        }
        var removed = 0;
        foreach (var directory in completed.OrderByDescending(d => File.GetLastWriteTimeUtc(Path.Combine(d.FullName, "completed.json"))).Skip(Math.Max(2, keep)))
        {
            if (ContainsLink(directory.FullName)) continue;
            try { directory.Delete(true); removed++; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { } // A helper may still be exiting.
        }
        return removed;
    }
    private static bool ContainsLink(string root)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(root))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return true;
            if (Directory.Exists(path) && ContainsLink(path)) return true;
        }
        return false;
    }
}
