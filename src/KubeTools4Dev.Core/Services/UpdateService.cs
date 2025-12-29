using KubeTools4Dev.Core.Services.Interfaces;
using Velopack;
using Velopack.Sources;

namespace KubeTools4Dev.Core.Services;

/// <summary>
/// Update service powered by Velopack.
/// </summary>
/// <seealso cref="IUpdateService" />
public class UpdateService : IUpdateService
{
    /// <summary>
    /// The update manager
    /// </summary>
    private readonly UpdateManager _updateManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateService"/> class.
    /// </summary>
    public UpdateService()
    {
        // We can initialize this later or rely on DI to pass the manager
        // For now, simpler to just use the static logic or wrap it.
        // But purely static usage of VelopackApp doesn't fit well with this service if we want to mock it.
        // Velopack's UpdateManager is the way to go.
        _updateManager = new UpdateManager(new GithubSource("https://github.com/DevilDogTG/KubeTools4Dev", null, false));
    }

    /// <summary>
    /// Gets the current version.
    /// </summary>
    /// <value>
    /// The current version.
    /// </value>
    public string CurrentVersion
    {
        get
        {
            return _updateManager?.CurrentVersion?.ToString() ?? "0.0.0";
        }
    }

    /// <summary>
    /// Checks for updates asynchronous.
    /// </summary>
    /// <returns></returns>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (_updateManager == null) return null;
        try
        {
            return await _updateManager.CheckForUpdatesAsync();
        }
        catch (Exception)
        {
            // Log or ignore
            return null;
        }
    }

    /// <summary>
    /// Downloads the and update asynchronous.
    /// </summary>
    /// <param name="updateInfo">The update information.</param>
    public async Task DownloadAndUpdateAsync(UpdateInfo updateInfo)
    {
        if (_updateManager == null) return;
        await _updateManager.DownloadUpdatesAsync(updateInfo);
        _updateManager.ApplyUpdatesAndRestart(updateInfo);
    }
}
