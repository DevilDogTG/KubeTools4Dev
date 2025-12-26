using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

namespace KubeTools4Dev.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _applicationName = "KubeTools4Dev";

    [ObservableProperty]
    private string _version = "1.1.0";

    [ObservableProperty]
    private string _description = "A developer tool for Kubernetes monitoring and port forwarding.";

    [ObservableProperty]
    private string _projectUrl = "https://github.com/DevilDogTG/KubeTools4Dev";

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
