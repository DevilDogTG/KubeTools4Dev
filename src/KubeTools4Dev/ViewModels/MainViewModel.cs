using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DMNSN.Core;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Main application view model.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IClusterConnectionManager _manager;
    private readonly ILogger<MainViewModel> _logger;

    /// <summary>Gets the cluster tree navigation state.</summary>
    public ClusterTreeViewModel ClusterTree { get; }

    /// <summary>Gets or sets the pod list view model.</summary>
    [ObservableProperty]
    private PodListViewModel _podList;

    /// <summary>Gets or sets the service list view model.</summary>
    [ObservableProperty]
    private ServiceListViewModel _serviceList;

    /// <summary>Gets or sets the deployment list view model.</summary>
    [ObservableProperty]
    private DeploymentListViewModel _deploymentList;

    /// <summary>Gets or sets the settings view model.</summary>
    [ObservableProperty]
    private SettingsViewModel _settings;

    /// <summary>Gets or sets the active content scope (which cluster/namespace/resource is shown).</summary>
    [ObservableProperty]
    private ContentScopeContext? _activeScope;

    /// <summary>Gets or sets whether the Settings panel is visible instead of a resource list.</summary>
    [ObservableProperty]
    private bool _isSettingsPanelVisible;

    /// <summary>Gets whether the Pods panel is visible.</summary>
    public bool IsPodsPanelVisible => ActiveScope?.Kind == ResourceKind.Pods && !IsSettingsPanelVisible;

    /// <summary>Gets whether the Services panel is visible.</summary>
    public bool IsServicesPanelVisible => ActiveScope?.Kind == ResourceKind.Services && !IsSettingsPanelVisible;

    /// <summary>Gets whether the Deployments panel is visible.</summary>
    public bool IsDeploymentsPanelVisible => ActiveScope?.Kind == ResourceKind.Deployments && !IsSettingsPanelVisible;

    /// <summary>Gets whether the welcome/empty-state panel is visible.</summary>
    public bool IsWelcomePanelVisible => ActiveScope == null && !IsSettingsPanelVisible;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel" /> class.
    /// </summary>
    public MainViewModel(
        IClusterConnectionManager manager,
        ClusterTreeViewModel clusterTree,
        PodListViewModel podListViewModel,
        ServiceListViewModel serviceListViewModel,
        DeploymentListViewModel deploymentListViewModel,
        SettingsViewModel settingsViewModel,
        ILogger<MainViewModel> logger)
    {
        _manager = manager;
        _logger = logger;
        ClusterTree = clusterTree;

        PodList = podListViewModel;
        ServiceList = serviceListViewModel;
        DeploymentList = deploymentListViewModel;
        Settings = settingsViewModel;

        ClusterTree.ResourceNodeSelected += OnResourceNodeSelected;
        ClusterTree.AddClusterRequested += OnAddClusterRequested;

        _ = InitializeAsync();
    }

    /// <summary>Cleanups resources used by the view model.</summary>
    public void Cleanup()
    {
        _logger.Information("Starting cleanup application");
        ClusterTree.ResourceNodeSelected -= OnResourceNodeSelected;
        ClusterTree.AddClusterRequested -= OnAddClusterRequested;
        ServiceList?.Cleanup();
        DeploymentList?.Dispose();
    }

    /// <summary>Activates the Settings panel.</summary>
    [RelayCommand]
    private void ShowSettings()
    {
        ActiveScope = null;
        IsSettingsPanelVisible = true;
    }

    partial void OnActiveScopeChanged(ContentScopeContext? value)
    {
        IsSettingsPanelVisible = false;
        OnPropertyChanged(nameof(IsPodsPanelVisible));
        OnPropertyChanged(nameof(IsServicesPanelVisible));
        OnPropertyChanged(nameof(IsDeploymentsPanelVisible));
        OnPropertyChanged(nameof(IsWelcomePanelVisible));
    }

    partial void OnIsSettingsPanelVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPodsPanelVisible));
        OnPropertyChanged(nameof(IsServicesPanelVisible));
        OnPropertyChanged(nameof(IsDeploymentsPanelVisible));
        OnPropertyChanged(nameof(IsWelcomePanelVisible));
    }

    private async Task InitializeAsync()
    {
        await ClusterTree.InitializeAsync();
    }

    private async void OnAddClusterRequested()
    {
        await ShowAddClusterDialogAsync();
    }

    /// <summary>
    /// Shows the Add Cluster dialog. Override in tests to skip Avalonia window interaction.
    /// </summary>
    protected virtual async Task ShowAddClusterDialogAsync()
    {
        var vm = new AddClusterDialogViewModel(_manager, ClusterTree);
        var dialog = new Views.AddClusterDialog();
        vm.ConfirmRequested += () => dialog.Close();
        vm.CancelRequested += () => dialog.Close();
        dialog.DataContext = vm;

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
    }

    private async void OnResourceNodeSelected(ContentScopeContext ctx)
    {
        ActiveScope = ctx;

        var svc = _manager.GetService(ctx.ClusterId);
        if (svc is null)
        {
            _logger.LogWarning("Cluster {Id} is not connected; ignoring selection.", ctx.ClusterId);
            return;
        }

        switch (ctx.Kind)
        {
            case ResourceKind.Pods:
                await PodList.UpdateScopeAsync(svc, ctx.Namespace);
                break;
            case ResourceKind.Services:
                var pf = _manager.GetPortForwardService(ctx.ClusterId);
                if (pf is null)
                {
                    _logger.LogWarning("Cluster {Id} has no port-forward service; skipping Services panel.", ctx.ClusterId);
                    return;
                }
                await ServiceList.UpdateScopeAsync(svc, pf, ctx.Namespace);
                break;
            case ResourceKind.Deployments:
                await DeploymentList.UpdateScopeAsync(svc, ctx.Namespace);
                break;
        }
    }
}
