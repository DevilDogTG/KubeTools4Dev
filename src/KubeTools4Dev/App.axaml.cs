using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using KubeTools4Dev.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;

namespace KubeTools4Dev;

/// <summary>
/// Main application class.
/// </summary>
/// <seealso cref="Avalonia.Application" />
public partial class App : Application
{
    /// <summary>
    /// Initializes the application by loading XAML etc.
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Called when [framework initialization completed].
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            var serviceProvider = services.BuildServiceProvider();

            var viewModel = serviceProvider.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            desktop.Exit += (s, e) =>
            {
                viewModel.Cleanup();
                if (serviceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };
        }
    }

    /// <summary>
    /// Configures the services.
    /// </summary>
    /// <param name="services">The services.</param>
    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

        // Core Services
        services.AddSingleton<IKubernetesService, KubernetesService>();
        services.AddSingleton<IPortForwardService, PortForwardService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // ViewModels
        // MainViewModel as Singleton (usually shared for the main window)
        services.AddSingleton<MainViewModel>();
        // Child ViewModels - can be Singleton or Transient depending on whether they need to share state or be recreated.
        // Given MainViewModel holds references to them, Singleton is redundant if Main is Singleton, but safe.
        // Transient is safer if they are "owned" by MainViewModel but we want DI to build them.
        services.AddTransient<PodListViewModel>();
        services.AddTransient<ServiceListViewModel>();
        services.AddSingleton<SettingsViewModel>();

        base.OnFrameworkInitializationCompleted();
    }
}