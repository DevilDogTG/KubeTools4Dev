using KubeTools4Dev.Core.Models;
using System.Text.Json;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Interface for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the refresh interval in seconds.
    /// </summary>
    int RefreshIntervalSeconds { get; set; }

    /// <summary>
    /// Gets or sets the list of excluded services.
    /// </summary>
    List<string> ExcludedServices { get; set; }

    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    void Save();
}

/// <summary>
/// Service for managing application settings, persisting them to a JSON file.
/// </summary>
/// <seealso cref="ISettingsService" />
public class SettingsService : ISettingsService
{
    /// <summary>
    /// The folder name for settings storage.
    /// </summary>
    private const string FolderName = "ElysianMonitor";

    /// <summary>
    /// The file name for settings storage.
    /// </summary>
    private const string FileName = "settings.json";

    /// <summary>
    /// The full file path for settings storage.
    /// </summary>
    private readonly string _filePath;

    /// <summary>
    /// Gets or sets the refresh interval in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the list of excluded services.
    /// </summary>
    public List<string> ExcludedServices { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class.
    /// </summary>
    public SettingsService()
    {
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
                    ExcludedServices = settings.ExcludedServices ?? new();
                }
            }
            catch
            {
                // Ignore load errors, use defaults 
            }
        }
    }

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
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // Ignore save errors 
        }
    }
}
