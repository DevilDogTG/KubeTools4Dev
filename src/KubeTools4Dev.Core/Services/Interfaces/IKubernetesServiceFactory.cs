namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Creates <see cref="IKubernetesService"/> instances for use by the cluster connection manager.
/// </summary>
public interface IKubernetesServiceFactory
{
    /// <summary>Creates a new, disconnected <see cref="IKubernetesService"/> instance.</summary>
    IKubernetesService Create();
}
