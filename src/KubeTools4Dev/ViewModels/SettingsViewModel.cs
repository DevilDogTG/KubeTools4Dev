using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// Setting view model.
/// </summary>
/// <seealso cref="KubeTools4Dev.ViewModels.ViewModelBase" />
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
    /// The excluded services
    /// </summary>
    public ObservableCollection<ExclusionItem> ExcludedServices { get; } = [];

    /// <summary>
    /// The new excluded service
    /// </summary>
    [ObservableProperty]
    private string _newExcludedService = string.Empty;

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
    private string _logPath = string.Empty;

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
    /// Browses the log path.
    /// </summary>
    [RelayCommand]
    private static async Task BrowseLogPath()
    {
        // Since we removed window, we need another way to get StorageProvider or pass it in.
        // For Tab based, we usually can't easily get window without TopLevel.
        // Let's rely on TopLevel.GetTopLevel() if we had a view reference, but VM shouldn't know View.
        // Alternative: Inject a service or use a weak reference to a visual root?
        // Or just simplify: Text Box input only for now to avoid complexity of finding TopLevel in VM without View.
        // Alternatively, pass it in command parameter?
        // Let's disable Browse for now or assume manual entry, OR try to resolve TopLevel later.
        // Wait, BrowseLogPath used _window.StorageProvider.
        // Let's leave BrowseLogPath empty or comment out for this iteration as "Text Input only" 
        // until we implement a proper DialogService or pass Control to command.

        // _logger.Warning("BrowseLogPath not implemented for Tab view yet.");
    }

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
    /// Adds the excluded service.
    /// </summary>
    [RelayCommand]
    private void AddExcludedService()
    {
        if (!string.IsNullOrWhiteSpace(NewExcludedService))
        {
            // Avoid duplicates
            var trimmed = NewExcludedService.Trim();
            bool exists = false;
            foreach (var item in ExcludedServices)
            {
                if (item.Value.Equals(trimmed, System.StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

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
    /// Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        // General
        LogLevel = _settingsService.General.LogLevel;
        LogPath = _settingsService.General.LogPath ?? "";

        // Pods
        RefreshIntervalSeconds = _settingsService.Pods.RefreshIntervalSeconds;
        WatchRetryDelayMilliseconds = _settingsService.Pods.WatchRetryDelayMilliseconds;

        // Services
        var sourceList = _settingsService.Services.ExcludedServices ?? [];

        // Remove items not in source
        for (int i = ExcludedServices.Count - 1; i >= 0; i--)
        {
            if (!sourceList.Contains(ExcludedServices[i].Value))
            {
                ExcludedServices.RemoveAt(i);
            }
        }

        // Add items not in current list
        foreach (var s in sourceList)
        {
            if (!ExcludedServices.Any(x => x.Value == s))
            {
                ExcludedServices.Add(new ExclusionItem(s));
            }
        }

        HiddenServiceNamesText = string.Join(", ", _settingsService.Services.HiddenServiceNames);
        HiddenServiceTypesText = string.Join(", ", _settingsService.Services.HiddenServiceTypes);
    }

    /// <summary>
    /// Called when [settings changed].
    /// </summary>
    private void OnSettingsChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Invoke(LoadSettings);
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

/// <summary>
/// Exclusion item wrapper.
/// </summary>
public partial class ExclusionItem(string value) : ObservableObject
{
    [ObservableProperty]
    private string _value = value;
}
