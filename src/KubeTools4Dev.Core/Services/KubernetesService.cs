using DMNSN.Core;
using k8s;
using k8s.Autorest;
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
    public IKubernetes Client => _client ?? throw new InvalidOperationException("Not connected to Kubernetes");

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
    /// <returns><c>true</c> if connection was successful; otherwise, <c>false</c>.</returns>
    public async Task<bool> ConnectAsync(string? kubeConfigPath = null)
    {
        try
        {
            logger.Information("Connecting to Kubernetes...");
            KubernetesClientConfiguration config;
            if (string.IsNullOrEmpty(kubeConfigPath))
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }
            else
            {
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath);
            }

            _client = new Kubernetes(config);

            // Verify connection by listing nodes or just checking api versions
            await _client.Version.GetCodeAsync();

            logger.LogInformation("Connected to Kubernetes successfully.");
            return true;
        }
        catch (Exception ex)
        {
            // Log error
            logger.Error(ex, "Failed to connect to Kubernetes");
            Console.WriteLine($"Failed to connect to Kubernetes: {ex.Message}");
            _client = null;
            return false;
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
        if (_client == null) throw new InvalidOperationException("Not connected");

        if (IsAllNamespaces(namespaceName))
        {
            var list = await _client.CoreV1.ListPodForAllNamespacesAsync();
            return list.Items;
        }
        else
        {
            var list = await _client.CoreV1.ListNamespacedPodAsync(namespaceName);
            return list.Items;
        }
    }

    /// <summary>
    /// Gets the services asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of services in the specified namespace.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async Task<IEnumerable<V1Service>> GetServicesAsync(string namespaceName = "default")
    {
        if (_client == null) throw new InvalidOperationException("Not connected");

        if (IsAllNamespaces(namespaceName))
        {
            var list = await _client.CoreV1.ListServiceForAllNamespacesAsync();
            return list.Items;
        }
        else
        {
            var list = await _client.CoreV1.ListNamespacedServiceAsync(namespaceName);
            return list.Items.Where(s => s.Metadata.Name != "kubernetes");
        }
    }

    /// <summary>
    /// Watches the pods asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    /// <exception cref="InvalidOperationException">Not connected</exception>
    public async IAsyncEnumerable<(WatchEventType Type, V1Pod Item)> WatchPodsAsync(string namespaceName = "default", [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_client == null) throw new InvalidOperationException("Not connected");

        Task<HttpOperationResponse<V1PodList>> responseTask;

        if (IsAllNamespaces(namespaceName))
        {
            responseTask = _client.CoreV1.ListPodForAllNamespacesWithHttpMessagesAsync(watch: true, cancellationToken: cancellationToken);
        }
        else
        {
            responseTask = _client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(namespaceName, watch: true, cancellationToken: cancellationToken);
        }

        await foreach (var (type, item) in responseTask.WatchAsync<V1Pod, V1PodList>(cancellationToken: cancellationToken))
        {
            yield return (type, item);
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
