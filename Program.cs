using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace FanShop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WriteStartupLog();

        RegisterGlobalExceptionHandlers();

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            WriteCrashLog("Fatal startup exception", ex);

            Console.Error.WriteLine(ex);
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
                WriteTextLog(
                    $"Unhandled object. IsTerminating: {eventArgs.IsTerminating}",
                    eventArgs.ExceptionObject?.ToString() ?? "null");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            WriteCrashLog(
                "Unobserved task exception",
                eventArgs.Exception);

            eventArgs.SetObserved();
        };
    }

    private static void WriteStartupLog()
    {
        try
        {
            var text = $"""
                        ==================================================
                        Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                        Application started
                        Version: {GetAppVersion()}
                        Process path: {Environment.ProcessPath}
                        Base directory: {AppContext.BaseDirectory}
                        Current directory: {Environment.CurrentDirectory}
                        AppData: {GetAppDataDirectory()}
                        OS: {Environment.OSVersion}
                        64-bit process: {Environment.Is64BitProcess}

                        """;

            WriteToLogFile("startup.log", text);
        }
        catch (Exception ex)
        {
            WriteFallbackLog("Startup log error", ex);
        }
    }

    private static void WriteCrashLog(
        string source,
        Exception exception)
    {
        var text = $"""
                    ==================================================
                    Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Source: {source}
                    Version: {GetAppVersion()}
                    Process path: {Environment.ProcessPath}
                    Base directory: {AppContext.BaseDirectory}
                    Current directory: {Environment.CurrentDirectory}
                    AppData: {GetAppDataDirectory()}
                    OS: {Environment.OSVersion}
                    64-bit process: {Environment.Is64BitProcess}

                    {exception}

                    """;

        try
        {
            WriteToLogFile("crash.log", text);
        }
        catch
        {
            WriteFallbackLog(source, exception);
        }
    }

    private static void WriteTextLog(
        string source,
        string details)
    {
        var text = $"""
                    ==================================================
                    Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                    Source: {source}

                    {details}

                    """;

        try
        {
            WriteToLogFile("crash.log", text);
        }
        catch
        {
            try
            {
                var fallbackPath = Path.Combine(
                    Path.GetTempPath(),
                    "FanShop-crash.log");

                File.AppendAllText(fallbackPath, text);
            }
            catch
            {
            }
        }
    }

    private static void WriteToLogFile(
        string fileName,
        string content)
    {
        var logDirectory = GetLogDirectory();

        Directory.CreateDirectory(logDirectory);

        var logPath = Path.Combine(
            logDirectory,
            fileName);

        File.AppendAllText(logPath, content);
    }

    private static string GetLogDirectory()
    {
        return Path.Combine(
            GetAppDataDirectory(),
            "FanShop",
            "Logs");
    }

    private static string GetAppDataDirectory()
    {
        return Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData);
    }

    private static void WriteFallbackLog(
        string source,
        Exception exception)
    {
        try
        {
            var fallbackPath = Path.Combine(
                Path.GetTempPath(),
                "FanShop-crash.log");

            var text = $"""
                        ==================================================
                        Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                        Source: {source}
                        Process path: {Environment.ProcessPath}
                        Base directory: {AppContext.BaseDirectory}
                        AppData: {GetAppDataDirectory()}

                        {exception}

                        """;

            File.AppendAllText(fallbackPath, text);
        }
        catch
        {
        }
    }

    private static string GetAppVersion()
    {
        return typeof(Program)
                   .Assembly
                   .GetName()
                   .Version?
                   .ToString()
               ?? "unknown";
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
