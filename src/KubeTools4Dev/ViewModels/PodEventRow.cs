namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Immutable display row for the pod detail Events tab. The age is formatted once at load
/// time (the tab has a manual Refresh, so rows do not tick).
/// </summary>
/// <param name="Type">The event type, e.g. <c>Normal</c> or <c>Warning</c>.</param>
/// <param name="Reason">The machine-readable reason, e.g. <c>BackOff</c>.</param>
/// <param name="Message">The human-readable event message.</param>
/// <param name="Count">The number of times the event has occurred.</param>
/// <param name="Age">The pre-formatted age of the most recent occurrence, e.g. <c>12m</c>.</param>
/// <param name="IsWarning">Whether this is a <c>Warning</c> event (drives row coloring).</param>
public sealed record PodEventRow(
    string Type,
    string Reason,
    string Message,
    int Count,
    string Age,
    bool IsWarning)
{
    /// <summary>Gets the age combined with the repeat count when the event occurred more
    /// than once, e.g. <c>12m (x7)</c>.</summary>
    public string AgeDisplay => Count > 1 ? $"{Age} (x{Count})" : Age;
}
