using Avalonia.Controls;

namespace KubeTools4Dev.Views;

/// <summary>
/// Code-behind for the cluster tree sidebar.
/// Interaction (expand/collapse, connect, resource selection) is handled entirely via ViewModel commands.
/// </summary>
public partial class ClusterTreeView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="ClusterTreeView"/>.</summary>
    public ClusterTreeView()
    {
        InitializeComponent();
    }
}
