using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;

namespace KubeTools4Dev.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="SettingsService"/> persistence and load behaviour.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ILogger<SettingsService> _logger;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"KubeTools4Dev_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
        _logger = Substitute.For<ILogger<SettingsService>>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string SettingsFilePath => Path.Combine(_tempDir, "settings.json");

    private SettingsService CreateService() =>
        new(_logger, SettingsFilePath);

    [Fact]
    public void Constructor_NoFile_UsesDefaultSettings()
    {
        var svc = CreateService();

        Assert.Equal("Information", svc.General.LogLevel);
        Assert.Equal(5, svc.Pods.RefreshIntervalSeconds);
        Assert.Empty(svc.Services.ExcludedServices);
    }

    [Fact]
    public void Save_CreatesFile_AtConfiguredPath()
    {
        var svc = CreateService();
        svc.Save();

        Assert.True(File.Exists(SettingsFilePath));
    }

    [Fact]
    public void Save_RaisesSettingsChanged_Event()
    {
        var svc = CreateService();
        var raised = false;
        svc.SettingsChanged += () => raised = true;

        svc.Save();

        Assert.True(raised);
    }

    [Fact]
    public void Save_ThenLoad_RoundtripsModifiedSettings()
    {
        var svc = CreateService();
        svc.General.LogLevel = "Debug";
        svc.Pods.RefreshIntervalSeconds = 10;
        svc.Services.ExcludedServices.Add("my-ns/my-svc:8080");
        svc.Save();

        var svc2 = CreateService();

        Assert.Equal("Debug", svc2.General.LogLevel);
        Assert.Equal(10, svc2.Pods.RefreshIntervalSeconds);
        Assert.Contains("my-ns/my-svc:8080", svc2.Services.ExcludedServices);
    }

    [Fact]
    public void Constructor_CorruptedFile_FallsBackToDefaults()
    {
        File.WriteAllText(SettingsFilePath, "{ this is not valid JSON }}}");

        var svc = CreateService();

        Assert.NotNull(svc.General);
        Assert.NotNull(svc.Pods);
        Assert.NotNull(svc.Services);
        Assert.Equal("Information", svc.General.LogLevel);
    }

    [Fact]
    public void Constructor_EmptyJsonFile_FallsBackToDefaults()
    {
        File.WriteAllText(SettingsFilePath, "null");

        var svc = CreateService();

        Assert.NotNull(svc.General);
        Assert.Equal("Information", svc.General.LogLevel);
    }

    [Fact]
    public void Constructor_PartialJsonFile_FillsMissingWithDefaults()
    {
        // File has only pods settings; general and services should default
        var partial = new { Pods = new { RefreshIntervalSeconds = 15 } };
        File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(partial));

        var svc = CreateService();

        Assert.Equal(15, svc.Pods.RefreshIntervalSeconds);
        Assert.NotNull(svc.General);
        Assert.NotNull(svc.Services);
    }

    [Fact]
    public void Save_MultipleInvocations_RaisesEventEachTime()
    {
        var svc = CreateService();
        var count = 0;
        svc.SettingsChanged += () => count++;

        svc.Save();
        svc.Save();
        svc.Save();

        Assert.Equal(3, count);
    }

    [Fact]
    public void GetDefaultLogPath_ReturnsEmptyString_WhenNoAppsettings()
    {
        var svc = CreateService();
        Assert.Equal(string.Empty, svc.GetDefaultLogPath());
    }
}
