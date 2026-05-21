using KubeTools4Dev.Core.ViewModels;
using System.Collections.Generic;

namespace KubeTools4Dev.Core.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="SidebarViewModel"/> sidebar panel expand/collapse state.
/// Navigation selection is now handled by <see cref="ClusterTreeViewModel"/>.
/// </summary>
public class SidebarViewModelTests
{
    private readonly SidebarViewModel _sut = new();

    [Fact]
    public void InitialState_IsExpanded_WithWidth220()
    {
        Assert.True(_sut.IsSidebarExpanded);
        Assert.Equal(220.0, _sut.SidebarWidth);
    }

    [Fact]
    public void ToggleSidebarCommand_Collapses_WhenExpanded()
    {
        _sut.ToggleSidebarCommand.Execute(null);

        Assert.False(_sut.IsSidebarExpanded);
        Assert.Equal(0.0, _sut.SidebarWidth);
    }

    [Fact]
    public void ToggleSidebarCommand_Expands_WhenCollapsed()
    {
        _sut.IsSidebarExpanded = false;

        _sut.ToggleSidebarCommand.Execute(null);

        Assert.True(_sut.IsSidebarExpanded);
        Assert.Equal(220.0, _sut.SidebarWidth);
    }

    [Fact]
    public void ToggleSidebarCommand_RaisesPropertyChanged_ForSidebarWidth()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.ToggleSidebarCommand.Execute(null);

        Assert.Contains(nameof(SidebarViewModel.IsSidebarExpanded), raised);
        Assert.Contains(nameof(SidebarViewModel.SidebarWidth), raised);
    }
}
