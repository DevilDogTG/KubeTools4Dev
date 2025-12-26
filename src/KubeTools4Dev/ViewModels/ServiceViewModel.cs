using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for a single service.
/// </summary>
/// <seealso cref="CommunityToolkit.Mvvm.ComponentModel.ObservableObject" />
public partial class ServiceViewModel : ObservableObject
{
    /// <summary>
    /// The duration timer
    /// </summary>
    private readonly DispatcherTimer _durationTimer;

    /// <summary>
    /// The pf service
    /// </summary>
    private readonly IPortForwardService _pfService;

    /// <summary>
    /// The port
    /// </summary>
    private readonly V1ServicePort _port;

    /// <summary>
    /// The service
    /// </summary>
    private readonly V1Service _service;

    /// <summary>
    /// The settings key
    /// </summary>
    private readonly string _settingsKey;

    /// <summary>
    /// The settings service
    /// </summary>
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// The duration text
    /// </summary>
    [ObservableProperty]
    private string _durationText = "";
    /// <summary>
    /// The is excluded
    /// </summary>
    private bool _isExcluded;

    /// <summary>
    /// The is forwarding
    /// </summary>
    private bool _isForwarding;

    /// <summary>
    /// The local port
    /// </summary>
    [ObservableProperty]
    private int _localPort;

    /// <summary>
    /// The name
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// The namespace
    /// </summary>
    [ObservableProperty]
    private string _namespace;

    /// <summary>
    /// The pf CTS
    /// </summary>
    private CancellationTokenSource? _pfCts;
    /// <summary>
    /// The start time
    /// </summary>
    private DateTime? _startTime;

    /// <summary>
    /// The status
    /// </summary>
    [ObservableProperty]
    private string _status = "Stopped";

    /// <summary>
    /// The target port
    /// </summary>
    [ObservableProperty]
    private object _targetPort;

    /// <summary>
    /// The target port display
    /// </summary>
    [ObservableProperty]
    private string _targetPortDisplay;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceViewModel"/> class.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="port">The port.</param>
    /// <param name="pfService">The pf service.</param>
    /// <param name="settingsService">The settings service.</param>
    public ServiceViewModel(
        V1Service service,
        V1ServicePort port,
        IPortForwardService pfService,
        ISettingsService settingsService)
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

        _isExcluded = _settingsService.Services.ExcludedServices.Contains(_settingsKey);

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

    /// <summary>
    /// Gets or sets a value indicating whether this instance is excluded.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is excluded; otherwise, <c>false</c>.
    /// </value>
    public bool IsExcluded
    {
        get => _isExcluded;
        set
        {
            if (SetProperty(ref _isExcluded, value))
            {
                if (value)
                {
                    if (!_settingsService.Services.ExcludedServices.Contains(_settingsKey))
                    {
                        _settingsService.Services.ExcludedServices.Add(_settingsKey);
                        _settingsService.Save();
                    }
                }
                else
                {
                    _settingsService.Services.ExcludedServices.Remove(_settingsKey);
                    _settingsService.Save();
                }
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is forwarding.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is forwarding; otherwise, <c>false</c>.
    /// </value>
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

    /// <summary>
    /// Starts the forwarding.
    /// </summary>
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

    /// <summary>
    /// Stops the forwarding.
    /// </summary>
    private void StopForwarding()
    {
        _pfCts?.Cancel();
        Status = "Stopped";
        StopTimer();
    }

    /// <summary>
    /// Stops the timer.
    /// </summary>
    private void StopTimer()
    {
        _durationTimer.Stop();
        DurationText = "";
        _startTime = null;
    }
}
