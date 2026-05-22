using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Groups cluster nodes that come from the same kubeconfig file.
/// Displayed as e.g. "Local Kubeconfigs" in the sidebar tree header.
/// </summary>
public partial class KubeConfigSourceNodeViewModel : ObservableObject
{
    /// <summary>Initializes a new source node with the given display name and file path.</summary>
    public KubeConfigSourceNodeViewModel(string displayName, string filePath)
    {
        DisplayName = displayName;
        FilePath = filePath;
    }

    /// <summary>Gets the display name for this kubeconfig source group.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the absolute path to the kubeconfig file.</summary>
    public string FilePath { get; }

    /// <summary>Gets or sets whether the cluster list under this source is expanded.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>Gets the cluster nodes derived from this kubeconfig source.</summary>
    public ObservableCollection<ClusterNodeViewModel> Clusters { get; } = [];

    /// <summary>Toggles the <see cref="IsExpanded"/> state.</summary>
    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
