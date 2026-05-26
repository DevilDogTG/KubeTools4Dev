using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using k8s;
using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
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
    /// The cluster identifier; set by <see cref="UpdateScopeAsync"/> and used to load/save profiles.
    /// </summary>
    private string _clusterId = string.Empty;

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
    /// Gets or sets a value indicating whether the service list is loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// The services
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServiceViewModel> _services = [];

    /// <summary>
    /// Gets or sets the list of port-forward profiles for the current cluster.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PortForwardProfileViewModel> _profiles = [];

    /// <summary>
    /// Gets or sets the currently selected port-forward profile.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private PortForwardProfileViewModel? _selectedProfile;

    /// <summary>
    /// Gets or sets a value indicating whether the active profile is currently forwarding.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopProfileCommand))]
    private bool _isProfileRunning;

    /// <summary>
    /// Gets or sets a value indicating whether the new-profile name input row is visible.
    /// </summary>
    [ObservableProperty]
    private bool _isProfileNameInputVisible;

    /// <summary>
    /// Gets or sets the name entered by the user when creating a new profile.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProfileCommand))]
    private string _newProfileName = string.Empty;

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
    /// Switches the view to a different cluster, port-forward service, and namespace, then re-initializes.
    /// </summary>
    /// <param name="kubeService">The Kubernetes service for the cluster.</param>
    /// <param name="portForwardService">The port-forward service for the cluster.</param>
    /// <param name="namespaceName">The namespace to scope the view to (empty = all namespaces).</param>
    /// <param name="clusterId">The cluster identifier used to load/save profiles.</param>
    public async Task UpdateScopeAsync(
        IKubernetesService kubeService,
        IPortForwardService portForwardService,
        string namespaceName,
        string clusterId)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _portForwardService?.StopAll();

        _allServices.Clear();
        UpdateFilteredList();

        _kubeService = kubeService;
        _portForwardService = portForwardService;
        _namespaceName = namespaceName;

        if (_clusterId != clusterId)
        {
            _clusterId = clusterId;
            IsProfileRunning = false;
            LoadProfiles();
        }

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
                        IsPortInUseCheck = _connectionManager.IsLocalPortInUse,
                        AddToProfileCallback = OnServiceAddToProfile,
                        RemoveFromProfileCallback = OnServiceRemoveFromProfile
                    };
                    viewModel.IsInSelectedProfile = IsServiceInSelectedProfile(viewModel);
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

    // ── Profile commands ─────────────────────────────────────────────────────

    /// <summary>
    /// Shows the profile name input field so the user can type a name before confirming.
    /// </summary>
    [RelayCommand]
    private void ShowCreateProfileInput()
    {
        NewProfileName = string.Empty;
        IsProfileNameInputVisible = true;
    }

    /// <summary>
    /// Hides the profile name input without creating a profile.
    /// </summary>
    [RelayCommand]
    private void CancelCreateProfile()
    {
        IsProfileNameInputVisible = false;
        NewProfileName = string.Empty;
    }

    /// <summary>
    /// Creates a new profile with <see cref="NewProfileName"/> and saves it.
    /// </summary>
    private bool CanCreateProfile() =>
        !string.IsNullOrWhiteSpace(NewProfileName);

    /// <summary>
    /// Creates a new profile with the name provided in <see cref="NewProfileName"/> and saves it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateProfile))]
    private void CreateProfile()
    {
        var profile = new PortForwardProfile
        {
            Id = Guid.NewGuid(),
            Name = NewProfileName.Trim()
        };

        var vm = new PortForwardProfileViewModel(profile);
        Profiles.Add(vm);
        SelectedProfile = vm;

        SaveProfiles();

        IsProfileNameInputVisible = false;
        NewProfileName = string.Empty;
    }

    /// <summary>
    /// Deletes the currently selected profile and saves.
    /// </summary>
    private bool CanDeleteProfile() => SelectedProfile is not null;

    /// <summary>
    /// Removes the currently selected profile from the list and saves.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteProfile))]
    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;

        if (IsProfileRunning)
        {
            // Stop any active forwards belonging to this profile first.
            StopProfileForwards();
        }

        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
        SaveProfiles();
    }

    /// <summary>
    /// Starts forwarding all services listed in the selected profile.
    /// </summary>
    private bool CanStartProfile() =>
        SelectedProfile is not null &&
        SelectedProfile.Entries.Count > 0 &&
        !IsProfileRunning;

    /// <summary>
    /// Starts port-forwarding for every entry in the selected profile that has a matching
    /// loaded service view model.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStartProfile))]
    private void StartProfile()
    {
        if (SelectedProfile is null) return;

        foreach (var entry in SelectedProfile.Entries)
        {
            var match = FindServiceViewModel(entry.Namespace, entry.ServiceName, entry.TargetPort);
            if (match is null || match.IsForwarding || match.IsExcluded) continue;

            if (entry.LocalPort > 0)
                match.LocalPort = entry.LocalPort;

            match.IsForwarding = true;
        }

        IsProfileRunning = true;
    }

    /// <summary>
    /// Stops port-forwarding for every entry in the selected profile.
    /// </summary>
    private bool CanStopProfile() => IsProfileRunning;

    /// <summary>
    /// Stops all port-forwards that were started by the selected profile.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStopProfile))]
    private void StopProfile()
    {
        StopProfileForwards();
        IsProfileRunning = false;
    }

    // ── Profile helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Called when a <see cref="ServiceViewModel"/> requests to be added to the selected profile.
    /// </summary>
    private void OnServiceAddToProfile(ServiceViewModel svm)
    {
        if (SelectedProfile is null) return;
        if (IsServiceInSelectedProfile(svm)) return;

        var entry = new PortForwardProfileEntry
        {
            Namespace = svm.Namespace,
            ServiceName = svm.Name,
            TargetPort = svm.TargetPortDisplay,
            LocalPort = svm.LocalPort
        };

        SelectedProfile.AddEntry(entry);
        svm.IsInSelectedProfile = true;
        SaveProfiles();
    }

    /// <summary>
    /// Called when a <see cref="ServiceViewModel"/> requests to be removed from the selected profile.
    /// </summary>
    private void OnServiceRemoveFromProfile(ServiceViewModel svm)
    {
        if (SelectedProfile is null) return;

        var entryVm = SelectedProfile.Entries.FirstOrDefault(e =>
            e.Namespace == svm.Namespace &&
            e.ServiceName == svm.Name &&
            e.TargetPort == svm.TargetPortDisplay);

        if (entryVm is null) return;

        SelectedProfile.RemoveEntry(entryVm);
        svm.IsInSelectedProfile = false;
        SaveProfiles();
    }

    /// <summary>
    /// Stops all active forwards that belong to the currently selected profile.
    /// </summary>
    private void StopProfileForwards()
    {
        if (SelectedProfile is null) return;

        foreach (var entry in SelectedProfile.Entries)
        {
            var match = FindServiceViewModel(entry.Namespace, entry.ServiceName, entry.TargetPort);
            if (match is not null && match.IsForwarding)
                match.IsForwarding = false;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="svm"/> matches an entry in the selected profile.
    /// </summary>
    private bool IsServiceInSelectedProfile(ServiceViewModel svm) =>
        SelectedProfile?.Contains(svm.Namespace, svm.Name, svm.TargetPortDisplay) ?? false;

    /// <summary>
    /// Updates <see cref="ServiceViewModel.IsInSelectedProfile"/> on every loaded service VM.
    /// </summary>
    private void RefreshIsInSelectedProfile()
    {
        foreach (var svm in _allServices)
            svm.IsInSelectedProfile = IsServiceInSelectedProfile(svm);
    }

    /// <summary>
    /// Finds the first <see cref="ServiceViewModel"/> in <see cref="_allServices"/> that matches
    /// the given namespace, service name, and target port string.
    /// </summary>
    private ServiceViewModel? FindServiceViewModel(string ns, string name, string targetPort) =>
        _allServices.FirstOrDefault(s =>
            s.Namespace == ns &&
            s.Name == name &&
            s.TargetPortDisplay == targetPort);

    /// <summary>
    /// Loads profiles for the current cluster from settings into <see cref="Profiles"/>.
    /// </summary>
    private void LoadProfiles()
    {
        Profiles.Clear();
        SelectedProfile = null;

        if (string.IsNullOrEmpty(_clusterId)) return;

        var clusterEntry = FindClusterEntry();
        if (clusterEntry is null) return;

        foreach (var profile in clusterEntry.PortForwardProfiles)
            Profiles.Add(new PortForwardProfileViewModel(profile));

        SelectedProfile = Profiles.FirstOrDefault();
    }

    /// <summary>
    /// Persists all profiles for the current cluster back to settings.
    /// </summary>
    private void SaveProfiles()
    {
        if (string.IsNullOrEmpty(_clusterId)) return;

        var clusterEntry = FindClusterEntry();
        if (clusterEntry is null) return;

        clusterEntry.PortForwardProfiles = [.. Profiles.Select(p => p.Model)];
        _settingsService.Save();
    }

    /// <summary>
    /// Finds the <see cref="ClusterEntry"/> for the current <see cref="_clusterId"/>.
    /// </summary>
    private ClusterEntry? FindClusterEntry() =>
        _settingsService.Clusters.Clusters
            .FirstOrDefault(c => c.Id.ToString() == _clusterId);

    /// <summary>
    /// Called when <see cref="SelectedProfile"/> changes: refreshes in-profile indicators and
    /// resets running state.
    /// </summary>
    partial void OnSelectedProfileChanged(PortForwardProfileViewModel? value)
    {
        IsProfileRunning = false;
        RefreshIsInSelectedProfile();
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
        // Nothing extra needed here for now; profile running state is managed explicitly.
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
                                            IsPortInUseCheck = _connectionManager.IsLocalPortInUse,
                                            AddToProfileCallback = OnServiceAddToProfile,
                                            RemoveFromProfileCallback = OnServiceRemoveFromProfile
                                        };
                                        newVm.IsInSelectedProfile = IsServiceInSelectedProfile(newVm);
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