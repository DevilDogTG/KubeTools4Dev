using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PodDetailViewModel"/> log streaming and describe loading.
/// </summary>
public class PodDetailViewModelTests
{
    private readonly ILogger<PodDetailViewModel> _logger = Substitute.For<ILogger<PodDetailViewModel>>();
    private readonly IKubernetesService _kubeService = Substitute.For<IKubernetesService>();

    private TestableViewModel MakeVm(bool isLogsView = true, params string[] containers)
    {
        var vm = new TestableViewModel(_logger, _kubeService)
        {
            Pod = MakePod("test-pod", "default", containers),
            IsLogsView = isLogsView
        };
        return vm;
    }

    private static PodViewModel MakePod(string name, string ns, params string[] containers)
    {
        var pod = new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns },
            Status = new V1PodStatus { Phase = "Running" },
            Spec = new V1PodSpec
            {
                Containers = (containers.Length > 0 ? containers : new[] { "main" })
                    .Select(c => new V1Container { Name = c })
                    .ToList()
            }
        };
        return new PodViewModel(pod);
    }

    private IEnumerable<string> LogLines(PodDetailViewModel vm)
        => vm.PodLogsText.Split(Environment.NewLine);

    // --- Initialize ---

    [Fact]
    public void Initialize_SetsLogsTitle_WhenIsLogsViewTrue()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());

        var vm = MakeVm(isLogsView: true);
        vm.Initialize();

        Assert.Equal("Logs — test-pod [default]", vm.WindowTitle);
    }

    [Fact]
    public void Initialize_SetsDescribeTitle_WhenIsLogsViewFalse()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod").Returns(Task.FromResult("output"));

        var vm = MakeVm(isLogsView: false);
        vm.Initialize();

        Assert.Equal("Describe — test-pod [default]", vm.WindowTitle);
    }

    [Fact]
    public void Initialize_SelectsFirstContainer_AndHidesPickerForSingleContainer()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());

        var vm = MakeVm(isLogsView: true, "app");
        vm.Initialize();

        Assert.Equal("app", vm.SelectedContainer);
        Assert.False(vm.HasMultipleContainers);
    }

    [Fact]
    public void Initialize_ShowsPickerForMultiContainerPod()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());

        var vm = MakeVm(isLogsView: true, "app", "sidecar");
        vm.Initialize();

        Assert.True(vm.HasMultipleContainers);
        Assert.Equal(["app", "sidecar"], vm.Containers);
        Assert.Equal("app", vm.SelectedContainer);
    }

    // --- LoadDescribeAsync ---

    [Fact]
    public async Task LoadDescribeAsync_SetsDescribeText_OnSuccess()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod").Returns(Task.FromResult("describe output"));

        var vm = MakeVm(isLogsView: false);
        await vm.LoadDescribeAsync();

        Assert.Equal("describe output", vm.PodDescribeText);
    }

    [Fact]
    public async Task LoadDescribeAsync_ClearsLoadingFlag_AfterSuccess()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod").Returns(Task.FromResult("output"));

        var vm = MakeVm(isLogsView: false);
        await vm.LoadDescribeAsync();

        Assert.False(vm.IsDescribeLoading);
    }

    [Fact]
    public async Task LoadDescribeAsync_SetsErrorText_OnException()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod")
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        var vm = MakeVm(isLogsView: false);
        await vm.LoadDescribeAsync();

        Assert.Contains("connection refused", vm.PodDescribeText);
        Assert.False(vm.IsDescribeLoading);
    }

    // --- StartLogStreamAsync ---

    [Fact]
    public async Task StartLogStreamAsync_AddsConnectingMessage_Then_LogLines()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Lines("line-a", "line-b"));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        Assert.Contains("line-a", LogLines(vm));
        Assert.Contains("line-b", LogLines(vm));
        Assert.Contains("Connecting to log stream", vm.PodLogsText);
    }

    [Fact]
    public async Task StartLogStreamAsync_CapsTextAtMaxLogLines()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(NLines(PodDetailViewModel.MaxLogLines + 5));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        var lines = LogLines(vm).ToList();
        Assert.True(lines.Count <= PodDetailViewModel.MaxLogLines);
        Assert.Contains($"line-{PodDetailViewModel.MaxLogLines + 4}", lines);
        Assert.DoesNotContain("line-0", lines);
    }

    [Fact]
    public async Task StartLogStreamAsync_AddsErrorLineWithExceptionType_OnException()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ThrowingLines(new InvalidOperationException("stream broken")));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        Assert.Contains("stream broken", vm.PodLogsText);
        Assert.Contains(nameof(InvalidOperationException), vm.PodLogsText);
    }

    [Fact]
    public async Task StartLogStreamAsync_PassesSelectedContainer()
    {
        var requestedContainers = new List<string?>();
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                lock (requestedContainers) requestedContainers.Add(ci.ArgAt<string?>(2));
                return EmptyLines();
            });

        var vm = MakeVm(isLogsView: true, "app", "sidecar");
        vm.Initialize();

        await WaitUntilAsync(() => { lock (requestedContainers) return requestedContainers.Contains("app"); });
        _ = _kubeService.Received().StreamPodLogsAsync("default", "test-pod", "app", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangingSelectedContainer_RestartsStreamWithNewContainer()
    {
        var requestedContainers = new List<string?>();
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                lock (requestedContainers) requestedContainers.Add(ci.ArgAt<string?>(2));
                return EmptyLines();
            });

        var vm = MakeVm(isLogsView: true, "app", "sidecar");
        vm.Initialize();
        await WaitUntilAsync(() => { lock (requestedContainers) return requestedContainers.Contains("app"); });

        vm.SelectedContainer = "sidecar";

        await WaitUntilAsync(() => { lock (requestedContainers) return requestedContainers.Contains("sidecar"); });
        _ = _kubeService.Received().StreamPodLogsAsync("default", "test-pod", "sidecar", Arg.Any<CancellationToken>());
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
            await Task.Delay(10);
        Assert.True(condition(), "Timed out waiting for condition.");
    }

    // --- DescribeException ---

    [Fact]
    public void DescribeException_WalksInnerChain()
    {
        var ex = new InvalidOperationException("outer",
            new System.Xml.XmlException("xml is unhappy"));

        var result = PodDetailViewModel.DescribeException(ex);

        Assert.Contains("InvalidOperationException: outer", result);
        Assert.Contains("XmlException: xml is unhappy", result);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DoesNotThrow_WhenNoStreamStarted()
    {
        var vm = MakeVm(isLogsView: true);
        var ex = Record.Exception(() => vm.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_CancelsActiveStream()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(x => BlockingLines(x.ArgAt<CancellationToken>(3)));

        var vm = MakeVm(isLogsView: true);
        var streamTask = vm.StartLogStreamAsync();

        vm.Dispose();
        await streamTask;
    }

    // --- Helpers ---

    private sealed class TestableViewModel(
        ILogger<PodDetailViewModel> logger,
        IKubernetesService kubeService) : PodDetailViewModel(logger, kubeService)
    {
        protected override Task DispatchToUIAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private static async IAsyncEnumerable<string> EmptyLines(
        [EnumeratorCancellation] CancellationToken _ = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<string> Lines(
        params string[] items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> NLines(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return $"line-{i}";
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<string> ThrowingLines(Exception ex)
    {
        if (ex is not null)
        {
            await Task.Yield();
            throw ex;
        }
        yield break;
    }

    private static async IAsyncEnumerable<string> BlockingLines(
        [EnumeratorCancellation] CancellationToken token = default)
    {
        await Task.Delay(Timeout.Infinite, token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        yield break;
    }
}
