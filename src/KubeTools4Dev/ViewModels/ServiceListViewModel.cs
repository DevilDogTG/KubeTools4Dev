using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
/// <seealso cref="ViewModelBase" />
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceListViewModel"/> class.
/// </remarks>
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
    private readonly List<ServiceViewModel> _allServices = [];
    private CancellationTokenSource? _cts;

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
    /// Cleanups this instance.
    /// </summary>
    public void Cleanup()
    {
        _cts?.Cancel();
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
            _cts = new CancellationTokenSource();

            _allServices.Clear();
            foreach (var svc in relevantServices)
            {
                if (svc.Spec.Ports == null) continue;

                foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
                {
                    _allServices.Add(new ServiceViewModel(svc, port, portForwardService, settingsService));
                }
            }
            UpdateFilteredList();

            // Start Watch
            _ = WatchServicesAsync(_cts.Token);
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
    /// Stops all.
    /// </summary>
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
                var staleVMs = _allServices
                    .Where(vm => !currentKeys.Contains($"{vm.Namespace}/{vm.Name}"))
                    .ToList();

                foreach (var vm in staleVMs)
                {
                    _allServices.Remove(vm);
                    vm.IsForwarding = false; // Ensure background task stops
                }
                
                if (staleVMs.Any())
                {
                    UpdateFilteredList();
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reconcile stale services");
        }
    }

    private void UpdateFilteredList()
    {
        // Simple list update, assume no filtering for now or add if needed later
        var sorted = _allServices.OrderBy(s => s.Namespace).ThenBy(s => s.Name).ToList();

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
        // Since we want to preserve order, we might need to clear and add if strict order is required.
        // But to avoid UI flicker, let's try to be smart or just Clear/Add for simplicity as done in PodList
        // However, we want to KEEP references to VMs if they already exist to preserve state (PortForwarding status)
        // Since _allServices KEEPS the references if we update them in Watch, we can just repopulate if list changed size or order.

        // Actually, easiest way given Avalonia's diffing capability usually handles it well, 
        // but let's try to match existing items.

        Services.Clear();
        foreach (var item in sorted)
        {
            Services.Add(item);
        }
    }

    private async Task WatchServicesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Prune stale services before starting/restarting watch
                // This handles cases where we missed DELETED events (e.g. disconnect)
                await ReconcileStaleServices();

                await foreach (var (type, item) in kubeService.WatchServicesAsync("", cancellationToken: token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (item.Metadata.Name == "kubernetes" || item.Spec.Type == "ExternalName") return;

                        // Identify existing items for this service
                        // A Service might spawn multiple ViewModels (one per Port)
                        // We need to match by Name/Namespace

                        var existingVMs = _allServices.Where(s => s.Name == item.Metadata.Name && s.Namespace == item.Metadata.NamespaceProperty).ToList();

                        if (type == k8s.WatchEventType.Deleted)
                        {
                            foreach (var vm in existingVMs)
                            {
                                _allServices.Remove(vm);
                            }
                        }
                        else
                        {
                            // Added or Modified.
                            // If modified, ports could change.

                            // 1. Update existing VMs
                            foreach (var vm in existingVMs)
                            {
                                vm.Update(item);
                            }

                            // 2. Check for new ports or removed ports?
                            // This is complex because we split 1 Service into N ViewModels.
                            // Simplified approach: 
                            // If Added, add all ports.
                            // If Modified, strictly speaking we should reconcile ports.
                            // For now, if we assume ports don't change often dynamically, we can just Update metadata.
                            // But if they DO change, our list is stale.

                            // Robust approach for Modified:
                            // Remove all existing VMs for this service and re-add them?
                            // BAD: Kills port forwarding state.

                            // Middle ground:
                            // Find ports in new item.
                            if (item.Spec.Ports != null)
                            {
                                var newPorts = item.Spec.Ports.Where(p => p.Protocol == "TCP").ToList();
                                
                                // Remove VMs for ports that no longer exist
                                foreach (var vm in existingVMs.ToList())
                                {
                                    // Check if vm.TargetPort (original port) still exists in newPorts
                                    // vm.TargetPort is object, stored as int from port.Port
                                    if (!newPorts.Any(p => p.Port == (int)vm.TargetPort))
                                    {
                                        _allServices.Remove(vm);
                                    }
                                }

                                // Add VMs for new ports
                                foreach (var port in newPorts)
                                {
                                    var newId = $"{item.Metadata.NamespaceProperty}/{item.Metadata.Name}:{port.Port}";
                                    if (!_allServices.Any(s => s.Id == newId))
                                    {
                                         _allServices.Add(new ServiceViewModel(item, port, portForwardService, settingsService));
                                    }
                                }
                            }
                            
                            if (existingVMs.Count == 0 && type == WatchEventType.Added)
                            {
                                // Fresh add
                                if (item.Spec.Ports != null)
                                {
                                    foreach (var port in item.Spec.Ports.Where(p => p.Protocol == "TCP"))
                                    {
                                        var newId = $"{item.Metadata.NamespaceProperty}/{item.Metadata.Name}:{port.Port}";
                                        if (!_allServices.Any(s => s.Id == newId))
                                        {
                                            _allServices.Add(new ServiceViewModel(item, port, portForwardService, settingsService));
                                        }
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
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Watch Service Error");
                // Backoff
                await Task.Delay(5000, token);
            }
        }
    }
}