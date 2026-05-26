using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for a single service.
/// </summary>
/// <seealso cref="ObservableObject" />
public partial class ServiceViewModel : ObservableObject
{
    /// <summary>
    /// The duration timer
    /// </summary>
    private readonly DispatcherTimer _durationTimer;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<ServiceViewModel> _logger;

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
    /// Optional callback that returns <c>true</c> when a local port is already in use by another cluster's port-forward.
    /// When set, checked before starting a new forward; if the port is taken, the forward is rejected with a warning.
    /// </summary>
    public Func<int, bool>? IsPortInUseCheck { get; set; }

    /// <summary>
    /// Optional callback invoked when the user requests to add this service to the selected profile.
    /// Set by <see cref="ServiceListViewModel"/>.
    /// </summary>
    public Action<ServiceViewModel>? AddToProfileCallback { get; set; }

    /// <summary>
    /// Optional callback invoked when the user requests to remove this service from the selected profile.
    /// Set by <see cref="ServiceListViewModel"/>.
    /// </summary>
    public Action<ServiceViewModel>? RemoveFromProfileCallback { get; set; }

    /// <summary>
    /// The duration text
    /// </summary>
    [ObservableProperty]
    private string _durationText = "";

    /// <summary>
    /// Gets or sets a value indicating whether this service is part of the currently selected
    /// port-forward profile.  Updated by <see cref="ServiceListViewModel"/> when the selection
    /// changes or entries are added/removed.
    /// </summary>
    [ObservableProperty]
    private bool _isInSelectedProfile;
    /// <summary>
    /// The forwarding task
    /// </summary>
    private Task? _forwardingTask;

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
    /// The pf cancellation token source
    /// </summary>
    private CancellationTokenSource? _pfCancellationTokenSource;
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
    /// Initializes a new instance of the <see cref="ServiceViewModel" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="service">The service.</param>
    /// <param name="port">The port.</param>
    /// <param name="pfService">The pf service.</param>
    /// <param name="settingsService">The settings service.</param>
    public ServiceViewModel(
        ILogger<ServiceViewModel> logger,
        V1Service service,
        V1ServicePort port,
        IPortForwardService pfService,
        ISettingsService settingsService)
    {
        _logger = logger;
        _service = service;
        _port = port;
        _pfService = pfService;
        _settingsService = settingsService;

        Name = service.Metadata.Name;
        Namespace = service.Metadata.NamespaceProperty;
        _settingsKey = $"{Namespace}/{Name}:{port.Port}";

        // Use the Service Port as the destination by default
        TargetPort = port.Port;
        LocalPort = port.Port; // Default to same port

        // Display 
        TargetPortDisplay = port.Port.ToString();

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
    /// Gets the unique identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public string Id => $"{Namespace}/{Name}:{TargetPort}";

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
    /// Adds this service to the currently selected profile via <see cref="AddToProfileCallback"/>.
    /// </summary>
    [RelayCommand]
    private void AddToProfile() => AddToProfileCallback?.Invoke(this);

    /// <summary>
    /// Removes this service from the currently selected profile via <see cref="RemoveFromProfileCallback"/>.
    /// </summary>
    [RelayCommand]
    private void RemoveFromProfile() => RemoveFromProfileCallback?.Invoke(this);

    /// <summary>
    /// Opens the browser.
    /// </summary>
    [RelayCommand]
    private void OpenBrowser()
    {
        if (LocalPort > 0)
        {
            try
            {
                var url = $"http://localhost:{LocalPort}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }
    }

    /// <summary>
    /// Starts the forwarding.
    /// </summary>
    private async void StartForwarding()
    {
        if (IsPortInUseCheck?.Invoke(LocalPort) == true)
        {
            Status = $"Port {LocalPort} in use";
            IsForwarding = false;
            return;
        }

        Status = "Starting";
        _pfCancellationTokenSource?.Cancel();

        if (_forwardingTask != null)
        {
            try
            {
                await _forwardingTask;
            }
            catch (Exception)
            {
                _logger.Information("Waiting old task, prevent race condition.");
            }
        }
        _pfCancellationTokenSource?.Dispose();

        _pfCancellationTokenSource = new CancellationTokenSource();
        var token = _pfCancellationTokenSource.Token;

        try
        {
            // Run in background
            _forwardingTask = Task.Run(async () =>
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

                    await _pfService.StartServicePortForwardAsync(Name, Namespace, TargetPort, LocalPort, token);
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
        _pfCancellationTokenSource?.Cancel();

        // Don't dispose immediately, let the task finish or next StartForwarding handle it
        // Or we can fire-and-forget a cleanup if we want to be strict, but for now relying on next StartForwarding is safer than disposed exception.
        // But to avoid "leak" if never started again, we can try to await if possible, but this is sync.
        // We will just Cancel here. The next StartForwarding or Cleanup (if we added it) would Dispose.
        // Actually, let's replicate the safe cleanup pattern: Use a local reference to clean up asynchronously.
        var ctsToDispose = _pfCancellationTokenSource;
        var taskToAwait = _forwardingTask;

        _pfCancellationTokenSource = null;
        _forwardingTask = null;

        if (ctsToDispose != null)
        {
            _ = Task.Run(async () =>
            {
                if (taskToAwait != null)
                {
                    try { await taskToAwait; } catch { }
                }
                ctsToDispose.Dispose();
            });
        }

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
