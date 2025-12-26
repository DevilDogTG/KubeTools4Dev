using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// About view model.
/// </summary>
/// <seealso cref="ViewModelBase" />
public partial class AboutViewModel : ViewModelBase
{
    /// <summary>
    /// The application name
    /// </summary>
    [ObservableProperty]
    private string _applicationName = "KubeTools4Dev";

    /// <summary>
    /// The version
    /// </summary>
    [ObservableProperty]
    private string _version = "1.1.0";

    /// <summary>
    /// The description
    /// </summary>
    [ObservableProperty]
    private string _description = "A developer tool for Kubernetes monitoring and port forwarding.";

    /// <summary>
    /// The project URL
    /// </summary>
    [ObservableProperty]
    private string _projectUrl = "https://github.com/DevilDogTG/KubeTools4Dev";

    /// <summary>
    /// Opens the URL.
    /// </summary>
    [RelayCommand]
    private void OpenUrl()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
        }
        catch
        {
            // best effort to open browser
        }
    }
}
