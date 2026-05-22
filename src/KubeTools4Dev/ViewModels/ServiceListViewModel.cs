using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using k8s;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the list of services.
/// </summary>
/// <seealso cref="ViewModelBase" />
public partial class ServiceListViewModel : ViewModelBase
{
    /// <summary>
    /// All services (ViewModels)
    /// </summary>
    private readonly List<ServiceViewModel> _allServices = [];

    /// <summary>
    /// The kube service; set by <see cref="UpdateScopeAsync"/> before first use.
    /// </summary>
    private IKubernetesService? _kubeService;

    /// <summary>
    /// The current namespace filter (empty = all namespaces).
    /// </summary>
    private string _namespaceName = "";

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<ServiceListViewModel> _logger;

    /// <summary>
    /// The logger factory
    /// </summary>
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// The cluster connection manager (used for cross-cluster port-conflict detection).
    /// </summary>
    private readonly IClusterConnectionManager _connectionManager;

    /// <summary>
    /// The port forward service; set by <see cref="UpdateScopeAsync"/> before first use.
    /// </summary>
    private IPortForwardService? _portForwardService;

    /// <summary>
    /// The settings service
    /// </summary>
    private readonly ISettingsService _settingsService;
    /// <summary>
    /// The cancellation token source
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;
    /// <summary>
    /// The filter text
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// The is loading
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// The services
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServiceViewModel> _services = [];

    /// <summary>
    /// The watch task
    /// </summary>
    private Task? _watchTask;
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceListViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="connectionManager">The cluster connection manager for cross-cluster port-conflict checks.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public ServiceListViewModel(
        ISettingsService settingsService,
        IClusterConnectionManager connectionManager,
        ILogger<ServiceListViewModel> logger,
        ILoggerFactory loggerFactory)
    {
        _settingsService = settingsService;
        _connectionManager = connectionManager;
        _logger = logger;
        _loggerFactory = loggerFactory;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Cleanups this instance.
    /// </summary>
    public void Cleanup()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _portForwardService?.StopAll();
    }

    /// <summary>
    /// Switches the view to a different cluster service, port-forward service, and namespace, then re-initializes.
    /// </summary>
    public async Task UpdateScopeAsync(
        IKubernetesService kubeService,
        IPortForwardService portForwardService,
        string namespaceName)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _portForwardService?.StopAll();

        _kubeService = kubeService;
        _portForwardService = portForwardService;
        _namespaceName = namespaceName;

