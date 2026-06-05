using DMNSN.Core;
using k8s;
using k8s.Models;
using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

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
    /// Connects using the default context of the given kubeconfig file (or the system default).
    /// </summary>
    public Task<string> ConnectAsync(string? kubeConfigPath = null)
        => ConnectAsync(kubeConfigPath, contextName: null);

    /// <summary>
    /// Connects to a specific context within a kubeconfig file.
    /// </summary>
    public async Task<string> ConnectAsync(string? kubeConfigPath, string? contextName)
    {
        try
        {
            logger.LogInformation("Connecting to Kubernetes (context: {Context})...", contextName ?? "default");
            KubernetesClientConfiguration config;

            if (string.IsNullOrEmpty(kubeConfigPath))
            {
                config = KubernetesClientConfiguration.BuildDefaultConfig();
            }
            else if (string.IsNullOrEmpty(contextName))
            {
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigPath);
            }
            else
            {
                config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
                    kubeconfig: new FileInfo(kubeConfigPath),
                    currentContext: contextName);
            }

            _client = new Kubernetes(config);
            await _client.Version.GetCodeAsync();

            logger.LogInformation("Connected to Kubernetes successfully.");
            return config.CurrentContext;
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to connect to Kubernetes");
            _client = null;
            return string.Empty;
        }
    }

    /// <summary>
    /// Enumerates all context names from the given kubeconfig file without creating a connection.
    /// </summary>
    public static IReadOnlyList<string> EnumerateContexts(string kubeConfigPath)
    {
        try
        {
            var config = KubernetesClientConfiguration.LoadKubeConfig(kubeConfigPath);
            return config.Contexts?.Select(c => c.Name).ToList() ?? [];
        }
        catch
        {
            return [];
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
    /// Gets the pod metrics asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of pod metrics in the specified namespace.</returns>
    public async Task<PodMetricsList?> GetPodMetricsAsync(string namespaceName = "default")
    {
        try
        {
            return IsAllNamespaces(namespaceName)
                ? await Client.GetKubernetesPodsMetricsAsync()
                : await Client.GetKubernetesPodsMetricsByNamespaceAsync(namespaceName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get pod metrics. Metrics server might not be installed.");
            return null;
        }
    }

    /// <summary>
    /// Streams the pod logs asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <param name="container">Container to stream logs from; required by the API when the pod has more than one container. <c>null</c> lets the API pick the only container.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of log lines.</returns>
    public async IAsyncEnumerable<string> StreamPodLogsAsync(
        string namespaceName,
        string podName,
        string? container = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var stream = await Client.CoreV1.ReadNamespacedPodLogAsync(
            name: podName,
            namespaceParameter: namespaceName,
            container: container,
            follow: true,
            tailLines: 1000,
            cancellationToken: cancellationToken);

        using var reader = new System.IO.StreamReader(stream);
        
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            
            yield return line;
        }
    }

    /// <summary>
    /// Gets the pod describe asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <returns>A string representing the describe output.</returns>
    public async Task<string> GetPodDescribeAsync(string namespaceName, string podName)
    {
        try
        {
            var pod = await Client.CoreV1.ReadNamespacedPodAsync(podName, namespaceName);
            var events = await ListPodEventsAsync(namespaceName, podName);

            var yaml = k8s.KubernetesYaml.Serialize(pod);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("--- POD YAML ---");
            sb.AppendLine(yaml);
            sb.AppendLine();
            sb.AppendLine("--- EVENTS ---");

            foreach (var evt in events.Items)
            {
                sb.AppendLine($"[{evt.Type}] {evt.Reason} - {evt.Message} ({evt.LastTimestamp})");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get pod describe");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the Kubernetes events involving the specified pod, newest first.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <returns>The pod's events as display-ready projections, newest first.</returns>
    public async Task<IReadOnlyList<PodEventInfo>> GetPodEventsAsync(string namespaceName, string podName)
    {
        var events = await ListPodEventsAsync(namespaceName, podName);
        return PodEventInfo.FromEvents(events.Items);
    }

    /// <summary>
    /// Lists the raw events whose involved object is the specified pod. Shared by
    /// <see cref="GetPodDescribeAsync"/> and <see cref="GetPodEventsAsync"/>.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="podName">Name of the pod.</param>
    /// <returns>The raw event list from the API server.</returns>
    private Task<Corev1EventList> ListPodEventsAsync(string namespaceName, string podName) =>
        Client.CoreV1.ListNamespacedEventAsync(namespaceName, fieldSelector: $"involvedObject.name={podName}");

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
    /// Gets the deployments asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <returns>A list of deployments in the specified namespace.</returns>
    public async Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(string namespaceName = "default")
    {
        return (
            IsAllNamespaces(namespaceName)
                ? await Client.AppsV1.ListDeploymentForAllNamespacesAsync()
                : await Client.AppsV1.ListNamespacedDeploymentAsync(namespaceName)
            ).Items;
    }

    /// <summary>
    /// Watches the deployments asynchronous.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An async enumerable of watch events.</returns>
    public async IAsyncEnumerable<(WatchEventType Type, V1Deployment Item)> WatchDeploymentsAsync(
        string namespaceName = "default",
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        if (IsAllNamespaces(namespaceName))
        {
            await foreach (var (type, item) in Client.AppsV1
                .WatchListDeploymentForAllNamespacesAsync(cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
        else
        {
            await foreach (var (type, item) in Client.AppsV1
                .WatchListNamespacedDeploymentAsync(
                    namespaceName,
                    cancellationToken: cancellationToken))
            {
                yield return (type, item);
            }
        }
    }

    /// <summary>
    /// Patches the deployment asynchronous using a Strategic Merge Patch.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="deploymentName">Name of the deployment.</param>
    /// <param name="replicas">The desired replica count.</param>
    /// <param name="imageTag">The full image tag to apply to the first container.</param>
    /// <returns>A task representing the asynchronous patch operation.</returns>
    public async Task PatchDeploymentAsync(string namespaceName, string deploymentName, int replicas, string imageTag)
    {
        var current = await Client.AppsV1.ReadNamespacedDeploymentAsync(deploymentName, namespaceName);
        var container = current.Spec?.Template?.Spec?.Containers?.FirstOrDefault()
            ?? throw new InvalidOperationException($"Deployment '{deploymentName}' has no containers to patch.");
        var containerName = container.Name;

        var patchBody = JsonSerializer.Serialize(new
        {
            spec = new
            {
                replicas,
                template = new
                {
                    spec = new
                    {
                        containers = new[]
                        {
                            new { name = containerName, image = imageTag }
                        }
                    }
                }
            }
        });

        await Client.AppsV1.PatchNamespacedDeploymentAsync(
            new V1Patch(patchBody, V1Patch.PatchType.StrategicMergePatch),
            deploymentName,
            namespaceName);
    }

    /// <summary>
    /// Restarts the deployment asynchronous by annotating the pod template metadata.
    /// </summary>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="deploymentName">Name of the deployment.</param>
    /// <returns>A task representing the asynchronous restart operation.</returns>
    public async Task RestartDeploymentAsync(string namespaceName, string deploymentName)
    {
        var timestamp = DateTime.UtcNow.ToString("o");

        var patchBody = JsonSerializer.Serialize(new
        {
            spec = new
            {
                template = new
                {
                    metadata = new
                    {
                        annotations = new Dictionary<string, string>
                        {
                            ["kubectl.kubernetes.io/restartedAt"] = timestamp
                        }
                    }
                }
            }
        });

        await Client.AppsV1.PatchNamespacedDeploymentAsync(
            new V1Patch(patchBody, V1Patch.PatchType.MergePatch),
            deploymentName,
            namespaceName);
    }

    /// <summary>
    /// Gets all namespace names in the cluster.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetNamespacesAsync()
    {
        var result = await Client.CoreV1.ListNamespaceAsync();
        return result.Items?.Select(n => n.Metadata.Name).ToList() ?? [];
    }

    /// <summary>
    /// Watches all namespaces in the cluster for add, delete, and modify events.
    /// </summary>
    /// <param name="cancellationToken">Token used to stop the watch stream.</param>
    /// <returns>An async-enumerable stream of namespace watch events.</returns>
    public async IAsyncEnumerable<(WatchEventType Type, V1Namespace Item)> WatchNamespacesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        await foreach (var (type, item) in Client.CoreV1.WatchListNamespaceAsync(
            cancellationToken: cancellationToken))
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
