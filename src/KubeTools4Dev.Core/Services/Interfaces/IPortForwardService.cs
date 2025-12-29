namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Port forwarding service interface for forwarding local ports to Kubernetes pods.
/// </summary>
public interface IPortForwardService
{
    /// <summary>
    /// Starts the service port forward asynchronous.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="namespaceName">Name of the namespace.</param>
    /// <param name="targetPort">The target port.</param>
    /// <param name="localPort">The local port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartServicePortForwardAsync(
        string serviceName,
        string namespaceName,
        object targetPort,
        int localPort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops all active port forwards.
    /// </summary>
    void StopAll();
}
