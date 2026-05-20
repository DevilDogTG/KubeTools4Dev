using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Main application view model.
/// </summary>
/// <seealso cref="ViewModelBase" />
public partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// The kube service
    /// </summary>
    private readonly IKubernetesService _kubeService;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>
    /// The connection status
    /// </summary>
    [ObservableProperty]
    private string _connectionStatus = "Not Connected";

    /// <summary>
    /// The is connected
    /// </summary>
    [ObservableProperty]
    private bool _isConnected;

    /// <summary>Gets the sidebar navigation state (expanded/collapsed + active view).</summary>
    public SidebarViewModel Sidebar { get; } = new();

    /// <summary>
    /// The pod list
    /// </summary>
    [ObservableProperty]
    private PodListViewModel _podList;

    /// <summary>
    /// The service list
    /// </summary>
    [ObservableProperty]
    private ServiceListViewModel _serviceList;

    /// <summary>
    /// The deployment list
    /// </summary>
    [ObservableProperty]
    private DeploymentListViewModel _deploymentList;

    /// <summary>
    /// The settings view model
    /// </summary>
    [ObservableProperty]
    private SettingsViewModel _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel" /> class.
    /// </summary>
    /// <param name="kubeService">The kube service.</param>
    /// <param name="podListViewModel">The pod list view model.</param>
    /// <param name="serviceListViewModel">The service list view model.</param>
    /// <param name="deploymentListViewModel">The deployment list view model.</param>
    /// <param name="settingsViewModel">The settings view model.</param>
    /// <param name="logger">The logger.</param>
    public MainViewModel(
        IKubernetesService kubeService,
        PodListViewModel podListViewModel,
        ServiceListViewModel serviceListViewModel,
        DeploymentListViewModel deploymentListViewModel,
        SettingsViewModel settingsViewModel,
        ILogger<MainViewModel> logger)
    {
        _kubeService = kubeService;
        _logger = logger;

        PodList = podListViewModel;
        ServiceList = serviceListViewModel;
        DeploymentList = deploymentListViewModel;
        Settings = settingsViewModel;

        // Auto-connect on start
        _ = ConnectCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Cleanups resources used by the view model.
    /// </summary>
    public void Cleanup()
    {
        _logger.Information("Starting cleanup application");
        ServiceList?.Cleanup();
        DeploymentList?.Dispose();
    }

    /// <summary>
    /// The cluster name
    /// </summary>
    [ObservableProperty]
    private string _clusterName = string.Empty;

    /// <summary>
    /// Connects Kubernetes instance command.
    /// </summary>
    [RelayCommand]
    private async Task Connect()
    {
        _logger.Debug("Connecting to Kubernetes cluster");
        ConnectionStatus = "Connecting...";
        var currentContext = await _kubeService.ConnectAsync();
        if (!string.IsNullOrEmpty(currentContext))
        {
            _logger.Debug("Connected to Kubernetes cluster successfully");
            ClusterName = currentContext;
            ConnectionStatus = "Connected to Kubernetes";
            IsConnected = true;
            await PodList.InitializeAsync();
            await ServiceList.InitializeAsync();
            await DeploymentList.InitializeAsync();
        }
        else
        {
            _logger.Warning("Failed to connect to Kubernetes cluster");
            ConnectionStatus = "Connection Failed";
            ClusterName = "N/A";
            IsConnected = false;
        }
    }
}
