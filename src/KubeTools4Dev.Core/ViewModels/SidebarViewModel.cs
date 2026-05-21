using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KubeTools4Dev.Core.ViewModels;

/// <summary>
/// Encapsulates sidebar panel state: expanded/collapsed toggle and computed width.
/// Navigation selection is now handled by <see cref="ClusterTreeViewModel"/>.
/// Contains no Avalonia dependencies so it can be tested without platform initialization.
/// </summary>
public partial class SidebarViewModel : ObservableObject
{
    /// <summary>Gets or sets a value indicating whether the sidebar panel is in expanded (full-width) state.</summary>
    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    /// <summary>Gets the sidebar panel width in pixels (220 expanded, 0 collapsed).</summary>
    public double SidebarWidth => IsSidebarExpanded ? 220.0 : 0.0;

    partial void OnIsSidebarExpandedChanged(bool value) => OnPropertyChanged(nameof(SidebarWidth));

    /// <summary>Toggles the sidebar between expanded and collapsed states.</summary>
    [RelayCommand]
    private void ToggleSidebar() => IsSidebarExpanded = !IsSidebarExpanded;
}
