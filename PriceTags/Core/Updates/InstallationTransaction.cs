using System.Text.Json;

namespace FanShop.Updates;

public sealed record BackupEntry(string Path, PackageFile? Original);
public sealed record InstallationJournal(string WorkDirectory, string Version, BackupEntry[] Files);

public sealed class InstallationTransaction : IDisposable
{
    public const string JournalName = ".fanshop-update-journal.json";
    private readonly string _install;
    private readonly string _work;
    private readonly FileStream _lock;
    private InstallationJournal? _journal;
    private string JournalPath => Path.Combine(_install, JournalName);
    public InstallationTransaction(string installDirectory, string workDirectory)
    {
        _install = Path.GetFullPath(installDirectory); _work = Path.GetFullPath(workDirectory);
        if (_work.StartsWith(Path.TrimEndingDirectorySeparator(_install) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || _install.Equals(_work, StringComparison.OrdinalIgnoreCase)) throw new IOException("Резервная копия должна находиться вне папки приложения.");
        Directory.CreateDirectory(_install); Directory.CreateDirectory(_work);
        if ((File.GetAttributes(_install) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(_work) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Ссылки в путях установщика запрещены.");
        _lock = new FileStream(Path.Combine(_install, ".fanshop-update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public IReadOnlyList<string> Apply(string payload, UpdateManifest next, Action<string>? afterMutation = null)
    {
        next.Verify(payload);
        if (File.Exists(JournalPath)) throw new IOException("Сначала необходимо восстановить незавершённое обновление.");
        var previous = File.Exists(Path.Combine(_install, UpdateManifest.FileName)) ? UpdateManifest.Read(_install) : null;
        var owned = (previous?.Files ?? []).Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var legacy = next.LegacyFiles.Concat(previous?.LegacyFiles ?? []).ToArray();
        var newNames = next.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool LegacyMatches(string path) => legacy.Any(f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase) && UpdateManifest.Matches(_install, f));
        foreach (var entry in next.Files)
        {
            var target = PackagePaths.Resolve(_install, entry.Path);
            if (Directory.Exists(target)) throw new IOException("Папка конфликтует с файлом обновления: " + entry.Path);
            if (File.Exists(target) && !owned.Contains(entry.Path) && !LegacyMatches(entry.Path) && !UpdateManifest.Matches(_install, entry))
                throw new IOException("Нельзя заменить неизвестный пользовательский файл: " + entry.Path);
        }
        var obsolete = (previous?.Files ?? []).Concat(legacy).Where(f => !newNames.Contains(f.Path))
            .Where(f => UpdateManifest.Matches(_install, f)).Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        var affected = next.Files.Select(f => f.Path).Concat(obsolete).Append(UpdateManifest.FileName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var backup = Path.Combine(_work, "backup");
        if (Directory.Exists(backup)) throw new IOException("Каталог резервной копии уже существует.");
        Directory.CreateDirectory(backup);
        var entries = new List<BackupEntry>();
        foreach (var relative in affected)
        {
            var target = PackagePaths.Resolve(_install, relative);
            var original = File.Exists(target) ? UpdateManifest.Describe(_install, relative) : null;
            if (original is not null)
            {
                AtomicFiles.Copy(target, PackagePaths.Resolve(backup, relative));
                if (!UpdateManifest.Matches(backup, original)) throw new IOException("Резервная копия не прошла проверку: " + relative);
            }
            entries.Add(new(relative, original));
        }
        _journal = new(_work, next.Version, entries.ToArray());
        AtomicFiles.WriteJson(JournalPath, _journal); // Durable rollback description exists before any installed file changes.
        try
        {
            foreach (var entry in next.Files)
            {
                if (UpdateManifest.Matches(_install, entry)) continue;
                AtomicFiles.Copy(PackagePaths.Resolve(payload, entry.Path), PackagePaths.Resolve(_install, entry.Path));
                afterMutation?.Invoke(entry.Path);
            }
            foreach (var relative in obsolete)
            { FileAccessRetry.Run(() => File.Delete(PackagePaths.Resolve(_install, relative))); afterMutation?.Invoke(relative); }
            AtomicFiles.Copy(Path.Combine(payload, UpdateManifest.FileName), Path.Combine(_install, UpdateManifest.FileName));
            foreach (var entry in next.Files)
                if (!UpdateManifest.Matches(_install, entry)) throw new IOException("Ошибка проверки установленного файла: " + entry.Path);
            return obsolete;
        }
        catch { Rollback(); throw; }
    }
    public void Commit()
    {
        if (_journal is null) throw new InvalidOperationException("Нет установленного обновления.");
        AtomicFiles.WriteJson(Path.Combine(_work, "completed.json"), new { Success = true, Version = _journal.Version, Time = DateTimeOffset.UtcNow });
        File.Delete(JournalPath); _journal = null;
    }
    public void Rollback()
    {
        if (_journal is null)
        {
            if (!File.Exists(JournalPath)) return;
            var info = new FileInfo(JournalPath);
            if (info.Length > 32 * 1024 * 1024) throw new InvalidDataException("Повреждён журнал обновления.");
            _journal = JsonSerializer.Deserialize<InstallationJournal>(File.ReadAllText(JournalPath), UpdateManifest.JsonOptions)
                ?? throw new InvalidDataException("Журнал обновления не прочитан.");
        }
        if (!Path.GetFullPath(_journal.WorkDirectory).Equals(_work, StringComparison.OrdinalIgnoreCase) || _journal.Files is null || _journal.Files.Length > 20000)
            throw new InvalidDataException("Журнал обновления содержит неправильный путь резервной копии.");
        var backup = Path.Combine(_work, "backup");
        foreach (var entry in _journal.Files)
        {
            PackagePaths.Normalize(entry.Path);
            if (PackagePaths.IsUserData(entry.Path)) throw new InvalidDataException("Пользовательские данные не могут участвовать в откате.");
            if (entry.Original is { } original && (original.Path != entry.Path || !UpdateManifest.Matches(backup, original)))
                throw new IOException("Повреждена резервная копия: " + entry.Path);
        }
        foreach (var entry in _journal.Files.Reverse())
        {
            var target = PackagePaths.Resolve(_install, entry.Path);
            if (entry.Original is not null)
            {
                if (!UpdateManifest.Matches(_install, entry.Original)) AtomicFiles.Copy(PackagePaths.Resolve(backup, entry.Path), target);
            }
            else if (File.Exists(target)) FileAccessRetry.Run(() => File.Delete(target));
        }
        AtomicFiles.WriteJson(Path.Combine(_work, "completed.json"), new { Success = false, Version = _journal.Version, Time = DateTimeOffset.UtcNow });
        File.Delete(JournalPath); _journal = null;
    }
    public void Dispose() => _lock.Dispose(); // An uncommitted transaction deliberately leaves its durable journal for recovery.
}

public static class LegacyPackageCleanup
{
    public static IReadOnlyList<string> Run(string installDirectory, string backupDirectory, Action<Exception>? reportError = null)
    {
        if (!File.Exists(Path.Combine(installDirectory, UpdateManifest.FileName))) return [];
        var manifest = UpdateManifest.Read(installDirectory);
        var current = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = new List<string>();
        foreach (var group in manifest.LegacyFiles.Where(f => !current.Contains(f.Path)).GroupBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var matching = group.FirstOrDefault(f => UpdateManifest.Matches(installDirectory, f));
                if (matching is null) continue; // Preserve unknown or modified files, even if their extension is .dll.
                var source = PackagePaths.Resolve(installDirectory, matching.Path);
                var backupName = matching.Sha256 + "/" + matching.Path;
                var target = PackagePaths.Resolve(backupDirectory, backupName);
                AtomicFiles.Copy(source, target);
                if (!UpdateManifest.Matches(installDirectory, matching)) throw new IOException("Файл изменился во время очистки.");
                File.Delete(source); removed.Add(matching.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { reportError?.Invoke(ex); }
        }
        return removed;
    }
}
