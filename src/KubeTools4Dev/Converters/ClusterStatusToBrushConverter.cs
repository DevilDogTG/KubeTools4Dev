using Avalonia.Data.Converters;
using Avalonia.Media;
using KubeTools4Dev.Core.ViewModels;
using System;
using System.Globalization;

namespace KubeTools4Dev.Converters;

/// <summary>
/// Converts a <see cref="ClusterConnectionStatus"/> to a status-dot brush color.
/// Connected = green, Error = red, else grey.
/// </summary>
public class ClusterStatusToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ClusterConnectionStatus status
            ? status switch
            {
                ClusterConnectionStatus.Connected => Brushes.LimeGreen,
                ClusterConnectionStatus.Error => Brushes.Red,
                ClusterConnectionStatus.Connecting => Brushes.Yellow,
                _ => Brushes.Gray
            }
            : Brushes.Gray;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
