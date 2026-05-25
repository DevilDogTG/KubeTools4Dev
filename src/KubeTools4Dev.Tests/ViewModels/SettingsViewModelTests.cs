using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.ViewModels;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace KubeTools4Dev.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="SettingsViewModel"/> stepper command bounds and step increments.
/// </summary>
public class SettingsViewModelTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly ILogger<SettingsViewModel> _logger = Substitute.For<ILogger<SettingsViewModel>>();

    private SettingsViewModel MakeVm(int refreshIntervalSeconds = 10, int watchRetryDelayMs = 5000)
    {
        _settingsService.General.Returns(new GeneralSettings());
        _settingsService.Pods.Returns(new PodsSettings
        {
            RefreshIntervalSeconds = refreshIntervalSeconds,
            WatchRetryDelayMilliseconds = watchRetryDelayMs
        });
        _settingsService.Services.Returns(new ServicesSettings());
        _settingsService.GetDefaultLogPath().Returns(string.Empty);
        return new SettingsViewModel(_settingsService, _logger);
    }

    // ── RefreshIntervalSeconds ────────────────────────────────────────────────

    /// <summary>
    /// Decrement with value above minimum should decrease by one.
    /// </summary>
    [Fact]
    public void DecrementRefreshIntervalSecondsCommand_DecrementsValue_WhenAboveMinimum()
    {
        var vm = MakeVm(refreshIntervalSeconds: 10);

        vm.DecrementRefreshIntervalSecondsCommand.Execute(null);

        Assert.Equal(9, vm.RefreshIntervalSeconds);
    }

    /// <summary>
    /// Decrement at floor (1) must not go below 1.
    /// </summary>
    [Fact]
    public void DecrementRefreshIntervalSecondsCommand_ClampsAt1_WhenAlreadyAtMinimum()
    {
        var vm = MakeVm(refreshIntervalSeconds: 1);

        vm.DecrementRefreshIntervalSecondsCommand.Execute(null);

        Assert.Equal(1, vm.RefreshIntervalSeconds);
    }

    /// <summary>
    /// Increment with value below maximum should increase by one.
    /// </summary>
    [Fact]
    public void IncrementRefreshIntervalSecondsCommand_IncrementsValue_WhenBelowMaximum()
    {
        var vm = MakeVm(refreshIntervalSeconds: 10);

        vm.IncrementRefreshIntervalSecondsCommand.Execute(null);

        Assert.Equal(11, vm.RefreshIntervalSeconds);
    }

    /// <summary>
    /// Increment at ceiling (60) must not exceed 60.
    /// </summary>
    [Fact]
    public void IncrementRefreshIntervalSecondsCommand_ClampsAt60_WhenAlreadyAtMaximum()
    {
        var vm = MakeVm(refreshIntervalSeconds: 60);

        vm.IncrementRefreshIntervalSecondsCommand.Execute(null);

        Assert.Equal(60, vm.RefreshIntervalSeconds);
    }

    // ── WatchRetryDelay ───────────────────────────────────────────────────────

    /// <summary>
    /// Decrement with value above minimum should decrease by 500 ms.
    /// </summary>
    [Fact]
    public void DecrementWatchRetryDelayCommand_DecrementsByStep_WhenAboveMinimum()
    {
        var vm = MakeVm(watchRetryDelayMs: 5000);

        vm.DecrementWatchRetryDelayCommand.Execute(null);

        Assert.Equal(4500, vm.WatchRetryDelayMilliseconds);
    }

    /// <summary>
    /// Decrement at floor (1000 ms) must stay at 1000.
    /// </summary>
    [Fact]
    public void DecrementWatchRetryDelayCommand_ClampsAt1000_WhenAlreadyAtMinimum()
    {
        var vm = MakeVm(watchRetryDelayMs: 1000);

        vm.DecrementWatchRetryDelayCommand.Execute(null);

        Assert.Equal(1000, vm.WatchRetryDelayMilliseconds);
    }

    /// <summary>
    /// Increment with value below maximum should increase by 500 ms.
    /// </summary>
    [Fact]
    public void IncrementWatchRetryDelayCommand_IncrementsByStep_WhenBelowMaximum()
    {
        var vm = MakeVm(watchRetryDelayMs: 5000);

        vm.IncrementWatchRetryDelayCommand.Execute(null);

        Assert.Equal(5500, vm.WatchRetryDelayMilliseconds);
    }

    /// <summary>
    /// Increment at ceiling (60 000 ms) must not exceed 60 000.
    /// </summary>
    [Fact]
    public void IncrementWatchRetryDelayCommand_ClampsAt60000_WhenAlreadyAtMaximum()
    {
        var vm = MakeVm(watchRetryDelayMs: 60000);

        vm.IncrementWatchRetryDelayCommand.Execute(null);

        Assert.Equal(60000, vm.WatchRetryDelayMilliseconds);
    }
}
