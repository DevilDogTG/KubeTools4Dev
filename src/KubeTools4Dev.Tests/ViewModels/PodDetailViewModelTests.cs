using k8s.Models;
using KubeTools4Dev.Core.Models;
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
/// Tests for <see cref="PodDetailViewModel"/> log streaming, describe loading, and events loading.
/// </summary>
public class PodDetailViewModelTests
{
    private static readonly DateTime Now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly ILogger<PodDetailViewModel> _logger = Substitute.For<ILogger<PodDetailViewModel>>();
    private readonly IKubernetesService _kubeService = Substitute.For<IKubernetesService>();

    private TestableViewModel MakeVm(int viewIndex = PodDetailViewModel.LogsViewIndex, params string[] containers)
    {
        var vm = new TestableViewModel(_logger, _kubeService)
        {
            Pod = MakePod("test-pod", "default", containers),
            SelectedViewIndex = viewIndex
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

        var vm = MakeVm();
        vm.Initialize();

        Assert.Equal("Logs — test-pod [default]", vm.WindowTitle);
    }

    [Fact]
    public void Initialize_SetsDescribeTitle_WhenIsLogsViewFalse()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod").Returns(Task.FromResult("output"));

        var vm = MakeVm(PodDetailViewModel.DescribeViewIndex);
        vm.Initialize();

        Assert.Equal("Describe — test-pod [default]", vm.WindowTitle);
    }

