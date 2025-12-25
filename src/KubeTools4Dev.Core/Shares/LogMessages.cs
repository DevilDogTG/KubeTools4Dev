using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Shares;

/// <summary>
/// Shared high-performance logging methods using source generators.
/// </summary>
public static partial class LogMessages
{
    /// <summary>
    /// Informations the specified message.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    public static partial void Info(this ILogger logger, string message);

    /// <summary>
    /// Warns the specified message.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Message}")]
    public static partial void Warn(this ILogger logger, string message);

    /// <summary>
    /// Errors the specified message.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Error(this ILogger logger, string message);

    /// <summary>
    /// Errors the specified ex.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="ex">The ex.</param>
    /// <param name="message">The message.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    public static partial void Error(this ILogger logger, Exception ex, string message);

    /// <summary>
    /// Debugs the specified message.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="message">The message.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "{Message}")]
    public static partial void Debug(this ILogger logger, string message);
}
