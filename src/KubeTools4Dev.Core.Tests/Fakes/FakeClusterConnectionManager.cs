using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using NSubstitute;

namespace KubeTools4Dev.Core.Tests.Fakes;

/// <summary>
/// In-memory fake implementation of <see cref="IClusterConnectionManager"/> for unit tests.
/// </summary>
public class FakeClusterConnectionManager : IClusterConnectionManager
{
    private readonly Dictionary<string, ClusterConnectionStatus> _statuses = new();
    private readonly Dictionary<string, IKubernetesService> _services = new();

    /// <summary>Controls which cluster IDs ConnectClusterAsync will treat as succeeding.</summary>
    public HashSet<string> SuccessfulClusterIds { get; } = new();

    /// <inheritdoc/>
    public event Action<string, ClusterConnectionStatus, string?>? ClusterStatusChanged;

    /// <inheritdoc/>
    public IReadOnlyList<string> GetConnectedClusterIds()
        => _services.Keys.ToList();

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> EnumerateContextsAsync(string kubeConfigPath)
        => Task.FromResult<IReadOnlyList<string>>(["ctx-a", "ctx-b"]);

    /// <inheritdoc/>
    public Task AddKubeConfigSourceAsync(string kubeConfigPath, IEnumerable<string> selectedContexts)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task ConnectClusterAsync(string clusterId, CancellationToken ct = default)
    {
        FireStatus(clusterId, ClusterConnectionStatus.Connecting, null);
        await Task.Yield();

        if (SuccessfulClusterIds.Contains(clusterId))
        {
            var svc = Substitute.For<IKubernetesService>();
            svc.IsConnected.Returns(true);
            _services[clusterId] = svc;
            FireStatus(clusterId, ClusterConnectionStatus.Connected, null);
        }
        else
        {
            FireStatus(clusterId, ClusterConnectionStatus.Error, "Simulated failure");
        }
    }

    /// <inheritdoc/>
    public Task DisconnectClusterAsync(string clusterId)
    {
        _services.Remove(clusterId);
        FireStatus(clusterId, ClusterConnectionStatus.Disconnected, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IKubernetesService? GetService(string clusterId)
        => _services.TryGetValue(clusterId, out var s) ? s : null;

    /// <inheritdoc/>
    public IPortForwardService? GetPortForwardService(string clusterId)
        => _services.ContainsKey(clusterId) ? Substitute.For<IPortForwardService>() : null;

    /// <inheritdoc/>
    public bool IsLocalPortInUse(int localPort) => false;

    private void FireStatus(string id, ClusterConnectionStatus status, string? msg)
    {
        _statuses[id] = status;
        ClusterStatusChanged?.Invoke(id, status, msg);
    }
}