    [Fact]
    public void Initialize_SelectsFirstContainer_AndHidesPickerForSingleContainer()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());

        var vm = MakeVm(PodDetailViewModel.LogsViewIndex, "app");
        vm.Initialize();

        Assert.Equal("app", vm.SelectedContainer);
        Assert.False(vm.HasMultipleContainers);
    }

    [Fact]
    public void Initialize_ShowsPickerForMultiContainerPod()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());

        var vm = MakeVm(PodDetailViewModel.LogsViewIndex, "app", "sidecar");
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

        var vm = MakeVm(PodDetailViewModel.DescribeViewIndex);
        await vm.LoadDescribeAsync();

        Assert.Equal("describe output", vm.PodDescribeText);
    }

    [Fact]
    public async Task LoadDescribeAsync_ClearsLoadingFlag_AfterSuccess()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod").Returns(Task.FromResult("output"));

        var vm = MakeVm(PodDetailViewModel.DescribeViewIndex);
        await vm.LoadDescribeAsync();

        Assert.False(vm.IsDescribeLoading);
    }

    [Fact]
    public async Task LoadDescribeAsync_SetsErrorText_OnException()
    {
        _kubeService.GetPodDescribeAsync("default", "test-pod")
            .ThrowsAsync(new InvalidOperationException("connection refused"));

        var vm = MakeVm(PodDetailViewModel.DescribeViewIndex);
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

        var vm = MakeVm();
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

        var vm = MakeVm();
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

        var vm = MakeVm();
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

        var vm = MakeVm(PodDetailViewModel.LogsViewIndex, "app", "sidecar");
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

        var vm = MakeVm(PodDetailViewModel.LogsViewIndex, "app", "sidecar");
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

    // --- LoadEventsAsync / Events tab ---

    private void StubEvents(params PodEventInfo[] events) =>
        _kubeService.GetPodEventsAsync("default", "test-pod")
            .Returns(Task.FromResult<IReadOnlyList<PodEventInfo>>(events));

    [Fact]
    public void Initialize_SetsEventsTitle_WhenEventsViewSelected()
    {
        StubEvents();

        var vm = MakeVm(PodDetailViewModel.EventsViewIndex);
        vm.Initialize();

        Assert.Equal("Events — test-pod [default]", vm.WindowTitle);
    }

    [Fact]
    public async Task LoadEventsAsync_MapsRowsWithFormattedAge()
    {
        StubEvents(
            new PodEventInfo { Type = "Warning", Reason = "BackOff", Message = "Back-off restarting", Count = 7, Timestamp = Now.AddMinutes(-12) },
            new PodEventInfo { Type = "Normal", Reason = "Pulled", Message = "Image pulled", Count = 1, Timestamp = Now.AddHours(-3) });

        var vm = MakeVm(PodDetailViewModel.EventsViewIndex);
        await vm.LoadEventsAsync();

        Assert.Equal(2, vm.PodEvents.Count);
        var first = vm.PodEvents[0];
        Assert.Equal("Warning", first.Type);
        Assert.Equal("BackOff", first.Reason);
        Assert.True(first.IsWarning);
        Assert.Equal("12m (x7)", first.AgeDisplay);
        Assert.Equal("3h", vm.PodEvents[1].AgeDisplay);
        Assert.False(vm.IsEventsLoading);
        Assert.False(vm.HasEventsError);
        Assert.False(vm.ShowNoEvents);
    }

    [Fact]
    public async Task LoadEventsAsync_SetsError_OnException()
    {
        _kubeService.GetPodEventsAsync("default", "test-pod")
            .ThrowsAsync(new InvalidOperationException("forbidden"));

        var vm = MakeVm(PodDetailViewModel.EventsViewIndex);
        await vm.LoadEventsAsync();

        Assert.True(vm.HasEventsError);
        Assert.Contains("forbidden", vm.EventsError);
        Assert.False(vm.IsEventsLoading);
        Assert.False(vm.ShowNoEvents);
    }

    [Fact]
    public async Task LoadEventsAsync_EmptyList_ShowsNoEventsState()
    {
        StubEvents();

        var vm = MakeVm(PodDetailViewModel.EventsViewIndex);
        await vm.LoadEventsAsync();

        Assert.Empty(vm.PodEvents);
        Assert.True(vm.ShowNoEvents);
        Assert.False(vm.HasEventsError);
    }

    [Fact]
    public async Task LoadEventsAsync_ErrorThenRefresh_ClearsErrorOnSuccess()
    {
        _kubeService.GetPodEventsAsync("default", "test-pod")
            .ThrowsAsync(new InvalidOperationException("transient"));

        var vm = MakeVm(PodDetailViewModel.EventsViewIndex);
        await vm.LoadEventsAsync();
        Assert.True(vm.HasEventsError);

        StubEvents(new PodEventInfo { Type = "Normal", Reason = "Scheduled", Message = "ok", Count = 1, Timestamp = Now });
        await vm.RefreshEventsCommand.ExecuteAsync(null);

        Assert.False(vm.HasEventsError);
        Assert.Single(vm.PodEvents);
    }

    [Fact]
    public async Task SwitchingToEventsTab_LoadsEventsLazily_OnlyOnce()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());
        var calls = 0;
        _kubeService.GetPodEventsAsync("default", "test-pod")
            .Returns(_ =>
            {
                Interlocked.Increment(ref calls);
                return Task.FromResult<IReadOnlyList<PodEventInfo>>([]);
            });

        var vm = MakeVm();
        vm.Initialize();
        Assert.Equal(0, Volatile.Read(ref calls));

        vm.SelectedViewIndex = PodDetailViewModel.EventsViewIndex;
        await WaitUntilAsync(() => Volatile.Read(ref calls) == 1);
        Assert.Equal("Events — test-pod [default]", vm.WindowTitle);

        // Switching away and back must not trigger a second load.
        vm.SelectedViewIndex = PodDetailViewModel.LogsViewIndex;
        vm.SelectedViewIndex = PodDetailViewModel.EventsViewIndex;
        Assert.Equal(1, Volatile.Read(ref calls));
    }

    [Fact]
    public void ShowContainerPicker_OnlyOnLogsTab_ForMultiContainerPod()
    {
        _kubeService.StreamPodLogsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(EmptyLines());
        StubEvents();

        var vm = MakeVm(PodDetailViewModel.LogsViewIndex, "app", "sidecar");
        vm.Initialize();
        Assert.True(vm.ShowContainerPicker);

        vm.SelectedViewIndex = PodDetailViewModel.EventsViewIndex;
        Assert.False(vm.ShowContainerPicker);

        vm.SelectedViewIndex = PodDetailViewModel.LogsViewIndex;
        Assert.True(vm.ShowContainerPicker);
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
        var vm = MakeVm();
        var ex = Record.Exception(() => vm.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Dispose_CancelsActiveStream()
    {
        _kubeService.StreamPodLogsAsync("default", "test-pod", Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(x => BlockingLines(x.ArgAt<CancellationToken>(3)));

        var vm = MakeVm();
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

        protected override DateTime UtcNow => Now;
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
