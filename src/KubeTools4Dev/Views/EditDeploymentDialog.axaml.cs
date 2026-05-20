using Avalonia.Controls;

namespace KubeTools4Dev.Views;

/// <summary>
/// Modal dialog window for editing a Kubernetes deployment's replica count and image tag.
/// </summary>
/// <seealso cref="Avalonia.Controls.Window" />
public partial class EditDeploymentDialog : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EditDeploymentDialog"/> class.
    /// Required by the Avalonia XAML runtime loader.
    /// </summary>
    public EditDeploymentDialog()
    {
        InitializeComponent();
    }
}
