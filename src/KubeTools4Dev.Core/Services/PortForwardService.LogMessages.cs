using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Log messages for PortForwardService.
/// </summary>
/// <seealso cref="Interfaces.IPortForwardService" />
public partial class PortForwardService
{
    /// <summary>
    /// Logs when a connection is accepted.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[{ConnId}] Connection accepted, forwarding to {PodName}:{RemotePort}")]
    private partial void LogConnectionAccepted(string connId, string podName, int remotePort);

    /// <summary>
    /// Logs the total lifetime of a connection when it closes.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "[{ConnId}] Connection to {PodName}:{RemotePort} closed after {Duration:g}")]
    private partial void LogConnectionLifetime(string connId, string podName, int remotePort, TimeSpan duration);

    /// <summary>
    /// Logs a periodic heartbeat for a long-running connection.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[{ConnId}] Port-forward alive: {PodName}:{RemotePort}, running {Duration:g}")]
    private partial void LogConnectionHeartbeat(string connId, string podName, int remotePort, TimeSpan duration);

    /// <summary>
    /// Logs connection errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[{ConnId}] Connection error for pod {PodName}: {ErrorMessage}")]
    private partial void LogConnectionError(string connId, string podName, string errorMessage);

    /// <summary>
    /// Logs connection timeout errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[{ConnId}] Timed out connecting to pod {PodName}:{RemotePort}")]
    private partial void LogConnectionTimeout(string connId, string podName, int remotePort);

    /// <summary>
    /// Logs when no pod is found for a service.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No running pod found for service {ServiceName} in namespace {Namespace}")]
    private partial void LogNoPodFound(string serviceName, string @namespace);

    /// <summary>
    /// Logs port forwarding errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Port forward error for service {ServiceName}: {ErrorMessage}")]
    private partial void LogPortForwardError(string serviceName, string errorMessage);

    /// <summary>
    /// Logs when the listener is ready.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Listening on port {LocalPort} -> {PodName}:{RemotePort}")]
    private partial void LogPortForwardListening(int localPort, string podName, int remotePort);

    /// <summary>
    /// Logs when port forwarding is starting.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting port forward for service {ServiceName} via pod {PodName} ({LocalPort}:{RemotePort})")]
    private partial void LogPortForwardStarting(string serviceName, string podName, int localPort, int remotePort);

    /// <summary>
    /// Logs when port forwarding is starting.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Failed to cancel port forward for {Key}")]
    private partial void LogPortForwardStopFailed(string key);

    /// <summary>
    /// Logs when a port is already in use.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Port {LocalPort} is already in use")]
    private partial void LogPortInUse(int localPort);

    /// <summary>
    /// Logs when a stream is closed.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "[{ConnId}] Stream closed for pod {PodName} ({Direction}) [{ExceptionType}]: {Message}")]
    private partial void LogStreamClosed(string connId, string podName, string direction, string exceptionType, string message);

    /// <summary>
    /// Logs WebSocket errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[{ConnId}] WebSocket error for pod {PodName}: {ErrorMessage}")]
    private partial void LogWebSocketError(string connId, string podName, string errorMessage);
}
