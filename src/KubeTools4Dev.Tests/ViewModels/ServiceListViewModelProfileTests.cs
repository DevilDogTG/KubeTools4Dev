using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Core.ViewModels;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using Xunit;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for port-forward profile CRUD and persistence logic in <see cref="ServiceListViewModel"/>.
/// </summary>
public class ServiceListViewModelProfileTests
{
    private static readonly Guid ClusterId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly IClusterConnectionManager _manager = Substitute.For<IClusterConnectionManager>();
    private readonly ILogger<ServiceListViewModel> _logger = Substitute.For<ILogger<ServiceListViewModel>>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();

    private ClusterEntry _clusterEntry = new() { Id = ClusterId, DisplayName = "test-cluster" };

    private ServiceListViewModel MakeVm()
    {
        _settings.Services.Returns(new ServicesSettings());
        _settings.Clusters.Returns(new ClustersSettings { Clusters = [_clusterEntry] });
        _settings.Namespaces.Returns(new NamespacesSettings());
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        return new ServiceListViewModel(_settings, _manager, _logger, _loggerFactory);
    }

    /// <summary>
    /// Creates a VM and seeds it with the cluster ID so that SaveProfiles works.
    /// </summary>
    private async Task<ServiceListViewModel> MakeVmWithClusterAsync()
    {
        var vm = MakeVm();
        var pfService = Substitute.For<IPortForwardService>();
        var kubeService = Substitute.For<IKubernetesService>();
        kubeService.IsConnected.Returns(false);
        await vm.UpdateScopeAsync(kubeService, pfService, "default", ClusterId.ToString());
        _settings.ClearReceivedCalls(); // clear calls from UpdateScopeAsync setup
        return vm;
    }

    // ── ShowCreateProfileInput / Cancel ───────────────────────────────────────

    [Fact]
    public void ShowCreateProfileInputCommand_SetsIsProfileNameInputVisible()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        #endregion

