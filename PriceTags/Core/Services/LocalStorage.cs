using System.Text.Json;
using Microsoft.Extensions.Logging;
using FanShop.PriceTags.Models;

namespace FanShop.PriceTags.Services;

public sealed class LocalStorage : IDisposable
{
    private readonly FileStream _instanceLock;
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };
    public string DirectoryPath { get; }
    public LocalStorage(string directoryPath)
    {
        DirectoryPath = directoryPath; Directory.CreateDirectory(directoryPath);
        try { _instanceLock = new FileStream(Path.Combine(directoryPath, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new IOException("Раздел ценников уже открыт в другом экземпляре FanShop либо каталог данных недоступен. Закройте второй экземпляр и проверьте права доступа.", ex); }
    }
    public void Dispose() => _instanceLock.Dispose();
    public T? Read<T>(string name)
    {
        lock (_gate)
        {
            var path = Path.Combine(DirectoryPath, name);
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), _options)
                ?? throw new InvalidDataException($"Файл {name} не содержит данных.") : default;
        }
    }
    public void Write<T>(string name, T data)
    {
        lock (_gate)
        {
            var path = Path.Combine(DirectoryPath, name);
            var temp = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, data, _options);
                    stream.Flush(true);
                }
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
public sealed class SettingsService(LocalStorage storage)
{
    public PriceTagSettings Load() => storage.Read<PriceTagSettings>("settings.json") ?? new();
    public void Save(PriceTagSettings settings)
    {
        if (settings.Port < 1024 || settings.Port > 65535) throw new InvalidOperationException("Порт должен быть от 1024 до 65535.");
        (settings.Delays ?? new()).Validate();
        storage.Write("settings.json", settings);
    }
}
public sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly string _directory = directory;
    private readonly object _gate = new();
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }
    private sealed class FileLogger(FileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            lock (owner._gate)
            {
                try
                {
                    Directory.CreateDirectory(owner._directory);
                    File.AppendAllText(Path.Combine(owner._directory, $"price-tags-{DateTime.Now:yyyy-MM-dd}.log"),
                        $"{DateTimeOffset.Now:O} [{level}] {category}: {formatter(state, exception)} {exception}\n");
                }
                catch (IOException) { System.Diagnostics.Trace.WriteLine("Не удалось записать лог ценников."); }
                catch (UnauthorizedAccessException) { System.Diagnostics.Trace.WriteLine("Нет доступа к логу ценников."); }
            }
        }
    }
}
