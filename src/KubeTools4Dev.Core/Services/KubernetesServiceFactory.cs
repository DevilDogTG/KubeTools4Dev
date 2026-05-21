using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Default factory that creates real <see cref="KubernetesService"/> instances.
/// </summary>
public class KubernetesServiceFactory(ILoggerFactory loggerFactory) : IKubernetesServiceFactory
{
    /// <inheritdoc />
    public IKubernetesService Create()
        => new KubernetesService(loggerFactory.CreateLogger<KubernetesService>());
}
