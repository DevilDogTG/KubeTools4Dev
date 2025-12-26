using Avalonia.Controls;
using KubeTools4Dev.ViewModels;

namespace KubeTools4Dev.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }
}
