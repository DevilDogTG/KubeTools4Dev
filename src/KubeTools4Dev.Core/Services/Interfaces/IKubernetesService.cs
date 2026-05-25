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
    /// Connects to a specific context within a kubeconfig file.
    /// </summary>
    /// <param name="kubeConfigPath">Path to the kubeconfig file.</param>
    /// <param name="contextName">The context name to use.</param>
    /// <returns>The resolved context name, or empty string on failure.</returns>
    Task<string> ConnectAsync(string? kubeConfigPath, string? contextName);

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

    /// <summary>
    /// Gets the deployments asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace. Pass empty string or "*" for all namespaces.</param>
    /// <returns>A list of deployments in the specified namespace.</returns>
    Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(string namespaceName = "default");

    /// <summary>
    /// Watches the deployments asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace. Pass empty string or "*" for all namespaces.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events containing the event type and the affected deployment.</returns>
    IAsyncEnumerable<(WatchEventType Type, V1Deployment Item)> WatchDeploymentsAsync(
        string namespaceName = "default",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Patches the deployment asynchronous. Updates replica count and/or image tag on the first container
    /// using a Strategic Merge Patch so that other containers are not affected.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="deploymentName">Name of the deployment.</param>
    /// <param name="replicas">The desired replica count (must be ≥ 0).</param>
    /// <param name="imageTag">The full image tag to apply to the first container (e.g. "nginx:1.25").</param>
    /// <returns>A task representing the asynchronous patch operation.</returns>
    Task PatchDeploymentAsync(string namespaceName, string deploymentName, int replicas, string imageTag);

    /// <summary>
    /// Restarts the deployment asynchronous. Applies a JSON Merge Patch setting the
    /// <c>kubectl.kubernetes.io/restartedAt</c> annotation on the pod template metadata,
    /// which causes a rolling restart equivalent to <c>kubectl rollout restart</c>.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="deploymentName">Name of the deployment.</param>
    /// <returns>A task representing the asynchronous restart operation.</returns>
    Task RestartDeploymentAsync(string namespaceName, string deploymentName);

    /// <summary>
    /// Gets all namespace names in the cluster.
    /// </summary>
    /// <returns>A read-only list of namespace names.</returns>
    Task<IReadOnlyList<string>> GetNamespacesAsync();

    /// <summary>
    /// Watches all namespaces in the cluster for add, delete, and modify events.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop the watch stream.</param>
    /// <returns>
    /// An async-enumerable stream of <see cref="k8s.WatchEventType"/> / <see cref="k8s.Models.V1Namespace"/> pairs.
    /// Yields one item per namespace event until <paramref name="cancellationToken"/> is cancelled.
    /// </returns>
    IAsyncEnumerable<(k8s.WatchEventType Type, k8s.Models.V1Namespace Item)> WatchNamespacesAsync(
        CancellationToken cancellationToken = default);
}
