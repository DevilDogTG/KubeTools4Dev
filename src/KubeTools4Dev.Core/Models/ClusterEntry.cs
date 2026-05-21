namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Represents a persisted cluster/context entry in the cluster registry.
/// </summary>
public class ClusterEntry
{
    /// <summary>Unique identifier for this entry.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Absolute path to the kubeconfig file that contains this context.</summary>
    public string KubeConfigPath { get; set; } = string.Empty;

    /// <summary>The context name within the kubeconfig file.</summary>
    public string ContextName { get; set; } = string.Empty;

    /// <summary>Human-readable display name shown in the tree.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>When false, the cluster is hidden from the tree but not deleted.</summary>
    public bool IsEnabled { get; set; } = true;
}
