using Avalonia.Controls;

namespace KubeTools4Dev.Views;

/// <summary>
/// Pods list view.
/// </summary>
/// <seealso cref="Avalonia.Controls.UserControl" />
public partial class PodListView : UserControl
{
    private bool _autoScroll = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="PodListView"/> class.
    /// </summary>
    public PodListView()
    {
        InitializeComponent();
    }

    private void LogsScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            // If the content size changed (e.g. lines added)
            if (e.ExtentDelta.Y != 0)
            {
                if (_autoScroll)
                {
                    sv.ScrollToEnd();
                }
            }
            else
            {
                // If it's a standard user scroll, evaluate if we are at the bottom
                var maxScroll = sv.Extent.Height - sv.Viewport.Height;
                if (maxScroll < 0) maxScroll = 0;
                
                // Allow a small threshold to consider "at the bottom"
                _autoScroll = sv.Offset.Y >= maxScroll - 15;
            }
        }
    }
}
