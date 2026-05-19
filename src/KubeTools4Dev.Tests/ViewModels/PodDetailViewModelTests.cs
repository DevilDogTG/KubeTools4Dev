using k8s.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using System.Collections.Generic;
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

    private TestableViewModel MakeVm(bool isLogsView = true)
    {
        var vm = new TestableViewModel(_logger, _kubeService)
        {
            Pod = MakePod("test-pod", "default"),
            IsLogsView = isLogsView
        };
        return vm;
    }

    private static PodViewModel MakePod(string name, string ns)
    {
        var pod = new V1Pod
        {
            Metadata = new V1ObjectMeta { Name = name, NamespaceProperty = ns },
            Status = new V1PodStatus { Phase = "Running" }
        };
        return new PodViewModel(pod);
    }

    // --- Initialize ---

    [Fact]
    public void Initialize_SetsLogsTitle_WhenIsLogsViewTrue()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
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
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<CancellationToken>())
            .Returns(Lines("line-a", "line-b"));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        Assert.Contains("line-a", vm.PodLogsList);
        Assert.Contains("line-b", vm.PodLogsList);
    }

    [Fact]
    public async Task StartLogStreamAsync_CapsListAt1000Lines()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<CancellationToken>())
            .Returns(NLines(1005));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        Assert.True(vm.PodLogsList.Count <= 1001);
    }

    [Fact]
    public async Task StartLogStreamAsync_AddsErrorLine_OnException()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<CancellationToken>())
            .Returns(ThrowingLines(new InvalidOperationException("stream broken")));

        var vm = MakeVm(isLogsView: true);
        await vm.StartLogStreamAsync();

        Assert.Contains(vm.PodLogsList, l => l.Contains("stream broken"));
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
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<CancellationToken>())
            .Returns(x => BlockingLines(x.ArgAt<CancellationToken>(2)));

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
