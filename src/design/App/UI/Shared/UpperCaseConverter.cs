using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace App.UI.Shared;

/// <summary>
/// Converts a string to uppercase. Used by Pill styles to match Vue's text-transform: uppercase.
/// </summary>
public class UpperCaseConverter : IValueConverter
{
    public static readonly UpperCaseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpperInvariant();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
