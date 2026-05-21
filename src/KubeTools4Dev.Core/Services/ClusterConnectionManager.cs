using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Manages a pool of per-cluster <see cref="IKubernetesService"/> and <see cref="IPortForwardService"/>
/// instances, supporting simultaneous connections to multiple Kubernetes clusters.
/// </summary>
public class ClusterConnectionManager : IClusterConnectionManager
{
    private readonly ISettingsService _settings;
    private readonly IKubernetesServiceFactory _kubeFactory;
    private readonly IPortForwardServiceFactory _portForwardFactory;
    private readonly ILogger<ClusterConnectionManager> _logger;

    private record Session(
        IKubernetesService KubeService,
        IPortForwardService PortForwardService);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    /// <inheritdoc />
    public event Action<string, ClusterConnectionStatus, string?>? ClusterStatusChanged;

    /// <summary>Initializes a new instance of <see cref="ClusterConnectionManager"/>.</summary>
    public ClusterConnectionManager(
        ISettingsService settings,
        IKubernetesServiceFactory kubeFactory,
        IPortForwardServiceFactory portForwardFactory,
        ILogger<ClusterConnectionManager> logger)
    {
        _settings = settings;
        _kubeFactory = kubeFactory;
        _portForwardFactory = portForwardFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedClusterIds()
        => _sessions.Keys.ToList();

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> EnumerateContextsAsync(string kubeConfigPath)
    {
        var contexts = KubernetesService.EnumerateContexts(kubeConfigPath);
        return Task.FromResult(contexts);
    }

    /// <inheritdoc />
    public Task AddKubeConfigSourceAsync(string kubeConfigPath, IEnumerable<string> selectedContexts)
    {
        foreach (var context in selectedContexts)
        {
            // Skip duplicates
            if (_settings.Clusters.Clusters.Any(c =>
                    c.KubeConfigPath == kubeConfigPath && c.ContextName == context))
            {
                continue;
            }

            _settings.Clusters.Clusters.Add(new ClusterEntry
            {
                KubeConfigPath = kubeConfigPath,
                ContextName = context,
                DisplayName = context
            });
        }
        _settings.Save();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ConnectClusterAsync(string clusterId, CancellationToken ct = default)
    {
        var entry = FindEntry(clusterId);
        if (entry is null)
        {
            _logger.LogWarning("ConnectClusterAsync: cluster {Id} not found in registry.", clusterId);
            return;
        }

        FireStatus(clusterId, ClusterConnectionStatus.Connecting, null);

        var kubeService = _kubeFactory.Create();
        var portForwardService = _portForwardFactory.Create(kubeService);

        try
        {
            var context = await kubeService.ConnectAsync(entry.KubeConfigPath, entry.ContextName);

            if (string.IsNullOrEmpty(context))
            {
                FireStatus(clusterId, ClusterConnectionStatus.Error, "Connection failed");
                return;
            }

            _sessions[clusterId] = new Session(kubeService, portForwardService);
            FireStatus(clusterId, ClusterConnectionStatus.Connected, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect cluster {Id}.", clusterId);
            FireStatus(clusterId, ClusterConnectionStatus.Error, ex.Message);
        }
    }

    /// <inheritdoc />
    public Task DisconnectClusterAsync(string clusterId)
    {
        if (_sessions.TryRemove(clusterId, out var session))
        {
            session.PortForwardService.StopAll();
        }
        FireStatus(clusterId, ClusterConnectionStatus.Disconnected, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IKubernetesService? GetService(string clusterId)
        => _sessions.TryGetValue(clusterId, out var s) ? s.KubeService : null;

    /// <inheritdoc />
    public IPortForwardService GetPortForwardService(string clusterId)
    {
        if (_sessions.TryGetValue(clusterId, out var s))
            return s.PortForwardService;

        // Return an unconnected instance for the cluster so callers always get a non-null value.
        // Port-forwards will fail gracefully until the cluster is actually connected.
        var entry = FindEntry(clusterId);
        var kubeService = _kubeFactory.Create();
        var pf = _portForwardFactory.Create(kubeService);
        return pf;
    }

    /// <inheritdoc />
    public bool IsLocalPortInUse(int localPort)
        => _sessions.Values.Any(s => s.PortForwardService.GetActiveLocalPorts().Contains(localPort));

    private ClusterEntry? FindEntry(string clusterId)
        => _settings.Clusters.Clusters.FirstOrDefault(c => c.Id.ToString() == clusterId);

    private void FireStatus(string clusterId, ClusterConnectionStatus status, string? message)
        => ClusterStatusChanged?.Invoke(clusterId, status, message);
}
