using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ihc_openvisual.Converters;

/// <summary>Maps a bool to <see cref="FontWeight.Bold"/> (true) / <see cref="FontWeight.Normal"/> (false) — locality
/// labels render bold (US-006). One-way.</summary>
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public static readonly BoolToFontWeightConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? FontWeight.Bold : FontWeight.Normal;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
