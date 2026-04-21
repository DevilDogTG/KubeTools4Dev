using Velopack;

namespace KubeTools4Dev.Core.Services.Interfaces;

/// <summary>
/// Defines operations for checking, downloading, and applying application updates.
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
    /// Downloads and applies the update asynchronously.
    /// </summary>
    /// <param name="updateInfo">The update information.</param>
    /// <returns></returns>
    Task DownloadAndUpdateAsync(UpdateInfo updateInfo);
}
