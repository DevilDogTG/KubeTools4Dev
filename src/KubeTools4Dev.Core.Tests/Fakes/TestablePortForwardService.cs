using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace KubeTools4Dev.Core.Tests.Fakes;

/// <summary>
/// Testable subclass of <see cref="PortForwardService"/> that allows overriding
/// low-level socket and Kubernetes WebSocket operations via mock delegates,
/// avoiding the need for a full <c>ITcpListenerFactory</c> abstraction.
/// </summary>
internal sealed class TestablePortForwardService(
    IKubernetesService kubernetesService,
    ILogger<PortForwardService> logger
) : PortForwardService(kubernetesService, logger)
{
    /// <summary>Gets or sets the mock delegate for <see cref="AcceptSocketAsync"/>.</summary>
    public Func<Socket, CancellationToken, ValueTask<Socket>>? AcceptSocketAsyncMock { get; set; }

    /// <summary>Gets or sets the mock delegate for <see cref="GetPodNameAsync"/>.</summary>
    public Func<string, string, CancellationToken, Task<string?>>? GetPodNameAsyncMock { get; set; }

    /// <summary>Gets or sets the mock delegate for <see cref="ConnectWebSocketAsync"/>.</summary>
    public Func<string, string, int, CancellationToken, Task<WebSocket>>? ConnectWebSocketAsyncMock { get; set; }

    /// <inheritdoc/>
    protected internal override ValueTask<Socket> AcceptSocketAsync(Socket listener, CancellationToken cancellationToken)
        => AcceptSocketAsyncMock is not null
            ? AcceptSocketAsyncMock(listener, cancellationToken)
            : base.AcceptSocketAsync(listener, cancellationToken);

    /// <inheritdoc/>
    protected internal override Task<string?> GetPodNameAsync(string serviceName, string namespaceName, CancellationToken cancellationToken)
        => GetPodNameAsyncMock is not null
            ? GetPodNameAsyncMock(serviceName, namespaceName, cancellationToken)
            : base.GetPodNameAsync(serviceName, namespaceName, cancellationToken);

    /// <inheritdoc/>
    protected internal override Task<WebSocket> ConnectWebSocketAsync(string podName, string namespaceName, int remotePort, CancellationToken cancellationToken)
        => ConnectWebSocketAsyncMock is not null
            ? ConnectWebSocketAsyncMock(podName, namespaceName, remotePort, cancellationToken)
            : base.ConnectWebSocketAsync(podName, namespaceName, remotePort, cancellationToken);
}
