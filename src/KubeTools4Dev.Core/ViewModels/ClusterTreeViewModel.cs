using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Root ViewModel for the sidebar cluster tree.
/// Owns the observable tree of <see cref="KubeConfigSourceNodeViewModel"/> nodes,
/// handles startup auto-discovery, and fires <see cref="ResourceNodeSelected"/> when the user
/// selects a leaf (Pods / Services / Deployments).
/// </summary>
public partial class ClusterTreeViewModel : ObservableObject
{
    private readonly IClusterConnectionManager _manager;
    private readonly ISettingsService _settings;
    private readonly ILogger<ClusterTreeViewModel> _logger;
    private readonly SynchronizationContext? _uiContext;

    /// <summary>Gets the top-level kubeconfig source groups shown in the sidebar.</summary>
    public ObservableCollection<KubeConfigSourceNodeViewModel> Sources { get; } = [];

    /// <summary>
    /// Fired when the user selects a resource type leaf node in the tree.
    /// The handler should update the main content panel accordingly.
    /// </summary>
    public event Action<ContentScopeContext>? ResourceNodeSelected;

    /// <summary>
    /// Raised by the App layer to open the Add Cluster dialog.
    /// The App layer subscribes to this event and shows the dialog.
    /// </summary>
    public event Action? AddClusterRequested;

    /// <summary>Initializes a new instance of <see cref="ClusterTreeViewModel"/>.</summary>
    public ClusterTreeViewModel(
        IClusterConnectionManager manager,
        ISettingsService settings,
        ILogger<ClusterTreeViewModel> logger)
    {
        _manager = manager;
        _settings = settings;
        _logger = logger;
        _uiContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Populates the tree from persisted settings.
    /// If the registry is empty and auto-discovery is enabled, discovers contexts from the default kubeconfig.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_settings.Clusters.Clusters.Count == 0 && _settings.Clusters.AutoDiscoverDefaultKubeConfig)
        {
            await AutoDiscoverDefaultKubeConfigAsync();
        }

        PostToUi(RebuildTree);
    }

    /// <summary>
    /// Adds a kubeconfig source with selected contexts and refreshes the tree.
    /// </summary>
    public async Task AddSourceAsync(string kubeConfigPath, IEnumerable<string> selectedContexts)
    {
        await _manager.AddKubeConfigSourceAsync(kubeConfigPath, selectedContexts);
        PostToUi(RebuildTree);
    }

    [RelayCommand]
    private void AddCluster() => AddClusterRequested?.Invoke();

    /// <summary>
    /// Rebuilds the <see cref="Sources"/> collection from the persisted settings.
    /// Groups entries by kubeconfig file path. Must be called on the UI thread.
    /// </summary>
    private void RebuildTree()
    {
        // Dispose existing cluster nodes so they unsubscribe from manager events.
        foreach (var source in Sources)
        {
            foreach (var cluster in source.Clusters)
                cluster.Dispose();
        }
        Sources.Clear();

        var groups = _settings.Clusters.Clusters
            .Where(c => c.IsEnabled)
            .GroupBy(c => c.KubeConfigPath);

        foreach (var group in groups)
        {
            var sourceName = DeriveSourceDisplayName(group.Key);
            var sourceNode = new KubeConfigSourceNodeViewModel(sourceName, group.Key);

            foreach (var entry in group)
            {
                var clusterNode = new ClusterNodeViewModel(
                    entry.Id.ToString(),
                    entry.DisplayName,
                    _manager,
                    ctx => ResourceNodeSelected?.Invoke(ctx),
                    namespaceWatchRetryDelayMs: _settings.Namespaces.WatchRetryDelayMilliseconds);

                sourceNode.Clusters.Add(clusterNode);
            }

            Sources.Add(sourceNode);
        }
    }

    private void PostToUi(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }

    private async Task AutoDiscoverDefaultKubeConfigAsync()
    {
        var defaultPath = GetDefaultKubeConfigPath();
        if (!File.Exists(defaultPath))
        {
            _logger.LogInformation("Default kubeconfig not found at {Path}; skipping auto-discovery.", defaultPath);
            return;
        }

        try
        {
            var contexts = await _manager.EnumerateContextsAsync(defaultPath);
            if (contexts.Count > 0)
            {
                await _manager.AddKubeConfigSourceAsync(defaultPath, contexts);
                _logger.LogInformation("Auto-discovered {Count} context(s) from {Path}.", contexts.Count, defaultPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-discovery of default kubeconfig failed.");
        }
    }

    private static string GetDefaultKubeConfigPath()
    {
        var envVar = Environment.GetEnvironmentVariable("KUBECONFIG");
        if (!string.IsNullOrEmpty(envVar))
            return envVar.Split(Path.PathSeparator)[0];

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kube",
            "config");
    }

    private static string DeriveSourceDisplayName(string kubeConfigPath)
    {
        var defaultPath = GetDefaultKubeConfigPath();
        return string.Equals(kubeConfigPath, defaultPath, StringComparison.OrdinalIgnoreCase)
            ? "Local Kubeconfigs"
            : Path.GetFileNameWithoutExtension(kubeConfigPath);
    }
}
