using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using k8s.Models;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

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
                catch (Exception)
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
