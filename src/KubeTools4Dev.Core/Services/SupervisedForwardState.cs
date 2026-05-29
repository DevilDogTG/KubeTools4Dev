namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Lifecycle state of a single port-forward entry being watched by
/// <see cref="Interfaces.IProfilePortForwardSupervisor"/>.
/// </summary>
public enum SupervisedForwardState
{
    /// <summary>Not yet started.</summary>
    Idle,

    /// <summary>Listener is being created.</summary>
    Starting,

    /// <summary>Listener is up and forwarding.</summary>
    Forwarding,

    /// <summary>Forward has dropped; waiting before the next retry attempt.</summary>
    Retrying,

    /// <summary>All retry attempts exhausted; supervisor gave up on this entry.</summary>
    Failed,

    /// <summary>Cancelled by an explicit profile-level Stop.</summary>
    Stopped,

    /// <summary>Removed from supervision by an explicit per-entry user action.</summary>
    Unsupervised
}

/// <summary>
/// Immutable snapshot of a supervised entry's state, broadcast via
/// <see cref="Interfaces.IProfilePortForwardSupervisor.EntryStateChanged"/>.
/// </summary>
/// <param name="ProfileId">Owning profile.</param>
/// <param name="Namespace">Service namespace.</param>
/// <param name="ServiceName">Service name.</param>
/// <param name="TargetPort">Target port (string to support named ports).</param>
/// <param name="LocalPort">Local port bound on the developer machine.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="AttemptCount">1-based number of attempts so far (including the current one).</param>
/// <param name="MaxAttempts">Configured maximum attempts before <see cref="SupervisedForwardState.Failed"/>.</param>
/// <param name="LastError">Most recent failure message, when applicable.</param>
public sealed record SupervisedForwardSnapshot(
    Guid ProfileId,
    string Namespace,
    string ServiceName,
    string TargetPort,
    int LocalPort,
    SupervisedForwardState State,
    int AttemptCount,
    int MaxAttempts,
    string? LastError);

/// <summary>
/// Details about why a supervised profile was stopped because one of its entries
/// exhausted its retry budget.
/// </summary>
/// <param name="ProfileId">Profile that was stopped.</param>
/// <param name="FailedNamespace">Namespace of the entry that exhausted retries.</param>
/// <param name="FailedServiceName">Service name of the entry that exhausted retries.</param>
/// <param name="AttemptCount">Number of attempts made before giving up.</param>
/// <param name="LastError">Most recent failure message, when applicable.</param>
public sealed record ProfileFailureReason(
    Guid ProfileId,
    string FailedNamespace,
    string FailedServiceName,
    int AttemptCount,
    string? LastError);
