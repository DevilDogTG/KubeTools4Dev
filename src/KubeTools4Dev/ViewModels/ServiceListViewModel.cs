using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using k8s;
using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services;
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

/// <summary>Severity of a banner shown at the top of the service list.</summary>
public enum BannerSeverity
{
    /// <summary>Informational notice (default tint).</summary>
    Info,

    /// <summary>Warning notice.</summary>
    Warning,

    /// <summary>Error notice.</summary>
    Error,
}

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
    /// Profile supervisor for the current cluster; set by <see cref="UpdateScopeAsync"/>.
    /// </summary>
    private IProfilePortForwardSupervisor? _supervisor;

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
    [NotifyCanExecuteChangedFor(nameof(ToggleProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private PortForwardProfileViewModel? _selectedProfile;

    /// <summary>
    /// Gets or sets a value indicating whether the active profile is currently forwarding.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleProfileCommand))]
    [NotifyPropertyChangedFor(nameof(ProfileToggleLabel))]
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
    /// Banner notice text shown above the service list. <c>null</c> means no banner.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DismissBannerCommand))]
    [NotifyPropertyChangedFor(nameof(HasBannerMessage))]
    private string? _bannerMessage;

    /// <summary>
    /// Severity of <see cref="BannerMessage"/>, controls the banner color.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBannerInfo))]
    [NotifyPropertyChangedFor(nameof(IsBannerWarning))]
    [NotifyPropertyChangedFor(nameof(IsBannerError))]
    private BannerSeverity _bannerSeverity = BannerSeverity.Info;

    /// <summary>True when <see cref="BannerMessage"/> is non-empty; bound by the banner's IsVisible.</summary>
    public bool HasBannerMessage => !string.IsNullOrEmpty(BannerMessage);

    /// <summary>True when the current banner severity is <see cref="BannerSeverity.Info"/>.</summary>
    public bool IsBannerInfo => BannerSeverity == BannerSeverity.Info;

    /// <summary>True when the current banner severity is <see cref="BannerSeverity.Warning"/>.</summary>
    public bool IsBannerWarning => BannerSeverity == BannerSeverity.Warning;

    /// <summary>True when the current banner severity is <see cref="BannerSeverity.Error"/>.</summary>
    public bool IsBannerError => BannerSeverity == BannerSeverity.Error;

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
        DetachSupervisor();
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
        DetachSupervisor();

        _allServices.Clear();
        UpdateFilteredList();

        _kubeService = kubeService;
        _portForwardService = portForwardService;
        _namespaceName = namespaceName;
        AttachSupervisor(_connectionManager.GetProfileSupervisor(clusterId));

        if (_clusterId != clusterId)
        {
            _clusterId = clusterId;
            IsProfileRunning = false;
            LoadProfiles();
        }

        await InitializeAsync();
    }

    /// <summary>
    /// Subscribes to events on the given supervisor for the current cluster.
    /// </summary>
    private void AttachSupervisor(IProfilePortForwardSupervisor? supervisor)
    {
        _supervisor = supervisor;
        if (_supervisor is null) return;
        _supervisor.EntryStateChanged += OnSupervisorEntryStateChanged;
        _supervisor.ProfileStoppedDueToFailure += OnSupervisorProfileStoppedDueToFailure;
    }

    /// <summary>
    /// Unsubscribes from the current supervisor's events. Idempotent.
    /// </summary>
    private void DetachSupervisor()
    {
        if (_supervisor is null) return;
        _supervisor.EntryStateChanged -= OnSupervisorEntryStateChanged;
        _supervisor.ProfileStoppedDueToFailure -= OnSupervisorProfileStoppedDueToFailure;
        _supervisor = null;
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
    private async Task DeleteProfileAsync()
    {
        if (SelectedProfile is null) return;

        if (IsProfileRunning)
        {
            await StopProfileForwardsAsync();
        }

        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
        SaveProfiles();
    }

    /// <summary>
    /// Label shown on the profile toggle button. Reflects the action available in the current
    /// state, not the current state itself.
    /// </summary>
    public string ProfileToggleLabel => IsProfileRunning ? "■ Stop" : "▶ Forward";

    /// <summary>
    /// Can-execute for <see cref="ToggleProfileCommand"/>: a profile must be selected with at
    /// least one entry. The same gate applies in both directions — stopping a running profile
    /// also requires the profile to still exist.
    /// </summary>
    private bool CanToggleProfile() =>
        SelectedProfile is not null && SelectedProfile.Entries.Count > 0;

    /// <summary>
    /// Single command that toggles the selected profile between supervised and stopped.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanToggleProfile))]
    private async Task ToggleProfileAsync()
    {
        if (IsProfileRunning) await StopProfileInternalAsync();
        else await StartProfileInternalAsync();
    }

    /// <summary>
    /// Starts every entry in the selected profile under supervision.
    /// </summary>
    private async Task StartProfileInternalAsync()
    {
        if (SelectedProfile is null || _supervisor is null) return;

        BannerMessage = null;

        // Sync LocalPort overrides into the matching ServiceViewModels so the row reflects what's
        // actually being forwarded.
        foreach (var entry in SelectedProfile.Entries)
        {
            var match = FindServiceViewModel(entry.Namespace, entry.ServiceName, entry.TargetPort);
            if (match is not null && entry.LocalPort > 0)
                match.LocalPort = entry.LocalPort;
        }

        IsProfileRunning = true;
        await _supervisor.StartProfileAsync(
            SelectedProfile.Id,
            [.. SelectedProfile.Entries.Select(e => e.Model)]);
    }

    /// <summary>
    /// Stops all port-forwards that were started by the selected profile.
    /// </summary>
    private async Task StopProfileInternalAsync()
    {
        if (SelectedProfile is null) return;
        IsProfileRunning = false;
        if (_supervisor is not null)
            await _supervisor.StopProfileAsync(SelectedProfile.Id);
    }

    /// <summary>
    /// Clears the banner notice (bound to the dismiss button in <c>ServiceListView.axaml</c>).
    /// </summary>
    private bool CanDismissBanner() => !string.IsNullOrEmpty(BannerMessage);

    /// <summary>Clears the banner notice.</summary>
    [RelayCommand(CanExecute = nameof(CanDismissBanner))]
    private void DismissBanner()
    {
        BannerMessage = null;
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
        ToggleProfileCommand.NotifyCanExecuteChanged();
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
        ToggleProfileCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Stops all active supervised forwards that belong to the currently selected profile.
    /// Used by <see cref="DeleteProfileAsync"/> before removing the profile from the list.
    /// </summary>
    private async Task StopProfileForwardsAsync()
    {
        if (SelectedProfile is null || _supervisor is null) return;
        await _supervisor.StopProfileAsync(SelectedProfile.Id);
    }

    /// <summary>
    /// Dispatches an action onto the UI thread. Virtual so unit tests can override and run
    /// inline without an Avalonia dispatcher.
    /// </summary>
    protected virtual void DispatchToUI(Action action) => Dispatcher.UIThread.Post(action);

    /// <summary>
    /// Handles a supervised-entry state change by mirroring it onto the matching row's Status
    /// text and surfacing per-entry "Unsupervised" notices to the banner.
    /// </summary>
    private void OnSupervisorEntryStateChanged(SupervisedForwardSnapshot snapshot)
    {
        DispatchToUI(() => ApplyEntrySnapshot(snapshot));
    }

    /// <summary>
    /// Applies a supervisor snapshot to the matching <see cref="ServiceViewModel"/> (if loaded)
    /// and updates profile-level state on the UI thread.
    /// </summary>
    private void ApplyEntrySnapshot(SupervisedForwardSnapshot snapshot)
    {
        var live = snapshot.State
            is SupervisedForwardState.Starting
            or SupervisedForwardState.Forwarding
            or SupervisedForwardState.Retrying;

        // Profile-level state — updated regardless of whether a row VM exists for this entry
        // (rows may not yet be loaded, e.g., on cluster reconnect).
        if (live)
        {
            IsProfileRunning = true;
        }
        else if (SelectedProfile is not null && _supervisor is not null
                 && !_supervisor.IsProfileRunning(SelectedProfile.Id))
        {
            IsProfileRunning = false;
        }

        if (snapshot.State == SupervisedForwardState.Unsupervised)
        {
            BannerSeverity = BannerSeverity.Info;
            BannerMessage = $"{snapshot.ServiceName} is no longer supervised. "
                + "Use ▶ Start to resume monitoring the whole profile.";
        }

        // Row-level state — only if the matching ServiceViewModel is loaded.
        var match = FindServiceViewModel(snapshot.Namespace, snapshot.ServiceName, snapshot.TargetPort);
        if (match is null) return;

        // Set IsSupervised BEFORE IsForwarding so the IsForwarding setter sees the supervised flag
        // and skips the manual Start/Stop path.
        match.IsSupervised = live;
        match.OnSupervisedStopRequested = live
            ? () => UnsuperviseServiceAsync(snapshot.Namespace, snapshot.ServiceName, snapshot.TargetPort)
            : null;

        // When the row is unsupervised but the profile is still running, allow the user to
        // toggle it back on and re-enter the supervised set without restarting the whole profile.
        match.OnSupervisedResumeRequested = snapshot.State == SupervisedForwardState.Unsupervised
            ? () => ResumeSupervisedEntryAsync(snapshot)
            : null;

        match.Status = snapshot.State switch
        {
            SupervisedForwardState.Starting    => "Starting",
            SupervisedForwardState.Forwarding  => "Forwarding",
            SupervisedForwardState.Retrying    => $"Retrying ({snapshot.AttemptCount}/{snapshot.MaxAttempts})",
            SupervisedForwardState.Failed      => $"Failed ({snapshot.AttemptCount}/{snapshot.MaxAttempts})",
            SupervisedForwardState.Unsupervised => "Unsupervised",
            SupervisedForwardState.Stopped     => "Stopped",
            _ => match.Status,
        };

        // Sync the row's ToggleSwitch to the supervisor's view. Because IsSupervised was set above,
        // setting IsForwarding=true will not trigger a duplicate manual StartForwarding().
        if (live)
        {
            match.IsForwarding = true;
            // Drive the duration timer here — the supervisor owns the port-forward task,
            // so the row's StartForwarding (which usually starts the timer) is skipped.
            if (snapshot.State == SupervisedForwardState.Forwarding)
                match.StartDurationTimerIfStopped();
        }
        else if (snapshot.State is SupervisedForwardState.Stopped
                                or SupervisedForwardState.Failed
                                or SupervisedForwardState.Unsupervised)
        {
            match.IsForwarding = false;
            match.StopDurationTimer();
        }
    }

    /// <summary>
    /// Routes a supervised row's toggle-off through the supervisor.
    /// </summary>
    private async Task UnsuperviseServiceAsync(string ns, string serviceName, string targetPort)
    {
        if (_supervisor is null) return;
        await _supervisor.UnsuperviseAsync(ns, serviceName, targetPort);
    }

    /// <summary>
    /// Re-adds a previously-unsupervised entry to its profile's supervised set, provided the
    /// profile is still running. Otherwise no-ops (the row stays off — user can press
    /// ▶ Forward to relaunch the whole profile).
    /// </summary>
    private async Task ResumeSupervisedEntryAsync(SupervisedForwardSnapshot snapshot)
    {
        if (_supervisor is null) return;
        if (!_supervisor.IsProfileRunning(snapshot.ProfileId))
        {
            // Profile already fully stopped — the user-toggled-on row stays off.
            DispatchToUI(() =>
            {
                var match = FindServiceViewModel(snapshot.Namespace, snapshot.ServiceName, snapshot.TargetPort);
                if (match is not null && match.IsForwarding) match.IsForwarding = false;
            });
            return;
        }

        var entry = new PortForwardProfileEntry
        {
            Namespace = snapshot.Namespace,
            ServiceName = snapshot.ServiceName,
            TargetPort = snapshot.TargetPort,
            LocalPort = snapshot.LocalPort,
        };
        await _supervisor.StartProfileAsync(snapshot.ProfileId, [entry]);

        // Clear the now-stale "no longer supervised" banner.
        DispatchToUI(() => BannerMessage = null);
    }

    /// <summary>
    /// Handles the supervisor's "profile stopped because an entry failed" notification: shows
    /// an error banner and turns off the running flag.
    /// </summary>
    private void OnSupervisorProfileStoppedDueToFailure(ProfileFailureReason reason)
    {
        DispatchToUI(() =>
        {
            IsProfileRunning = false;
            BannerSeverity = BannerSeverity.Error;
            BannerMessage = $"Profile stopped — {reason.FailedServiceName} failed permanently "
                + $"after {reason.AttemptCount} attempts."
                + (string.IsNullOrEmpty(reason.LastError) ? string.Empty : $" Last error: {reason.LastError}");
        });
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