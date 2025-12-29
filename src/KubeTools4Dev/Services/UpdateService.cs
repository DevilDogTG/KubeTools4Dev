using System;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace KubeTools4Dev.Services
{
    public interface IUpdateService
    {
        Task<UpdateInfo?> CheckForUpdatesAsync();
        Task DownloadAndUpdateAsync(UpdateInfo updateInfo);
        string CurrentVersion { get; }
    }

    public class UpdateService : IUpdateService
    {
        private readonly UpdateManager _updateManager;

        public UpdateService()
        {
             // We can initialize this later or rely on DI to pass the manager
             // For now, simpler to just use the static logic or wrap it.
             // But purely static usage of VelopackApp doesn't fit well with this service if we want to mock it.
             // Velopack's UpdateManager is the way to go.
             try
             {
                 _updateManager = new UpdateManager(new GithubSource("https://github.com/DevilDogTG/KubeTools4Dev", null, false));
             }
             catch
             {
                 // Likely running in dev mode or not installed
                 _updateManager = null;
             }
        }

        public string CurrentVersion
        {
            get
            {
               return _updateManager?.CurrentVersion?.ToString() ?? "0.0.0";
            }
        }

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

        public async Task DownloadAndUpdateAsync(UpdateInfo updateInfo)
        {
             if (_updateManager == null) return;
             await _updateManager.DownloadUpdatesAsync(updateInfo);
             _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
    }
}