        await InitializeAsync();
    }

    /// <summary>
    /// Initializes the asynchronous.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_kubeService is null || _portForwardService is null) return;
        IsLoading = true;
        try
        {
            if (!_kubeService.IsConnected) return;

            var services = await _kubeService.GetServicesAsync(_namespaceName);

            // Filter out internal kubernetes service or headless
            var relevantServices = services.Where(s =>
                s.Metadata.Name != "kubernetes"
                && s.Spec.Type != "ExternalName");

            // Stop existing watch if any
            _cancellationTokenSource?.Cancel();
            if (_watchTask != null)
            {
                try
                {
                    await _watchTask;
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("Previous watch task was canceled.");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error while waiting for previous watch task to complete");
                }
            }
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _allServices.Clear();
            foreach (var svc in relevantServices)
            {
                if (svc.Spec.Ports == null) continue;

                foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
                {
                    var viewModel = new ServiceViewModel(
                        _loggerFactory.CreateLogger<ServiceViewModel>(),
                        svc,
                        port,
                        _portForwardService,
                        _settingsService)
                    {
                        IsPortInUseCheck = _connectionManager.IsLocalPortInUse
                    };
                    viewModel.PropertyChanged += OnServicePropertyChanged;
                    _allServices.Add(viewModel);
                }
            }
            UpdateFilteredList();

            // Start Watch
            _watchTask = WatchServicesAsync(_cancellationTokenSource.Token);
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

    /// <summary>
    /// Determines whether this instance [can stop all].
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance [can stop all]; otherwise, <c>false</c>.
    /// </returns>
    private bool CanStopAll() => _allServices.Any(s => s.IsForwarding);

    /// <summary>
    /// Forwards all.
    /// </summary>
    [RelayCommand]
    private async Task ForwardAll()
    {
        foreach (var svc in Services)
        {
            if (!svc.IsForwarding && !svc.IsExcluded)
            {
                svc.IsForwarding = true;
            }
        }
    }

    /// <summary>
    /// Called when [filter text changed].
    /// </summary>
    /// <param name="value">The value.</param>
    partial void OnFilterTextChanged(string value)
    {
        UpdateFilteredList();
    }

    /// <summary>
    /// Called when [service property changed].
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
    private void OnServicePropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServiceViewModel.IsForwarding))
        {
            StopAllCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Called when [settings changed].
    /// </summary>
    private void OnSettingsChanged()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            UpdateFilteredList();
            foreach (var svc in Services)
            {
                // Create key to check
                var key = $"{svc.Namespace}/{svc.Name}:{svc.TargetPortDisplay}";
                bool shouldBeExcluded = _settingsService.Services.ExcludedServices.Contains(key);
                if (svc.IsExcluded != shouldBeExcluded)
                {
                    svc.IsExcluded = shouldBeExcluded;
                }
            }
        });
    }
    /// <summary>
    /// Reconciles the stale services.
    /// </summary>
    private async Task ReconcileStaleServices()
    {
        try
        {
            var currentServices = await _kubeService!.GetServicesAsync();
            var currentKeys = new HashSet<string>(currentServices.Select(s => $"{s.Metadata.NamespaceProperty}/{s.Metadata.Name}"));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Find VMs that are no longer in the cluster
                var staleViewModels = _allServices
                    .Where(viewModel =>
                        !currentKeys.Contains($"{viewModel.Namespace}/{viewModel.Name}"))
                    .ToList();

                foreach (var viewModel in staleViewModels)
                {
                    if (viewModel.IsForwarding)
                    {
                        viewModel.IsForwarding = false;
                    }
                    viewModel.PropertyChanged -= OnServicePropertyChanged;
                    _allServices.Remove(viewModel);
                }

                if (staleViewModels.Count != 0)
                {
                    UpdateFilteredList();
                }
            });
        }
        catch (Exception)
        {
            _logger.LogWarning("Failed to reconcile stale services");
        }
    }

    /// <summary>
    /// Stops all.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopAll))]
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
    /// <summary>
    /// Updates the filtered list.
    /// </summary>
    private void UpdateFilteredList()
    {
        var query = _allServices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            query = query.Where(s =>
                (s.Name?.Contains(
                    FilterText,
                    StringComparison.OrdinalIgnoreCase) ?? false)
                || (s.Namespace?.Contains(
                    FilterText,
                    StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Apply Settings Filter (Hidden Services)
        if (_settingsService.Services?.HiddenServiceNames != null)
        {
            query = query.Where(s => !_settingsService.Services.HiddenServiceNames.Contains(s.Name));
        }

        var sorted = query
            .OrderBy(s => s.Namespace)
            .ThenBy(s => s.Name)
            .ToList();

        // Sync local collection 

        // 1. Remove items not in sorted
        for (int i = Services.Count - 1; i >= 0; i--)
        {
            if (!sorted.Contains(Services[i]))
            {
                Services.RemoveAt(i);
            }
        }

        // 2. Add or Move items
        for (int i = 0; i < sorted.Count; i++)
        {
            var item = sorted[i];

            if (i >= Services.Count)
            {
                Services.Add(item);
            }
            else if (Services[i] != item)
            {
                int oldIndex = Services.IndexOf(item);
                if (oldIndex >= 0)
                {
                    Services.Move(oldIndex, i);
                }
                else
                {
                    Services.Insert(i, item);
                }
            }
        }
    }

    /// <summary>
    /// Watches the services asynchronous.
    /// </summary>
    /// <param name="token">The token.</param>
    private async Task WatchServicesAsync(CancellationToken token)
    {
        var reconcileFailureCount = 0;
        const int MaxReconcileFailures = 5;
        while (!token.IsCancellationRequested)
        {
            try
            {
                try
                {
                    await ReconcileStaleServices();
                    reconcileFailureCount = 0;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    reconcileFailureCount++;
                    if (reconcileFailureCount >= MaxReconcileFailures)
                    {
                        _logger.LogError("Stopping service watch after repeated reconciliation failures.");
                        break;
                    }
                    await Task.Delay(5000, token);
                    continue;
                }

                await foreach (var (type, item) in _kubeService!.WatchServicesAsync(_namespaceName, cancellationToken: token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (item.Metadata.Name == "kubernetes" || item.Spec.Type == "ExternalName") return;

                        var existingViewModels = _allServices
                            .Where(service =>
                                service.Name == item.Metadata.Name
                                && service.Namespace == item.Metadata.NamespaceProperty)
                            .ToList();

                        if (type == WatchEventType.Deleted)
                        {
                            foreach (var vm in existingViewModels)
                            {
                                vm.PropertyChanged -= OnServicePropertyChanged;
                                _allServices.Remove(vm);
                            }
                        }
                        else
                        {
                            if (item.Spec.Ports != null)
                            {
                                var newPorts = item.Spec.Ports
                                    .Where(p => p.Protocol == "TCP")
                                    .ToList();

                                foreach (var viewModel in existingViewModels.ToList())
                                {
                                    if (!newPorts.Any(p => p.Port == (int)viewModel.TargetPort))
                                    {
                                        viewModel.PropertyChanged -= OnServicePropertyChanged;
                                        _allServices.Remove(viewModel);
                                    }
                                }

                                foreach (var port in newPorts)
                                {
                                    var newId = $"{item.Metadata.NamespaceProperty}/{item.Metadata.Name}:{port.Port}";
                                    if (!_allServices.Any(s => s.Id == newId))
                                    {
                                        var newVm = new ServiceViewModel(
                                            _loggerFactory.CreateLogger<ServiceViewModel>(),
                                            item,
                                            port,
                                            _portForwardService!,
                                            _settingsService)
                                        {
                                            IsPortInUseCheck = _connectionManager.IsLocalPortInUse
                                        };
                                        newVm.PropertyChanged += OnServicePropertyChanged;
                                        _allServices.Add(newVm);
                                    }
                                }
                            }
                        }
                        UpdateFilteredList();
                    });
                }
                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Service watch cancelled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watch Service Error");
                await Task.Delay(5000, token);
            }
        }
    }
}