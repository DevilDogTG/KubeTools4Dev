using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
    /// <summary>Maximum number of log lines retained in the window.</summary>
    internal const int MaxLogLines = 1000;

    private readonly ILogger<PodDetailViewModel> _logger = logger;
    private readonly IKubernetesService _kubeService = kubeService;
    private readonly List<string> _logLines = [];
    private CancellationTokenSource? _logStreamCts;
    private bool _initialized;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private PodViewModel _pod = null!;

    /// <summary>Gets or sets a value indicating whether this window shows logs (true) or describe (false).</summary>
    public bool IsLogsView { get; set; }

    /// <summary>The accumulated log text shown in the window (selectable for copying).</summary>
    [ObservableProperty]
    private string _podLogsText = string.Empty;

    /// <summary>Names of the pod's containers; a picker is shown when there is more than one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleContainers))]
    private IReadOnlyList<string> _containers = [];

    /// <summary>The container whose logs are streamed. Changing it restarts the stream.</summary>
    [ObservableProperty]
    private string? _selectedContainer;

    /// <summary>Gets a value indicating whether the container picker should be visible.</summary>
    public bool HasMultipleContainers => Containers.Count > 1;

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
        {
            // Multi-container pods require an explicit container for the log API; default to
            // the first one. _initialized stays false so this assignment does not restart the
            // (not yet started) stream via OnSelectedContainerChanged.
            Containers = Pod.ContainerNames;
            SelectedContainer = Containers.Count > 0 ? Containers[0] : null;
            _initialized = true;
            _ = StartLogStreamAsync();
        }
        else
        {
            _initialized = true;
            _ = LoadDescribeAsync();
        }
    }

    /// <summary>Restarts the log stream when the user picks a different container.</summary>
    partial void OnSelectedContainerChanged(string? value)
    {
        if (_initialized && IsLogsView)
            _ = StartLogStreamAsync();
    }

    /// <summary>Dispatches an action to the UI thread. Override in derived classes to control dispatch, e.g. in unit tests.</summary>
    protected virtual Task DispatchToUIAsync(Action action) =>
        Dispatcher.UIThread.InvokeAsync(action).GetTask();

    internal async Task StartLogStreamAsync()
    {
        _logStreamCts?.Cancel();
        _logStreamCts = new CancellationTokenSource();
        var token = _logStreamCts.Token;
        var container = SelectedContainer;

        await DispatchToUIAsync(() =>
        {
            _logLines.Clear();
            _logLines.Add(container is not null && HasMultipleContainers
                ? $"Connecting to log stream for {Pod.Name} [{container}]..."
                : $"Connecting to log stream for {Pod.Name}...");
            RebuildLogText();
        });

        try
        {
            await foreach (var line in _kubeService.StreamPodLogsAsync(Pod.Namespace, Pod.Name, container, token))
            {
                if (token.IsCancellationRequested) break;

                await DispatchToUIAsync(() =>
                {
                    _logLines.Add(line);
                    if (_logLines.Count > MaxLogLines)
                        _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
                    RebuildLogText();
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stream is cancelled
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            LogStreamError(ex, Pod.Name);
            var detail = DescribeException(ex);
            await DispatchToUIAsync(() =>
            {
                _logLines.Add($"Error streaming logs: {detail}");
                RebuildLogText();
            });
        }
        catch (Exception)
        {
            // Stream was restarted/cancelled mid-flight (e.g. container switch) — ignore.
        }
    }

    private void RebuildLogText() => PodLogsText = string.Join(Environment.NewLine, _logLines);

    /// <summary>
    /// Builds a diagnostic message for a failed log stream: exception types, messages, the
    /// inner-exception chain, and — for Kubernetes API errors — the response body, which holds
    /// the actual reason (e.g. "a container name must be specified for pod x").
    /// </summary>
    internal static string DescribeException(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (sb.Length > 0) sb.Append(" -> ");
            sb.Append(current.GetType().Name).Append(": ").Append(current.Message);

            if (current is k8s.Autorest.HttpOperationException { Response.Content: { Length: > 0 } content })
                sb.Append(" [").Append(content.Trim()).Append(']');
        }
        return sb.ToString();
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
