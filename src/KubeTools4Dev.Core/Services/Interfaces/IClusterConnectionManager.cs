using KubeTools4Dev.Core.ViewModels;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Manages a pool of per-cluster <see cref="IKubernetesService"/> and <see cref="IPortForwardService"/> instances,
/// supporting simultaneous connections to multiple Kubernetes clusters.
/// </summary>
public interface IClusterConnectionManager
{
    /// <summary>Returns the IDs of all currently connected clusters.</summary>
    IReadOnlyList<string> GetConnectedClusterIds();

    /// <summary>
    /// Enumerates all context names from the given kubeconfig file without connecting.
    /// </summary>
    Task<IReadOnlyList<string>> EnumerateContextsAsync(string kubeConfigPath);

    /// <summary>
    /// Registers the selected contexts from a kubeconfig file and persists them to settings.
    /// </summary>
    Task AddKubeConfigSourceAsync(string kubeConfigPath, IEnumerable<string> selectedContexts);

    /// <summary>
    /// Initiates an async connection for the given cluster ID.
    /// Status transitions are reported via <see cref="ClusterStatusChanged"/>.
    /// </summary>
    Task ConnectClusterAsync(string clusterId, CancellationToken ct = default);

    /// <summary>Disconnects and disposes the service instances for the given cluster.</summary>
    Task DisconnectClusterAsync(string clusterId);

    /// <summary>Returns the <see cref="IKubernetesService"/> for a connected cluster, or <c>null</c> if not connected.</summary>
    IKubernetesService? GetService(string clusterId);

    /// <summary>Returns the <see cref="IPortForwardService"/> for a cluster (connected or not — created on demand).</summary>
    IPortForwardService GetPortForwardService(string clusterId);

    /// <summary>
    /// Returns <c>true</c> if the given local port is already in use by any active port-forward across all clusters.
    /// </summary>
    bool IsLocalPortInUse(int localPort);

    /// <summary>
    /// Fired whenever a cluster's connection status changes.
    /// Arguments: clusterId, new status, optional error message.
    /// </summary>
    event Action<string, ClusterConnectionStatus, string?> ClusterStatusChanged;
}
