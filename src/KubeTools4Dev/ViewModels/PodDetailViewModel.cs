using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the pod detail popup window, managing log streaming and describe output.
/// Each instance is independent and owns its own lifecycle and cancellation token.
/// </summary>
public partial class PodDetailViewModel : ViewModelBase, IDisposable
{
    private readonly IKubernetesService _kubeService;
    private readonly ILogger<PodDetailViewModel> _logger;
    private CancellationTokenSource? _logStreamCts;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private PodViewModel _pod = null!;

    /// <summary>Gets or sets a value indicating whether this window shows logs (true) or describe (false).</summary>
    public bool IsLogsView { get; set; }

    [ObservableProperty]
    private ObservableCollection<string> _podLogsList = [];

    [ObservableProperty]
    private string _podDescribeText = string.Empty;

    [ObservableProperty]
    private bool _isDescribeLoading;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodDetailViewModel"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="kubeService">The Kubernetes service.</param>
    public PodDetailViewModel(ILogger<PodDetailViewModel> logger, IKubernetesService kubeService)
    {
        _logger = logger;
        _kubeService = kubeService;
    }

    /// <summary>
    /// Starts the initial operation (log streaming or describe load) based on <see cref="IsLogsView"/>.
    /// Must be called after <see cref="Pod"/> and <see cref="IsLogsView"/> are set.
    /// </summary>
    public void Initialize()
    {
        WindowTitle = IsLogsView
            ? $"Logs — {Pod.Name} [{Pod.Namespace}]"
            : $"Describe — {Pod.Name} [{Pod.Namespace}]";

        if (IsLogsView)
            _ = StartLogStreamAsync();
        else
            _ = LoadDescribeAsync();
    }

    private async Task StartLogStreamAsync()
    {
        _logStreamCts?.Cancel();
        _logStreamCts = new CancellationTokenSource();
        var token = _logStreamCts.Token;

        PodLogsList.Clear();
        PodLogsList.Add($"Connecting to log stream for {Pod.Name}...");

        try
        {
            await foreach (var line in _kubeService.StreamPodLogsAsync(Pod.Namespace, Pod.Name, token))
            {
                if (token.IsCancellationRequested) break;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (PodLogsList.Count > 1000)
                        PodLogsList.RemoveAt(0);
                    PodLogsList.Add(line);
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stream is cancelled
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming logs for {PodName}", Pod.Name);
            await Dispatcher.UIThread.InvokeAsync(() =>
                PodLogsList.Add($"Error streaming logs: {ex.Message}"));
        }
    }

    private async Task LoadDescribeAsync()
    {
        IsDescribeLoading = true;
        PodDescribeText = string.Empty;

        try
        {
            PodDescribeText = await _kubeService.GetPodDescribeAsync(Pod.Namespace, Pod.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading describe for {PodName}", Pod.Name);
            PodDescribeText = $"Error loading describe: {ex.Message}";
        }
        finally
        {
            IsDescribeLoading = false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources, cancelling any active log stream.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logStreamCts?.Cancel();
            _logStreamCts?.Dispose();
        }
    }
}
