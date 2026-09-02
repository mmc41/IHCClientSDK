using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Ihc.Vis;

namespace ihc_openvisual.Converters;

/// <summary>
/// Renders one data-line module row as the single sentence a screen reader (or a UIA client) hears in place of the
/// four loose <c>TextBlock</c>s a sighted reader reads under the column headers — <c>"Datalinie 1, Udgangsmodul,
/// Køkken, Loftlampe"</c>, or <c>"Datalinie 2, ikke i brug"</c> for a free line.
/// <para>
/// Avalonia's Windows bridge exposes no Grid/Table pattern, so a client cannot ask the module map for a cell by row
/// and column and cannot associate a value with its header; the row read as one labelled element is what the
/// platform CAN carry, and it is what this supplies (UX review USE-01). The wording repeats the header captions
/// because a header a client cannot reach programmatically may as well not be there.
/// </para>
/// One-way; the map is read-only.
/// </summary>
public sealed class DatalineModuleSummaryConverter : IValueConverter
{
    public static readonly DatalineModuleSummaryConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DatalineModule module ? Summarize(module) : string.Empty;

    /// <summary>The spoken form of one row. Public so the accessibility tests read the same wording the bindings do.</summary>
    public static string Summarize(DatalineModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        return module.InUse
            ? $"Datalinie {module.DataLine}, {module.ModuleType}, {module.Location}, {module.Description}"
            : $"Datalinie {module.DataLine}, ikke i brug";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
