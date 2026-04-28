using KubeTools4Dev.Core.Models;

namespace KubeTools4Dev.Core.Tests.Models;

/// <summary>
/// Tests for <see cref="GeneralSettings"/>, <see cref="PodsSettings"/>, and <see cref="ServicesSettings"/> defaults.
/// </summary>
public class SettingsModelTests
{
    [Fact]
    public void GeneralSettings_Defaults_LogLevel_IsInformation()
    {
        var settings = new GeneralSettings();
        Assert.Equal("Information", settings.LogLevel);
    }

    [Fact]
    public void GeneralSettings_Defaults_LogPath_IsNull()
    {
        var settings = new GeneralSettings();
        Assert.Null(settings.LogPath);
    }

    [Fact]
    public void PodsSettings_Defaults_RefreshInterval_IsFiveSeconds()
    {
        var settings = new PodsSettings();
        Assert.Equal(5, settings.RefreshIntervalSeconds);
    }

    [Fact]
    public void PodsSettings_Defaults_WatchRetryDelay_IsThreeSeconds()
    {
        var settings = new PodsSettings();
        Assert.Equal(3000, settings.WatchRetryDelayMilliseconds);
    }

    [Fact]
    public void ServicesSettings_Defaults_ExcludedServices_IsEmpty()
    {
        var settings = new ServicesSettings();
        Assert.Empty(settings.ExcludedServices);
    }

    [Fact]
    public void ServicesSettings_Defaults_HiddenServiceNames_ContainsKubernetes()
    {
        var settings = new ServicesSettings();
        Assert.Contains("kubernetes", settings.HiddenServiceNames);
    }

    [Fact]
    public void ServicesSettings_Defaults_HiddenServiceTypes_ContainsExternalName()
    {
        var settings = new ServicesSettings();
        Assert.Contains("ExternalName", settings.HiddenServiceTypes);
    }

    [Fact]
    public void ServicesSettings_Defaults_HiddenServiceNames_HasExactlyOneEntry()
    {
        var settings = new ServicesSettings();
        Assert.Single(settings.HiddenServiceNames);
    }

    [Fact]
    public void ServicesSettings_Defaults_HiddenServiceTypes_HasExactlyOneEntry()
    {
        var settings = new ServicesSettings();
        Assert.Single(settings.HiddenServiceTypes);
    }
}
