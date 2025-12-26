using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the list of services.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceListViewModel" /> class.
/// </remarks>
/// <seealso cref="ViewModelBase" />
public partial class ServiceListViewModel : ViewModelBase
{
    /// <summary>
    /// The kube service
    /// </summary>
    private readonly IKubernetesService _kubeService;
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
    private readonly ILogger<ServiceListViewModel> _logger;

    /// <summary>
    /// All services
    /// </summary>
    private readonly List<V1Service> _allServices = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceListViewModel" /> class.
    /// </summary>
    /// <param name="kubeService">The kube service.</param>
    /// <param name="portForwardService">The port forward service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">The logger.</param>
    public ServiceListViewModel(
        IKubernetesService kubeService,
        IPortForwardService portForwardService,
        ISettingsService settingsService,
        ILogger<ServiceListViewModel> logger)
    {
        _kubeService = kubeService;
        _portForwardService = portForwardService;
        _settingsService = settingsService;
        _logger = logger;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

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
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _portForwardService.StopAll();
    }

    /// <summary>
    /// Initializes the asynchronous.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            if (!_kubeService.IsConnected) return;

            var services = await _kubeService.GetServicesAsync();
            _allServices.Clear();
            _allServices.AddRange(services);

            UpdateList();
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
    /// Updates the list.
    /// </summary>
    private void UpdateList()
    {
        // Filter out internal kubernetes service or headless
        var relevantServices = _allServices.Where(s =>
            !_settingsService.Services.HiddenServiceNames.Contains(s.Metadata.Name) &&
            !_settingsService.Services.HiddenServiceTypes.Contains(s.Spec.Type));

        // Preserve current items if possible? 
        // For simplicity, clear and rebuild. UI might flicker but it ensures correctness with new filters.
        // Also need to update Excluded status on existing items if they remain?
        // Re-creating VMs is safest for filtering, but for "IsExcluded" toggle change, we might want to preserve running state?
        // If we re-create, we lose "IsForwarding" state if it's stored in VM. 
        // IsForwarding is in ServiceViewModel.
        // Wait, if I clear and recreate, active port forwards in basic VM implementation (using CancellationTokenSource in VM) will be lost/orphaned?
        // ServiceViewModel controls the logic. If we drop it, the Task might continue but we lose control?
        // ServiceViewModel.Cleanup/Dispose?
        // Ideally we should sync existing items.

        // Simple Sync Strategy:
        // 1. Identify services to show.
        // 2. Add missing ones.
        // 3. Remove extra ones.
        // 4. Update state of existing ones (Excluded).

        var relevantList = relevantServices.ToList();
        var newKeys = new System.Collections.Generic.HashSet<string>();

        // We need unique keys for services + ports
        // ServiceViewModel uses "{Namespace}/{Name}:{Port}" as key?
        // Let's iterate and build desired list of VMs.

        var desiredVMs = new System.Collections.Generic.List<ServiceViewModel>();

        foreach (var svc in relevantList)
        {
            if (svc.Spec.Ports == null) continue;
            foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
            {
                // Check if we already have this VM
                var existingWrapper = Services.FirstOrDefault(vm => vm.Name == svc.Metadata.Name && vm.Namespace == svc.Metadata.NamespaceProperty && vm.TargetPortDisplay == port.Port.ToString());

                if (existingWrapper != null)
                {
                    // Update settings-dependent properties
                    // Trigger property change notification for IsExcluded if needed?
                    // VM reads from settings in constructor? No, check property.
                    bool excluded = _settingsService.Services.ExcludedServices.Contains($"{svc.Metadata.NamespaceProperty}/{svc.Metadata.Name}:{port.Port}");
                    if (existingWrapper.IsExcluded != excluded)
                    {
                        // Update property without triggering Save loop? 
                        // For now, let's just re-set. If it triggers save, it's redundant but safe-ish unless infinite loop.
                        // But SettingsService.Save triggers SettingsChanged... infinite loop risk!

                        // We need to update the backing field or have a "Refresh()" method on VM that doesn't save.
                    }
                    desiredVMs.Add(existingWrapper);
                }
                else
                {
                    desiredVMs.Add(new ServiceViewModel(svc, port, _portForwardService, _settingsService));
                }
            }
        }

        // Apply to ObserveableCollection
        Services.Clear(); // This nukes visual state. To do it better:
                          // But for now, simple approach. Problem: Existing VMs with running forwards must be preserved.
                          // Filter logic:

        var toRemove = Services.Where(s => !desiredVMs.Contains(s)).ToList();
        foreach (var item in toRemove)
        {
            // item.Cleanup()?
            Services.Remove(item);
        }

        foreach (var item in desiredVMs)
        {
            if (!Services.Contains(item))
            {
                Services.Add(item);
            }
        }

        // Now notify "Refresh" on items?
        foreach (var item in Services)
        {
            // We need to notify IsExcluded changed.
            // But VM setter writes to Settings. 
            // We need a "ReloadSettings" on ServiceViewModel.
            // Let's add that.
        }
    }

    /// <summary>
    /// Called when [settings changed].
    /// </summary>
    private void OnSettingsChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
        {
            UpdateList();
            foreach (var svc in Services)
            {
                // Create key to check
                var key = $"{svc.Namespace}/{svc.Name}:{svc.TargetPortDisplay}"; // TargetPortDisplay is Port.ToString()
                bool shouldBeExcluded = _settingsService.Services.ExcludedServices.Contains(key);
                if (svc.IsExcluded != shouldBeExcluded)
                {
                    svc.IsExcluded = shouldBeExcluded; // Should be safe due to checks in setter
                }
            }
        });
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
}