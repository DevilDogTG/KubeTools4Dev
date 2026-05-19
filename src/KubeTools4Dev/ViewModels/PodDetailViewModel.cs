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
public partial class PodDetailViewModel(
    ILogger<PodDetailViewModel> logger,
    IKubernetesService kubeService) : ViewModelBase, IDisposable
{
    private readonly ILogger<PodDetailViewModel> _logger = logger;
    private readonly IKubernetesService _kubeService = kubeService;
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

    /// <summary>Dispatches an action to the UI thread. Override in derived classes to control dispatch, e.g. in unit tests.</summary>
    protected virtual Task DispatchToUIAsync(Action action) =>
        Dispatcher.UIThread.InvokeAsync(action).GetTask();

    internal async Task StartLogStreamAsync()
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

                await DispatchToUIAsync(() =>
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
            LogStreamError(ex, Pod.Name);
            await DispatchToUIAsync(() =>
                PodLogsList.Add($"Error streaming logs: {ex.Message}"));
        }
    }

    internal async Task LoadDescribeAsync()
    {
        IsDescribeLoading = true;
        PodDescribeText = string.Empty;

        try
        {
            PodDescribeText = await _kubeService.GetPodDescribeAsync(Pod.Namespace, Pod.Name);
        }
        catch (Exception ex)
        {
            LogDescribeError(ex, Pod.Name);
            PodDescribeText = $"Error loading describe: {ex.Message}";
        }
        finally
        {
            IsDescribeLoading = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error streaming logs for {PodName}")]
    private partial void LogStreamError(Exception ex, string podName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error loading describe output for {PodName}")]
    private partial void LogDescribeError(Exception ex, string podName);

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
