using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(ISettingsService settingsService, ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        LoadSettings();
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Invoke(LoadSettings);
    }

    [ObservableProperty]
    private string _logLevel;

    [ObservableProperty]
    private string _logPath;

    [ObservableProperty]
    private int _refreshIntervalSeconds;

    [ObservableProperty]
    private int _watchRetryDelayMilliseconds;

    [ObservableProperty]
    private string _excludedServicesText; 
    // Simplified for UI: comma or newline separated string, parsed back to list on save

    [ObservableProperty]
    private string _hiddenServiceNamesText;

    [ObservableProperty]
    private string _hiddenServiceTypesText;

    public ObservableCollection<string> LogLevels { get; } = new() { "Debug", "Information", "Warning", "Error", "Fatal" };

    private void LoadSettings()
    {
        // General
        LogLevel = _settingsService.General.LogLevel;
        LogPath = _settingsService.General.LogPath ?? "";

        // Pods
        RefreshIntervalSeconds = _settingsService.Pods.RefreshIntervalSeconds;
        WatchRetryDelayMilliseconds = _settingsService.Pods.WatchRetryDelayMilliseconds;

        // Services
        ExcludedServicesText = string.Join(", ", _settingsService.Services.ExcludedServices);
        HiddenServiceNamesText = string.Join(", ", _settingsService.Services.HiddenServiceNames);
        HiddenServiceTypesText = string.Join(", ", _settingsService.Services.HiddenServiceTypes);
    }

    [RelayCommand]
    private async Task BrowseLogPath()
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

    [RelayCommand]
    private void Save()
    {
        // General
        _settingsService.General.LogLevel = LogLevel;
        _settingsService.General.LogPath = string.IsNullOrWhiteSpace(LogPath) ? null : LogPath;

        // Pods
        _settingsService.Pods.RefreshIntervalSeconds = RefreshIntervalSeconds;
        _settingsService.Pods.WatchRetryDelayMilliseconds = WatchRetryDelayMilliseconds;

        // Services
        _settingsService.Services.ExcludedServices = ParseList(ExcludedServicesText);
        _settingsService.Services.HiddenServiceNames = ParseList(HiddenServiceNamesText);
        _settingsService.Services.HiddenServiceTypes = ParseList(HiddenServiceTypesText);

        _settingsService.Save();
        // Feedback?
    }

    private List<string> ParseList(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();
        var items = input.Split(new[] { ',', '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
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
}
