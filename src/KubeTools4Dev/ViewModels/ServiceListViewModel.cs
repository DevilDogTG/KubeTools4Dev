using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// 
/// </summary>
/// <seealso cref="ViewModelBase" />
/// <remarks>
/// Initializes a new instance of the <see cref="ServiceListViewModel"/> class.
/// </remarks>
/// <param name="kubeService">The kube service.</param>
/// <param name="portForwardService">The port forward service.</param>
/// <param name="settingsService">The settings service.</param>
/// <param name="logger">The logger.</param>
public partial class ServiceListViewModel(
    IKubernetesService kubeService,
    IPortForwardService portForwardService,
    ISettingsService settingsService,
    ILogger<ServiceListViewModel> logger
) : ViewModelBase
{

    /// <summary>
    /// The services
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ServiceViewModel> _services = [];

    /// <summary>
    /// The is loading
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Initializes the asynchronous.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            if (!kubeService.IsConnected) return;

            var services = await kubeService.GetServicesAsync();

            // Filter out internal kubernetes service or headless
            var relevantServices = services.Where(s => s.Metadata.Name != "kubernetes" && s.Spec.Type != "ExternalName");

            Services.Clear();
            foreach (var svc in relevantServices)
            {
                if (svc.Spec.Ports == null) continue;

                foreach (var port in svc.Spec.Ports.Where(p => p.Protocol == "TCP"))
                {
                    Services.Add(new ServiceViewModel(svc, port, portForwardService, settingsService));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize service list");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Forwards all.
    /// </summary>
    [RelayCommand]
    private async Task ForwardAll()
    {
        foreach (var svc in Services)
        {
            if (!svc.IsForwarding && !svc.IsExcluded)
            {
                svc.IsForwarding = true; // Triggers the command in setter
            }
        }
    }

    /// <summary>
    /// Stops all.
    /// </summary>
    [RelayCommand]
    private async Task StopAll()
    {
        foreach (var svc in Services)
        {
            if (svc.IsForwarding)
            {
                svc.IsForwarding = false;
            }
        }
    }

    /// <summary>
    /// Cleanups this instance.
    /// </summary>
    public void Cleanup()
    {
        portForwardService.StopAll();
    }
}