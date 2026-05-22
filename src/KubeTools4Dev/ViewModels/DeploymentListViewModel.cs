using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using k8s;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the list of Kubernetes deployments. Manages the observable collection,
/// text filtering, live watch, and per-row Rollout Restart and Edit commands.
/// </summary>
/// <seealso cref="ViewModelBase" />
/// <seealso cref="IDisposable" />
public partial class DeploymentListViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// All deployments (unfiltered).
    /// </summary>
    private readonly List<DeploymentViewModel> _allDeployments = [];

    /// <summary>
    /// The Kubernetes service used to fetch and patch deployments.
    /// </summary>
    private IKubernetesService? _kubernetesService;

    /// <summary>
    /// The current namespace filter (empty = all namespaces).
    /// </summary>
    private string _namespaceName = "";

    /// <summary>
    /// The logger.
    /// </summary>
    private readonly ILogger<DeploymentListViewModel> _logger;

    /// <summary>
    /// The cancellation token source for the watch loop.
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Optional periodic timer that updates <see cref="LastRefreshTime"/>. Created in <see cref="InitializeAsync"/>.
    /// </summary>
    private DispatcherTimer? _refreshTimer;

    /// <summary>
    /// Guards against double-dispose.
    /// </summary>
    private bool _disposed;

    /// <summary>
    /// The current filter text.
    /// </summary>
    [ObservableProperty]
    private string _filterText = string.Empty;

    /// <summary>
    /// Indicates whether an initial load is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Error message surfaced from the most recent failed command. Cleared at the start of each command.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Display string of the last time the list was refreshed.
    /// </summary>
    [ObservableProperty]
    private string _lastRefreshTime = "Never";

    /// <summary>
    /// The filtered and sorted observable collection of deployments shown in the view.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeploymentViewModel> _deployments = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentListViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DeploymentListViewModel(
        ILogger<DeploymentListViewModel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Switches the view to a different cluster service and namespace, then re-initializes.
    /// </summary>
    public async Task UpdateScopeAsync(IKubernetesService kubernetesService, string namespaceName)
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _refreshTimer?.Stop();

        _allDeployments.Clear();
        UpdateFilteredList();

        _kubernetesService = kubernetesService;
        _namespaceName = namespaceName;

        await InitializeAsync();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Initializes the view model: performs the initial load and starts the live watch loop.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_kubernetesService is null) return;
        IsLoading = true;
        try
        {
            if (!_kubernetesService.IsConnected) return;

            var deployments = await _kubernetesService.GetDeploymentsAsync(_namespaceName);
            _allDeployments.Clear();
            _allDeployments.AddRange(deployments.Select(d => new DeploymentViewModel(d)));
            UpdateFilteredList();
            UpdateRefreshTime();

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            _ = WatchDeploymentsAsync(_cancellationTokenSource.Token);

            if (_refreshTimer == null)
            {
                _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                _refreshTimer.Tick += OnRefreshTimerTick;
            }
            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize deployment list");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Releases managed resources: cancels the watch loop and stops the timer.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
        if (disposing)
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -= OnRefreshTimerTick;
            }
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Triggers a rollout restart of the specified deployment by annotating its pod template.
    /// </summary>
    /// <param name="deployment">The deployment view model to restart.</param>
    [RelayCommand]
    private async Task RolloutRestartAsync(DeploymentViewModel deployment)
    {
        ErrorMessage = string.Empty;
        try
        {
            await _kubernetesService!.RestartDeploymentAsync(deployment.Namespace, deployment.Name);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Restart failed: {ex.Message}";
            _logger.LogError(ex, "Failed to restart deployment {Name} in {Namespace}", deployment.Name, deployment.Namespace);
        }
    }

    /// <summary>
    /// Opens the Edit Deployment dialog and, on confirmation, patches the deployment.
    /// </summary>
    /// <param name="deployment">The deployment view model to edit.</param>
    [RelayCommand]
    private async Task EditDeploymentAsync(DeploymentViewModel deployment)
    {
        ErrorMessage = string.Empty;
        try
        {
            var vm = new EditDeploymentDialogViewModel(
                deployment.Name,
                deployment.DesiredReplicas,
                deployment.ImageTag);

            await ShowEditDialogAsync(vm);

            if (vm.IsConfirmed)
            {
                await _kubernetesService!.PatchDeploymentAsync(
                    deployment.Namespace,
                    deployment.Name,
                    vm.Replicas,
                    vm.ImageTag);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Edit failed: {ex.Message}";
            _logger.LogError(ex, "Failed to edit deployment {Name} in {Namespace}", deployment.Name, deployment.Namespace);
        }
    }

    /// <summary>
    /// Shows the Edit Deployment dialog. Override in tests to skip Avalonia window interaction.
    /// </summary>
    /// <param name="vm">The dialog view model.</param>
    protected virtual async Task ShowEditDialogAsync(EditDeploymentDialogViewModel vm)
    {
        var dialog = new Views.EditDeploymentDialog();
        vm.CloseCallback = () => dialog.Close();
        dialog.DataContext = vm;

        var owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show(); // non-blocking fallback for headless/test envs; IsConfirmed will be false
    }

    /// <summary>
    /// Called when [filter text changed].
    /// </summary>
    /// <param name="value">The new filter value.</param>
    partial void OnFilterTextChanged(string value) => UpdateFilteredList();

    /// <summary>
    /// Handles the refresh timer tick by updating the displayed refresh time.
    /// </summary>
    private void OnRefreshTimerTick(object? sender, EventArgs e) => UpdateRefreshTime();

    /// <summary>
    /// Updates the <see cref="LastRefreshTime"/> display string to the current local time.
    /// </summary>
    private void UpdateRefreshTime() => LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    /// Rebuilds <see cref="Deployments"/> from <c>_allDeployments</c> applying the current filter text.
    /// </summary>
    private void UpdateFilteredList()
    {
        var query = _allDeployments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            query = query.Where(d =>
                (d.Name?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.Namespace?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var sorted = query.OrderBy(d => d.Namespace).ThenBy(d => d.Name).ToList();

        Deployments.Clear();
        foreach (var d in sorted)
            Deployments.Add(d);
    }

    /// <summary>
    /// Runs the live watch loop. Reconnects automatically after errors with a 5-second back-off.
    /// </summary>
    /// <param name="token">Cancellation token that stops the loop on disposal.</param>
    private async Task WatchDeploymentsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await foreach (var (type, item) in _kubernetesService!.WatchDeploymentsAsync(_namespaceName, cancellationToken: token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateRefreshTime();

                        var existing = _allDeployments.FirstOrDefault(d =>
                            d.Name == item.Metadata.Name &&
                            d.Namespace == item.Metadata.NamespaceProperty);

                        if (type == WatchEventType.Deleted)
                        {
                            if (existing != null)
                                _allDeployments.Remove(existing);
                        }
                        else
                        {
                            if (existing != null)
                                existing.Update(item);
                            else
                                _allDeployments.Add(new DeploymentViewModel(item));
                        }

                        UpdateFilteredList();
                    });
                }

                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deployment watch error");
                await Task.Delay(5000, token);
            }
        }
    }
}
