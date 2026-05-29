using KubeTools4Dev.Core.Services.Interfaces;
using System.Collections.Concurrent;

namespace KubeTools4Dev.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IPortForwardService"/> for supervisor tests. The
/// <see cref="StartHandler"/> hook lets a test drive each call's completion shape:
/// immediate return, throw, or wait-for-cancellation. Each call is recorded in
/// <see cref="Calls"/>.
/// </summary>
internal sealed class FakePortForwardService : IPortForwardService
{
    public record CallRecord(string Service, string Namespace, object TargetPort, int LocalPort);

    /// <summary>All recorded calls, in order received.</summary>
    public List<CallRecord> Calls { get; } = new();

    /// <summary>
    /// Per-call completion hook. Defaults to a hang-until-cancel handler, mirroring
    /// the real listener loop behavior. Tests override to simulate drops, crashes,
    /// or successful long-running forwards.
    /// </summary>
    public Func<CallRecord, CancellationToken, Task> StartHandler { get; set; }
        = static async (_, ct) =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        };

    /// <summary>Manually-started ports (added to <see cref="_active"/>) for IsSupervised tests.</summary>
    private readonly ConcurrentDictionary<int, byte> _active = new();

    /// <summary>Adds a "manual" forward that the supervisor should not see.</summary>
    public void TrackManualForward(int localPort) => _active.TryAdd(localPort, 0);

    /// <inheritdoc/>
    public async Task StartServicePortForwardAsync(
        string serviceName,
        string namespaceName,
        object targetPort,
        int localPort,
        CancellationToken cancellationToken)
    {
        var record = new CallRecord(serviceName, namespaceName, targetPort, localPort);
        lock (Calls) { Calls.Add(record); }
        _active.TryAdd(localPort, 0);
        try
        {
            await StartHandler(record, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _active.TryRemove(localPort, out _);
        }
    }

    /// <inheritdoc/>
    public void StopAll() => _active.Clear();

    /// <inheritdoc/>
    public IReadOnlySet<int> GetActiveLocalPorts() => new HashSet<int>(_active.Keys);
}
