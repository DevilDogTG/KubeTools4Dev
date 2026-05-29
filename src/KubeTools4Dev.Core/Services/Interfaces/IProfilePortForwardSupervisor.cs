using KubeTools4Dev.Core.Models;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Watches the port-forwards started by a named <see cref="PortForwardProfile"/> and
/// automatically restarts any that drop, using a bounded exponential backoff. Manual
/// (non-profile) port-forwards are unaffected — they never enter the supervised set.
/// </summary>
public interface IProfilePortForwardSupervisor
{
    /// <summary>
    /// Begin supervising every entry in <paramref name="entries"/> under the given
    /// <paramref name="profileId"/>. Calling this for a profile that is already running
    /// is a no-op (idempotent).
    /// </summary>
    /// <param name="profileId">Identifier of the owning profile.</param>
    /// <param name="entries">Service entries to forward and monitor.</param>
    /// <returns>A task that completes once every entry's runner has been queued.</returns>
    Task StartProfileAsync(Guid profileId, IReadOnlyList<PortForwardProfileEntry> entries);

    /// <summary>
    /// Stops every entry currently supervised under <paramref name="profileId"/> and
    /// removes them from the supervised set.
    /// </summary>
    Task StopProfileAsync(Guid profileId);

    /// <summary>
    /// Removes a single entry from supervision (cancels its runner, fires an
    /// <see cref="SupervisedForwardState.Unsupervised"/> snapshot) without affecting
    /// other entries in the same profile.
    /// </summary>
    Task UnsuperviseAsync(string namespaceName, string serviceName, string targetPort);

    /// <summary>
    /// Returns <see langword="true"/> if the (ns, service, targetPort) tuple is
    /// currently in the supervised set.
    /// </summary>
    bool IsSupervised(string namespaceName, string serviceName, string targetPort);

    /// <summary>
    /// Returns <see langword="true"/> if at least one entry of <paramref name="profileId"/>
    /// is in <see cref="SupervisedForwardState.Starting"/>,
    /// <see cref="SupervisedForwardState.Forwarding"/> or
    /// <see cref="SupervisedForwardState.Retrying"/>.
    /// </summary>
    bool IsProfileRunning(Guid profileId);

    /// <summary>
    /// Cancels every supervised entry across every profile.
    /// </summary>
    void StopAll();

    /// <summary>
    /// Raised every time a supervised entry transitions state. Always carries the
    /// latest snapshot for the entry; subscribers should compare by
    /// (Namespace, ServiceName, TargetPort).
    /// </summary>
    event Action<SupervisedForwardSnapshot>? EntryStateChanged;

    /// <summary>
    /// Raised after the supervisor has cancelled every entry of a profile because one
    /// entry exhausted its retry budget. The UI is expected to surface this to the
    /// user (e.g., as a top-level banner).
    /// </summary>
    event Action<ProfileFailureReason>? ProfileStoppedDueToFailure;
}
