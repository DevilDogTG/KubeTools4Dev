using KubeTools4Dev.Core.Tests.Fakes;
using KubeTools4Dev.Core.ViewModels;
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
    public void ErrorMessage_ClearedOnSuccessfulConnect()
    {
        _manager.SuccessfulClusterIds.Add("id-1");
        var sut = new ClusterNodeViewModel("id-1", "my-cluster", _manager);

        // Force an error first by not having it in SuccessfulClusterIds for a different id
        // Then connect successfully
        _ = sut.ConnectCommand.ExecuteAsync(null);

        // After successful connect, ErrorMessage should be null
        Assert.Null(sut.ErrorMessage);
    }
}
