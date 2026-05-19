using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
/// View model for the list of pods.
/// </summary>
/// <seealso cref="IDisposable" />
/// <seealso cref="ViewModelBase" />
public partial class PodListViewModel : ViewModelBase, IDisposable
{
    /// <summary>
    /// All pods
    /// </summary>
    private readonly List<PodViewModel> _allPods = [];

    /// <summary>
    /// The kube service
    /// </summary>
    private readonly IKubernetesService _kubeService;
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger<PodListViewModel> _logger;

    /// <summary>
    /// The refresh timer
    /// </summary>
    private readonly DispatcherTimer _refreshTimer;

    /// <summary>
    /// The settings service
    /// </summary>
    private readonly ISettingsService _settingsService;
    /// <summary>
    /// The CTS
    /// </summary>
    private CancellationTokenSource? _cancellationTokenSource;
    /// <summary>
    /// The filter text
    /// </summary>
    [ObservableProperty]
    private string _filterText = "";

    /// <summary>
    /// The is loading
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    private readonly Func<PodViewModel, int, PodDetailViewModel> _podDetailFactory;

    /// <summary>
    /// The last refresh time
    /// </summary>
    [ObservableProperty]
    private string _lastRefreshTime = "Never";

    /// <summary>
    /// The pods
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PodViewModel> _pods = [];

