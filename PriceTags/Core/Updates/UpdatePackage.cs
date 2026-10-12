using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FanShop.Updates;

public static class UpdatePackage
{
    public static UpdateManifest Extract(string archivePath, string destination, string expectedVersion, string expectedSha256)
    {
        if (!UpdateManifest.HashFile(archivePath).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Контрольная сумма ZIP не совпадает.");
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
            throw new IOException("Каталог распаковки должен быть пустым.");
        Directory.CreateDirectory(destination);
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > 15000 || archive.Entries.Sum(e => e.Length) > 1_073_741_824)
            throw new InvalidDataException("Архив превышает допустимый размер.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName;
            var isDirectory = name.EndsWith('/');
            var relative = PackagePaths.Normalize(isDirectory ? name.TrimEnd('/') : name);
            if (!names.Add(relative) || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Повторяющийся путь или ссылка в ZIP.");
        }
        var manifestEntry = archive.Entries.SingleOrDefault(e => e.FullName == UpdateManifest.FileName)
            ?? throw new InvalidDataException("В ZIP отсутствует манифест в корне.");
        if (manifestEntry.Length > 32 * 1024 * 1024) throw new InvalidDataException("Манифест слишком велик.");
        UpdateManifest manifest;
        using (var stream = manifestEntry.Open()) manifest = JsonSerializer.Deserialize<UpdateManifest>(stream, UpdateManifest.JsonOptions)
            ?? throw new InvalidDataException("Манифест не прочитан.");
        manifest.Validate();
        if (!System.Version.Parse(manifest.Version).Equals(System.Version.Parse(expectedVersion)))
            throw new InvalidDataException("Версия ZIP не совпадает с тегом релиза.");
        var expected = manifest.Files.Select(f => f.Path).Append(UpdateManifest.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(e => !e.FullName.EndsWith('/')))
        {
            if (!expected.Remove(entry.FullName)) throw new InvalidDataException("Посторонний файл ZIP: " + entry.FullName);
            var path = PackagePaths.Resolve(destination, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            entry.ExtractToFile(path, false);
        }
        if (expected.Count != 0) throw new InvalidDataException("ZIP содержит не все файлы манифеста.");
        manifest.Verify(destination); return manifest;
    }
}
