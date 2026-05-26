using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.ViewModels;
using System.Text.Json;
using Xunit;

namespace KubeTools4Dev.Core.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PortForwardProfileViewModel"/> and <see cref="PortForwardProfileEntryViewModel"/>.
/// </summary>
public class PortForwardProfileViewModelTests
{
    #region PortForwardProfileViewModel

    [Fact]
    public void Name_Get_ReturnsModelName()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile { Name = "Dev Stack" };
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 2: Act / Assert
        Assert.Equal("Dev Stack", sut.Name);
        #endregion
    }

    [Fact]
    public void Name_Set_UpdatesModel()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile { Name = "Old Name" };
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 2: Act
        sut.Name = "New Name";
        #endregion

        #region PHASE 3: Assert
        Assert.Equal("New Name", profile.Name);
        #endregion
    }

    [Fact]
    public void Name_Set_RaisesPropertyChanged()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile { Name = "Old" };
        var sut = new PortForwardProfileViewModel(profile);
        string? changedProp = null;
        sut.PropertyChanged += (_, e) => changedProp = e.PropertyName;
        #endregion

        #region PHASE 2: Act
        sut.Name = "New";
        #endregion

        #region PHASE 3: Assert
        Assert.Equal(nameof(PortForwardProfileViewModel.Name), changedProp);
        #endregion
    }

    [Fact]
    public void Constructor_PopulatesEntries_FromModel()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile
        {
            Entries =
            [
                new() { Namespace = "default", ServiceName = "api", TargetPort = "8080", LocalPort = 8080 },
                new() { Namespace = "default", ServiceName = "db",  TargetPort = "5432", LocalPort = 5432 }
            ]
        };
        #endregion

        #region PHASE 2: Act
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 3: Assert
        Assert.Equal(2, sut.Entries.Count);
        Assert.Equal("api", sut.Entries[0].ServiceName);
        Assert.Equal("db",  sut.Entries[1].ServiceName);
        #endregion
    }

    [Fact]
    public void AddEntry_AddsToEntriesAndModel()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile();
        var sut = new PortForwardProfileViewModel(profile);
        var entry = new PortForwardProfileEntry
        {
            Namespace = "default", ServiceName = "api", TargetPort = "3000", LocalPort = 3000
        };
        #endregion

        #region PHASE 2: Act
        sut.AddEntry(entry);
        #endregion

        #region PHASE 3: Assert
        Assert.Single(sut.Entries);
        Assert.Single(profile.Entries);
        Assert.Equal("api", sut.Entries[0].ServiceName);
        #endregion
    }

    [Fact]
    public void RemoveEntry_RemovesFromEntriesAndModel()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile
        {
            Entries = [new() { Namespace = "default", ServiceName = "api", TargetPort = "3000", LocalPort = 3000 }]
        };
        var sut = new PortForwardProfileViewModel(profile);
        var entryVm = sut.Entries[0];
        #endregion

        #region PHASE 2: Act
        sut.RemoveEntry(entryVm);
        #endregion

        #region PHASE 3: Assert
        Assert.Empty(sut.Entries);
        Assert.Empty(profile.Entries);
        #endregion
    }

    [Fact]
    public void Contains_ReturnsTrue_WhenEntryExists()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile
        {
            Entries = [new() { Namespace = "ns1", ServiceName = "svc1", TargetPort = "80", LocalPort = 8080 }]
        };
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 2: Act / Assert
        Assert.True(sut.Contains("ns1", "svc1", "80"));
        #endregion
    }

    [Fact]
    public void Contains_ReturnsFalse_WhenEntryAbsent()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile
        {
            Entries = [new() { Namespace = "ns1", ServiceName = "svc1", TargetPort = "80", LocalPort = 8080 }]
        };
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 2: Act / Assert
        Assert.False(sut.Contains("ns1", "svc2", "80"));
        Assert.False(sut.Contains("ns2", "svc1", "80"));
        Assert.False(sut.Contains("ns1", "svc1", "443"));
        #endregion
    }

    [Fact]
    public void Model_ReturnsUnderlyingProfile()
    {
        #region PHASE 1: Arrange
        var profile = new PortForwardProfile { Name = "My Profile" };
        var sut = new PortForwardProfileViewModel(profile);
        #endregion

        #region PHASE 2: Act / Assert
        Assert.Same(profile, sut.Model);
        #endregion
    }

    #endregion

    #region PortForwardProfileEntryViewModel

    [Fact]
    public void EntryViewModel_Properties_ReflectModel()
    {
        #region PHASE 1: Arrange
        var entry = new PortForwardProfileEntry
        {
            Namespace = "kube-system",
            ServiceName = "metrics-server",
            TargetPort = "443",
            LocalPort = 4430
        };
        var sut = new PortForwardProfileEntryViewModel(entry, _ => { });
        #endregion

        #region PHASE 2: Act / Assert
        Assert.Equal("kube-system",    sut.Namespace);
        Assert.Equal("metrics-server", sut.ServiceName);
        Assert.Equal("443",            sut.TargetPort);
        Assert.Equal(4430,             sut.LocalPort);
        #endregion
    }

    [Fact]
    public void EntryViewModel_LocalPort_Set_UpdatesModel()
    {
        #region PHASE 1: Arrange
        var entry = new PortForwardProfileEntry { LocalPort = 8080 };
        var sut = new PortForwardProfileEntryViewModel(entry, _ => { });
        #endregion

        #region PHASE 2: Act
        sut.LocalPort = 9090;
        #endregion

        #region PHASE 3: Assert
        Assert.Equal(9090, entry.LocalPort);
        #endregion
    }

    [Fact]
    public void RemoveCommand_InvokesCallback()
    {
        #region PHASE 1: Arrange
        PortForwardProfileEntryViewModel? removed = null;
        var entry = new PortForwardProfileEntry { ServiceName = "test" };
        var sut = new PortForwardProfileEntryViewModel(entry, vm => removed = vm);
        #endregion

        #region PHASE 2: Act
        sut.RemoveCommand.Execute(null);
        #endregion

        #region PHASE 3: Assert
        Assert.Same(sut, removed);
        #endregion
    }

    #endregion

    #region ClusterEntry + JSON serialization

    [Fact]
    public void ClusterEntry_PortForwardProfiles_DefaultsToEmptyList()
    {
        #region PHASE 1: Arrange / Act / Assert
        var entry = new ClusterEntry();
        Assert.NotNull(entry.PortForwardProfiles);
        Assert.Empty(entry.PortForwardProfiles);
        #endregion
    }

    [Fact]
    public void ClusterEntry_PortForwardProfiles_RoundTripsViaJson()
    {
        #region PHASE 1: Arrange
        var entry = new ClusterEntry
        {
            DisplayName = "local",
            PortForwardProfiles =
            [
                new()
                {
                    Name = "Dev",
                    Entries =
                    [
                        new() { Namespace = "default", ServiceName = "api", TargetPort = "8080", LocalPort = 8080 }
                    ]
                }
            ]
        };
        #endregion

        #region PHASE 2: Act
        var json = JsonSerializer.Serialize(entry);
        var restored = JsonSerializer.Deserialize<ClusterEntry>(json);
        #endregion

        #region PHASE 3: Assert
        Assert.NotNull(restored);
        Assert.Single(restored.PortForwardProfiles);
        Assert.Equal("Dev", restored.PortForwardProfiles[0].Name);
        Assert.Single(restored.PortForwardProfiles[0].Entries);
        Assert.Equal("api", restored.PortForwardProfiles[0].Entries[0].ServiceName);
        Assert.Equal(8080,  restored.PortForwardProfiles[0].Entries[0].LocalPort);
        #endregion
    }

    #endregion
}
