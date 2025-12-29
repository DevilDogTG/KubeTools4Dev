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
        Message = "Connection accepted, forwarding to {PodName}:{RemotePort}")]
    private partial void LogConnectionAccepted(string podName, int remotePort);

    /// <summary>
    /// Logs connection errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Connection error for pod {PodName}: {ErrorMessage}")]
    private partial void LogConnectionError(string podName, string errorMessage);

    /// <summary>
    /// Logs connection timeout errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Timed out connecting to pod {podName}:{remotePort}")]
    private partial void LogConnectionTimeout(string podName, int remotePort);

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
        Message = "Stream closed for pod {PodName} ({Direction}): {Message}")]
    private partial void LogStreamClosed(string podName, string direction, string message);

    /// <summary>
    /// Logs WebSocket errors.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "WebSocket error for pod {PodName}: {ErrorMessage}")]
    private partial void LogWebSocketError(string podName, string errorMessage);

    /// <summary>
    /// Logs when traffic data starts flowing.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Traffic started for {PodName} ({Direction})")]
    private partial void LogTrafficStart(string podName, string direction);

    /// <summary>
    /// Logs when traffic data ends.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Traffic ended for {PodName} ({Direction}). Total bytes: {TotalBytes}")]
    private partial void LogTrafficEnd(string podName, string direction, long totalBytes);

    /// <summary>
    /// Logs error output from the pod.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Pod error output for {PodName}: {Output}")]
    private partial void LogPodErrorOutput(string podName, string output);

    /// <summary>
    /// Logs port resolution details.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Resolved Service Port {ServicePort} to Target Port {TargetPort} for pod {PodName}")]
    private partial void LogPortResolution(int servicePort, int targetPort, string podName);
}
