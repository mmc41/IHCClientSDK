using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ihc_openvisual.Converters;

/// <summary>Maps a bool to a fixed brush (true) / <see cref="Brushes.Transparent"/> (false). The shared
/// <see cref="DropTarget"/> instance paints the current drag-over drop-target row (A-30, bound to
/// <c>TreeNodeViewModel.IsDropTarget</c>). One-way.</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    /// <summary>Highlights the current drag-over drop target — a translucent accent fill, legible over either theme
    /// and without hiding the row's label.</summary>
    public static readonly BoolToBrushConverter DropTarget =
        new(new SolidColorBrush(Color.FromArgb(0x66, 0x3B, 0x82, 0xF6)));

    private readonly IBrush _whenTrue;

    private BoolToBrushConverter(IBrush whenTrue) => _whenTrue = whenTrue;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? _whenTrue : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
