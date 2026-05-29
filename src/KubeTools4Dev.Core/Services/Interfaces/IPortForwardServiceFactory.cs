namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Creates <see cref="IPortForwardService"/> instances bound to a specific <see cref="IKubernetesService"/>
/// and matching <see cref="IProfilePortForwardSupervisor"/> instances bound to a specific
/// <see cref="IPortForwardService"/>.
/// </summary>
public interface IPortForwardServiceFactory
{
    /// <summary>Creates a new <see cref="IPortForwardService"/> backed by the given <paramref name="kubernetesService"/>.</summary>
    IPortForwardService Create(IKubernetesService kubernetesService);

    /// <summary>
    /// Creates a new <see cref="IProfilePortForwardSupervisor"/> that watches forwards started
    /// through <paramref name="portForwardService"/> and restarts them on drop.
    /// </summary>
    IProfilePortForwardSupervisor CreateSupervisor(IPortForwardService portForwardService);
}
