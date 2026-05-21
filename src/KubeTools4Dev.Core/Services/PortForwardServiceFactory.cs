using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Default factory that creates real <see cref="PortForwardService"/> instances.
/// </summary>
public class PortForwardServiceFactory(ILoggerFactory loggerFactory) : IPortForwardServiceFactory
{
    /// <inheritdoc />
    public IPortForwardService Create(IKubernetesService kubernetesService)
        => new PortForwardService(kubernetesService, loggerFactory.CreateLogger<PortForwardService>());
}
