using DMNSN.Core;
using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Service for managing application settings, persisting them to a JSON file.
/// </summary>
/// <seealso cref="ISettingsService" />
public class SettingsService : ISettingsService
{
    /// <summary>
    /// The file name for settings storage.
    /// </summary>
    private const string FileName = "settings.json";

    /// <summary>
    /// The folder name for settings storage.
    /// </summary>
    private const string FolderName = "KubeTools4Dev";

    /// <summary>
    /// The cached json serializer options
    /// </summary>
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// The full file path for settings storage.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SettingsService(
        ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, FolderName);
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }
        _filePath = Path.Combine(folder, FileName);
        Load();
    }

    /// <summary>
    /// Gets or sets the list of excluded services.
    /// </summary>
    public List<string> ExcludedServices { get; set; } = [];

    /// <summary>
    /// Gets or sets the refresh interval in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    public void Save()
    {
        try
        {
            var settings = new SettingsModel
            {
                RefreshIntervalSeconds = RefreshIntervalSeconds,
                ExcludedServices = ExcludedServices
            };
            var json = JsonSerializer.Serialize(settings, CachedJsonSerializerOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            _logger.Warning("Failed to save settings.");
        }
    }

    /// <summary>
    /// Loads settings from the file.
    /// </summary>
    private void Load()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var settings = JsonSerializer.Deserialize<SettingsModel>(json);
                if (settings != null)
                {
                    RefreshIntervalSeconds = settings.RefreshIntervalSeconds;
                    ExcludedServices = settings.ExcludedServices ?? [];
                }
            }
            catch
            {
                _logger.Information("Failed to load settings, using defaults.");
            }
        }
    }
}
