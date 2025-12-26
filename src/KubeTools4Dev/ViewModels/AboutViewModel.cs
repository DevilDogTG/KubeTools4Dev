using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

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
    private string _applicationName = string.Empty;

    /// <summary>
    /// The copyright
    /// </summary>
    [ObservableProperty]
    private string _copyright = string.Empty;

    /// <summary>
    /// The description
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// The project URL
    /// </summary>
    [ObservableProperty]
    private string _projectUrl = string.Empty;

    /// <summary>
    /// The version
    /// </summary>
    [ObservableProperty]
    private string _version = string.Empty;
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutViewModel"/> class.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">Cannot load assembly</exception>
    public AboutViewModel()
    {
        var assembly = typeof(AboutViewModel).Assembly;

        _applicationName = assembly
            .GetCustomAttributes(typeof(AssemblyTitleAttribute), false)
            .Cast<AssemblyTitleAttribute>()
            .FirstOrDefault()?.Title
                ?? "KubeTools4Dev";

        var rawVersion = assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
            .Cast<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                    ?? "1.1.0";

        _version = rawVersion;
        if (rawVersion.Contains('+'))
        {
            var parts = rawVersion.Split('+');
            _version = (parts.Length == 2 && parts[1].Length > 8)
                ? $"{parts[0]}+{parts[1][..8]}"
                : rawVersion;
        }

        _description = assembly
            .GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
            .Cast<AssemblyDescriptionAttribute>()
            .FirstOrDefault()?.Description
                ?? "A developer tool for Kubernetes.";

        var metadata = assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>();

        _projectUrl = metadata
            .FirstOrDefault(m => m.Key == "RepositoryUrl")?
            .Value
                ?? "https://github.com/DevilDogTG/KubeTools4Dev";

        _copyright = assembly
            .GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)
            .Cast<AssemblyCopyrightAttribute>()
            .FirstOrDefault()?.Copyright
                ?? "Copyright © 2025 DevilDogTG";
    }

    /// <summary>
    /// Opens the URL.
    /// </summary>
    [RelayCommand]
    private void OpenUrl()
        => Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
}
