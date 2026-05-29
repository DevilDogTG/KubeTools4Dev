using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Tests.Fakes;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KubeTools4Dev.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="ProfilePortForwardSupervisor"/>.
/// </summary>
public sealed class ProfilePortForwardSupervisorTests : IDisposable
{
    private readonly IReadOnlyList<TimeSpan> _origBackoff;
    private readonly int _origMaxAttempts;
    private readonly FakePortForwardService _fakePf = new();
    private readonly ILogger<ProfilePortForwardSupervisor> _logger
        = Substitute.For<ILogger<ProfilePortForwardSupervisor>>();
    private readonly ProfilePortForwardSupervisor _sut;
    private readonly List<SupervisedForwardSnapshot> _snapshots = new();
    private readonly List<ProfileFailureReason> _failures = new();

    public ProfilePortForwardSupervisorTests()
    {
        _origBackoff = ProfilePortForwardSupervisor.BackoffSchedule;
        _origMaxAttempts = ProfilePortForwardSupervisor.MaxAttempts;

        // Fast defaults; tests can override further.
        ProfilePortForwardSupervisor.BackoffSchedule = [TimeSpan.FromMilliseconds(10)];
        ProfilePortForwardSupervisor.MaxAttempts = 3;

        _sut = new ProfilePortForwardSupervisor(_fakePf, _logger);
        _sut.EntryStateChanged += s => { lock (_snapshots) _snapshots.Add(s); };
        _sut.ProfileStoppedDueToFailure += r => { lock (_failures) _failures.Add(r); };
    }

    public void Dispose()
    {
        _sut.StopAll();
        ProfilePortForwardSupervisor.BackoffSchedule = _origBackoff;
        ProfilePortForwardSupervisor.MaxAttempts = _origMaxAttempts;
    }

    private static PortForwardProfileEntry Entry(
        string svc = "svc", string ns = "ns", string targetPort = "80", int localPort = 8080)
        => new() { Namespace = ns, ServiceName = svc, TargetPort = targetPort, LocalPort = localPort };