    /// <summary>
    /// The refresh interval seconds
    /// </summary>
    [ObservableProperty]
    private int _refreshIntervalSeconds = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodListViewModel" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kubeService">The kube service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="podDetailFactory">Factory for creating pod detail popup view models.</param>
    public PodListViewModel(
        ILogger<PodListViewModel> logger,
        IKubernetesService kubeService,
        ISettingsService settingsService,
        Func<PodViewModel, int, PodDetailViewModel> podDetailFactory)
    {
        _logger = logger;
        _kubeService = kubeService;
        _settingsService = settingsService;
        _podDetailFactory = podDetailFactory;

        _refreshIntervalSeconds = _settingsService.Pods.RefreshIntervalSeconds;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds)
        };

        _refreshTimer.Tick += OnRefreshTimerTick;

        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Initializes the asynchronous.
    /// </summary>
    public async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            if (!_kubeService.IsConnected) return;

            // Initial Load - All Namespaces
            var pods = await _kubeService.GetPodsAsync(""); // "" = All namespaces
            var podVms = pods.Select(p => new PodViewModel(p));

            _allPods.Clear();
            _allPods.AddRange(podVms);
            UpdateFilteredList();

            UpdateRefreshTime();

            // Start Watch
            _cancellationTokenSource = new CancellationTokenSource();
            _ = WatchPodsAsync(_cancellationTokenSource.Token);

            _ = FetchAndUpdateMetricsAsync();

            _refreshTimer.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize pod list");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsService.SettingsChanged -= OnSettingsChanged;
            _refreshTimer.Stop();
            _refreshTimer.Tick -= OnRefreshTimerTick;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// Decrements the refresh interval.
    /// </summary>
    [RelayCommand]
    private void DecrementRefreshInterval()
    {
        RefreshIntervalSeconds = Math.Clamp(RefreshIntervalSeconds - 1, 1, 60);
    }

    /// <summary>
    /// Increments the refresh interval.
    /// </summary>
    [RelayCommand]
    private void IncrementRefreshInterval()
    {
        RefreshIntervalSeconds = Math.Clamp(RefreshIntervalSeconds + 1, 1, 60);
    }

    /// <summary>
    /// Called when [filter text changed].
    /// </summary>
    /// <param name="value">The value.</param>
    partial void OnFilterTextChanged(string value)
    {
        UpdateFilteredList();
    }

    /// <summary>
    /// Called when [refresh interval seconds changed].
    /// </summary>
    /// <param name="value">The value.</param>
    partial void OnRefreshIntervalSecondsChanged(int value)
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Interval = TimeSpan.FromSeconds(value);
            _settingsService.Pods.RefreshIntervalSeconds = value;
            _settingsService.Save();
        }
    }

    /// <summary>
    /// Called when [refresh timer tick].
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
    private async void OnRefreshTimerTick(object? sender, EventArgs e) => await TriggerRefreshAsync();

    /// <summary>
    /// Called when [settings changed].
    /// </summary>
    private void OnSettingsChanged()
    {
        // Update interval
        var newInterval = _settingsService.Pods.RefreshIntervalSeconds;
        if (RefreshIntervalSeconds != newInterval)
        {
            RefreshIntervalSeconds = newInterval;
            // OnRefreshIntervalSecondsChanged handles timer update automatically
        }
    }
    /// <summary>
    /// Triggers the refresh.
    /// </summary>
    private async Task TriggerRefreshAsync()
    {
        UpdateRefreshTime();
        foreach (var pod in Pods)
        {
            pod.RefreshAge();
        }

        await FetchAndUpdateMetricsAsync();
    }

    private async Task FetchAndUpdateMetricsAsync()
    {
        try
        {
            if (!_kubeService.IsConnected) return;

            var metrics = await _kubeService.GetPodMetricsAsync("");
            if (metrics?.Items == null) return;

            var metricsDict = metrics.Items.ToDictionary(m => (m.Metadata.NamespaceProperty, m.Metadata.Name));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var pod in _allPods)
                {
                    if (metricsDict.TryGetValue((pod.Namespace, pod.Name), out var podMetrics))
                    {
                        pod.UpdateMetrics(podMetrics);
                    }
                    else
                    {
                        pod.UpdateMetrics(null!);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update pod metrics.");
        }
    }

    /// <summary>
    /// Updates the filtered list.
    /// </summary>
    private void UpdateFilteredList()
    {
        var query = _allPods.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterText))
        {
            query = query.Where(p =>
                (p.Name?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (p.Namespace?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Always order by Namespace, then Name
        var sorted = query.OrderBy(p => p.Namespace).ThenBy(p => p.Name).ToList();

        // Update ObservableCollection
        // Note: For large lists this might be slow, but for pods list (usually < 100-200) it's fine to clear and add.
        // Optimization: In a real app we might use DynamicData or smart diffing.
        Pods.Clear();
        foreach (var p in sorted)
        {
            Pods.Add(p);
        }
    }

    /// <summary>
    /// Updates the refresh time.
    /// </summary>
    private void UpdateRefreshTime()
    {
        LastRefreshTime = DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Watches the pods asynchronous.
    /// </summary>
    /// <param name="token">The token.</param>
    private async Task WatchPodsAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Watch all namespaces
                await foreach (var (type, item) in _kubeService.WatchPodsAsync("", cancellationToken: token))
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateRefreshTime();

                        var existing = _allPods.FirstOrDefault(p => p.Name == item.Metadata.Name && p.Namespace == item.Metadata.NamespaceProperty);

                        if (type == k8s.WatchEventType.Deleted)
                        {
                            if (existing != null)
                            {
                                _allPods.Remove(existing);
                            }
                        }
                        else
                        {
                            if (existing != null)
                            {
                                existing.Update(item);
                            }
                            else
                            {
                                _allPods.Add(new PodViewModel(item));
                            }
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
                _logger.LogError(ex, "Watch Error");
            }
        }
    }

    [RelayCommand]
    private void ShowLogs(PodViewModel pod) => OpenPodDetailWindow(pod, 0);

    [RelayCommand]
    private void ShowDescribe(PodViewModel pod) => OpenPodDetailWindow(pod, 1);

    private void OpenPodDetailWindow(PodViewModel pod, int tabIndex)
    {
        var vm = _podDetailFactory(pod, tabIndex);
        var window = new Views.PodDetailWindow(vm);
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d && d.MainWindow is not null)
            window.Show(d.MainWindow);
        else
            window.Show();
    }
}
