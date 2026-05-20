using Avalonia.Controls;

namespace KubeTools4Dev.Views;

/// <summary>
/// View displaying the list of Kubernetes deployments with filter, DataGrid, and per-row action buttons.
/// </summary>
/// <seealso cref="Avalonia.Controls.UserControl" />
public partial class DeploymentListView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentListView"/> class.
    /// </summary>
    public DeploymentListView()
    {
        InitializeComponent();
    }
}
