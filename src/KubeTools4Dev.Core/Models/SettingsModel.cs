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

    /// <summary>
    /// Gets or sets the namespaces settings.
    /// </summary>
    public NamespacesSettings Namespaces { get; set; } = new();
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
    /// Gets or sets the hidden service names.
    /// </summary>
    public List<string> HiddenServiceNames { get; set; } = ["kubernetes"];

    /// <summary>
    /// Gets or sets the hidden service types.
    /// </summary>
    public List<string> HiddenServiceTypes { get; set; } = ["ExternalName"];
}

/// <summary>
/// Represents the namespaces settings controlling how the namespace list is kept up to date.
/// </summary>
public class NamespacesSettings
{
    /// <summary>
    /// Gets or sets the delay in milliseconds before retrying a failed namespace watch stream.
    /// Defaults to 5000 ms (5 seconds).
    /// </summary>
    public int WatchRetryDelayMilliseconds { get; set; } = 5000;
}
