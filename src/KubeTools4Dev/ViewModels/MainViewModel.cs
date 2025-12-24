using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IKubernetesService _kubeService;
    private readonly IPortForwardService _portForwardService;
    private readonly ISettingsService _settingsService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private PodListViewModel _podList;

    [ObservableProperty]
    private ServiceListViewModel _serviceList;

    [ObservableProperty]
    private string _connectionStatus = "Not Connected";

    [ObservableProperty]
    private bool _isConnected;

    public MainViewModel(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<MainViewModel>();

        // Manual DI for simplicity in this specific scope
        _kubeService = new KubernetesService(loggerFactory.CreateLogger<KubernetesService>());
        _portForwardService = new PortForwardService(loggerFactory.CreateLogger<PortForwardService>());
        _settingsService = new SettingsService();

        PodList = new PodListViewModel(_kubeService, _settingsService, loggerFactory.CreateLogger<PodListViewModel>());
        ServiceList = new ServiceListViewModel(_kubeService, _portForwardService, _settingsService, loggerFactory.CreateLogger<ServiceListViewModel>());

        // Auto-connect on start
        _ = ConnectCommand.ExecuteAsync(null);
    }

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
