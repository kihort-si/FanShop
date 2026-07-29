using Avalonia;

namespace FanShop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RegisterGlobalExceptionHandlers();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Fatal startup exception", ex);
            throw;
        }
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
            {
                WriteCrashLog(
                    $"Unhandled exception. IsTerminating: {eventArgs.IsTerminating}",
                    exception);
            }
            else
            {
                WriteCrashLog(
                    $"Unhandled non-Exception object. IsTerminating: {eventArgs.IsTerminating}",
                    eventArgs.ExceptionObject?.ToString() ?? "null");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            WriteCrashLog("Unobserved task exception", eventArgs.Exception);
            eventArgs.SetObserved();
        };
    }

    private static void WriteCrashLog(string source, Exception exception)
    {
        WriteCrashLog(source, exception.ToString());
    }

    private static void WriteCrashLog(string source, string details)
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FanShop",
                "Logs");

            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "crash.log");

            var message = $"""
                           
                           ==================================================
                           Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                           Source: {source}
                           App version: {GetAppVersion()}
                           OS: {Environment.OSVersion}
                           64-bit process: {Environment.Is64BitProcess}
                           
                           {details}
                           
                           """;

            File.AppendAllText(logPath, message);
        }
        catch
        {
        }
    }

    private static string GetAppVersion()
    {
        return typeof(Program).Assembly
                   .GetName()
                   .Version?
                   .ToString()
               ?? "unknown";
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
