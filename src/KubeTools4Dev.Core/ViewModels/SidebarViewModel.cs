using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Encapsulates sidebar navigation state: expanded/collapsed toggle and active view selection.
/// Contains no Avalonia dependencies so it can be tested without platform initialization.
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    /// <summary>Gets or sets a value indicating whether the sidebar is in expanded (icon + text) state.</summary>
    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    /// <summary>Gets the sidebar width in pixels (180 expanded, 52 collapsed).</summary>
    public double SidebarWidth => IsSidebarExpanded ? 180.0 : 52.0;

    partial void OnIsSidebarExpandedChanged(bool value) => OnPropertyChanged(nameof(SidebarWidth));

    /// <summary>Toggles the sidebar between expanded and collapsed states.</summary>
    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;

    /// <summary>Gets or sets the selected navigation index (0 = Pods, 1 = Services, 2 = Deployments, 3 = Settings → was 2).</summary>
    [ObservableProperty]
    private int _selectedNavIndex;

    /// <summary>Gets a value indicating whether the Pods view is visible.</summary>
    public bool IsPodsVisible => SelectedNavIndex == 0;

    /// <summary>Gets a value indicating whether the Services view is visible.</summary>
    public bool IsServicesVisible => SelectedNavIndex == 1;

    /// <summary>Gets a value indicating whether the Deployments view is visible.</summary>
    public bool IsDeploymentsVisible => SelectedNavIndex == 2;

    /// <summary>Gets a value indicating whether the Settings view is visible.</summary>
    public bool IsSettingsVisible => SelectedNavIndex == 3;

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPodsVisible));
        OnPropertyChanged(nameof(IsServicesVisible));
        OnPropertyChanged(nameof(IsDeploymentsVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
    }
}
