using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Net.Sockets;
using k8s.Models;

namespace KubeTools4Dev.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="PortForwardService"/>.
/// </summary>
public class PortForwardServiceTests
{
    private readonly IKubernetesService _kubernetesService;
    private readonly ILogger<PortForwardService> _logger;
    private readonly PortForwardService _sut;

    public PortForwardServiceTests()
    {
        _kubernetesService = Substitute.For<IKubernetesService>();
        _logger = Substitute.For<ILogger<PortForwardService>>();
        _sut = new PortForwardService(_kubernetesService, _logger);
    }

    [Fact]
    public void StopAll_WhenNoForwards_DoesNotThrow()
    {
        // Should complete gracefully with nothing to stop.
        var ex = Record.Exception(() => _sut.StopAll());
        Assert.Null(ex);
    }

    [Fact]
    public async Task StartServicePortForwardAsync_InvalidStringPort_ThrowsArgumentException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.StartServicePortForwardAsync(
                serviceName: "svc",
                namespaceName: "ns",
                targetPort: "not-a-port",
                localPort: 8080,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task StartServicePortForwardAsync_UnsupportedPortType_ThrowsArgumentException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.StartServicePortForwardAsync(
                serviceName: "svc",
                namespaceName: "ns",
                targetPort: 3.14,  // double is not a supported port type
                localPort: 8080,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task StartServicePortForwardAsync_IntegerPort_AcceptsWithoutArgumentException()
    {
        // With a pre-cancelled token and no Kubernetes client set up, the service
        // should accept an int port type (no ArgumentException) and exit cleanly.
        _kubernetesService.Client.Returns((k8s.IKubernetes)null!);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // OperationCanceledException or NullReferenceException may be raised because the
        // client is null — but NOT an ArgumentException about the port type.
        var ex = await Record.ExceptionAsync(() =>
            _sut.StartServicePortForwardAsync(
                serviceName: "svc",
                namespaceName: "ns",
                targetPort: 8080,
                localPort: 9090,
                cancellationToken: cts.Token));

        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    public async Task StartServicePortForwardAsync_ValidStringPort_AcceptsWithoutArgumentException()
    {
        _kubernetesService.Client.Returns((k8s.IKubernetes)null!);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Record.ExceptionAsync(() =>
            _sut.StartServicePortForwardAsync(
                serviceName: "svc",
                namespaceName: "ns",
                targetPort: "8080",
                localPort: 9090,
                cancellationToken: cts.Token));

        Assert.IsNotType<ArgumentException>(ex);
    }

    [Fact]
    public async Task AcceptAndForwardConnectionsAsync_IgnoresConnectionReset()
    {
        using var listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
        using var cts = new CancellationTokenSource();
        int callCount = 0;

        var testSut = new TestablePortForwardService(_kubernetesService, _logger);
        testSut.AcceptSocketAsyncMock = (l, token) =>
        {
            callCount++;
            if (callCount == 1)
            {
                // First call: simulate ConnectionReset from client abort
                throw new SocketException((int)SocketError.ConnectionReset);
            }
            
            // Second call: cancel to break the loop
            cts.Cancel();
            throw new OperationCanceledException(token);
        };

        // Method should catch ConnectionReset, continue loop, and exit cleanly on Cancel
        var ex = await Record.ExceptionAsync(() => 
            testSut.AcceptAndForwardConnectionsAsync(listener, "svc", "ns", 8080, cts.Token));

        Assert.Null(ex); // Loop exited cleanly without rethrowing
        Assert.Equal(2, callCount); // Verified it retried after the first exception
    }

    [Fact]
    public async Task HandleSingleConnectionAsync_TimesOutWhenWebSocketHangs()
    {
        // Lower timeout to 50ms to speed up the test
        var originalTimeout = PortForwardService.ConnectionTimeout;
        PortForwardService.ConnectionTimeout = TimeSpan.FromMilliseconds(50);

        try
        {
            using var handler = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
            using var cts = new CancellationTokenSource();

            var testSut = new TestablePortForwardService(_kubernetesService, _logger);
            
            // Bypass pod name resolution
            testSut.GetPodNameAsyncMock = (svc, ns, token) => Task.FromResult<string?>("pod1");

            // Mock WebSocket connection to hang indefinitely
            testSut.ConnectWebSocketAsyncMock = async (pod, ns, port, token) => 
            { 
                await Task.Delay(Timeout.InfiniteTimeSpan, token); 
                return null!; 
            };

            var ex = await Record.ExceptionAsync(() => 
                testSut.HandleSingleConnectionAsync(handler, "svc", "ns", 8080, cts.Token));

            // It should catch the OperationCanceledException and exit gracefully
            Assert.Null(ex);
        }
        finally
        {
            PortForwardService.ConnectionTimeout = originalTimeout;
        }
    }

    private class TestablePortForwardService : PortForwardService
    {
        public Func<Socket, CancellationToken, ValueTask<Socket>>? AcceptSocketAsyncMock { get; set; }
        public Func<string, string, CancellationToken, Task<string?>>? GetPodNameAsyncMock { get; set; }
        public Func<string, string, int, CancellationToken, Task<System.Net.WebSockets.WebSocket>>? ConnectWebSocketAsyncMock { get; set; }

        public TestablePortForwardService(IKubernetesService kubernetesService, ILogger<PortForwardService> logger)
            : base(kubernetesService, logger)
        {
        }

        protected internal override ValueTask<Socket> AcceptSocketAsync(Socket listener, CancellationToken cancellationToken)
        {
            if (AcceptSocketAsyncMock != null)
            {
                return AcceptSocketAsyncMock(listener, cancellationToken);
            }
            return base.AcceptSocketAsync(listener, cancellationToken);
        }

        protected internal override Task<string?> GetPodNameAsync(string serviceName, string namespaceName, CancellationToken cancellationToken)
        {
            if (GetPodNameAsyncMock != null)
            {
                return GetPodNameAsyncMock(serviceName, namespaceName, cancellationToken);
            }
            return base.GetPodNameAsync(serviceName, namespaceName, cancellationToken);
        }

        protected internal override Task<System.Net.WebSockets.WebSocket> ConnectWebSocketAsync(string podName, string namespaceName, int remotePort, CancellationToken cancellationToken)
        {
            if (ConnectWebSocketAsyncMock != null)
            {
                return ConnectWebSocketAsyncMock(podName, namespaceName, remotePort, cancellationToken);
            }
            return base.ConnectWebSocketAsync(podName, namespaceName, remotePort, cancellationToken);
        }
    }
}
