using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents a single Kubernetes cluster/context in the navigation tree.
/// </summary>
public partial class ClusterNodeViewModel : ObservableObject, IDisposable
{
    private readonly IClusterConnectionManager _manager;
    private readonly Action<ContentScopeContext>? _resourceSelectedCallback;
    private readonly SynchronizationContext? _uiContext;
    private bool _disposed;

    /// <summary>Initializes a new cluster node.</summary>
    /// <param name="id">The cluster ID (string form of <see cref="Models.ClusterEntry.Id"/>).</param>
    /// <param name="displayName">The display name shown in the tree.</param>
    /// <param name="manager">The cluster connection manager.</param>
    /// <param name="resourceSelectedCallback">
    /// Optional callback invoked when the user selects a resource type leaf node inside this cluster.
    /// Passed down to each <see cref="NamespaceNodeViewModel"/> created on connect.
    /// </param>
    public ClusterNodeViewModel(
        string id,
        string displayName,
        IClusterConnectionManager manager,
        Action<ContentScopeContext>? resourceSelectedCallback = null)
    {
        Id = id;
        DisplayName = displayName;
        _manager = manager;
        _resourceSelectedCallback = resourceSelectedCallback;
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
            await LoadNamespacesAsync();
        else if (status is ClusterConnectionStatus.Disconnected or ClusterConnectionStatus.Error)
            Namespaces.Clear();
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
            foreach (var name in names)
                Namespaces.Add(new NamespaceNodeViewModel(name, Id, _resourceSelectedCallback));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Namespaces unavailable: {ex.Message}";
        }
    }
}
