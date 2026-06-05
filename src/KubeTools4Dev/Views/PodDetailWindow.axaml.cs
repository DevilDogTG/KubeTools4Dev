using Avalonia.Controls;
using KubeTools4Dev.ViewModels;
using System;

namespace KubeTools4Dev.Views;

/// <summary>
/// Popup window that displays pod logs and describe output.
/// Multiple instances can be opened simultaneously for side-by-side viewing.
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
            vm.Initialize();
    }

    /// <inheritdoc/>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is PodDetailViewModel vm)
            vm.Dispose();
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
        // Turning Follow on jumps straight to the newest line.
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
