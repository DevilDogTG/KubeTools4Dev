using k8s;
using k8s.Models;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Kubernetes service interface for interacting with Kubernetes clusters.
/// </summary>
public interface IKubernetesService
{
    /// <summary>
    /// Gets a value indicating whether this instance is connected.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is connected; otherwise, <c>false</c>.
    /// </value>
    bool IsConnected { get; }

    /// <summary>
    /// Connects the asynchronous.
    /// </summary>
    /// <param name="kubeConfigPath">The kube configuration path.</param>
    /// <returns></returns>
    Task<bool> ConnectAsync(string? kubeConfigPath = null);

    /// <summary>
    /// Gets the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns></returns>
    Task<IEnumerable<V1Pod>> GetPodsAsync(string namespaceName = "default");

    /// <summary>
    /// Watches the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
    IAsyncEnumerable<(WatchEventType Type, V1Pod Item)> WatchPodsAsync(string namespaceName = "default", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns></returns>
    Task<IEnumerable<V1Service>> GetServicesAsync(string namespaceName = "default");

    /// <summary>
    /// Gets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    IKubernetes Client { get; }
}
