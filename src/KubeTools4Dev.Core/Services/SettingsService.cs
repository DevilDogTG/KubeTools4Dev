using KubeTools4Dev.Core.Models;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Service for managing application settings, persisting them to a JSON file.
/// </summary>
/// <seealso cref="KubeTools4Dev.Core.Services.Interfaces.ISettingsService" />
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
    /// The actual settings model.
    /// </summary>
    private SettingsModel _settings = new();

    /// <summary>
    /// The default log path found in appsettings.json
    /// </summary>
    private string _defaultLogPath = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, filePath: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService" /> class with an explicit file path.
    /// Intended for use in tests or scenarios where the default AppData path must be overridden.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="filePath">The full path to the settings file. When <c>null</c>, defaults to the standard AppData location.</param>
    internal SettingsService(ILogger<SettingsService> logger, string? filePath)
    {
        _logger = logger;
        if (filePath != null)
        {
            _filePath = filePath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, FolderName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            _filePath = Path.Combine(folder, FileName);
        }
        Load();
    }

    /// <inheritdoc />
    public GeneralSettings General => _settings.General;

    /// <inheritdoc />
    public PodsSettings Pods => _settings.Pods;

    /// <inheritdoc />
    public ServicesSettings Services => _settings.Services;

    /// <inheritdoc />
    public ClustersSettings Clusters => _settings.Clusters;

    /// <inheritdoc />
    public event Action? SettingsChanged;

    /// <inheritdoc />
    public string GetDefaultLogPath() => _defaultLogPath;

    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, CachedJsonSerializerOptions);
            File.WriteAllText(_filePath, json);
            SettingsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings.");
        }
    }

    /// <summary>
    /// Loads settings from the file.
    /// </summary>
    private void Load()
    {
        // 1. Load Defaults from appsettings.json (Current Directory)
        try
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            var defaultSettings = config.GetSection("Settings").Get<SettingsModel>();
            if (defaultSettings != null)
            {
                _settings = defaultSettings;
            }

            // Extract default log path
            // 1. Check Settings:General:LogPath first (Preferred source of truth)
            var generalLogPath = _settings.General?.LogPath;
            if (!string.IsNullOrEmpty(generalLogPath))
            {
                _defaultLogPath = generalLogPath;
            }
            else
            {
                // 2. Fallback to Serilog config
                var writeTo = config.GetSection("Serilog:WriteTo").GetChildren();
                foreach (var sink in writeTo.Where(sink => sink["Name"] == "File"))
                {
                    var args = sink.GetSection("Args");
                    var path = args["path"];
                    if (!string.IsNullOrEmpty(path))
                    {
                        _defaultLogPath = path;
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load default settings from appsettings.json.");
        }

        // 2. Load User Overrides from AppData
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var userSettings = JsonSerializer.Deserialize<SettingsModel>(json);

                if (userSettings != null)
                {
                    _settings = userSettings;

                    // Re-instantiate if nulls (e.g. corrupted file)
                    _settings.General ??= new GeneralSettings();
                    _settings.Pods ??= new PodsSettings();
                    _settings.Services ??= new ServicesSettings();
                    _settings.Clusters ??= new ClustersSettings();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load user settings, using defaults.");
            }
        }
    }
}
