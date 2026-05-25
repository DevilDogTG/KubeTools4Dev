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
    /// <param name="name">
    /// The Kubernetes namespace name, used as the namespace value in <see cref="ContentScopeContext"/>.
    /// Pass an empty string to represent the "all namespaces" virtual node.
    /// </param>
    /// <param name="clusterId">The ID of the owning cluster.</param>
    /// <param name="selectCallback">
    /// Optional callback invoked when the user selects a resource type leaf node.
    /// Each <see cref="ResourceTypeNodeViewModel"/> will have its <c>SelectCommand</c> wired to this callback.
    /// </param>
    /// <param name="displayName">
    /// Optional display label shown in the sidebar. When <see langword="null"/>, defaults to <paramref name="name"/>.
    /// Use this to show a human-readable label for virtual nodes (e.g., "(all namespaces)") while keeping
    /// <paramref name="name"/> as the actual namespace value passed to the API.
    /// </param>
    public NamespaceNodeViewModel(string name, string clusterId, Action<ContentScopeContext>? selectCallback = null, string? displayName = null)
    {
        Name = name;
        DisplayName = displayName ?? name;
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

    /// <summary>Gets the Kubernetes namespace name used in API calls and <see cref="ContentScopeContext"/>.</summary>
    /// <remarks>
    /// An empty string represents the "all namespaces" virtual node; the value is passed directly to
    /// <see cref="ContentScopeContext.Namespace"/> and routes to the cluster-wide API endpoints.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the human-readable label displayed in the sidebar.
    /// Defaults to <see cref="Name"/> unless an explicit display name was supplied at construction time.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether this node is the virtual "all namespaces" sentinel.
    /// When <see langword="true"/>, the node has no real Kubernetes namespace and queries run across all namespaces.
    /// </summary>
    public bool IsAllNamespaces => string.IsNullOrEmpty(Name);

    /// <summary>Gets the ID of the cluster this namespace belongs to.</summary>
    public string ClusterId { get; }

    /// <summary>Gets or sets whether the resource type list under this namespace is expanded.</summary>
    [ObservableProperty]
    private bool _isExpanded = false;

    /// <summary>Gets the fixed resource type children for this namespace.</summary>
    public ObservableCollection<ResourceTypeNodeViewModel> ResourceTypes { get; }

    /// <summary>Toggles the <see cref="IsExpanded"/> state.</summary>
    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
