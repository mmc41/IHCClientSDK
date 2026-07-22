using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Schema;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Projects the SDK-authoritative variable-type registry (<see cref="VariableTypeRegistry"/>, ADR-002/D07) into the
/// "Insert variable" palette (US-027): the app supplies a display <see cref="Labels">label</see> and derives the
/// section kind ('I'/'O'/'V') from each type's <see cref="VariableRole"/>, over the registry's tags. EVERY registry
/// type is given a label here — a unit completeness test enforces that — so a variable type the engine supports can
/// never silently vanish from the UI (the M3 drift that dropped six types). Avalonia-free, so the projection is
/// unit-tested. The taxonomy is SDK data; the labels are app presentation (D07).
/// </summary>
public static class VariablePalette
{
    // App presentation: the display label for each variable type. Every registry tag must appear here (the
    // completeness test enforces it); the Entries projection reads the label by tag.
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["resource_input"] = "Input",
        ["resource_output"] = "Output",
        ["resource_flag"] = "Flag",
        ["resource_counter"] = "Counter",
        ["resource_integer"] = "Integer",
        ["resource_floating_point"] = "Decimal",
        ["resource_timer"] = "Timer",
        ["resource_timertime"] = "Timer value",
        ["resource_weekday"] = "Weekday",
        ["resource_date"] = "Date",
        ["resource_time"] = "Time of day",
        ["resource_temperature"] = "Temperature",
        ["resource_light"] = "Light",
        ["resource_light_level"] = "Light level",
        ["resource_humidity_level"] = "Humidity",
        ["resource_holiday"] = "Holiday",
        ["resource_enum"] = "Enum",
        // Power/energy meter types (T017, D03): now user-insertable variable types mapped to their SDK resource tags.
        ["kW"] = "Power (kW)",
        ["kWh"] = "Energy (kWh)",
        ["W"] = "Power (W)",
        ["Wh"] = "Energy (Wh)",
    };

    /// <summary>The palette entries — (display label, resource tag, section kind 'I'nput / 'O'utput / 'V'alue) —
    /// projected over the SDK registry in registry order.</summary>
    public static readonly IReadOnlyList<(string Label, string Tag, char Kind)> Entries =
        VariableTypeRegistry.All
            .Select(t => (Labels[t.Tag], t.Tag, KindOf(t.Role)))
            .ToList();

    private static char KindOf(VariableRole role) => role switch
    {
        VariableRole.Input => 'I',
        VariableRole.Output => 'O',
        _ => 'V',
    };
}
