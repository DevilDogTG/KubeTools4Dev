using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents a single Kubernetes cluster/context in the navigation tree.
/// </summary>
public partial class ClusterNodeViewModel : ObservableObject
{
    private readonly IClusterConnectionManager _manager;

    /// <summary>Initializes a new cluster node.</summary>
    public ClusterNodeViewModel(
        string id,
        string displayName,
        IClusterConnectionManager manager)
    {
        Id = id;
        DisplayName = displayName;
        _manager = manager;

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

    /// <summary>Gets the namespace child nodes; populated after a successful connection.</summary>
    public ObservableCollection<NamespaceNodeViewModel> Namespaces { get; } = [];

    /// <summary>Initiates a connection to this cluster.</summary>
    [RelayCommand]
    private Task Connect() => _manager.ConnectClusterAsync(Id);

    /// <summary>Disconnects from this cluster.</summary>
    [RelayCommand]
    private Task Disconnect() => _manager.DisconnectClusterAsync(Id);

    private async void OnClusterStatusChanged(string clusterId, ClusterConnectionStatus status, string? errorMsg)
    {
        if (clusterId != Id) return;
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
            Namespaces.Clear();
            foreach (var name in names)
                Namespaces.Add(new NamespaceNodeViewModel(name, Id));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Namespaces unavailable: {ex.Message}";
        }
    }
}
