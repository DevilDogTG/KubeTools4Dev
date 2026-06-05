using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.Tests.Fakes;
using KubeTools4Dev.Core.ViewModels;
using NSubstitute;
using System.Collections.Generic;

namespace KubeTools4Dev.Core.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ClusterNodeViewModel"/> status transitions and property notifications.
/// </summary>
public class ClusterNodeViewModelTests
{
    private readonly FakeClusterConnectionManager _manager = new();

    [Fact]
    public void InitialStatus_IsDisconnected()
    {
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        Assert.Equal(ClusterConnectionStatus.Disconnected, sut.Status);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task ConnectCommand_SetsConnectingThenConnected_WhenSuccess()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        var statuses = new List<ClusterConnectionStatus>();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClusterNodeViewModel.Status))
                statuses.Add(sut.Status);
        };

        await sut.ConnectCommand.ExecuteAsync(null);

        Assert.Contains(ClusterConnectionStatus.Connecting, statuses);
        Assert.Equal(ClusterConnectionStatus.Connected, sut.Status);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task ConnectCommand_SetsError_WhenFailed()
    {
        // Don't add to SuccessfulClusterIds => will fail
        var sut = new ClusterNodeViewModel("id-bad", "bad-cluster", _manager);

        await sut.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(ClusterConnectionStatus.Error, sut.Status);
        Assert.NotNull(sut.ErrorMessage);
    }

    [Fact]
    public async Task DisconnectCommand_SetsDisconnected()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);
        await sut.ConnectCommand.ExecuteAsync(null);

        await sut.DisconnectCommand.ExecuteAsync(null);

        Assert.Equal(ClusterConnectionStatus.Disconnected, sut.Status);
    }

    [Fact]
    public void StatusChange_RaisesPropertyChanged()
    {
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Simulate status change from outside via manager event
        _manager.SuccessfulClusterIds.Add("id-1");
        _ = sut.ConnectCommand.ExecuteAsync(null);

        Assert.Contains(nameof(ClusterNodeViewModel.Status), raised);
    }

    [Fact]
    public async Task ErrorMessage_ClearedOnSuccessfulConnect()
    {
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        // First attempt fails (id-1 not in SuccessfulClusterIds yet) — sets ErrorMessage
        await sut.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(ClusterConnectionStatus.Error, sut.Status);
        Assert.NotNull(sut.ErrorMessage);

        // Second attempt succeeds — ErrorMessage should be cleared
        _manager.SuccessfulClusterIds.Add("id-1");
        await sut.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(ClusterConnectionStatus.Connected, sut.Status);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public void Dispose_UnsubscribesFromManagerEvent()
    {
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        sut.Dispose();

        // After dispose, status events for this cluster should NOT update the disposed VM.
        var initialStatus = sut.Status;
        _manager.SuccessfulClusterIds.Add("id-1");
        // Fire a status event by triggering connect on the manager directly.
        _ = _manager.ConnectClusterAsync("id-1");

        // Allow any queued continuations to run.
        Thread.Sleep(50);

        Assert.Equal(initialStatus, sut.Status);
    }

    // ── ToggleAndConnectCommand ───────────────────────────────────────────────

    [Fact]
    public async Task ToggleAndConnectCommand_Expanding_WhenDisconnected_CallsConnect()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);
        // Initial state: IsExpanded = false, Status = Disconnected

        var connectingObserved = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClusterNodeViewModel.Status) &&
                sut.Status == ClusterConnectionStatus.Connecting)
                connectingObserved = true;
        };

        await sut.ToggleAndConnectCommand.ExecuteAsync(null);

        Assert.True(sut.IsExpanded);
        Assert.True(connectingObserved);
        Assert.Equal(ClusterConnectionStatus.Connected, sut.Status);
    }

    [Fact]
    public async Task ToggleAndConnectCommand_Collapsing_DoesNotCallConnect()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);
        await sut.ConnectCommand.ExecuteAsync(null); // already connected, IsExpanded stays false

        var connectingObserved = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ClusterNodeViewModel.Status) &&
                sut.Status == ClusterConnectionStatus.Connecting)
                connectingObserved = true;
        };

        await sut.ToggleAndConnectCommand.ExecuteAsync(null); // expand (no re-connect: already connected)
        await sut.ToggleAndConnectCommand.ExecuteAsync(null); // collapse

        Assert.False(sut.IsExpanded);
        Assert.False(connectingObserved);
        Assert.Equal(ClusterConnectionStatus.Connected, sut.Status);
    }

    // ── T2: "all namespaces" sentinel node ────────────────────────────────────

    [Fact]
    public async Task Connect_PrependsSentinelNode_AsFirstNamespaceEntry()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        await sut.ConnectCommand.ExecuteAsync(null);

        Assert.NotEmpty(sut.Namespaces);
        var sentinel = sut.Namespaces[0];
        Assert.Equal(ClusterNodeViewModel.AllNamespacesKey, sentinel.Name);
        Assert.Equal(ClusterNodeViewModel.AllNamespacesDisplayName, sentinel.DisplayName);
        Assert.True(sentinel.IsAllNamespaces);
    }

    // ── T3: namespace watch events ────────────────────────────────────────────

    [Fact]
    public async Task WatchNamespacesAsync_AddedEvent_AppendsNewNamespaceNode()
    {
        var svc = CreateConfiguredService();
        svc.WatchNamespacesAsync(Arg.Any<CancellationToken>())
           .Returns(WatchEvents((k8s.WatchEventType.Added, MakeNamespace("kube-system"))));

        _manager.ServiceFactory = _ => svc;
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager, namespaceWatchRetryDelayMs: 0);

        await sut.ConnectCommand.ExecuteAsync(null);

        await WaitForAsync(() => sut.Namespaces.Any(n => n.Name == "kube-system"), TimeSpan.FromSeconds(2));

        Assert.Contains(sut.Namespaces, n => n.Name == "kube-system");
    }

    [Fact]
    public async Task WatchNamespacesAsync_DeletedEvent_RemovesExistingNode()
    {
        var svc = CreateConfiguredService(initialNamespaces: ["default"]);
        svc.WatchNamespacesAsync(Arg.Any<CancellationToken>())
           .Returns(WatchEvents((k8s.WatchEventType.Deleted, MakeNamespace("default"))));

        _manager.ServiceFactory = _ => svc;
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager, namespaceWatchRetryDelayMs: 0);

        await sut.ConnectCommand.ExecuteAsync(null);

        // Wait for LoadNamespacesAsync to add the sentinel node before checking for removal.
        // Without this, Namespaces may be empty when the condition is first evaluated;
        // !Any(...) would return true on an empty collection, causing WaitForAsync to exit
        // prematurely — before LoadNamespacesAsync runs and re-adds "default".
        await WaitForAsync(() => sut.Namespaces.Any(n => n.IsAllNamespaces), TimeSpan.FromSeconds(2));

        await WaitForAsync(() => !sut.Namespaces.Any(n => n.Name == "default"), TimeSpan.FromSeconds(2));

        Assert.DoesNotContain(sut.Namespaces, n => n.Name == "default");
    }

    [Fact]
    public async Task WatchNamespacesAsync_AddedWithSentinelName_DoesNotDuplicateSentinel()
    {
        var svc = CreateConfiguredService();
        svc.WatchNamespacesAsync(Arg.Any<CancellationToken>())
           .Returns(WatchEvents((k8s.WatchEventType.Added, MakeNamespace(""))));

        _manager.ServiceFactory = _ => svc;
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager, namespaceWatchRetryDelayMs: 0);

        await sut.ConnectCommand.ExecuteAsync(null);

        // Allow watch Task.Run to complete
        await Task.Delay(200);

        Assert.Equal(1, sut.Namespaces.Count(n => n.Name == ""));
    }

    // ── Watch test helpers ────────────────────────────────────────────────────

    private static IKubernetesService CreateConfiguredService(IReadOnlyList<string>? initialNamespaces = null)
    {
        var svc = Substitute.For<IKubernetesService>();
        svc.IsConnected.Returns(true);
        svc.GetNamespacesAsync().Returns(Task.FromResult<IReadOnlyList<string>>(initialNamespaces ?? []));
        return svc;
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (condition()) return;
            }
            catch (InvalidOperationException)
            {
                // The watch task mutated the collection while the condition was enumerating
                // it ("Collection was modified") — treat as not-yet-satisfied and re-poll.
            }
            await Task.Delay(10);
        }
    }

    private static async IAsyncEnumerable<(k8s.WatchEventType Type, k8s.Models.V1Namespace Item)> WatchEvents(
        params (k8s.WatchEventType Type, k8s.Models.V1Namespace Item)[] events)
    {
        foreach (var e in events)
            yield return e;
    }

    private static k8s.Models.V1Namespace MakeNamespace(string name)
        => new() { Metadata = new k8s.Models.V1ObjectMeta { Name = name } };
}
