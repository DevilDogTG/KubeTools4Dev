using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KubeTools4Dev.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace KubeTools4Dev.ViewModels;

/// <summary>
/// View model for the pod detail popup window, managing the Logs / Describe / Events tabs.
/// Each instance is independent and owns its own lifecycle and cancellation token.
/// Tab content loads lazily on first activation; logs keep streaming when the user
/// switches away so the tail is intact on return.
/// </summary>
public partial class PodDetailViewModel(
    ILogger<PodDetailViewModel> logger,
    IKubernetesService kubeService) : ViewModelBase, IDisposable
{
    /// <summary>Maximum number of log lines retained in the window.</summary>
    internal const int MaxLogLines = 1000;

    /// <summary>Tab index of the Logs view.</summary>
    internal const int LogsViewIndex = 0;

    /// <summary>Tab index of the Describe view.</summary>
    internal const int DescribeViewIndex = 1;

    /// <summary>Tab index of the Events view.</summary>
    internal const int EventsViewIndex = 2;

    private readonly ILogger<PodDetailViewModel> _logger = logger;
    private readonly IKubernetesService _kubeService = kubeService;
    private readonly List<string> _logLines = [];
    private CancellationTokenSource? _logStreamCts;
    private int _streamGeneration;
    private bool _initialized;
    private bool _logsStarted;
    private bool _describeLoaded;
    private bool _eventsLoaded;

    [ObservableProperty]
    private string _windowTitle = string.Empty;

    [ObservableProperty]
    private PodViewModel _pod = null!;

    /// <summary>The selected tab: <see cref="LogsViewIndex"/>, <see cref="DescribeViewIndex"/>,
    /// or <see cref="EventsViewIndex"/>. Switching lazily loads the tab's content.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLogsView))]
    [NotifyPropertyChangedFor(nameof(ShowContainerPicker))]
    private int _selectedViewIndex;

    /// <summary>Gets a value indicating whether the Logs tab is selected.</summary>
    public bool IsLogsView => SelectedViewIndex == LogsViewIndex;

    /// <summary>The accumulated log text shown in the window (selectable for copying).
    /// When <see cref="LogFilter"/> is set, only matching lines are shown; the underlying
    /// ring buffer keeps every line.</summary>
    [ObservableProperty]
    private string _podLogsText = string.Empty;

    /// <summary>Case-insensitive substring filter applied to the displayed log lines.
    /// Empty shows all lines. Display-only: arriving lines are always buffered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogFilter))]
    private string _logFilter = string.Empty;

    /// <summary>Gets a value indicating whether a log filter is active.</summary>
    public bool HasLogFilter => LogFilter.Length > 0;

    /// <summary>Match summary shown while a filter is active, e.g. <c>42 / 1000 lines</c>.</summary>
    [ObservableProperty]
    private string _logFilterStatus = string.Empty;

    /// <summary>Whether the logs view auto-scrolls to the newest line. The view turns this
    /// off when the user scrolls up and back on when they return to the bottom; the Follow
    /// toggle sets it explicitly.</summary>
    [ObservableProperty]
    private bool _isFollowingLogs = true;

    /// <summary>Names of the pod's containers; a picker is shown when there is more than one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleContainers))]
    [NotifyPropertyChangedFor(nameof(ShowContainerPicker))]
    private IReadOnlyList<string> _containers = [];

    /// <summary>The container whose logs are streamed. Changing it restarts the stream.</summary>
    [ObservableProperty]
    private string? _selectedContainer;

    /// <summary>Gets a value indicating whether the container picker should be visible.</summary>
    public bool HasMultipleContainers => Containers.Count > 1;

    /// <summary>Gets a value indicating whether the container picker should be visible
    /// (multi-container pod and the Logs tab is selected).</summary>
    public bool ShowContainerPicker => HasMultipleContainers && IsLogsView;

    [ObservableProperty]
    private string _podDescribeText = string.Empty;

    [ObservableProperty]
    private bool _isDescribeLoading;

    /// <summary>The pod's events shown in the Events tab, newest first.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoEvents))]
    private IReadOnlyList<PodEventRow> _podEvents = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoEvents))]
    private bool _isEventsLoading;

    /// <summary>Error text for a failed events load; empty when the last load succeeded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEventsError))]
    [NotifyPropertyChangedFor(nameof(ShowNoEvents))]
    private string _eventsError = string.Empty;

    /// <summary>Gets a value indicating whether the last events load failed.</summary>
    public bool HasEventsError => EventsError.Length > 0;

    /// <summary>Gets a value indicating whether the "no events" empty state should be shown.</summary>
    public bool ShowNoEvents => !IsEventsLoading && !HasEventsError && PodEvents.Count == 0;

    /// <summary>
    /// Starts the initially selected tab's load. Must be called after <see cref="Pod"/> and
    /// <see cref="SelectedViewIndex"/> are set.
    /// </summary>
    public void Initialize()
    {
        // Multi-container pods require an explicit container for the log API; default to
        // the first one. _initialized stays false so this assignment does not restart the
        // (not yet started) stream via OnSelectedContainerChanged.
        Containers = Pod.ContainerNames;
        SelectedContainer = Containers.Count > 0 ? Containers[0] : null;
        _initialized = true;
        UpdateWindowTitle();
        LoadSelectedView();
    }

    /// <summary>Restarts the log stream when the user picks a different container.</summary>
    partial void OnSelectedContainerChanged(string? value)
    {
        if (_initialized && _logsStarted)
            _ = StartLogStreamAsync();
    }

    /// <summary>Lazily loads the newly selected tab's content.</summary>
    partial void OnSelectedViewIndexChanged(int value)
    {
        if (!_initialized)
            return;

        UpdateWindowTitle();
        LoadSelectedView();
    }

    /// <summary>Kicks off the selected tab's load unless it has already run.</summary>
    private void LoadSelectedView()
    {
        switch (SelectedViewIndex)
        {
            case LogsViewIndex when !_logsStarted:
                _logsStarted = true;
                _ = StartLogStreamAsync();
                break;
            case DescribeViewIndex when !_describeLoaded:
                _describeLoaded = true;
                _ = LoadDescribeAsync();
                break;
            case EventsViewIndex when !_eventsLoaded:
                _eventsLoaded = true;
                _ = LoadEventsAsync();
                break;
        }
    }

    private void UpdateWindowTitle() => WindowTitle = SelectedViewIndex switch
    {
        DescribeViewIndex => $"Describe — {Pod.Name} [{Pod.Namespace}]",
        EventsViewIndex => $"Events — {Pod.Name} [{Pod.Namespace}]",
        _ => $"Logs — {Pod.Name} [{Pod.Namespace}]",
    };

    /// <summary>Dispatches an action to the UI thread. Override in derived classes to control dispatch, e.g. in unit tests.</summary>
    protected virtual Task DispatchToUIAsync(Action action) =>
        Dispatcher.UIThread.InvokeAsync(action).GetTask();

    internal async Task StartLogStreamAsync()
    {
        _logStreamCts?.Cancel();
        _logStreamCts = new CancellationTokenSource();
        var token = _logStreamCts.Token;
        var container = SelectedContainer;

        // Generation stamp: a superseded stream may still have UI work queued behind the new
        // stream's reset (dispatch-order race on container switch); stale lambdas check the
        // stamp and leave the new stream's buffer alone.
        var generation = Interlocked.Increment(ref _streamGeneration);

        await DispatchToUIAsync(() =>
        {
            if (generation != Volatile.Read(ref _streamGeneration)) return;
            _logLines.Clear();
            _logLines.Add(container is not null && HasMultipleContainers
                ? $"Connecting to log stream for {Pod.Name} [{container}]..."
                : $"Connecting to log stream for {Pod.Name}...");
            RebuildLogText();
        });

        try
        {
            // A channel decouples the network read from UI updates: during bursts (e.g. the
            // initial tailLines backlog) the reader drains every available line into ONE batch,
            // so the text rebuild runs once per batch instead of once per line. On quiet
            // streams each line still flushes immediately.
            var channel = Channel.CreateUnbounded<string>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

            var producer = Task.Run(async () =>
            {
                try
                {
                    await foreach (var line in _kubeService.StreamPodLogsAsync(Pod.Namespace, Pod.Name, container, token))
                        channel.Writer.TryWrite(line);
                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    // Surfaces to the reader loop below via WaitToReadAsync.
                    channel.Writer.Complete(ex);
                }
            }, token);

            while (await channel.Reader.WaitToReadAsync(token))
            {
                var batch = new List<string>();
                while (channel.Reader.TryRead(out var line))
                    batch.Add(line);

                await DispatchToUIAsync(() =>
                {
                    if (generation != Volatile.Read(ref _streamGeneration)) return;
                    _logLines.AddRange(batch);
                    if (_logLines.Count > MaxLogLines)
                        _logLines.RemoveRange(0, _logLines.Count - MaxLogLines);
                    RebuildLogText();
                });
            }

            await producer;
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
                if (generation != Volatile.Read(ref _streamGeneration)) return;
                _logLines.Add($"Error streaming logs: {detail}");
                RebuildLogText();
            });
        }
        catch (Exception ex)
        {
            // Stream was restarted/cancelled mid-flight (e.g. container switch). Not shown to
            // the user, but logged at debug level in case a real fault coincides with cancel.
            LogStreamEndedAfterCancellation(Pod.Name, ex.Message);
        }
    }

    /// <summary>Re-applies the display filter when the user edits it. Runs on the UI thread
    /// (TextBox binding), the same thread all <c>_logLines</c> mutations are dispatched to.</summary>
    partial void OnLogFilterChanged(string value) => RebuildLogText();

    /// <summary>Gets the full unfiltered buffer for saving to a file — the active display
    /// filter never narrows an export.</summary>
    public string GetFullLogText() => string.Join(Environment.NewLine, _logLines);

    /// <summary>Gets the suggested file name for a log export, e.g.
    /// <c>my-pod-app-20260605-143000.log</c> (container omitted for single-container pods).</summary>
    public string SuggestedLogFileName
    {
        get
        {
            var container = HasMultipleContainers && SelectedContainer is not null ? $"-{SelectedContainer}" : string.Empty;
            return $"{Pod.Name}{container}-{UtcNow:yyyyMMdd-HHmmss}.log";
        }
    }

    /// <summary>Appends a locally generated line (e.g. a save-failure notice) to the log
    /// buffer. Must be called on the UI thread.</summary>
    internal void AddLocalLogLine(string line)
    {
        _logLines.Add(line);
        RebuildLogText();
    }

    private void RebuildLogText()
    {
        if (LogFilter.Length == 0)
        {
            PodLogsText = string.Join(Environment.NewLine, _logLines);
            LogFilterStatus = string.Empty;
            return;
        }

        var matches = _logLines
            .Where(l => l.Contains(LogFilter, StringComparison.OrdinalIgnoreCase))
            .ToList();
        PodLogsText = string.Join(Environment.NewLine, matches);
        LogFilterStatus = $"{matches.Count} / {_logLines.Count} lines";
    }

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

    /// <summary>Gets the current UTC instant. Override in derived classes to control time, e.g. in unit tests.</summary>
    protected virtual DateTime UtcNow => DateTime.UtcNow;

    /// <summary>Reloads the Events tab on user request.</summary>
    [RelayCommand]
    private Task RefreshEvents() => LoadEventsAsync();

    internal async Task LoadEventsAsync()
    {
        IsEventsLoading = true;
        EventsError = string.Empty;

        try
        {
            var events = await _kubeService.GetPodEventsAsync(Pod.Namespace, Pod.Name);
            var now = UtcNow;
            PodEvents = events
                .Select(e => new PodEventRow(e.Type, e.Reason, e.Message, e.Count, e.FormatAge(now), e.IsWarning))
                .ToList();
        }
        catch (Exception ex)
        {
            LogEventsError(ex, Pod.Name);
            EventsError = $"Error loading events: {DescribeException(ex)}";
        }
        finally
        {
            IsEventsLoading = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error streaming logs for {PodName}")]
    private partial void LogStreamError(Exception ex, string podName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Log stream for {PodName} ended after cancellation: {Reason}")]
    private partial void LogStreamEndedAfterCancellation(string podName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error loading describe output for {PodName}")]
    private partial void LogDescribeError(Exception ex, string podName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error loading events for {PodName}")]
    private partial void LogEventsError(Exception ex, string podName);

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
