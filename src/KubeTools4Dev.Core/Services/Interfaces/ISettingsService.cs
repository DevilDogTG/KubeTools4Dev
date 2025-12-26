namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Interface for managing application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the list of excluded services.
    /// </summary>
    List<string> ExcludedServices { get; set; }

    /// <summary>
    /// Gets or sets the refresh interval in seconds.
    /// </summary>
    int RefreshIntervalSeconds { get; set; }
    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    void Save();
}
