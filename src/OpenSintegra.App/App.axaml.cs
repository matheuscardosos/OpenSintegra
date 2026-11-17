using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace OpenSintegra.App;

public partial class App : Application
{
    public static string? InitialFilePath { get; set; }
    public static string? InitialAuditXmlPath { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(InitialFilePath, InitialAuditXmlPath);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
