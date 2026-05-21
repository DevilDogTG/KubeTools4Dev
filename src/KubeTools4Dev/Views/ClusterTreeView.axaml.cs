using Avalonia.Controls;
using Avalonia.Input;
using KubeTools4Dev.Core.ViewModels;

namespace KubeTools4Dev.Views;

/// <summary>
/// Code-behind for the cluster tree sidebar.
/// Handles selection and double-click gestures since they require Avalonia UI thread access.
/// </summary>
public partial class ClusterTreeView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="ClusterTreeView"/>.</summary>
    public ClusterTreeView()
    {
        InitializeComponent();

        var tree = this.Find<TreeView>("ClusterTree")!;
        tree.SelectionChanged += OnTreeSelectionChanged;
        tree.DoubleTapped += OnTreeDoubleTapped;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ClusterTreeViewModel vm) return;
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is not ResourceTypeNodeViewModel resourceNode) return;

        var nsNode = FindParentItem<NamespaceNodeViewModel>(tree, resourceNode);
        var clusterNode = nsNode != null ? FindParentItem<ClusterNodeViewModel>(tree, nsNode) : null;

        if (nsNode != null && clusterNode != null)
        {
            vm.SelectResourceNode(resourceNode, nsNode, clusterNode);
        }
    }

    private void OnTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TreeView tree) return;

        if (tree.SelectedItem is ClusterNodeViewModel cluster)
        {
            cluster.ConnectCommand.Execute(null);
        }
    }

    /// <summary>
    /// Searches the ViewModel tree to find the direct parent of type <typeparamref name="T"/> that contains <paramref name="childItem"/>.
    /// </summary>
    private static T? FindParentItem<T>(TreeView tree, object childItem) where T : class
    {
        if (tree.ItemsSource is not System.Collections.IEnumerable sources) return null;

        foreach (var source in sources)
        {
            if (source is not KubeConfigSourceNodeViewModel sourceNode) continue;

            foreach (var cluster in sourceNode.Clusters)
            {
                if (typeof(T) == typeof(ClusterNodeViewModel))
                {
                    foreach (var ns in cluster.Namespaces)
                    {
                        if (childItem is ResourceTypeNodeViewModel rt && ns.ResourceTypes.Contains(rt))
                            return cluster as T;
                        if (childItem is NamespaceNodeViewModel nsItem && nsItem == ns)
                            return cluster as T;
                    }
                }
                else if (typeof(T) == typeof(NamespaceNodeViewModel))
                {
                    foreach (var ns in cluster.Namespaces)
                    {
                        if (childItem is ResourceTypeNodeViewModel rt && ns.ResourceTypes.Contains(rt))
                            return ns as T;
                    }
                }
            }
        }
        return null;
    }
}
