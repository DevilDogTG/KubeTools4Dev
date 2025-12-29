using Velopack;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// 
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Gets the current version.
    /// </summary>
    /// <value>
    /// The current version.
    /// </value>
    string CurrentVersion { get; }

    /// <summary>
    /// Checks for updates asynchronous.
    /// </summary>
    /// <returns></returns>
    Task<UpdateInfo?> CheckForUpdatesAsync();

    /// <summary>
    /// Downloads the and update asynchronous.
    /// </summary>
    /// <param name="updateInfo">The update information.</param>
    /// <returns></returns>
    Task DownloadAndUpdateAsync(UpdateInfo updateInfo);
}
