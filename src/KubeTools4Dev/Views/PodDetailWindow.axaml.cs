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
    private bool _autoScroll = true;

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
        if (sender is ScrollViewer sv)
        {
            if (e.ExtentDelta.Y != 0)
            {
                if (_autoScroll)
                    sv.ScrollToEnd();
            }
            else
            {
                var maxScroll = sv.Extent.Height - sv.Viewport.Height;
                if (maxScroll < 0) maxScroll = 0;
                _autoScroll = sv.Offset.Y >= maxScroll - 15;
            }
        }
    }
}
