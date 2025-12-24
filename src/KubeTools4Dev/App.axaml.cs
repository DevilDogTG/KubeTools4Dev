using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KubeTools4Dev.ViewModels;
using KubeTools4Dev.Views;
using Serilog;
using Microsoft.Extensions.Logging;

namespace ElysianMonitor;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create a logger factory that uses the Serilog logger configured in Program.cs
            var loggerFactory = new LoggerFactory().AddSerilog();

            var viewModel = new MainViewModel(loggerFactory);
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            desktop.Exit += (s, e) =>
            {
                viewModel.Cleanup();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}