using k8s;
using k8s.Models;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Kubernetes service interface for interacting with Kubernetes clusters.
/// </summary>
public interface IKubernetesService
{
    /// <summary>
    /// Gets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    IKubernetes Client { get; }

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
    /// <returns>Current context name</returns>
    Task<string> ConnectAsync(string? kubeConfigPath = null);

    /// <summary>
    /// Gets the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of pods in the specified namespace.</returns>
    Task<IEnumerable<V1Pod>> GetPodsAsync(string namespaceName = "default");

    /// <summary>
    /// Gets the pod metrics asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of pod metrics in the specified namespace.</returns>
    Task<PodMetricsList?> GetPodMetricsAsync(string namespaceName = "default");

    /// <summary>
    /// Streams the pod logs asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of log lines.</returns>
    IAsyncEnumerable<string> StreamPodLogsAsync(string namespaceName, string podName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the pod describe asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <returns>A string representing the describe output.</returns>
    Task<string> GetPodDescribeAsync(string namespaceName, string podName);

    /// <summary>
    /// Gets the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of services in the specified namespace.</returns>
    Task<IEnumerable<V1Service>> GetServicesAsync(string namespaceName = "default");

    /// <summary>
    /// Watches the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    IAsyncEnumerable<(WatchEventType Type, V1Pod Item)> WatchPodsAsync(
        string namespaceName = "default",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Watches the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    IAsyncEnumerable<(WatchEventType Type, V1Service Item)> WatchServicesAsync(
        string namespaceName = "default",
        CancellationToken cancellationToken = default);
}
