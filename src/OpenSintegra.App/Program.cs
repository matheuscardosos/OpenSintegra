using System;
using Avalonia;

namespace OpenSintegra.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0)
            App.InitialFilePath = args[0];
        if (args.Length > 1)
            App.InitialAuditXmlPath = args[1];

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(Array.Empty<string>());
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
