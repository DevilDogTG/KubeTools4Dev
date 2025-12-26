using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.ViewModels;


/// <summary>
/// Log messages for ServiceListViewModel.
/// </summary>
public partial class ServiceListViewModel
{
    /// <summary>
    /// Logs when a connection is accepted.
    /// </summary>
    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to reconcile stale services (attempt {FailureCount}/{MaxAttempts})")]
    private partial void LogReconcileStaleServicesFailed(int failureCount, int maxAttempts);
}
