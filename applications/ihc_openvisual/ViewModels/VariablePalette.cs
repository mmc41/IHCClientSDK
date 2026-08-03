using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Schema;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Labels the SDK-authoritative variable types (<see cref="VariableTypeRegistry"/>, ADR-002/D07) for the "Insert
/// variable" palette (US-027): the app supplies a display <see cref="Labels">label</see> per tag, over the registry's
/// tags and in the registry's order. WHICH types a section accepts is the engine's rule
/// (<c>ProjectAppService.GetInsertableVariableTypes</c>) and is never re-derived here. EVERY registry type is given a
/// label — a unit completeness test enforces that — so a variable type the engine supports can never silently vanish
/// from the UI (the M3 drift that dropped six types). Avalonia-free, so the projection is unit-tested. The taxonomy
/// is SDK data; the labels are app presentation (D07).
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

    /// <summary>The palette entries — (display label, resource tag) — projected over the SDK registry in registry
    /// order. The registry is the completeness contract: every tag here has a label, so a type the engine supports
    /// cannot silently vanish from the UI.</summary>
    public static readonly IReadOnlyList<(string Label, string Tag)> Entries =
        VariableTypeRegistry.All.Select(t => (Labels[t.Tag], t.Tag)).ToList();

    /// <summary>
    /// Labels the <paramref name="tags"/> the engine reports insertable for a section
    /// (<c>ProjectAppService.GetInsertableVariableTypes</c>), in registry order rather than the caller's order so the
    /// palette reads identically wherever it is raised (uxparity2 W1/D03).
    /// <para>
    /// The app supplies labels only; WHICH types a section accepts is the engine's rule and is never re-derived here.
    /// A tag with no label would be a registry type the UI forgot — the completeness test makes that impossible, so
    /// an unlabelled tag is dropped rather than shown as a raw tag.
    /// </para>
    /// </summary>
    public static IEnumerable<(string Label, string Tag)> LabelledTypes(IEnumerable<string> tags)
    {
        var offered = new HashSet<string>(tags, StringComparer.Ordinal);
        return Entries.Where(e => offered.Contains(e.Tag));
    }
}
