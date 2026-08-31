using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LxMusicPlugin.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var inverse = parameter?.ToString()?.ToLower() == "inverse";
        if (inverse) boolValue = !boolValue;
        return boolValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        var inverse = parameter?.ToString()?.ToLower() == "inverse";
        if (inverse) boolValue = !boolValue;
        return boolValue;
    }
}

public class DoubleToProgressConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return Math.Clamp(d, 0, 100);
        }
        return 0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            return Math.Clamp(d, 0, 100);
        }
        return 0;
    }
}