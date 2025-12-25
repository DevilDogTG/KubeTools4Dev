using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

public partial class PortForwardService
{
    [LoggerMessage(Level = LogLevel.Information, Message = "[kubectl {ServiceName}] {Data}")]
    private partial void LogKubectlInfo(string serviceName, string? data);

    [LoggerMessage(Level = LogLevel.Error, Message = "[kubectl {ServiceName}] {Data}")]
    private partial void LogKubectlError(string serviceName, string? data);
}
