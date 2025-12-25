namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// 
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
    /// <returns></returns>
    Task StartServicePortForwardAsync(
        string serviceName,
        string namespaceName,
        object targetPort,
        int localPort,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops all.
    /// </summary>
    void StopAll();
}
