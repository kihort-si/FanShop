using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FanShop.Updates;

public sealed record PackageFile(string Path, long Size, string Sha256);
public sealed record UpdateManifest
{
    public const string FileName = "fanshop-update-manifest.json";
    public int Format { get; init; } = 1;
    public string Application { get; init; } = "FanShop";
    public string Version { get; init; } = "";
    public PackageFile[] Files { get; init; } = [];
    public PackageFile[] LegacyFiles { get; init; } = [];
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public void Validate()
    {
        if (Format != 1 || Application != "FanShop" || !System.Version.TryParse(Version, out _) || Files is null || LegacyFiles is null
            || Files.Length is < 1 or > 10000 || LegacyFiles.Length > 100000)
            throw new InvalidDataException("Некорректный манифест обновления FanShop.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Files)
        {
            ValidateEntry(file);
            if (!paths.Add(file.Path)) throw new InvalidDataException("Повторяющийся путь в манифесте: " + file.Path);
        }
        foreach (var file in LegacyFiles) ValidateEntry(file);
        if (!paths.Contains("FanShop.exe") || !paths.Contains("FanShop.dll") || !paths.Contains("FanShop.runtimeconfig.json"))
            throw new InvalidDataException("В обновлении отсутствуют обязательные файлы приложения.");
    }
    private static void ValidateEntry(PackageFile file)
    {
        if (file is null || file.Path != PackagePaths.Normalize(file.Path) || PackagePaths.IsUserData(file.Path)
            || file.Path == FileName || file.Size < 0 || file.Size > 1_073_741_824
            || file.Sha256 is null || file.Sha256.Length != 64 || !file.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Недопустимая запись манифеста.");
    }
    public static UpdateManifest Read(string root)
    {
        var path = PackagePaths.Resolve(root, FileName);
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Манифест слишком велик.");
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Пустой манифест обновления.");
        manifest.Validate(); return manifest;
    }
    public void Write(string root)
    { Validate(); AtomicFiles.WriteJson(PackagePaths.Resolve(root, FileName), this); }
    public static string HashFile(string path)
    { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public static PackageFile Describe(string root, string relative)
    {
        var path = PackagePaths.Resolve(root, relative);
        return new(relative, new FileInfo(path).Length, HashFile(path));
    }
    public static bool Matches(string root, PackageFile entry)
    {
        var path = PackagePaths.Resolve(root, entry.Path);
        return File.Exists(path) && new FileInfo(path).Length == entry.Size
            && HashFile(path).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase);
    }
    public void Verify(string root)
    {
        Validate();
        var expected = Files.Select(f => f.Path).Append(FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var actual in PackagePaths.Enumerate(root))
            if (!expected.Remove(actual)) throw new InvalidDataException("Посторонний файл в обновлении: " + actual);
        if (expected.Count > 0) throw new InvalidDataException("Отсутствует файл обновления: " + expected.First());
        foreach (var file in Files)
            if (!Matches(root, file)) throw new InvalidDataException("Контрольная сумма не совпадает: " + file.Path);
    }
    public static UpdateManifest Build(string root, string version, string? previousArchive = null)
    {
        var files = PackagePaths.Enumerate(root).Where(p => p != FileName).Order(StringComparer.Ordinal).Select(p => Describe(root, p)).ToArray();
        var archives = previousArchive is null ? [] : Directory.Exists(previousArchive)
            ? Directory.GetFiles(previousArchive, "*.zip").Order(StringComparer.Ordinal).ToArray() : [previousArchive];
        var legacy = archives.SelectMany(ReadPreviousArchive)
            .DistinctBy(f => (f.Path.ToUpperInvariant(), f.Sha256.ToUpperInvariant())).ToArray();
        var manifest = new UpdateManifest { Version = version, Files = files, LegacyFiles = legacy };
        manifest.Validate(); return manifest;
    }
    private static PackageFile[] ReadPreviousArchive(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var executables = archive.Entries.Where(e => System.IO.Path.GetFileName(e.FullName.Replace('\\', '/')).Equals("FanShop.exe", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (executables.Length != 1) throw new InvalidDataException("В предыдущем архиве не определён единственный FanShop.exe.");
        var fullName = executables[0].FullName.Replace('\\', '/');
        var prefix = fullName[..^"FanShop.exe".Length];
        var legacy = new List<PackageFile>();
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.EndsWith('/')) continue;
            var relative = PackagePaths.Normalize(name[prefix.Length..]);
            if (relative == FileName)
            {
                if (entry.Length > 32 * 1024 * 1024) throw new InvalidDataException("Предыдущий манифест слишком велик.");
                using var input = entry.Open(); var previous = JsonSerializer.Deserialize<UpdateManifest>(input, JsonOptions)
                    ?? throw new InvalidDataException("Некорректный предыдущий манифест.");
                previous.Validate(); legacy.AddRange(previous.LegacyFiles); continue;
            }
            if (PackagePaths.IsUserData(relative)) continue;
            using var stream = entry.Open();
            legacy.Add(new(relative, entry.Length, Convert.ToHexString(SHA256.HashData(stream))));
        }
        return legacy.DistinctBy(f => (f.Path.ToUpperInvariant(), f.Sha256.ToUpperInvariant())).ToArray();
    }
}

public static class PackagePaths
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 1024 || path.Contains('\\') || path.StartsWith('/') || path.Any(char.IsControl))
            throw new InvalidDataException("Недопустимый путь пакета.");
        var parts = path.Split('/');
        foreach (var part in parts)
        {
            var stem = part.Split('.')[0];
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny([':', '*', '?', '"', '<', '>', '|']) >= 0
                || part.StartsWith(".fanshop", StringComparison.OrdinalIgnoreCase)
                || new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Недопустимый путь пакета: " + path);
        }
        return path;
    }
    public static bool IsUserData(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        var extension = System.IO.Path.GetExtension(name);
        return new[] { "settings.json", "session.json", "transfer.json", "adjustment-settings.json", "FanShop.db-wal", "FanShop.db-shm", "болванка.docx" }.Contains(name, StringComparer.OrdinalIgnoreCase)
            || new[] { ".db", ".sqlite", ".sqlite3" }.Contains(extension, StringComparer.OrdinalIgnoreCase)
            || new[] { "Data", "UserData", "Logs", "Reports", "Templates" }.Contains(path.Split('/')[0], StringComparer.OrdinalIgnoreCase);
    }
    public static string Resolve(string root, string relative)
    {
        Normalize(relative);
        var canonical = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(root));
        if (Directory.Exists(canonical) && (File.GetAttributes(canonical) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Каталог установки не должен быть ссылкой.");
        var current = canonical;
        foreach (var part in relative.Split('/'))
        {
            current = System.IO.Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Ссылки в путях обновления запрещены: " + relative);
        }
        return current;
    }
    public static IEnumerable<string> Enumerate(string root)
    {
        IEnumerable<string> Visit(string directory)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Ссылка в пакете обновления: " + path);
                if (Directory.Exists(path)) { foreach (var child in Visit(path)) yield return child; }
                else yield return Normalize(System.IO.Path.GetRelativePath(root, path).Replace('\\', '/'));
            }
        }
        return Visit(root);
    }
}

public static class AtomicFiles
{
    public static void WriteJson<T>(string destination, T value)
    {
        var temp = destination + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, UpdateManifest.JsonOptions); stream.Flush(true); }
        FileAccessRetry.Run(() => File.Move(temp, destination, true));
    }
    public static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
        var temp = destination + ".fanshop-copy-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var input = File.OpenRead(source))
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { input.CopyTo(output); output.Flush(true); }
            FileAccessRetry.Run(() => File.Move(temp, destination, true));
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public static class FileAccessRetry
{
    public static void Run(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try { action(); return; }
            catch (IOException ex) when (OperatingSystem.IsWindows() && attempt < 20 && (ex.HResult & 0xFFFF) is 5 or 32 or 33)
            { Thread.Sleep(250); }
        }
    }
}