    private async Task<SupervisedForwardSnapshot> WaitForAsync(
        Func<SupervisedForwardSnapshot, bool> predicate, int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            SupervisedForwardSnapshot? match;
            lock (_snapshots)
            {
                match = _snapshots.FirstOrDefault(predicate);
            }
            if (match is not null) return match;
            await Task.Delay(10);
        }
        throw new TimeoutException("Timed out waiting for snapshot.");
    }

    private async Task<ProfileFailureReason> WaitForFailureAsync(int timeoutMs = 5000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            lock (_failures)
            {
                if (_failures.Count > 0) return _failures[0];
            }
            await Task.Delay(10);
        }
        throw new TimeoutException("Timed out waiting for ProfileStoppedDueToFailure.");
    }

    [Fact]
    public async Task StartProfileAsync_StartsEachEntryOnce()
    {
        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid,
        [
            Entry("svc-a", "ns1", "80", 8081),
            Entry("svc-b", "ns1", "80", 8082),
        ]);

        await WaitForAsync(s => s.ServiceName == "svc-a"
            && s.State == SupervisedForwardState.Forwarding);
        await WaitForAsync(s => s.ServiceName == "svc-b"
            && s.State == SupervisedForwardState.Forwarding);

        lock (_fakePf.Calls)
        {
            Assert.Equal(2, _fakePf.Calls.Count);
            Assert.Contains(_fakePf.Calls, c => c.Service == "svc-a" && c.LocalPort == 8081);
            Assert.Contains(_fakePf.Calls, c => c.Service == "svc-b" && c.LocalPort == 8082);
        }
    }

    [Fact]
    public async Task StartProfileAsync_DuplicateCall_Idempotent()
    {
        var pid = Guid.NewGuid();
        var entries = new List<PortForwardProfileEntry> { Entry() };

        await _sut.StartProfileAsync(pid, entries);
        await WaitForAsync(s => s.State == SupervisedForwardState.Forwarding);

        await _sut.StartProfileAsync(pid, entries);

        // Give the system a tick to ensure no second runner kicked in.
        await Task.Delay(50);

        lock (_fakePf.Calls)
        {
            Assert.Single(_fakePf.Calls);
        }
    }

    [Fact]
    public async Task StopProfileAsync_CancelsAllEntriesForProfile()
    {
        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry("svc-a", localPort: 8081)]);
        await WaitForAsync(s => s.State == SupervisedForwardState.Forwarding);

        Assert.True(_sut.IsProfileRunning(pid));

        await _sut.StopProfileAsync(pid);

        Assert.False(_sut.IsProfileRunning(pid));
        Assert.False(_sut.IsSupervised("ns", "svc-a", "80"));
    }

    [Fact]
    public async Task StopProfileAsync_LeavesOtherProfilesRunning()
    {
        var pidA = Guid.NewGuid();
        var pidB = Guid.NewGuid();
        await _sut.StartProfileAsync(pidA, [Entry("svc-a", localPort: 8081)]);
        await _sut.StartProfileAsync(pidB, [Entry("svc-b", localPort: 8082)]);

        await WaitForAsync(s => s.ServiceName == "svc-a" && s.State == SupervisedForwardState.Forwarding);
        await WaitForAsync(s => s.ServiceName == "svc-b" && s.State == SupervisedForwardState.Forwarding);

        await _sut.StopProfileAsync(pidA);

        Assert.False(_sut.IsProfileRunning(pidA));
        Assert.True(_sut.IsProfileRunning(pidB));
    }

    [Fact]
    public async Task WhenForwardTaskCompletesUnexpectedly_RestartsAfterBackoff()
    {
        var callCount = 0;
        _fakePf.StartHandler = async (_, ct) =>
        {
            var n = Interlocked.Increment(ref callCount);
            if (n == 1) return; // First attempt drops immediately
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { }
        };

        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry()]);

        // Eventually a second Forwarding snapshot with AttemptCount == 2 should appear.
        await WaitForAsync(s => s.State == SupervisedForwardState.Forwarding && s.AttemptCount == 2);

        Assert.True(callCount >= 2);
    }

    [Fact]
    public async Task WhenForwardTaskCompletesUnexpectedly_EmitsRetryingSnapshot()
    {
        var callCount = 0;
        _fakePf.StartHandler = async (_, ct) =>
        {
            var n = Interlocked.Increment(ref callCount);
            if (n == 1) return;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { }
        };

        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry()]);

        var retrying = await WaitForAsync(s => s.State == SupervisedForwardState.Retrying);
        Assert.Equal(1, retrying.AttemptCount);
        Assert.Equal(ProfilePortForwardSupervisor.MaxAttempts, retrying.MaxAttempts);
    }

    [Fact]
    public async Task AfterMaxAttempts_MarksEntryFailedAndStopsWholeProfile()
    {
        // Every call returns immediately = perpetual drop.
        _fakePf.StartHandler = (_, _) => Task.CompletedTask;

        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid,
        [
            Entry("svc-a", localPort: 8081),
            Entry("svc-b", localPort: 8082),
        ]);

        // svc-a will exhaust first (or svc-b; whichever — the exhaustion stops both).
        await WaitForAsync(s => s.State == SupervisedForwardState.Failed);

        Assert.False(_sut.IsProfileRunning(pid));
    }

    [Fact]
    public async Task AfterMaxAttempts_EmitsProfileStoppedDueToFailure()
    {
        _fakePf.StartHandler = (_, _) => Task.CompletedTask;

        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry("svc-x", "ns-x", "80", 8081)]);

        var failure = await WaitForFailureAsync();
        Assert.Equal(pid, failure.ProfileId);
        Assert.Equal("svc-x", failure.FailedServiceName);
        Assert.Equal("ns-x", failure.FailedNamespace);
        Assert.Equal(ProfilePortForwardSupervisor.MaxAttempts, failure.AttemptCount);
    }

    [Fact]
    public async Task StopAll_CancelsEverythingAndIsIdempotent()
    {
        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry()]);
        await WaitForAsync(s => s.State == SupervisedForwardState.Forwarding);

        _sut.StopAll();
        _sut.StopAll(); // second call should not throw

        Assert.False(_sut.IsProfileRunning(pid));
    }

    [Fact]
    public async Task UnsuperviseAsync_CancelsOnlyOneEntry_LeavesProfileRunningForOthers()
    {
        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid,
        [
            Entry("svc-a", "ns1", "80", 8081),
            Entry("svc-b", "ns1", "80", 8082),
        ]);
        await WaitForAsync(s => s.ServiceName == "svc-a" && s.State == SupervisedForwardState.Forwarding);
        await WaitForAsync(s => s.ServiceName == "svc-b" && s.State == SupervisedForwardState.Forwarding);

        await _sut.UnsuperviseAsync("ns1", "svc-a", "80");

        Assert.False(_sut.IsSupervised("ns1", "svc-a", "80"));
        Assert.True(_sut.IsSupervised("ns1", "svc-b", "80"));
        Assert.True(_sut.IsProfileRunning(pid));
    }

    [Fact]
    public async Task UnsuperviseAsync_EmitsUnsupervisedSnapshot()
    {
        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry("svc-a", "ns1", "80", 8081)]);
        await WaitForAsync(s => s.State == SupervisedForwardState.Forwarding);

        await _sut.UnsuperviseAsync("ns1", "svc-a", "80");

        await WaitForAsync(s => s.ServiceName == "svc-a"
            && s.State == SupervisedForwardState.Unsupervised);
    }

    [Fact]
    public void IsSupervised_ReturnsFalseForManuallyStartedForwards()
    {
        // A direct call on the fake represents a manual port-forward not going through the supervisor.
        _fakePf.TrackManualForward(9000);

        Assert.False(_sut.IsSupervised("manual-ns", "manual-svc", "9000"));
    }

    [Fact]
    public async Task BackoffSchedule_AppliesIncreasingDelays()
    {
        ProfilePortForwardSupervisor.BackoffSchedule =
        [
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(80),
        ];
        ProfilePortForwardSupervisor.MaxAttempts = 4;

        var times = new List<DateTime>();
        _fakePf.StartHandler = (_, _) =>
        {
            lock (times) { times.Add(DateTime.UtcNow); }
            return Task.CompletedTask;
        };

        var pid = Guid.NewGuid();
        await _sut.StartProfileAsync(pid, [Entry()]);

        await WaitForAsync(s => s.State == SupervisedForwardState.Failed);

        lock (times)
        {
            Assert.True(times.Count >= 3);
            var delay1 = times[1] - times[0];
            var delay2 = times[2] - times[1];
            // delay2 should be noticeably larger than delay1 (80ms vs 20ms).
            Assert.True(delay2 > delay1, $"Expected delay2 ({delay2.TotalMilliseconds}ms) > delay1 ({delay1.TotalMilliseconds}ms).");
        }
    }
}
