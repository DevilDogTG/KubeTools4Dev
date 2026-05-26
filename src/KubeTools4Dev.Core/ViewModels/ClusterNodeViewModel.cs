using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents a single Kubernetes cluster/context in the navigation tree.
/// </summary>
public partial class ClusterNodeViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// The namespace name used for the "all namespaces" virtual sentinel node.
    /// An empty string routes to cluster-wide API calls in <see cref="IKubernetesService"/>.
    /// </summary>
    internal const string AllNamespacesKey = "";

    /// <summary>The display label shown for the "all namespaces" sentinel entry.</summary>
    internal const string AllNamespacesDisplayName = "(all namespaces)";

    private readonly IClusterConnectionManager _manager;
    private readonly Action<ContentScopeContext>? _resourceSelectedCallback;
    private readonly SynchronizationContext? _uiContext;
    private readonly ILogger<ClusterNodeViewModel>? _logger;
    private readonly int _namespaceWatchRetryDelayMs;
    private CancellationTokenSource? _namespaceCts;
    private bool _disposed;

    /// <summary>Initializes a new cluster node.</summary>
    /// <param name="id">The cluster ID (string form of <see cref="Models.ClusterEntry.Id"/>).</param>
    /// <param name="displayName">The display name shown in the tree.</param>
    /// <param name="manager">The cluster connection manager.</param>
    /// <param name="resourceSelectedCallback">
    /// Optional callback invoked when the user selects a resource type leaf node inside this cluster.
    /// Passed down to each <see cref="NamespaceNodeViewModel"/> created on connect.
    /// </param>
    /// <param name="logger">Optional logger for namespace watch diagnostics.</param>
    /// <param name="namespaceWatchRetryDelayMs">
    /// Delay in milliseconds before retrying a failed namespace watch. Defaults to 5000 ms.
    /// </param>
    public ClusterNodeViewModel(
        string id,
        string displayName,
        IClusterConnectionManager manager,
        Action<ContentScopeContext>? resourceSelectedCallback = null,
        ILogger<ClusterNodeViewModel>? logger = null,
        int namespaceWatchRetryDelayMs = 5000)
    {
        Id = id;
        DisplayName = displayName;
        _manager = manager;
        _resourceSelectedCallback = resourceSelectedCallback;
        _logger = logger;
        _namespaceWatchRetryDelayMs = namespaceWatchRetryDelayMs;
        _uiContext = SynchronizationContext.Current;

        _manager.ClusterStatusChanged += OnClusterStatusChanged;
    }

    /// <summary>Gets the cluster ID (string form of <see cref="Models.ClusterEntry.Id"/>).</summary>
    public string Id { get; }

    /// <summary>Gets the display name shown in the tree.</summary>
    public string DisplayName { get; }

    /// <summary>Gets or sets the current connection status.</summary>
    [ObservableProperty]
    private ClusterConnectionStatus _status = ClusterConnectionStatus.Disconnected;

    /// <summary>Gets or sets the error message when <see cref="Status"/> is <see cref="ClusterConnectionStatus.Error"/>.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Gets or sets whether the namespace list below this cluster is expanded.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Gets the namespace child nodes; populated after a successful connection.</summary>
    public ObservableCollection<NamespaceNodeViewModel> Namespaces { get; } = [];

    /// <summary>Initiates a connection to this cluster.</summary>
    [RelayCommand]
    private Task Connect() => _manager.ConnectClusterAsync(Id);

    /// <summary>Disconnects from this cluster.</summary>
    [RelayCommand]
    private Task Disconnect() => _manager.DisconnectClusterAsync(Id);

    /// <summary>
    /// Toggles the expanded state and initiates a connection when expanding while disconnected.
    /// Collapsing never triggers a disconnect so active port-forwards remain alive.
    /// </summary>
    [RelayCommand]
    private async Task ToggleAndConnect()
    {
        IsExpanded = !IsExpanded;
        if (IsExpanded && Status == ClusterConnectionStatus.Disconnected)
            await _manager.ConnectClusterAsync(Id);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _manager.ClusterStatusChanged -= OnClusterStatusChanged;
        CancelNamespaceWatch();
        GC.SuppressFinalize(this);
    }

    private void OnClusterStatusChanged(string clusterId, ClusterConnectionStatus status, string? errorMsg)
    {
        if (clusterId != Id) return;

        // Already on the captured UI context (or no context at all): apply inline.
        // Otherwise post to the UI context so ObservableCollection mutations stay on the UI thread.
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            ApplyStatusUpdate(status, errorMsg);
        else
            _uiContext.Post(_ => ApplyStatusUpdate(status, errorMsg), null);
    }

    private async void ApplyStatusUpdate(ClusterConnectionStatus status, string? errorMsg)
    {
        if (_disposed) return;

        Status = status;
        ErrorMessage = status == ClusterConnectionStatus.Error ? errorMsg : null;

        if (status == ClusterConnectionStatus.Connected)
        {
            await LoadNamespacesAsync();
        }
        else if (status is ClusterConnectionStatus.Disconnected or ClusterConnectionStatus.Error)
        {
            CancelNamespaceWatch();
            Namespaces.Clear();
        }
    }

    private async Task LoadNamespacesAsync()
    {
        try
        {
            var svc = _manager.GetService(Id);
            if (svc is null) return;

            var names = await svc.GetNamespacesAsync() ?? [];

            if (_disposed) return;

            Namespaces.Clear();

            // Prepend the virtual "all namespaces" sentinel so users can view cross-namespace resources.
            Namespaces.Add(new NamespaceNodeViewModel(
                AllNamespacesKey, Id, _resourceSelectedCallback, AllNamespacesDisplayName));

            foreach (var name in names)
                Namespaces.Add(new NamespaceNodeViewModel(name, Id, _resourceSelectedCallback));

            // Start the live namespace watch so additions/removals are reflected without reconnect.
            StartNamespaceWatch(svc);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Namespaces unavailable: {ex.Message}";
        }
    }

    // ── Namespace watch ─────────────────────────────────────────────────────

    private void StartNamespaceWatch(IKubernetesService svc)
    {
        CancelNamespaceWatch();
        _namespaceCts = new CancellationTokenSource();
        var token = _namespaceCts.Token;

        _ = Task.Run(async () => await WatchNamespacesLoopAsync(svc, token), token);
    }

    private void CancelNamespaceWatch()
    {
        if (_namespaceCts is null) return;
        _namespaceCts.Cancel();
        _namespaceCts.Dispose();
        _namespaceCts = null;
    }

    private async Task WatchNamespacesLoopAsync(IKubernetesService svc, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !_disposed)
        {
            try
            {
                await foreach (var (eventType, ns) in svc.WatchNamespacesAsync(cancellationToken))
                {
                    var namespaceName = ns.Metadata?.Name;
                    if (string.IsNullOrEmpty(namespaceName)) continue;

                    PostToUi(() => ApplyNamespaceWatchEvent(eventType, namespaceName));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested || _disposed) break;
                if (_logger is not null) LogNamespaceWatchFailed(_logger, Id, ex);
            }

            if (cancellationToken.IsCancellationRequested || _disposed) break;

            // Prevent tight spinning when the watch stream exhausts (normal or error path).
            if (_namespaceWatchRetryDelayMs > 0)
                await Task.Delay(_namespaceWatchRetryDelayMs, cancellationToken).ConfigureAwait(false);
            else
                await Task.Yield();
        }
    }

    private void ApplyNamespaceWatchEvent(k8s.WatchEventType eventType, string namespaceName)
    {
        if (_disposed) return;

        switch (eventType)
        {
            case k8s.WatchEventType.Added:
                // Do not add a duplicate or overwrite the "all namespaces" sentinel.
                if (Namespaces.Any(n => n.Name == namespaceName)) return;
                Namespaces.Add(new NamespaceNodeViewModel(namespaceName, Id, _resourceSelectedCallback));
                break;

            case k8s.WatchEventType.Deleted:
                var toRemove = Namespaces.FirstOrDefault(n => n.Name == namespaceName);
                if (toRemove is not null)
                    Namespaces.Remove(toRemove);
                break;
        }
    }

    private void PostToUi(Action action)
    {
        if (_uiContext is null || SynchronizationContext.Current == _uiContext)
            action();
        else
            _uiContext.Post(_ => action(), null);
    }
}