        #region PHASE 2: Act
        vm.ShowCreateProfileInputCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.True(vm.IsProfileNameInputVisible);
        Assert.Equal(string.Empty, vm.NewProfileName);
        #endregion
    }

    [Fact]
    public void CancelCreateProfileCommand_HidesInput()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.ShowCreateProfileInputCommand.Execute(null);
        vm.NewProfileName = "partial entry";
        #endregion

        #region PHASE 2: Act
        vm.CancelCreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.False(vm.IsProfileNameInputVisible);
        Assert.Equal(string.Empty, vm.NewProfileName);
        #endregion
    }

    // ── CreateProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void CreateProfileCommand_CannotExecute_WhenNameIsEmpty()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.NewProfileName = string.Empty;
        #endregion

        #region PHASE 2: Act / Assert
        Assert.False(vm.CreateProfileCommand.CanExecute(null));
        #endregion
    }

    [Fact]
    public void CreateProfileCommand_CanExecute_WhenNameIsNonEmpty()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.NewProfileName = "Dev";
        #endregion

        #region PHASE 2: Act / Assert
        Assert.True(vm.CreateProfileCommand.CanExecute(null));
        #endregion
    }

    [Fact]
    public void CreateProfileCommand_AddsProfileToCollection()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.NewProfileName = "My Profile";
        #endregion

        #region PHASE 2: Act
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.Single(vm.Profiles);
        Assert.Equal("My Profile", vm.Profiles[0].Name);
        #endregion
    }

    [Fact]
    public void CreateProfileCommand_SelectsNewProfile()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.NewProfileName = "Dev Stack";
        #endregion

        #region PHASE 2: Act
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.NotNull(vm.SelectedProfile);
        Assert.Equal("Dev Stack", vm.SelectedProfile!.Name);
        #endregion
    }

    [Fact]
    public async Task CreateProfileCommand_CallsSettingsSave()
    {
        #region PHASE 1: Arrange
        var vm = await MakeVmWithClusterAsync();
        vm.NewProfileName = "Prod";
        #endregion

        #region PHASE 2: Act
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        _settings.Received(1).Save();
        #endregion
    }

    [Fact]
    public void CreateProfileCommand_HidesInputAndClearsName()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.ShowCreateProfileInputCommand.Execute(null);
        vm.NewProfileName = "Test";
        #endregion

        #region PHASE 2: Act
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.False(vm.IsProfileNameInputVisible);
        Assert.Equal(string.Empty, vm.NewProfileName);
        #endregion
    }

    [Fact]
    public async Task CreateProfileCommand_PersistsProfileToClusterEntry()
    {
        #region PHASE 1: Arrange
        var vm = await MakeVmWithClusterAsync();
        vm.NewProfileName = "Staging";
        #endregion

        #region PHASE 2: Act
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert — profile model must be in the ClusterEntry list
        Assert.Single(_clusterEntry.PortForwardProfiles);
        Assert.Equal("Staging", _clusterEntry.PortForwardProfiles[0].Name);
        #endregion
    }

    // ── DeleteProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void DeleteProfileCommand_CannotExecute_WhenNoProfileSelected()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        #endregion

        #region PHASE 2: Act / Assert
        Assert.Null(vm.SelectedProfile);
        Assert.False(vm.DeleteProfileCommand.CanExecute(null));
        #endregion
    }

    [Fact]
    public async Task DeleteProfileCommand_RemovesSelectedProfile()
    {
        #region PHASE 1: Arrange
        var vm = await MakeVmWithClusterAsync();
        vm.NewProfileName = "ToDelete";
        vm.CreateProfileCommand.Execute(null);
        _settings.ClearReceivedCalls();
        #endregion

        #region PHASE 2: Act
        vm.DeleteProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.Empty(vm.Profiles);
        Assert.Null(vm.SelectedProfile);
        _settings.Received(1).Save();
        #endregion
    }

    [Fact]
    public async Task DeleteProfileCommand_RemovesProfileFromClusterEntry()
    {
        #region PHASE 1: Arrange
        var vm = await MakeVmWithClusterAsync();
        vm.NewProfileName = "Bye";
        vm.CreateProfileCommand.Execute(null);
        #endregion

        #region PHASE 2: Act
        vm.DeleteProfileCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.Empty(_clusterEntry.PortForwardProfiles);
        #endregion
    }

    // ── LoadProfiles ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadProfiles_PopulatesProfiles_FromExistingClusterEntry()
    {
        #region PHASE 1: Arrange — cluster entry has pre-saved profiles
        _clusterEntry.PortForwardProfiles =
        [
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        ];
        var vm = MakeVm();
        var pfService = Substitute.For<IPortForwardService>();
        var kubeService = Substitute.For<IKubernetesService>();
        kubeService.IsConnected.Returns(false); // returns early, no actual K8s calls
        #endregion

        #region PHASE 2: Act
        await vm.UpdateScopeAsync(kubeService, pfService, "default", ClusterId.ToString());
        #endregion

        #region PHASE 3: Assert
        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Alpha", vm.Profiles[0].Name);
        Assert.Equal("Beta",  vm.Profiles[1].Name);
        #endregion
    }

    [Fact]
    public async Task LoadProfiles_SelectsFirstProfile_WhenProfilesExist()
    {
        #region PHASE 1: Arrange
        _clusterEntry.PortForwardProfiles = [new() { Name = "First" }, new() { Name = "Second" }];
        var vm = MakeVm();
        var pfService = Substitute.For<IPortForwardService>();
        var kubeService = Substitute.For<IKubernetesService>();
        kubeService.IsConnected.Returns(false);
        #endregion

        #region PHASE 2: Act
        await vm.UpdateScopeAsync(kubeService, pfService, "default", ClusterId.ToString());
        #endregion

        #region PHASE 3: Assert
        Assert.NotNull(vm.SelectedProfile);
        Assert.Equal("First", vm.SelectedProfile!.Name);
        #endregion
    }

    // ── SelectedProfile change ────────────────────────────────────────────────

    [Fact]
    public void SelectedProfile_Change_ResetsIsProfileRunning()
    {
        #region PHASE 1: Arrange
        var vm = MakeVm();
        vm.NewProfileName = "P1"; vm.CreateProfileCommand.Execute(null);
        vm.NewProfileName = "P2"; vm.CreateProfileCommand.Execute(null);
        var p1 = vm.Profiles[0];
        var p2 = vm.Profiles[1];
        vm.SelectedProfile = p1;

        // Manually set IsProfileRunning (simulates it was started)
        typeof(ServiceListViewModel)
            .GetProperty(nameof(ServiceListViewModel.IsProfileRunning))!
            .SetValue(vm, true);
        #endregion

        #region PHASE 2: Act
        vm.SelectedProfile = p2;
        #endregion

        #region PHASE 3: Assert
        Assert.False(vm.IsProfileRunning);
        #endregion
    }

    // ── Toggle profile CanExecute ────────────────────────────────────────────

    [Fact]
    public void ToggleProfileCommand_CannotExecute_WhenNoProfileSelected()
    {
        var vm = MakeVm();
        Assert.False(vm.ToggleProfileCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleProfileCommand_CannotExecute_WhenProfileHasNoEntries()
    {
        var vm = MakeVm();
        vm.NewProfileName = "Empty"; vm.CreateProfileCommand.Execute(null);

        Assert.False(vm.ToggleProfileCommand.CanExecute(null));
    }
}
