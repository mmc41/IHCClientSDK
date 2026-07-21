using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Schema;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Projects the SDK-authoritative variable-type registry (<see cref="VariableTypeRegistry"/>, ADR-002/D07) into the
/// "Insert variable" palette (US-027): the app supplies a display <see cref="Labels">label</see> and derives the
/// section kind ('I'/'O'/'V') from each type's <see cref="VariableRole"/>, over the registry's tags. Every registry
/// type is either given a label here or listed in <see cref="Suppressed"/>, and a unit completeness test enforces
/// that — so a variable type the engine supports can never silently vanish from the UI (the M3 drift that dropped
/// six types). Avalonia-free, so the projection is unit-tested. The taxonomy is SDK data; the labels and the
/// present/suppress decision are app presentation (D07).
/// </summary>
public static class VariablePalette
{
    // App presentation: the display label for each OFFERED variable type. A registry type absent here must be listed
    // in Suppressed (the completeness test enforces it); the Entries projection reads the label by tag.
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
    };

    /// <summary>The SDK variable types deliberately NOT offered in the palette (D07): the power/energy meter reading
    /// types (<c>kW</c>/<c>kWh</c>/<c>W</c>/<c>Wh</c>) are specialised meter outputs — they appear on power-meter
    /// products with names like "Instantaneous power"/"Consumption" and <c>accessibility="read"</c>, not as general
    /// authorable block variables. Listed here so the omission is a DELIBERATE, tested decision, never a silent
    /// drift; move a tag into <see cref="Labels"/> to start offering it.</summary>
    public static readonly IReadOnlySet<string> Suppressed =
        new HashSet<string>(new[] { "kW", "kWh", "W", "Wh" }, StringComparer.Ordinal);

    /// <summary>The palette entries — (display label, resource tag, section kind 'I'nput / 'O'utput / 'V'alue) —
    /// projected over the SDK registry in registry order, excluding the suppressed types.</summary>
    public static readonly IReadOnlyList<(string Label, string Tag, char Kind)> Entries =
        VariableTypeRegistry.All
            .Where(t => !Suppressed.Contains(t.Tag))
            .Select(t => (Labels[t.Tag], t.Tag, KindOf(t.Role)))
            .ToList();

    private static char KindOf(VariableRole role) => role switch
    {
        VariableRole.Input => 'I',
        VariableRole.Output => 'O',
        _ => 'V',
    };
}
