using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using KubeTools4Dev.Core.Services.Interfaces;
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
    /// Initializes a new instance of the <see cref="MainViewModel" /> class.
    /// </summary>
    /// <param name="kubeService">The kube service.</param>
    /// <param name="podListViewModel">The pod list view model.</param>
    /// <param name="serviceListViewModel">The service list view model.</param>
    /// <param name="logger">The logger.</param>
    public MainViewModel(
        IKubernetesService kubeService,
        PodListViewModel podListViewModel,
        ServiceListViewModel serviceListViewModel,
        ILogger<MainViewModel> logger)
    {
        _kubeService = kubeService;
        _logger = logger;

        PodList = podListViewModel;
        ServiceList = serviceListViewModel;

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
        bool success = await _kubeService.ConnectAsync();
        if (success)
        {
            _logger.Debug("Connected to Kubernetes cluster successfully");
            
            try 
            {
                // Attempt to get the current context name. 
                // Since IKubernetesService doesn't expose it directly yet, we will resort to loading the config locally
                // strictly for display purposes. This is safe as the service likely uses the default config anyway.
                var config = k8s.KubernetesClientConfiguration.LoadKubeConfig();
                ClusterName = config.CurrentContext;
            }
            catch
            {
                ClusterName = "Unknown Cluster";
            }

            ConnectionStatus = "Connected to Kubernetes";
            IsConnected = true;
            await PodList.InitializeAsync();
            await ServiceList.InitializeAsync();
        }
        else
        {
            _logger.Warning("Failed to connect to Kubernetes cluster");
            ConnectionStatus = "Connection Failed";
            IsConnected = false;
        }
    }
}
