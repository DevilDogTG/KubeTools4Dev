using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Log messages for <see cref="ProfilePortForwardSupervisor"/>.
/// </summary>
public partial class ProfilePortForwardSupervisor
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Profile {ProfileId} started — supervising {EntryCount} entries")]
    private partial void LogProfileStarted(Guid profileId, int entryCount);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Profile {ProfileId} stopped")]
    private partial void LogProfileStopped(Guid profileId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Supervising forward {Key} (attempt {Attempt}/{MaxAttempts})")]
    private partial void LogForwardSupervised(string key, int attempt, int maxAttempts);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Supervised forward {Key} dropped after attempt {Attempt} (ran {RunSeconds:F1} s)")]
    private partial void LogForwardDropped(string key, int attempt, double runSeconds);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Supervised forward {Key} threw on attempt {Attempt} after {RunSeconds:F1} s: {ErrorMessage}")]
    private partial void LogForwardCrashed(string key, int attempt, double runSeconds, string errorMessage);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Supervised forward {Key} ran stably for {RunMinutes:F1} min — retry window reset (was attempt {Attempt})")]
    private partial void LogForwardRetryWindowReset(string key, double runMinutes, int attempt);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Supervised forward {Key} retrying after {DelayMs} ms (attempt {NextAttempt}/{MaxAttempts})")]
    private partial void LogForwardRetrying(string key, double delayMs, int nextAttempt, int maxAttempts);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Supervised forward {Key} exhausted retry budget ({MaxAttempts} attempts)")]
    private partial void LogForwardExhausted(string key, int maxAttempts);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Profile {ProfileId} stopped because {Key} failed permanently")]
    private partial void LogProfileStoppedDueToFailure(Guid profileId, string key);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Entry {Key} unsupervised by user")]
    private partial void LogEntryUnsupervised(string key);
}
