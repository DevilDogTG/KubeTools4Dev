using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Test subclass that runs UI-dispatched work inline and bypasses the Avalonia
/// <c>DispatcherTimer</c> so tests don't need a dispatcher loop.
/// </summary>
file sealed class TestServiceViewModel(
    ILogger<ServiceViewModel> logger,
    V1Service service,
    V1ServicePort port,
    IPortForwardService pfService,
    ISettingsService settingsService)
    : ServiceViewModel(logger, service, port, pfService, settingsService)
{
    protected override void DispatchToUI(Action action) => action();
    protected override void StartTimer() => DurationText = "00:00:00";
    protected override void StopTimer() => DurationText = "";
}

/// <summary>
/// Tests for <see cref="ServiceViewModel"/>'s manual port-forward lifecycle.
/// </summary>
public class ServiceViewModelTests
{
    private readonly ILogger<ServiceViewModel> _logger = Substitute.For<ILogger<ServiceViewModel>>();
    private readonly IPortForwardService _pfService = Substitute.For<IPortForwardService>();
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();

    private ServiceViewModel MakeVm()
    {
        var service = new V1Service
        {
            Metadata = new V1ObjectMeta { Name = "svc", NamespaceProperty = "ns" },
        };
        var port = new V1ServicePort { Port = 8080 };
        return new TestServiceViewModel(_logger, service, port, _pfService, _settings);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 2000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        Assert.True(condition(), "Timed out waiting for condition.");
    }

    [Fact]
    public async Task ManualForward_CleanReturnWithoutCancellation_TogglesOffAndShowsStopped()
    {
        // The forward task ends on its own (e.g. listener died because the port was taken).
        _pfService
            .StartServicePortForwardAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var vm = MakeVm();
        vm.IsForwarding = true;

        await WaitUntilAsync(() => !vm.IsForwarding);
        Assert.Equal("Stopped", vm.Status);
    }

    [Fact]
    public async Task ManualForward_Crash_ShowsFailedAndTogglesOff()
    {
        _pfService
            .StartServicePortForwardAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("boom")));

        var vm = MakeVm();
        vm.IsForwarding = true;

        await WaitUntilAsync(() => vm.Status == "Failed");
        Assert.False(vm.IsForwarding);
    }

    [Fact]
    public async Task ManualForward_UserStop_ShowsStoppedWithoutFailedFlash()
    {
        // Forward runs until its token is cancelled, then returns cleanly — the user-stop path.
        _pfService
            .StartServicePortForwardAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                catch (OperationCanceledException) { }
            });

        var vm = MakeVm();
        var sawFailed = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.Status) && vm.Status == "Failed") sawFailed = true;
        };

        vm.IsForwarding = true;
        await WaitUntilAsync(() => vm.Status == "Forwarding");

        vm.IsForwarding = false;

        await WaitUntilAsync(() => vm.Status == "Stopped");
        Assert.False(sawFailed);
    }
}
