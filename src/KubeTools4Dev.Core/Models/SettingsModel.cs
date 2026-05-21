namespace KubeTools4Dev.Core.Models;

/// <summary>
/// Represents the application settings model.
/// </summary>
internal class SettingsModel
{
    /// <summary>
    /// Gets or sets the excluded services.
    /// </summary>
    /// <value>
    /// The excluded services.
    /// </value>
    /// <summary>
    /// Gets or sets the general settings.
    /// </summary>
    public GeneralSettings General { get; set; } = new();

    /// <summary>
    /// Gets or sets the pods settings.
    /// </summary>
    public PodsSettings Pods { get; set; } = new();

    /// <summary>
    /// Gets or sets the services settings.
    /// </summary>
    public ServicesSettings Services { get; set; } = new();

    /// <summary>
    /// Gets or sets the cluster registry settings.
    /// </summary>
    public ClustersSettings Clusters { get; set; } = new();
}

/// <summary>
/// Represents the general settings.
/// </summary>
public class GeneralSettings
{
    /// <summary>
    /// Gets or sets the log level.
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Gets or sets the log path.
    /// </summary>
    public string? LogPath { get; set; }
}

/// <summary>
/// Represents the pods settings.
/// </summary>
public class PodsSettings
{
    /// <summary>
    /// Gets or sets the refresh interval in seconds.
    /// </summary>
    public int RefreshIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the watch retry delay in milliseconds.
    /// </summary>
    public int WatchRetryDelayMilliseconds { get; set; } = 3000;
}

/// <summary>
/// Represents the services settings.
/// </summary>
public class ServicesSettings
{
    /// <summary>
    /// Gets or sets the excluded services.
    /// </summary>
    public List<string> ExcludedServices { get; set; } = [];

    /// <summary>
    /// Gets or sets the hidden service names.
    /// </summary>
    public List<string> HiddenServiceNames { get; set; } = ["kubernetes"];

    /// <summary>
    /// Gets or sets the hidden service types.
    /// </summary>
    public List<string> HiddenServiceTypes { get; set; } = ["ExternalName"];
}
