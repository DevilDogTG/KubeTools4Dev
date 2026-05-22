using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Represents a Kubernetes namespace under a cluster node.
/// Always exposes exactly three resource type children: Pods, Services, Deployments.
/// </summary>
public partial class NamespaceNodeViewModel : ObservableObject
{
    /// <summary>
    /// Initializes a new namespace node for the given namespace name and cluster ID.
    /// </summary>
    /// <param name="name">The Kubernetes namespace name.</param>
    /// <param name="clusterId">The ID of the owning cluster.</param>
    /// <param name="selectCallback">
    /// Optional callback invoked when the user selects a resource type leaf node.
    /// Each <see cref="ResourceTypeNodeViewModel"/> will have its <c>SelectCommand</c> wired to this callback.
    /// </param>
    public NamespaceNodeViewModel(string name, string clusterId, Action<ContentScopeContext>? selectCallback = null)
    {
        Name = name;
        ClusterId = clusterId;
        ResourceTypes =
        [
            new ResourceTypeNodeViewModel("Pods", ResourceKind.Pods)
            {
                SelectCommand = selectCallback is null ? null
                    : new RelayCommand(() => selectCallback(new ContentScopeContext(clusterId, name, ResourceKind.Pods)))
            },
            new ResourceTypeNodeViewModel("Services", ResourceKind.Services)
            {
                SelectCommand = selectCallback is null ? null
                    : new RelayCommand(() => selectCallback(new ContentScopeContext(clusterId, name, ResourceKind.Services)))
            },
            new ResourceTypeNodeViewModel("Deployments", ResourceKind.Deployments)
            {
                SelectCommand = selectCallback is null ? null
                    : new RelayCommand(() => selectCallback(new ContentScopeContext(clusterId, name, ResourceKind.Deployments)))
            }
        ];
    }

    /// <summary>Gets the Kubernetes namespace name.</summary>
    public string Name { get; }

    /// <summary>Gets the ID of the cluster this namespace belongs to.</summary>
    public string ClusterId { get; }

    /// <summary>Gets or sets whether the resource type list under this namespace is expanded.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>Gets the fixed resource type children for this namespace.</summary>
    public ObservableCollection<ResourceTypeNodeViewModel> ResourceTypes { get; }

    /// <summary>Toggles the <see cref="IsExpanded"/> state.</summary>
    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
