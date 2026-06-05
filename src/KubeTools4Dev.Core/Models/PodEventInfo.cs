using k8s.Models;

namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Immutable projection of a Kubernetes event scoped to a single pod, carrying only the
/// fields the diagnostics UI displays. Decouples ViewModels from the raw
/// <see cref="Corev1Event"/> shape and centralizes the timestamp/count fallback rules.
/// </summary>
public sealed record PodEventInfo
{
    /// <summary>
    /// Gets the event type, e.g. <c>Normal</c> or <c>Warning</c>.
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Gets the machine-readable reason, e.g. <c>BackOff</c>, <c>Pulled</c>, <c>OOMKilling</c>.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable event message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of times this event has occurred.
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// Gets the most recent occurrence in UTC, or <c>null</c> when the event carries no
    /// usable timestamp at all.
    /// </summary>
    public DateTime? Timestamp { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a <c>Warning</c> event.
    /// </summary>
    public bool IsWarning => string.Equals(Type, "Warning", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a raw Kubernetes event to a <see cref="PodEventInfo"/>.
    /// Timestamp fallback order: <c>lastTimestamp</c> → <c>eventTime</c> → <c>firstTimestamp</c>
    /// → metadata <c>creationTimestamp</c> (older API servers and aggregated events populate
    /// different subsets). Count falls back from <c>count</c> to <c>series.count</c>, then 1.
    /// </summary>
    /// <param name="evt">The raw Kubernetes event.</param>
    /// <returns>The mapped projection.</returns>
    public static PodEventInfo FromEvent(Corev1Event evt) => new()
    {
        Type = evt.Type ?? string.Empty,
        Reason = evt.Reason ?? string.Empty,
        Message = evt.Message ?? string.Empty,
        Count = evt.Count ?? evt.Series?.Count ?? 1,
        Timestamp = evt.LastTimestamp ?? evt.EventTime ?? evt.FirstTimestamp ?? evt.Metadata?.CreationTimestamp,
    };

    /// <summary>
    /// Maps and orders raw events newest-first; events without any timestamp sort last.
    /// </summary>
    /// <param name="events">The raw Kubernetes events.</param>
    /// <returns>The mapped projections, newest first.</returns>
    public static IReadOnlyList<PodEventInfo> FromEvents(IEnumerable<Corev1Event> events) =>
        events
            .Select(FromEvent)
            .OrderByDescending(e => e.Timestamp ?? DateTime.MinValue)
            .ToList();

    /// <summary>
    /// Formats <see cref="Timestamp"/> as a compact age relative to <paramref name="now"/>,
    /// kubectl-style: <c>45s</c>, <c>12m</c>, <c>3h</c>, <c>5d</c>. Returns <c>"unknown"</c>
    /// when the event has no timestamp.
    /// </summary>
    /// <param name="now">The reference instant (UTC).</param>
    /// <returns>The formatted age.</returns>
    public string FormatAge(DateTime now)
    {
        if (Timestamp is not { } ts)
        {
            return "unknown";
        }

        var age = now - ts;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return age switch
        {
            { TotalSeconds: < 60 } => $"{(int)age.TotalSeconds}s",
            { TotalMinutes: < 60 } => $"{(int)age.TotalMinutes}m",
            { TotalHours: < 24 } => $"{(int)age.TotalHours}h",
            _ => $"{(int)age.TotalDays}d",
        };
    }
}
