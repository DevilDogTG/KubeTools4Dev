namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// The Kubernetes resource kinds surfaced in the navigation tree.
/// </summary>
public enum ResourceKind
{
    /// <summary>Pod resources.</summary>
    Pods,

    /// <summary>Service resources.</summary>
    Services,

    /// <summary>Deployment resources.</summary>
    Deployments
}
