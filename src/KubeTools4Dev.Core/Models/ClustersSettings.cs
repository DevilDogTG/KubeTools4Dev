namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Settings section for cluster registry persistence.
/// </summary>
public class ClustersSettings
{
    /// <summary>All registered cluster entries.</summary>
    public List<ClusterEntry> Clusters { get; set; } = [];

    /// <summary>
    /// When true and <see cref="Clusters"/> is empty on first launch, the app auto-discovers
    /// all contexts from the default kubeconfig (~/.kube/config) and populates the registry.
    /// </summary>
    public bool AutoDiscoverDefaultKubeConfig { get; set; } = true;
}
