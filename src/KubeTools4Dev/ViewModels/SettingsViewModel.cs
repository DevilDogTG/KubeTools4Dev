using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using KubeTools4Dev.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Settings view model.
/// </summary>
/// <seealso cref="ViewModelBase" />
public partial class SettingsViewModel : ViewModelBase
{
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<SettingsViewModel> _logger;

    /// <summary>
    /// The settings service
    /// </summary>
    private readonly ISettingsService _settingsService;

    /// <summary>
    /// The hidden service names text
    /// </summary>
    [ObservableProperty]
    private string _hiddenServiceNamesText = string.Empty;

    // Simplified for UI: comma or newline separated string, parsed back to list on save
    /// <summary>
    /// The hidden service types text
    /// </summary>
    [ObservableProperty]
    private string _hiddenServiceTypesText = string.Empty;
    /// <summary>
    /// The log level
    /// </summary>
    [ObservableProperty]
    private string _logLevel = string.Empty;

    /// <summary>
    /// The log path
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentLogPath))]
    private string _logPath = string.Empty;

    /// <summary>
    /// The new excluded service
    /// </summary>
    [ObservableProperty]
    private string _newExcludedService = string.Empty;
    /// <summary>
    /// The refresh interval seconds
    /// </summary>
    [ObservableProperty]
    private int _refreshIntervalSeconds;

    /// <summary>
    /// The watch retry delay milliseconds
    /// </summary>
    [ObservableProperty]
    private int _watchRetryDelayMilliseconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">The logger.</param>
    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        LoadSettings();
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Gets the current effective log path.
    /// </summary>
    public string CurrentLogPath => !string.IsNullOrWhiteSpace(LogPath)
        ? LogPath
        : _settingsService.GetDefaultLogPath();

    /// <summary>
    /// The excluded services
    /// </summary>
    public ObservableCollection<ExclusionItem> ExcludedServices { get; } = [];
    /// <summary>
    /// Gets the log levels.
    /// </summary>
    /// <value>
    /// The log levels.
    /// </value>
    public ObservableCollection<string> LogLevels { get; } = [
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal"
    ];

    /// <summary>
    /// Opens the about dialog.
    /// </summary>
    [RelayCommand]
    private static void OpenAbout()
    {
        var aboutWindow = new Views.AboutWindow();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is not null)
        {
            aboutWindow.ShowDialog(desktop.MainWindow);
        }
        else
        {
            aboutWindow.Show();
        }
    }

    /// <summary>
    /// Parses the list.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <returns></returns>
    private static List<string> ParseList(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var items = input.Split([',', '\r', '\n'], System.StringSplitOptions.RemoveEmptyEntries);
        var list = new List<string>();
        foreach (var item in items)
        {
            var trimmed = item.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                list.Add(trimmed);
            }
        }
        return list;
    }

    /// <summary>
    /// Adds the excluded service.
    /// </summary>
    [RelayCommand]
    private void AddExcludedService()
    {
        if (!string.IsNullOrWhiteSpace(NewExcludedService))
        {
            // Avoid duplicates
            var trimmed = NewExcludedService.Trim();
            bool exists = ExcludedServices.Any(item =>
                item.Value.Equals(
                    trimmed,
                    StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                ExcludedServices.Add(new ExclusionItem(trimmed));
                NewExcludedService = string.Empty;
                SyncToSettingsService();
                _settingsService.Save();
            }
        }
    }

    /// <summary>
    /// Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        // General
        LogLevel = _settingsService.General.LogLevel;
        LogPath = _settingsService.General.LogPath ?? _settingsService.GetDefaultLogPath();

        // Pods
        RefreshIntervalSeconds = _settingsService.Pods.RefreshIntervalSeconds;
        WatchRetryDelayMilliseconds = _settingsService.Pods.WatchRetryDelayMilliseconds;

        // Services
        var sourceList = _settingsService.Services.ExcludedServices ?? [];

        // Remove items not in service
        for (int i = ExcludedServices.Count - 1; i >= 0; i--)
        {
            if (!sourceList.Contains(ExcludedServices[i].Value))
            {
                ExcludedServices.RemoveAt(i);
            }
        }

        // Add items not in current list
        foreach (var service in sourceList
            .Where(service =>
                !ExcludedServices.Any(x => x.Value == service)))
        {
            ExcludedServices.Add(new ExclusionItem(service));
        }

        HiddenServiceNamesText = string.Join(", ", _settingsService.Services.HiddenServiceNames);
        HiddenServiceTypesText = string.Join(", ", _settingsService.Services.HiddenServiceTypes);
    }

    /// <summary>
    /// Called when [settings changed].
    /// </summary>
    private void OnSettingsChanged() => Dispatcher.UIThread.Invoke(LoadSettings);

    /// <summary>
    /// Removes the excluded service.
    /// </summary>
    /// <param name="item">The item.</param>
    [RelayCommand]
    private void RemoveExcludedService(ExclusionItem item)
    {
        if (item != null)
        {
            ExcludedServices.Remove(item);
            SyncToSettingsService();
            _settingsService.Save();
        }
    }
    /// <summary>
    /// Saves this instance.
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        SyncToSettingsService();
        _settingsService.Save();
        // Feedback?
    }

    /// <summary>
    /// Synchronizes the view model state to the settings service.
    /// </summary>
    private void SyncToSettingsService()
    {
        // General
        _settingsService.General.LogLevel = LogLevel;
        _settingsService.General.LogPath = string.IsNullOrWhiteSpace(LogPath) ? null : LogPath;

        // Pods
        _settingsService.Pods.RefreshIntervalSeconds = RefreshIntervalSeconds;
        _settingsService.Pods.WatchRetryDelayMilliseconds = WatchRetryDelayMilliseconds;

        // Services
        var excludedList = new List<string>();
        foreach (var item in ExcludedServices)
        {
            excludedList.Add(item.Value);
        }
        _settingsService.Services.ExcludedServices = excludedList;

        _settingsService.Services.HiddenServiceNames = ParseList(HiddenServiceNamesText);
        _settingsService.Services.HiddenServiceTypes = ParseList(HiddenServiceTypesText);
    }
}

