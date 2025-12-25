using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services;
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
/// 
/// </summary>
/// <seealso cref="KubeTools4Dev.ViewModels.ViewModelBase" />
public partial class PodListViewModel : ViewModelBase
{
    /// <summary>
    /// All pods
    /// </summary>
    private readonly List<PodViewModel> _allPods = new();

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
    private CancellationTokenSource? _cts;
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

    /// <summary>
    /// The last refresh time
    /// </summary>
    [ObservableProperty]
    private string _lastRefreshTime = "Never";

    /// <summary>
    /// The pods
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PodViewModel> _pods = new();

    /// <summary>
    /// The refresh interval seconds
    /// </summary>
    [ObservableProperty]
    private int _refreshIntervalSeconds = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodListViewModel"/> class.
    /// </summary>
    /// <param name="kubeService">The kube service.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="logger">The logger.</param>
    public PodListViewModel(IKubernetesService kubeService, ISettingsService settingsService, ILogger<PodListViewModel> logger)
    {
        _kubeService = kubeService;
        _settingsService = settingsService;
        _logger = logger;

        _refreshIntervalSeconds = _settingsService.RefreshIntervalSeconds;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(RefreshIntervalSeconds)
        };
        _refreshTimer.Tick += (s, e) => TriggerRefresh();
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
            _cts = new CancellationTokenSource();
            _ = WatchPodsAsync(_cts.Token);

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
            _settingsService.RefreshIntervalSeconds = value;
            _settingsService.Save();
        }
    }
    /// <summary>
    /// Triggers the refresh.
    /// </summary>
    private void TriggerRefresh()
    {
        // For now, just update the timestamp if we are "watching"
        // If the watch is stuck, this won't help unless we re-fetch.
        // User asked for "config interval to refresh". This usually implies re-fetching or ensuring liveliness.
        // Let's re-fetch if the user wants. But usually Watch is better.
        // Let's just update the "Age" of pods and "Last Updated" if needed.
        // Actually, "Last Updated" should reflect data change.
        // Let's update the AGE of pods periodically.
        UpdateRefreshTime();
        foreach (var pod in Pods)
        {
            pod.RefreshAge();
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
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watch Error");
            }

            // Wait before restart
            try
            {
                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
