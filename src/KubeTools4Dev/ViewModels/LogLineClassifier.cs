using System;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Severity of a log line as inferred by <see cref="LogLineClassifier"/>.
/// </summary>
public enum LogSeverity
{
    /// <summary>No recognized level token; rendered in the default foreground.</summary>
    Default,

    /// <summary>Error/fatal/critical level; rendered in the danger color.</summary>
    Error,

    /// <summary>Warning level; rendered in the warning color.</summary>
    Warning,

    /// <summary>Debug/trace/verbose level; rendered dimmed.</summary>
    Debug,
}

/// <summary>
/// Heuristic severity classifier for rendered log lines. Pure and stateless so it is
/// unit-testable independent of the view that colors the lines.
/// </summary>
public static class LogLineClassifier
{
    /// <summary>Only the head of the line is scanned — level tokens live in the prefix of
    /// every common format (Microsoft console, Serilog, bracketed, logfmt), and a short
    /// window avoids recoloring lines that merely mention "error" mid-message.</summary>
    internal const int ScanWindow = 80;

    // Ordered by precedence: a line carrying both (e.g. "warn: retry after error") colors
    // by the strongest match. Token sets cover Microsoft.Extensions.Logging console
    // ("fail:", "crit:", "dbug:", "trce:"), Serilog short forms ("[ERR]", "[FTL]", "[VRB]"),
    // full words, and logfmt ("level=error").
    private static readonly string[] ErrorTokens = ["error", "fatal", "crit", "fail", "[err]", "[ftl]", "panic"];
    private static readonly string[] WarningTokens = ["warn", "[wrn]"];
    private static readonly string[] DebugTokens = ["debug", "dbug", "trace", "trce", "verbose", "[dbg]", "[vrb]"];

    /// <summary>
    /// Infers the severity of a single log line from level tokens in its first
    /// <see cref="ScanWindow"/> characters. Best-effort: unrecognized formats return
    /// <see cref="LogSeverity.Default"/>.
    /// </summary>
    /// <param name="line">The log line as displayed.</param>
    /// <returns>The inferred severity.</returns>
    public static LogSeverity Classify(string line)
    {
        var head = line.AsSpan(0, Math.Min(line.Length, ScanWindow));

        if (ContainsAny(head, ErrorTokens)) return LogSeverity.Error;
        if (ContainsAny(head, WarningTokens)) return LogSeverity.Warning;
        if (ContainsAny(head, DebugTokens)) return LogSeverity.Debug;
        return LogSeverity.Default;
    }

    private static bool ContainsAny(ReadOnlySpan<char> head, string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (head.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
