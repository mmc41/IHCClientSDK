using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace ihc_openvisual.Converters;

/// <summary>Returns true when the bound enum value equals the converter parameter — used to drive the check
/// mark on the mutually-exclusive theme menu items.</summary>
public sealed class EnumMatchConverter : IValueConverter
{
    public static readonly EnumMatchConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is not null &&
        string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
