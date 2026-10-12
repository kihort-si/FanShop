using Avalonia;

namespace FanShop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (Services.Updates.UpdateBootstrap.RunCommand(args) is { } exitCode) return exitCode;
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
