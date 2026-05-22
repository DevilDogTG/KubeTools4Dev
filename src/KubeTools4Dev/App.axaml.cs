using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using KubeTools4Dev.ViewModels;
using KubeTools4Dev.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        // Core Services — per-cluster services are now managed by ClusterConnectionManager
        services.AddSingleton<IKubernetesServiceFactory, KubernetesServiceFactory>();
        services.AddSingleton<IPortForwardServiceFactory, PortForwardServiceFactory>();
        services.AddSingleton<IClusterConnectionManager, ClusterConnectionManager>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // Core ViewModels
        services.AddSingleton<ClusterTreeViewModel>();

        // App ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<PodListViewModel>();
        services.AddTransient<ServiceListViewModel>();
        services.AddTransient<DeploymentListViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<Func<PodViewModel, IKubernetesService, int, PodDetailViewModel>>(sp => (pod, svc, tab) =>
        {
            var logger = sp.GetRequiredService<ILogger<PodDetailViewModel>>();
            var vm = new PodDetailViewModel(logger, svc);
            vm.Pod = pod;
            vm.IsLogsView = tab == 0;
            return vm;
        });

        base.OnFrameworkInitializationCompleted();
    }
}
