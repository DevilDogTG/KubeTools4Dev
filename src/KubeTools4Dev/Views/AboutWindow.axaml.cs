using Avalonia.Controls;
using KubeTools4Dev.ViewModels;

namespace KubeTools4Dev.Views;

/// <summary>
/// About window.
/// </summary>
/// <seealso cref="Avalonia.Controls.Window" />
public partial class AboutWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutWindow"/> class.
    /// </summary>
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }
}
