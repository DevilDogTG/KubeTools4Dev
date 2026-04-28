using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;

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
}
