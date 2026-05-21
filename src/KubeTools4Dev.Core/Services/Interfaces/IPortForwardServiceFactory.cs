namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Creates <see cref="IPortForwardService"/> instances bound to a specific <see cref="IKubernetesService"/>.
/// </summary>
public interface IPortForwardServiceFactory
{
    /// <summary>Creates a new <see cref="IPortForwardService"/> backed by the given <paramref name="kubernetesService"/>.</summary>
    IPortForwardService Create(IKubernetesService kubernetesService);
}
