using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services;
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
    private readonly KubernetesService _kubeService;
    /// <summary>
    /// The port forward service
    /// </summary>
    private readonly IPortForwardService _portForwardService;
    /// <summary>
    /// The settings service
    /// </summary>
    private readonly ISettingsService _settingsService;
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<MainViewModel> _logger;

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
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    public MainViewModel(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        // Manual DI for simplicity in this specific scope
        _kubeService = new KubernetesService(loggerFactory.CreateLogger<KubernetesService>());
        _portForwardService = new PortForwardService(loggerFactory.CreateLogger<PortForwardService>());
        _settingsService = new SettingsService();

        PodList = new PodListViewModel(
            _kubeService,
            _settingsService,
            loggerFactory.CreateLogger<PodListViewModel>());
        ServiceList = new ServiceListViewModel(
            _kubeService,
            _portForwardService,
            _settingsService,
            loggerFactory.CreateLogger<ServiceListViewModel>());

        // Auto-connect on start
        _ = ConnectCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Connects Kubernetes instance command.
    /// </summary>
    [RelayCommand]
    private async Task Connect()
    {
        ConnectionStatus = "Connecting...";
        bool success = await _kubeService.ConnectAsync();
        if (success)
        {
            ConnectionStatus = "Connected to Kubernetes";
            IsConnected = true;
            await PodList.InitializeAsync();
            await ServiceList.InitializeAsync();
        }
        else
        {
            ConnectionStatus = "Connection Failed";
            IsConnected = false;
        }
    }
    public void Cleanup()
    {
        ServiceList?.Cleanup();
    }
}
