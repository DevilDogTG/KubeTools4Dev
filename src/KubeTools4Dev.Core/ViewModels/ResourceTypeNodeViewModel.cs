using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Leaf node in the cluster tree — represents a resource type (Pods, Services, or Deployments)
/// within a specific namespace and cluster.
/// </summary>
public class ResourceTypeNodeViewModel : ObservableObject
{
    /// <summary>Initializes a new instance with the given display name and resource kind.</summary>
    public ResourceTypeNodeViewModel(string displayName, ResourceKind kind)
    {
        DisplayName = displayName;
        Kind = kind;
    }

    /// <summary>Gets the display name shown in the tree (e.g. "Pods").</summary>
    public string DisplayName { get; }

    /// <summary>Gets the resource kind this node represents.</summary>
    public ResourceKind Kind { get; }

    /// <summary>
    /// Gets the command to select this resource type; wired by the parent <see cref="NamespaceNodeViewModel"/>.
    /// <c>null</c> when no selection callback was provided at construction.
    /// </summary>
    public IRelayCommand? SelectCommand { get; init; }
}
