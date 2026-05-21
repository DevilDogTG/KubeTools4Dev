using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents a Kubernetes namespace under a cluster node.
/// Always exposes exactly three resource type children: Pods, Services, Deployments.
/// </summary>
public class NamespaceNodeViewModel : ObservableObject
{
    /// <summary>Initializes a new namespace node for the given namespace name and cluster ID.</summary>
    public NamespaceNodeViewModel(string name, string clusterId)
    {
        Name = name;
        ClusterId = clusterId;
        ResourceTypes =
        [
            new ResourceTypeNodeViewModel("Pods", ResourceKind.Pods),
            new ResourceTypeNodeViewModel("Services", ResourceKind.Services),
            new ResourceTypeNodeViewModel("Deployments", ResourceKind.Deployments)
        ];
    }

    /// <summary>Gets the Kubernetes namespace name.</summary>
    public string Name { get; }

    /// <summary>Gets the ID of the cluster this namespace belongs to.</summary>
    public string ClusterId { get; }

    /// <summary>Gets the fixed resource type children for this namespace.</summary>
    public ObservableCollection<ResourceTypeNodeViewModel> ResourceTypes { get; }
}
