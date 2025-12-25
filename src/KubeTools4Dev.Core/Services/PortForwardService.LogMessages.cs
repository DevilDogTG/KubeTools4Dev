using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Log messages for PortForwardService.
/// </summary>
/// <seealso cref="KubeTools4Dev.Core.Services.Interfaces.IPortForwardService" />
public partial class PortForwardService
{
    /// <summary>
    /// Logs when a connection is accepted.
    /// </summary>
    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Connection accepted, forwarding to {PodName}:{RemotePort}")]
    private partial void LogConnectionAccepted(string podName, int remotePort);

    /// <summary>
    /// Logs connection errors.
    /// </summary>
    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Connection error for pod {PodName}: {ErrorMessage}")]
    private partial void LogConnectionError(string podName, string errorMessage);

    /// <summary>
    /// Logs connection timeout errors.
    /// </summary>
    [LoggerMessage(EventId = 1009, Level = LogLevel.Warning, Message = "Connection timeout for pod {PodName}: {ErrorMessage}")]
    private partial void LogConnectionTimeout(string podName, string errorMessage);

    /// <summary>
    /// Logs when no pod is found for a service.
    /// </summary>
    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "No running pod found for service {ServiceName} in namespace {Namespace}")]
    private partial void LogNoPodFound(string serviceName, string @namespace);

    /// <summary>
    /// Logs port forwarding errors.
    /// </summary>
    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Port forward error for service {ServiceName}: {ErrorMessage}")]
    private partial void LogPortForwardError(string serviceName, string errorMessage);

    /// <summary>
    /// Logs when the listener is ready.
    /// </summary>
    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Listening on port {LocalPort} -> {PodName}:{RemotePort}")]
    private partial void LogPortForwardListening(int localPort, string podName, int remotePort);

    /// <summary>
    /// Logs when port forwarding is starting.
    /// </summary>
    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Starting port forward for service {ServiceName} via pod {PodName} ({LocalPort}:{RemotePort})")]
    private partial void LogPortForwardStarting(string serviceName, string podName, int localPort, int remotePort);
    /// <summary>
    /// Logs when a port is already in use.
    /// </summary>
    [LoggerMessage(EventId = 1007, Level = LogLevel.Error, Message = "Port {LocalPort} is already in use")]
    private partial void LogPortInUse(int localPort);

    /// <summary>
    /// Logs when a stream is closed.
    /// </summary>
    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "Stream closed for pod {PodName} ({Direction}): {Message}")]
    private partial void LogStreamClosed(string podName, string direction, string message);

    /// <summary>
    /// Logs WebSocket errors.
    /// </summary>
    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "WebSocket error for pod {PodName}: {ErrorMessage}")]
    private partial void LogWebSocketError(string podName, string errorMessage);
}
