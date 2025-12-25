namespace KubeTools4Dev.Core.Models;

/// <summary>
/// 
/// </summary>
internal class SettingsModel
{
    /// <summary>
    /// Gets or sets the excluded services.
    /// </summary>
    /// <value>
    /// The excluded services.
    /// </value>
    public List<string> ExcludedServices { get; set; } = [];

    /// <summary>
    /// Gets or sets the refresh interval seconds.
    /// </summary>
    /// <value>
    /// The refresh interval seconds.
    /// </value>
    public int RefreshIntervalSeconds { get; set; }
}
