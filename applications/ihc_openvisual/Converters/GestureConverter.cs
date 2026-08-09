using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace ihc_openvisual.Converters;

/// <summary>Parses a display shortcut string (e.g. "Ctrl+I") into a <see cref="KeyGesture"/> for a data-bound
/// <c>MenuItem.InputGesture</c> (alignment F-25). A runtime binding does not invoke XAML's parse-time type
/// converter, so a plain string bound to the <see cref="KeyGesture"/>-typed property would silently fail — this
/// bridges it. Null/blank yields no gesture; a malformed string also yields none rather than throwing. One-way.</summary>
public sealed class GestureConverter : IValueConverter
{
    public static readonly GestureConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return null;
        try { return KeyGesture.Parse(s); }
        catch (Exception) { return null; }   // a malformed hint shows nothing rather than crashing the menu
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
