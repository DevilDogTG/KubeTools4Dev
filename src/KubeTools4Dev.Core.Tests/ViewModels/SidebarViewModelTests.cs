using KubeTools4Dev.Core.ViewModels;
using System.Collections.Generic;

namespace KubeTools4Dev.Core.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="SidebarViewModel"/> sidebar navigation state.
/// </summary>
public class SidebarViewModelTests
{
    private readonly SidebarViewModel _sut = new();

    [Fact]
    public void InitialState_IsExpanded_WithWidth180()
    {
        Assert.True(_sut.IsSidebarExpanded);
        Assert.Equal(180.0, _sut.SidebarWidth);
    }

    [Fact]
    public void ToggleSidebarCommand_Collapses_WhenExpanded()
    {
        _sut.ToggleSidebarCommand.Execute(null);

        Assert.False(_sut.IsSidebarExpanded);
        Assert.Equal(52.0, _sut.SidebarWidth);
    }

    [Fact]
    public void ToggleSidebarCommand_Expands_WhenCollapsed()
    {
        _sut.IsSidebarExpanded = false;

        _sut.ToggleSidebarCommand.Execute(null);

        Assert.True(_sut.IsSidebarExpanded);
        Assert.Equal(180.0, _sut.SidebarWidth);
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

    [Fact]
    public void InitialState_SelectedNavIndex_IsZero_PodsVisible()
    {
        Assert.Equal(0, _sut.SelectedNavIndex);
        Assert.True(_sut.IsPodsVisible);
        Assert.False(_sut.IsServicesVisible);
        Assert.False(_sut.IsSettingsVisible);
    }

    [Theory]
    [InlineData(0, true, false, false)]
    [InlineData(1, false, true, false)]
    [InlineData(2, false, false, true)]
    public void SelectedNavIndex_UpdatesVisibilityFlags(int index, bool pods, bool services, bool settings)
    {
        _sut.SelectedNavIndex = index;

        Assert.Equal(pods, _sut.IsPodsVisible);
        Assert.Equal(services, _sut.IsServicesVisible);
        Assert.Equal(settings, _sut.IsSettingsVisible);
    }

    [Fact]
    public void SelectedNavIndex_Change_RaisesPropertyChanged_ForAllVisibilityFlags()
    {
        var raised = new List<string?>();
        _sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _sut.SelectedNavIndex = 1;

        Assert.Contains(nameof(SidebarViewModel.SelectedNavIndex), raised);
        Assert.Contains(nameof(SidebarViewModel.IsPodsVisible), raised);
        Assert.Contains(nameof(SidebarViewModel.IsServicesVisible), raised);
        Assert.Contains(nameof(SidebarViewModel.IsSettingsVisible), raised);
    }
}
