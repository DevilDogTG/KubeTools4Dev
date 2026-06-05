using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KubeTools4Dev.Core.Services;

/// <inheritdoc cref="IProfilePortForwardSupervisor"/>
public sealed partial class ProfilePortForwardSupervisor(
    IPortForwardService portForwardService,
    ILogger<ProfilePortForwardSupervisor> logger
) : IProfilePortForwardSupervisor
{
    /// <summary>
    /// Backoff schedule applied between retry attempts. Indexed by attempt number minus
    /// one (so the 2nd attempt uses <c>BackoffSchedule[0]</c>). Attempt 1 is the initial
    /// start and has no preceding delay. When the index exceeds the schedule length, the
    /// last entry is reused.
    /// </summary>
    internal static IReadOnlyList<TimeSpan> BackoffSchedule { get; set; } =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    ];

    /// <summary>Maximum number of attempts before an entry is marked <see cref="SupervisedForwardState.Failed"/>.</summary>
    internal static int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// Minimum duration a forward must have been running for its preceding attempt count to be
    /// forgiven. A forward that ran at least this long before dropping resets the retry window,
    /// so occasional drops over a long app run (sleep/resume, API blips, pod redeploys) never
    /// accumulate into <see cref="SupervisedForwardState.Failed"/> — only rapid consecutive
    /// failures exhaust the budget.
    /// </summary>
    internal static TimeSpan StableRunThreshold { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Keyed by <c>"{ns}/{svc}:{targetPort}"</c> — identity of a service-to-forward.</summary>
    private readonly ConcurrentDictionary<string, SupervisedForward> _entries = new();

    private readonly object _stateLock = new();

    /// <inheritdoc/>
    public event Action<SupervisedForwardSnapshot>? EntryStateChanged;

    /// <inheritdoc/>
    public event Action<ProfileFailureReason>? ProfileStoppedDueToFailure;

    /// <inheritdoc/>
    public Task StartProfileAsync(Guid profileId, IReadOnlyList<PortForwardProfileEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        LogProfileStarted(profileId, entries.Count);

        foreach (var entry in entries)
        {
            var key = MakeKey(entry.Namespace, entry.ServiceName, entry.TargetPort);

            var sf = new SupervisedForward
            {
                Key = key,
                ProfileId = profileId,
                Namespace = entry.Namespace,
                ServiceName = entry.ServiceName,
                TargetPort = entry.TargetPort,
                LocalPort = entry.LocalPort,
                State = SupervisedForwardState.Idle,
                Cts = new CancellationTokenSource(),
            };

            // Idempotent: if the same key is already supervised, skip.
            if (!_entries.TryAdd(key, sf))
            {
                sf.Cts.Dispose();
                continue;
            }

            sf.Runner = Task.Run(() => RunSupervisedAsync(sf, sf.Cts.Token));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopProfileAsync(Guid profileId)
    {
        var toStop = _entries.Values.Where(e => e.ProfileId == profileId).ToList();
        foreach (var sf in toStop)
        {
            sf.Cts.Cancel();
        }

        foreach (var sf in toStop)
        {
            try
            {
                if (sf.Runner is not null)
                    await sf.Runner.ConfigureAwait(false);
            }
            catch
            {
                // Runner exits cleanly via cancellation; ignore residual exceptions.
            }
            _entries.TryRemove(sf.Key, out _);
            sf.Cts.Dispose();
        }

        LogProfileStopped(profileId);
    }

    /// <inheritdoc/>
    public async Task UnsuperviseAsync(string namespaceName, string serviceName, string targetPort)
    {
        var key = MakeKey(namespaceName, serviceName, targetPort);
        if (!_entries.TryGetValue(key, out var sf))
            return;

        sf.PendingUnsupervise = true;
        sf.Cts.Cancel();

        try
        {
            if (sf.Runner is not null)
                await sf.Runner.ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        SetState(sf, SupervisedForwardState.Unsupervised, null);
        _entries.TryRemove(sf.Key, out _);
        sf.Cts.Dispose();
        LogEntryUnsupervised(key);
    }

    /// <inheritdoc/>
    public bool IsSupervised(string namespaceName, string serviceName, string targetPort)
        => _entries.ContainsKey(MakeKey(namespaceName, serviceName, targetPort));

    /// <inheritdoc/>
    public bool IsProfileRunning(Guid profileId)
        => _entries.Values.Any(e => e.ProfileId == profileId && IsLiveState(e.State));

    /// <inheritdoc/>
    public void StopAll()
    {
        // Snapshot, cancel, and clear synchronously so callers cannot observe stale entries.
        var allEntries = _entries.Values.ToList();
        foreach (var sf in allEntries) sf.Cts.Cancel();
        _entries.Clear();

        // Dispose CTSs after runners exit. Non-blocking to keep StopAll synchronous for callers
        // like cluster-disconnect that should return immediately.
        _ = Task.Run(async () =>
        {
            foreach (var sf in allEntries)
            {
                try { if (sf.Runner is not null) await sf.Runner.ConfigureAwait(false); }
                catch { /* canceled */ }
                try { sf.Cts.Dispose(); }
                catch { /* already disposed elsewhere */ }
            }
        });
    }

    private static bool IsLiveState(SupervisedForwardState s)
        => s is SupervisedForwardState.Starting
              or SupervisedForwardState.Forwarding
              or SupervisedForwardState.Retrying;

    private static string MakeKey(string ns, string svc, string targetPort)
        => $"{ns}/{svc}:{targetPort}";

    private async Task RunSupervisedAsync(SupervisedForward sf, CancellationToken stopToken)
    {
        int attempt = 0;
        string? lastError = null;

        while (!stopToken.IsCancellationRequested && attempt < MaxAttempts)
        {
            attempt++;
            sf.AttemptCount = attempt;
            LogForwardSupervised(sf.Key, attempt, MaxAttempts);
            SetState(sf, SupervisedForwardState.Starting, lastError);

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(stopToken);
            var runTimer = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                SetState(sf, SupervisedForwardState.Forwarding, null);
                await portForwardService.StartServicePortForwardAsync(
                    sf.ServiceName,
                    sf.Namespace,
                    sf.TargetPort,
                    sf.LocalPort,
                    attemptCts.Token).ConfigureAwait(false);
                runTimer.Stop();

                if (stopToken.IsCancellationRequested) break;

                // Returned cleanly without cancellation = unexpected drop.
                LogForwardDropped(sf.Key, attempt, runTimer.Elapsed.TotalSeconds);
                lastError = null;
            }
            catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                runTimer.Stop();
                lastError = ex.Message;
                LogForwardCrashed(sf.Key, attempt, runTimer.Elapsed.TotalSeconds, ex.Message);
            }

            if (stopToken.IsCancellationRequested) break;

            // A forward that ran stably before this drop must not consume the retry budget —
            // otherwise rare drops over a long app run accumulate into a permanent Failed.
            if (runTimer.Elapsed >= StableRunThreshold)
            {
                LogForwardRetryWindowReset(sf.Key, runTimer.Elapsed.TotalMinutes, attempt);
                attempt = 0;
                sf.AttemptCount = 0;
            }

            if (attempt >= MaxAttempts)
            {
                SetState(sf, SupervisedForwardState.Failed, lastError);
                LogForwardExhausted(sf.Key, MaxAttempts);
                OnEntryExhausted(sf, lastError);
                return;
            }

            var delay = ComputeBackoff(attempt);
            LogForwardRetrying(sf.Key, delay.TotalMilliseconds, attempt + 1, MaxAttempts);
            SetState(sf, SupervisedForwardState.Retrying, lastError);

            try
            {
                await Task.Delay(delay, stopToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (sf.PendingUnsupervise)
        {
            // UnsuperviseAsync will publish the Unsupervised snapshot next; do not flash Stopped.
            return;
        }

        if (sf.State != SupervisedForwardState.Failed)
        {
            SetState(sf, SupervisedForwardState.Stopped, null);
        }
    }

    private static TimeSpan ComputeBackoff(int attempt)
    {
        // attempt can be 0 right after a stable-run reset — clamp to the first entry.
        var index = Math.Clamp(attempt - 1, 0, BackoffSchedule.Count - 1);
        return BackoffSchedule[index];
    }

    private void OnEntryExhausted(SupervisedForward failed, string? lastError)
    {
        var profileId = failed.ProfileId;

        // Snapshot siblings and cancel their runners.
        var siblings = _entries.Values
            .Where(e => e.ProfileId == profileId && e.Key != failed.Key)
            .ToList();
        foreach (var sf in siblings) sf.Cts.Cancel();

        // Remove ALL profile entries (including the failed one) from _entries synchronously so
        // a subsequent StartProfileAsync(pid, sameEntries) is not blocked by stale keys whose
        // TryAdd would fail and silently skip starting a new runner.
        var toCleanup = new List<SupervisedForward>(siblings) { failed };
        foreach (var sf in toCleanup) _entries.TryRemove(sf.Key, out _);

        LogProfileStoppedDueToFailure(profileId, failed.Key);

        var reason = new ProfileFailureReason(
            profileId,
            failed.Namespace,
            failed.ServiceName,
            failed.AttemptCount,
            lastError);
        ProfileStoppedDueToFailure?.Invoke(reason);

        // Dispose CTSs after the runners exit. Non-blocking so the failed runner — which is
        // currently mid-call — can return; awaiting our own Runner here would deadlock.
        _ = Task.Run(async () =>
        {
            foreach (var sf in toCleanup)
            {
                try { if (sf.Runner is not null) await sf.Runner.ConfigureAwait(false); }
                catch { /* runners exit cleanly via cancellation; ignore */ }
                try { sf.Cts.Dispose(); }
                catch { /* already disposed elsewhere; ignore */ }
            }
        });
    }

    private void SetState(SupervisedForward sf, SupervisedForwardState newState, string? lastError)
    {
        lock (_stateLock)
        {
            sf.State = newState;
            sf.LastError = lastError;
        }

        EntryStateChanged?.Invoke(new SupervisedForwardSnapshot(
            sf.ProfileId,
            sf.Namespace,
            sf.ServiceName,
            sf.TargetPort,
            sf.LocalPort,
            newState,
            sf.AttemptCount,
            MaxAttempts,
            lastError));
    }

    /// <summary>Mutable per-entry record held inside the supervisor.</summary>
    internal sealed class SupervisedForward
    {
        public required string Key { get; init; }
        public required Guid ProfileId { get; init; }
        public required string Namespace { get; init; }
        public required string ServiceName { get; init; }
        public required string TargetPort { get; init; }
        public required int LocalPort { get; init; }
        public required CancellationTokenSource Cts { get; init; }
        public SupervisedForwardState State { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
        public Task? Runner { get; set; }
        public bool PendingUnsupervise { get; set; }
    }
}
