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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the list of services.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceListViewModel" /> class.
/// </remarks>
/// <seealso cref="ViewModelBase" />
/// <param name="kubeService">The kube service.</param>
/// <param name="portForwardService">The port forward service.</param>
/// <param name="settingsService">The settings service.</param>
/// <param name="logger">The logger.</param>
public partial class ServiceListViewModel(
    IKubernetesService kubeService,
    IPortForwardService portForwardService,
    ISettingsService settingsService,
    ILogger<ServiceListViewModel> logger
) : ViewModelBase
{
    /// <summary>
    /// All services
    /// </summary>
    private readonly List<ServiceViewModel> _allServices = [];
    /// <summary>
    /// The cancellation token source used to manage cancellation of the service watcher.
    /// </summary>
    private CancellationTokenSource? _cts;

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
    /// Cleanups this instance.
    /// </summary>
    public void Cleanup()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        portForwardService.StopAll();
    }

    /// <summary>
    /// Initializes the asynchronous.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            if (!kubeService.IsConnected) return;

            var services = await kubeService.GetServicesAsync();

            // Filter out internal kubernetes service or headless
            var relevantServices = services.Where(s => s.Metadata.Name != "kubernetes" && s.Spec.Type != "ExternalName");

            // Stop existing watch if any
            _cts?.Cancel();
            if (_watchTask != null)
            {
                try
                {
                    await _watchTask;
                }
                catch (OperationCanceledException)
                {
                    logger.Information("The watch task is cancelled");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error while waiting for previous watch task to complete");
                }
            }
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            _allServices.Clear();
            foreach (var svc in relevantServices)
            {
                if (svc.Spec.Ports == null) continue;

                foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
                {
                    var vm = new ServiceViewModel(svc, port, portForwardService, settingsService);
                    vm.PropertyChanged += OnServicePropertyChanged;
                    _allServices.Add(vm);
                }
            }
            UpdateFilteredList();

            // Start Watch
            _watchTask = WatchServicesAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize service list");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Determines whether this instance can stop all.
    /// </summary>
    /// <returns>
    ///   <c>true</c> if this instance can stop all; otherwise, <c>false</c>.
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
                svc.IsForwarding = true; // Triggers the command in setter
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
    /// Handles the service property changed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
    private void OnServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServiceViewModel.IsForwarding))
        {
            StopAllCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>
    /// Reconciles the stale services.
    /// </summary>
    private async Task ReconcileStaleServices()
    {
        try
        {
            var currentServices = await kubeService.GetServicesAsync();
            var currentKeys = new HashSet<string>(currentServices.Select(s => $"{s.Metadata.NamespaceProperty}/{s.Metadata.Name}"));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Find VMs that are no longer in the cluster
                var staleViewModels = _allServices
                    .Where(viewModel =>
                        !currentKeys.Contains($"{viewModel.Namespace}/{viewModel.Name}"))
                    .ToList();

                foreach (var vm in staleViewModels)
                {
                    vm.PropertyChanged -= OnServicePropertyChanged;
                    _allServices.Remove(vm);
                    vm.IsForwarding = false; // Ensure background task stops
                }

                if (staleViewModels.Count != 0)
                {
                    UpdateFilteredList();
                }
            });
        }
        catch (Exception)
        {
            logger.Warning("Failed to reconcile stale services");
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

        var sorted = query
            .OrderBy(s => s.Namespace)
            .ThenBy(s => s.Name)
            .ToList();

        // Sync local collection with sorted list to minimize UI updates

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

            // If we are at the end of the existing list, just add remaining
            if (i >= Services.Count)
            {
                Services.Add(item);
            }
            // If the item at current position is different
            else if (Services[i] != item)
            {
                // Check if the item exists later in the list
                int oldIndex = Services.IndexOf(item);
                if (oldIndex >= 0)
                {
                    // Move it to the current position
                    Services.Move(oldIndex, i);
                }
                else
                {
                    // It's a new item, insert it
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
                // Prune stale services before starting/restarting watch
                // This handles cases where we missed DELETED events (e.g. disconnect)
                try
                {
                    await ReconcileStaleServices();
                    reconcileFailureCount = 0;
                }
                catch (OperationCanceledException)
                {
                    // Preserve cancellation semantics
                    throw;
                }
                catch (Exception)
                {
                    reconcileFailureCount++;
                    LogReconcileStaleServicesFailed(
                        reconcileFailureCount,
                        MaxReconcileFailures);
                    if (reconcileFailureCount >= MaxReconcileFailures)
                    {
                        logger.Error("Stopping service watch after repeated reconciliation failures.");
                        break;
                    }
                    // Wait briefly before retrying reconciliation
                    await Task.Delay(5000, token);
                    continue;
                }

                await foreach (var (type, item) in kubeService.WatchServicesAsync("", cancellationToken: token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (item.Metadata.Name == "kubernetes" || item.Spec.Type == "ExternalName") return;

                        // Identify existing items for this service
                        // A Service might spawn multiple ViewModels (one per Port)
                        // We need to match by Name/Namespace
                        var existingViewModels = _allServices.Where(s => s.Name == item.Metadata.Name && s.Namespace == item.Metadata.NamespaceProperty).ToList();

                        // Deleted
                        if (type == WatchEventType.Deleted)
                        {
                            foreach (var vm in existingViewModels)
                            {
                                vm.PropertyChanged -= OnServicePropertyChanged;
                                _allServices.Remove(vm);
                            }
                        }
                        // Added or Modified.
                        else
                        {
                            // Find ports in new item.
                            if (item.Spec.Ports != null)
                            {
                                var newPorts = item.Spec.Ports.Where(p => p.Protocol == "TCP").ToList();

                                // Remove ViewModels for ports that no longer exist
                                foreach (var viewModel in existingViewModels.ToList())
                                {
                                    // Check if the service port represented by viewModel.TargetPort still exists in newPorts
                                    // viewModel.TargetPort stores the Service port (port.Port) as an int boxed as object, not the pod's targetPort
                                    if (!newPorts.Any(p => p.Port == (int)viewModel.TargetPort))
                                    {
                                        viewModel.PropertyChanged -= OnServicePropertyChanged;
                                        _allServices.Remove(viewModel);
                                    }
                                }

                                // Add ViewModels for new ports
                                foreach (var port in newPorts)
                                {
                                    var newId = $"{item.Metadata.NamespaceProperty}/{item.Metadata.Name}:{port.Port}";
                                    if (!_allServices.Any(s => s.Id == newId))
                                    {
                                        var newVm = new ServiceViewModel(
                                            item,
                                            port,
                                            portForwardService,
                                            settingsService);
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
                logger.Warning("Service watch cancelled.");
                break;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Watch Service Error");
                await Task.Delay(5000, token);
            }
        }
    }
}