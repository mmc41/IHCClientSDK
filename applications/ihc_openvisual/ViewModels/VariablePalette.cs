using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Vis.Schema;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Labels the SDK-authoritative variable types (<see cref="VariableTypeRegistry"/>, ADR-002) for the "Insert
/// variable" palette (US-027): the app supplies a display <see cref="Labels">label</see> per tag, over the registry's
/// tags and in the registry's order. WHICH types a section accepts is the engine's rule
/// (<c>ProjectAppService.GetInsertableVariableTypes</c>) and is never re-derived here. EVERY registry type is given a
/// label — a unit completeness test enforces that — so a variable type the engine supports can never silently vanish
/// from the UI (the M3 drift that dropped six types). Avalonia-free, so the projection is unit-tested. The taxonomy
/// is SDK data; the labels are app presentation (D07).
/// </summary>
public static class VariablePalette
{
    // App presentation: the display label for each variable type, in the wording IHC Visual's Indsæt ▸ Variable
    // menu uses. Every registry tag must appear here (the completeness test enforces it); the Entries projection
    // reads the label by tag.
    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["resource_input"] = "Indgang",
        ["resource_output"] = "Udgang",
        ["resource_flag"] = "Flag",
        ["resource_counter"] = "Tæller",
        ["resource_integer"] = "Tal",
        ["resource_floating_point"] = "Kommatal",
        ["resource_timer"] = "Timer",
        ["resource_timertime"] = "Timertid",
        ["resource_weekday"] = "Ugedag",
        ["resource_date"] = "Dato",
        ["resource_time"] = "Tidspunkt",
        ["resource_temperature"] = "Temperatur",
        ["resource_light"] = "Lys",
        ["resource_light_level"] = "Lysniveau",
        ["resource_humidity_level"] = "Fugtighed",
        ["resource_holiday"] = "Helligdag",
        ["resource_enum"] = "Enum",
        // Power/energy meter types (T017, D03): now user-insertable variable types mapped to their SDK resource tags.
        ["kW"] = "kW",
        ["kWh"] = "kWh",
        ["W"] = "W",
        ["Wh"] = "Wh",
    };

    // Alignment F-12: the VENDOR's Indsæt ▸ Variable menu order, measured 2026-08-09 (armed bar dump). The vendor's
    // first item, Scenarie, is not a variable (US-024 owns scenes); Enum is OpenVisual's own extra route (the vendor
    // menu carries no Enum item) and goes last. The completeness test pins this list to exactly the registry's tags,
    // so a registry type can neither vanish from nor sneak past the vendor ordering.
    private static readonly string[] MenuOrder =
    [
        "resource_weekday", "resource_flag", "resource_integer", "resource_counter", "resource_time",
        "resource_date", "resource_timer", "resource_input", "resource_output", "resource_timertime",
        "kW", "kWh", "W", "Wh", "resource_holiday", "resource_floating_point", "resource_humidity_level",
        "resource_light", "resource_light_level", "resource_temperature", "resource_enum",
    ];

    /// <summary>The palette entries — (display label, resource tag) — every SDK registry type, in the vendor's
    /// Indsæt ▸ Variable menu order (F-12). The registry stays the completeness contract: every tag here has a
    /// label, so a type the engine supports cannot silently vanish from the UI.</summary>
    public static readonly IReadOnlyList<(string Label, string Tag)> Entries =
        MenuOrder.Select(tag => (Labels[tag], tag)).ToList();

    /// <summary>The label the palette gives <paramref name="tag"/> — the read side of <see cref="Labels"/>, for
    /// callers that need to FIND a palette entry by its SDK type rather than assert its wording. Addressing a menu
    /// item through the tag keeps the caller correct when the wording is revised, and keeps the wording itself
    /// stated in exactly one place. Throws for a tag the registry does not carry, which the completeness test makes
    /// unreachable.</summary>
    public static string LabelFor(string tag) => Labels[tag];

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
