using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace ihc_openvisual.ViewModels;

/// <summary>
/// Renders a typed variable's value the way a variable row shows it (US-027, uxparity2 W8/F7) — the presentation half
/// of a row label, kept Avalonia-free so every type can be unit-tested.
/// <para>
/// The formats are MEASURED, not chosen: `tmp/uxparity2/verify/V6/format-table.md` captures the rendered label for
/// all 21 registry types. They do not reduce to one rule — the decimal places differ by type (0/1/2/3), the unit
/// spacing differs by type, two types render a NAME rather than a number, three render nothing at all, and a date
/// drops its stored year. So this is a per-type table, deliberately, rather than a clever general formatter.
/// </para>
/// <para>
/// The separator is a comma throughout and weekday names are Danish: a project is authored in Danish and these
/// labels are the project's own data language, not the application's chrome.
/// </para>
/// </summary>
public static class VariableValueFormat
{
    // The value's own culture: comma decimal separator, matching every measured row.
    private static readonly CultureInfo Danish = CultureInfo.GetCultureInfo("da-DK");

    /// <summary>The weekday TOKENS the format stores, paired with the Danish names the rows render, in the order
    /// the reference application lists them. The `.vis` DTD declares <c>inivalue (monday | … | sunday) "monday"</c>
    /// — a token, never an index — so the lookup is by name and an absent or unrecognised value falls to the
    /// declared default, Monday. Reading it as an integer parsed nothing and fell back to element 0, so every
    /// weekday row read "Mandag" whatever the variable held (alignment F-43; the original's own row follows the
    /// value, measured).
    /// <para>Public because the weekday EDITOR offers the same seven, in the same order, mapped the same way: the
    /// token travels to the file and the label is ours to spell, so one table serves the row and the combo and a
    /// renamed label can never change a project.</para></summary>
    public static ImmutableArray<(string Token, string Label)> Weekdays { get; } =
    [
        ("monday", "Mandag"), ("tuesday", "Tirsdag"), ("wednesday", "Onsdag"), ("thursday", "Torsdag"),
        ("friday", "Fredag"), ("saturday", "Lørdag"), ("sunday", "Søndag"),
    ];

    private static readonly FrozenDictionary<string, string> WeekdayLabels =
        Weekdays.ToFrozenDictionary(d => d.Token, d => d.Label, StringComparer.Ordinal);

    // The fixed-point format strings the table below asks for, indexed by decimal places, so a row does not build
    // its own format string on every projection pass.
    private static readonly string[] FixedFormats = ["F0", "F1", "F2", "F3"];

    /// <summary>
    /// The rendered value for a variable of <paramref name="tag"/>, or <c>null</c> when the type shows no value at
    /// all (the two signal pins and holiday). <paramref name="attribute"/> supplies the element's effective
    /// attribute values; <paramref name="stateName"/> is the resolved enum state, which only an enum has.
    /// </summary>
    public static string? For(string tag, Func<string, string?> attribute, string? stateName = null)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        int Int(string attr, int fallback = 0) =>
            int.TryParse(attribute(attr), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : fallback;
        double Num() =>
            double.TryParse(attribute("inivalue"), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        string Fixed(int decimals) => Num().ToString(FixedFormats[decimals], Danish);
        string Time(bool milliseconds) => milliseconds
            ? $"{Int("hour"):00}:{Int("minute"):00}:{Int("second"):00},{Int("millisecond"):000}"
            : $"{Int("hour"):00}:{Int("minute"):00}:{Int("second"):00}";

        return tag switch
        {
            // No value is rendered for these — a signal pin shows its name alone, and so does a holiday.
            "resource_input" or "resource_output" or "resource_holiday" => null,

            "resource_flag" => attribute("inivalue") == "on" ? "ON" : "OFF",
            "resource_integer" or "resource_counter" => Fixed(0),
            "resource_floating_point" => Fixed(2),

            // A date shows day and month only — the stored year is not rendered.
            "resource_date" => $"{Int("day", 1):00}:{Int("month", 1):00}",
            "resource_time" => Time(milliseconds: false),
            "resource_timer" or "resource_timertime" => Time(milliseconds: true),
            "resource_weekday" => WeekdayLabel(attribute("inivalue")),

            // An enum renders its STATE's name; the caller resolves the state from the type definition.
            "resource_enum" => stateName,

            // Sensor and meter types: the decimals and the unit spacing are per type, not shared.
            "resource_light" => Fixed(0) + " Lux",
            "resource_light_level" => Fixed(0) + "%",
            "resource_temperature" => Fixed(1) + " °C",
            "resource_humidity_level" => Fixed(1) + "% RH",
            "kW" => Fixed(3) + "kW",
            "kWh" => Fixed(3) + "kWh",
            "W" => Fixed(0) + "W",
            "Wh" => Fixed(0) + "Wh",

            _ => null,
        };
    }

    /// <summary>The Danish name for a stored weekday token. An absent or unrecognised value falls to the DTD's
    /// declared default, Monday — the format omits an attribute sitting at its default, so "missing" is a day.</summary>
    private static string WeekdayLabel(string? token) =>
        token is not null && WeekdayLabels.TryGetValue(token, out string? label) ? label : Weekdays[0].Label;
}
