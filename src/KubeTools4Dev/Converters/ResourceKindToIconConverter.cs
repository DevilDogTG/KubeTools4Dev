using Avalonia.Data.Converters;
using KubeTools4Dev.Core.ViewModels;
using Material.Icons;
using System;
using System.Globalization;

namespace KubeTools4Dev.Converters;

/// <summary>
/// Converts a <see cref="ResourceKind"/> enum to a <see cref="MaterialIconKind"/> for sidebar tree icons.
/// </summary>
public class ResourceKindToIconConverter : IValueConverter
{
    /// <inheritdoc/>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ResourceKind kind
            ? kind switch
            {
                ResourceKind.Pods => MaterialIconKind.LayersTriple,
                ResourceKind.Services => MaterialIconKind.ShareVariant,
                ResourceKind.Deployments => MaterialIconKind.RocketLaunch,
                _ => MaterialIconKind.Help
            }
            : MaterialIconKind.Help;
    }

    /// <inheritdoc/>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
