using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Shares;

/// <summary>
/// Shared high-performance logging methods using source generators.
/// </summary>
public static partial class LogMessages
{
    // ===== General =====

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    public static partial void Info(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void Warn(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Error(this ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Error(this ILogger logger, Exception ex, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void Debug(this ILogger logger, string message);
}
