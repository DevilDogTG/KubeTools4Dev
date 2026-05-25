using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Log messages for <see cref="ClusterNodeViewModel"/>.
/// </summary>
public partial class ClusterNodeViewModel
{
    /// <summary>
    /// Logs when a namespace watch stream fails and a retry will be attempted.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Namespace watch for cluster {ClusterId} failed; retrying.")]
    private static partial void LogNamespaceWatchFailed(ILogger logger, string clusterId, Exception ex);
}
