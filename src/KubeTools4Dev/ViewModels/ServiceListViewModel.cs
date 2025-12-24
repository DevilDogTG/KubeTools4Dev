using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Services;
using k8s.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

public partial class ServiceListViewModel : ViewModelBase
{
    private readonly IKubernetesService _kubeService;
    private readonly IPortForwardService _portForwardService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ServiceListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<ServiceViewModel> _services = new();

    [ObservableProperty]
    private bool _isLoading;

    public ServiceListViewModel(IKubernetesService kubeService, IPortForwardService portForwardService, ISettingsService settingsService, ILogger<ServiceListViewModel> logger)
    {
        _kubeService = kubeService;
        _portForwardService = portForwardService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            if (!_kubeService.IsConnected) return;

            var services = await _kubeService.GetServicesAsync();

            // Filter out internal kubernetes service or headless
            var relevantServices = services.Where(s => s.Metadata.Name != "kubernetes" && s.Spec.Type != "ExternalName");

            Services.Clear();
            foreach (var svc in relevantServices)
            {
                if (svc.Spec.Ports == null) continue;

                foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
                {
                    Services.Add(new ServiceViewModel(svc, port, _portForwardService, _settingsService));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize service list");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ForwardAll()
    {
        foreach (var svc in Services)
        {
            if (!svc.IsForwarding && !svc.IsExcluded)
            {
                svc.IsForwarding = true; // Triggers the command in setter
            }
        }
    }

    [RelayCommand]
    private async Task StopAll()
    {
        foreach (var svc in Services)
        {
            if (svc.IsForwarding)
            {
                svc.IsForwarding = false;
            }
        }
    }

    public void Cleanup()
    {
        _portForwardService.StopAll();
    }
}

public partial class ServiceViewModel : ObservableObject
{
    private readonly V1Service _service;
    private readonly V1ServicePort _port;
    private readonly IPortForwardService _pfService;
    private readonly ISettingsService _settingsService;
    private CancellationTokenSource? _pfCts;
    private string _settingsKey;

    public ServiceViewModel(V1Service service, V1ServicePort port, IPortForwardService pfService, ISettingsService settingsService)
    {
        _service = service;
        _port = port;
        _pfService = pfService;
        _settingsService = settingsService;

        Name = service.Metadata.Name;
        Namespace = service.Metadata.NamespaceProperty;
        // Fix: Include target port in key to handle multi-port services correctly
        _settingsKey = $"{Namespace}/{Name}:{port.Port}";

        // User requests to use the Service Port as the destination by default, 
        // mimicking 'kubectl port-forward svc/name 8088:8088' behavior.
        // Original logic was: TargetPort = port.TargetPort; 
        TargetPort = port.Port;
        LocalPort = port.Port; // Default to same port
        
        // Display 
        TargetPortDisplay = port.Port.ToString();

        _isExcluded = _settingsService.ExcludedServices.Contains(_settingsKey);
        
        _durationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _durationTimer.Tick += (s, e) =>
        {
            if (_startTime.HasValue)
            {
                DurationText = (DateTime.Now - _startTime.Value).ToString(@"hh\:mm\:ss");
            }
        };
    }

    private DispatcherTimer _durationTimer;
    private DateTime? _startTime;

    [ObservableProperty] private string _durationText = "";

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _namespace;
    [ObservableProperty] private object _targetPort;
    [ObservableProperty] private string _targetPortDisplay;
    [ObservableProperty] private int _localPort;

    private bool _isExcluded;
    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (SetProperty(ref _isExcluded, value))
            {
                if (value)
                {
                    if (!_settingsService.ExcludedServices.Contains(_settingsKey))
                    {
                        _settingsService.ExcludedServices.Add(_settingsKey);
                        _settingsService.Save();
                    }
                }
                else
                {
                     if (_settingsService.ExcludedServices.Contains(_settingsKey))
                    {
                        _settingsService.ExcludedServices.Remove(_settingsKey);
                        _settingsService.Save();
                    }
                }
            }
        }
    }

    [ObservableProperty]
    private string _status = "Stopped";

    private bool _isForwarding;
    public bool IsForwarding
    {
        get => _isForwarding;
        set
        {
            if (SetProperty(ref _isForwarding, value))
            {
                if (value) StartForwarding();
                else StopForwarding();
            }
        }
    }

    private async void StartForwarding()
    {
        Status = "Starting";
        _pfCts = new CancellationTokenSource();
        try
        {
            // Run in background
            _ = Task.Run(async () =>
            {
                try
                {
                    Dispatcher.UIThread.Post(() => Status = "Forwarding");
                    // Start Timer
                    Dispatcher.UIThread.Post(() =>
                    {
                        _startTime = DateTime.Now;
                        DurationText = "00:00:00";
                        _durationTimer.Start();
                    });

                    await _pfService.StartServicePortForwardAsync(Name, Namespace, TargetPort, LocalPort, _pfCts.Token);
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Status = "Failed";
                        IsForwarding = false; // Reset toggle
                        StopTimer();
                    });
                }
            });
        }
        catch (Exception)
        {
            Status = "Failed";
            StopTimer();
        }
    }

    private void StopTimer()
    {
        _durationTimer.Stop();
        DurationText = "";
        _startTime = null;
    }

    private void StopForwarding()
    {
        _pfCts?.Cancel();
        Status = "Stopped";
        StopTimer();
    }
}
