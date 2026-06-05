using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using KubeTools4Dev.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace KubeTools4Dev.Views;

/// <summary>
/// Popup window that displays pod logs, describe output, and events.
/// Multiple instances can be opened simultaneously for side-by-side viewing.
/// Log lines are rendered as severity-colored <see cref="Run"/> inlines (built here from
/// <see cref="PodDetailViewModel.PodLogsText"/>) so text stays selectable across lines.
/// </summary>
public partial class PodDetailWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PodDetailWindow"/> class.
    /// This parameterless constructor is required by the Avalonia XAML runtime loader.
    /// Use <see cref="PodDetailWindow(PodDetailViewModel)"/> for all runtime usage.
    /// </summary>
    public PodDetailWindow() => InitializeComponent();

    /// <summary>
    /// Initializes a new instance of the <see cref="PodDetailWindow"/> class with a view model.
    /// </summary>
    /// <param name="viewModel">The view model for this window.</param>
    public PodDetailWindow(PodDetailViewModel viewModel) : this() => DataContext = viewModel;

    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is PodDetailViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            RebuildLogInlines(vm.PodLogsText);
            vm.Initialize();
        }
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is PodDetailViewModel vm)
        {
            vm.PropertyChanged -= OnViewModelPropertyChanged;
            vm.Dispose();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PodDetailViewModel.PodLogsText) && DataContext is PodDetailViewModel vm)
            RebuildLogInlines(vm.PodLogsText);
    }

    /// <summary>
    /// Re-renders the displayed log text as one <see cref="Run"/> per line, colored by
    /// <see cref="LogLineClassifier"/>. Runs once per Channels batch (the same cadence the
    /// previous plain-text binding updated at), not per line.
    /// </summary>
    private void RebuildLogInlines(string text)
    {
        var inlines = LogText.Inlines ??= new InlineCollection();
        inlines.Clear();
        if (text.Length == 0)
            return;

        var errorBrush = ResolveBrush("SemiColorDanger", Brushes.Crimson);
        var warningBrush = ResolveBrush("SemiColorWarning", Brushes.Orange);

        var lines = text.Split(Environment.NewLine);
        var runs = new List<Inline>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var run = new Run(i < lines.Length - 1 ? lines[i] + Environment.NewLine : lines[i]);
            switch (LogLineClassifier.Classify(lines[i]))
            {
                case LogSeverity.Error: run.Foreground = errorBrush; break;
                case LogSeverity.Warning: run.Foreground = warningBrush; break;
                case LogSeverity.Debug: run.Foreground = Brushes.Gray; break;
            }
            runs.Add(run);
        }
        inlines.AddRange(runs);
    }

    /// <summary>Resolves a theme color resource to a brush, falling back to a fixed color
    /// when the resource is missing (e.g. a future theme swap).</summary>
    private IBrush ResolveBrush(string key, IBrush fallback)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var value))
        {
            if (value is IBrush brush) return brush;
            if (value is Color color) return new SolidColorBrush(color);
        }
        return fallback;
    }

    private void LogsScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || DataContext is not PodDetailViewModel vm)
            return;

        if (e.ExtentDelta.Y != 0)
        {
            // Content grew: stick to the bottom while following.
            if (vm.IsFollowingLogs)
                sv.ScrollToEnd();
        }
        else
        {
            // User scroll: follow turns off when they scroll up and back on at the bottom,
            // keeping the Follow toggle in sync with the gesture.
            var maxScroll = sv.Extent.Height - sv.Viewport.Height;
            if (maxScroll < 0) maxScroll = 0;
            vm.IsFollowingLogs = sv.Offset.Y >= maxScroll - 15;
        }
    }

    private void FollowToggle_IsCheckedChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Turning Follow on jumps straight to the newest line. This also fires when the
        // scroll heuristic above sets IsFollowingLogs=true at the bottom; the resulting
        // ScrollToEnd re-enters ScrollChanged with an unchanged extent, which recomputes
        // IsFollowingLogs to the same value — the loop converges in one pass. Keep that
        // invariant in mind when editing either handler.
        if (sender is Avalonia.Controls.Primitives.ToggleButton { IsChecked: true })
            LogsScrollViewer.ScrollToEnd();
    }

    private async void SaveLogs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not PodDetailViewModel vm)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save logs",
                SuggestedFileName = vm.SuggestedLogFileName,
                DefaultExtension = "log",
                FileTypeChoices =
                [
                    new("Log files") { Patterns = ["*.log", "*.txt"] },
                    new("All files") { Patterns = ["*"] }
                ]
            });

            if (file is null)
                return;

            // Always export the full ring buffer, never the filtered view.
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new System.IO.StreamWriter(stream);
            await writer.WriteAsync(vm.GetFullLogText());
        }
        catch (Exception ex)
        {
            // async void: surface the failure in the log pane instead of crashing the app.
            vm.AddLocalLogLine($"Error saving logs: {ex.Message}");
        }
    }
}
