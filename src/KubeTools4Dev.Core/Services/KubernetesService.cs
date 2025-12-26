using DMNSN.Core;
using k8s;
using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Service for interacting with Kubernetes clusters.
/// </summary>
/// <seealso cref="IKubernetesService" />
public class KubernetesService(
    ILogger<KubernetesService> logger
) : IKubernetesService
{
    /// <summary>
    /// The client
    /// </summary>
    private Kubernetes? _client;

    /// <summary>
    /// Gets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    /// <exception cref="InvalidOperationException">Not connected to Kubernetes</exception>
    public IKubernetes Client => _client
        ?? throw new InvalidOperationException("Not connected to Kubernetes");

    /// <summary>
    /// Gets a value indicating whether this instance is connected.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is connected; otherwise, <c>false</c>.
    /// </value>
    public bool IsConnected => _client != null;

    /// <summary>
    /// Connects the asynchronous.
    /// </summary>
    /// <param name="kubeConfigPath">The kube configuration path.</param>
    /// <returns>Current context name</returns>
    public async Task<string> ConnectAsync(string? kubeConfigPath = null)
    {
        try
        {
            logger.Information("Connecting to Kubernetes...");
            KubernetesClientConfiguration config = string.IsNullOrEmpty(kubeConfigPath)
                ? KubernetesClientConfiguration.BuildDefaultConfig()
                : KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath);

            _client = new Kubernetes(config);

            // Verify connection by listing nodes or just checking api versions
            await _client.Version.GetCodeAsync();

            logger.LogInformation("Connected to Kubernetes successfully.");
            return config.CurrentContext;
        }
        catch (Exception ex)
        {
            // Log error
            logger.Error(ex, "Failed to connect to Kubernetes");
            _client = null;
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of pods in the specified namespace.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async Task<IEnumerable<V1Pod>> GetPodsAsync(string namespaceName = "default")
    {
        return (
            IsAllNamespaces(namespaceName)
                ? await Client.CoreV1.ListPodForAllNamespacesAsync()
                : await Client.CoreV1.ListNamespacedPodAsync(namespaceName)
            ).Items;
    }

    /// <summary>
    /// Gets the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of services in the specified namespace.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async Task<IEnumerable<V1Service>> GetServicesAsync(string namespaceName = "default")
    {
        return (
            IsAllNamespaces(namespaceName)
                ? await Client.CoreV1.ListServiceForAllNamespacesAsync()
                : await Client.CoreV1.ListNamespacedServiceAsync(namespaceName)
            ).Items
            .Where(s => s.Metadata.Name != "kubernetes");
    }

    /// <summary>
    /// Watches the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async IAsyncEnumerable<(WatchEventType Type, V1Pod Item)> WatchPodsAsync(
        string namespaceName = "default",
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (IsAllNamespaces(namespaceName))
        {
            await foreach (var (type, item) in Client.CoreV1
                .WatchListPodForAllNamespacesAsync(cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
        else
        {
            await foreach (var (type, item) in Client.CoreV1
                .WatchListNamespacedPodAsync(
                    namespaceName,
                    cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
    }

    /// <summary>
    /// Watches the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async IAsyncEnumerable<(WatchEventType Type, V1Service Item)> WatchServicesAsync(
        string namespaceName = "default",
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (IsAllNamespaces(namespaceName))
        {
            await foreach (var (type, item) in Client.CoreV1
                .WatchListServiceForAllNamespacesAsync(cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
        else
        {
            await foreach (var (type, item) in Client.CoreV1
                .WatchListNamespacedServiceAsync(
                    namespaceName,
                    cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
    }

    /// <summary>
    /// Determines whether [is all namespaces] [the specified namespaceName].
    /// </summary>
    /// <param name="namespaceName">The namespaceName.</param>
    /// <returns>
    ///   <c>true</c> if [is all namespaces] [the specified namespaceName]; otherwise, <c>false</c>.
    /// </returns>
    private static bool IsAllNamespaces(string namespaceName) => string.IsNullOrEmpty(namespaceName) || namespaceName == "*";
}
