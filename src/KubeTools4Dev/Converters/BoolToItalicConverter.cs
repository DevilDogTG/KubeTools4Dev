using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace KubeTools4Dev.Converters;

/// <summary>
/// Converts a <see cref="bool"/> to a <see cref="FontStyle"/>.
/// Returns <see cref="FontStyle.Italic"/> when the value is <see langword="true"/>;
/// <see cref="FontStyle.Normal"/> otherwise.
/// </summary>
public class BoolToItalicConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontStyle.Italic : FontStyle.Normal;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
