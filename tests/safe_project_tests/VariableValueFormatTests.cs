using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Schema;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// W8 / F7 / D07 (uxparity2 T027): the rendered value of a typed variable row, one case per registry type,
/// transcribed from the MEASURED table.
/// <para>
/// D07 requires exact per-type parity, and the measurement showed the formats do not reduce to one rule: decimals
/// differ by type (0/1/2/3), unit spacing differs by type, weekday and enum render a NAME, three types render
/// nothing, and a date drops its stored year. Each row below is therefore an independent expectation, not a
/// derivation — a derived expectation would reproduce whatever bug the formatter has.
/// </para>
/// </summary>
public class VariableValueFormatTests
{
    // The measured defaults: every fixture variable stores zeros (or nothing), and this is what the reference
    // application renders for each type in that state.
    private static readonly (string Tag, string? Expected)[] MeasuredDefaults =
    [
        ("resource_input", null),                 // signal pin — name only
        ("resource_output", null),                // signal pin — name only
        ("resource_holiday", null),               // never renders a value
        ("resource_flag", "OFF"),
        ("resource_integer", "0"),
        ("resource_counter", "0"),
        ("resource_floating_point", "0,00"),      // 2 decimals, comma
        ("resource_date", "01:01"),               // dd:MM — the stored YEAR is not rendered
        ("resource_time", "00:00:00"),            // no milliseconds
        ("resource_timer", "00:00:00,000"),       // milliseconds, comma-separated
        ("resource_timertime", "00:00:00,000"),
        ("resource_weekday", "Mandag"),           // a NAME, not a number
        // An enum renders its resolved STATE name; with no state resolved there is nothing to show. The populated
        // case (`NyTypeForThisProject = Værdi1`) is covered in StoredValues_AreRendered_NotJustTheDefaults.
        ("resource_enum", null),
        ("resource_light", "0 Lux"),              // space before the unit
        ("resource_light_level", "0%"),           // no space
        ("resource_temperature", "0,0 °C"),       // 1 decimal, space
        ("resource_humidity_level", "0,0% RH"),   // 1 decimal, %-tight then a space
        ("kW", "0,000kW"),                        // 3 decimals, no space
        ("kWh", "0,000kWh"),
        ("W", "0W"),                              // integer, no space
        ("Wh", "0Wh"),
    ];

    // Nothing stored: every attribute read comes back null, so each type falls to its default rendering.
    private static string? Unset(string _) => null;

    [Test]
    public void EveryType_RendersItsMeasuredDefault()
    {
        Assert.Multiple(() =>
        {
            foreach ((string tag, string? expected) in MeasuredDefaults)
                Assert.That(VariableValueFormat.For(tag, Unset), Is.EqualTo(expected), $"{tag}");
        });
    }

    // The table above must cover the whole registry — a type added to the SDK without a measured format would
    // otherwise render blank and nobody would notice.
    [Test]
    public void TheMeasuredTable_CoversEveryRegistryType()
    {
        var covered = MeasuredDefaults.Select(r => r.Tag).ToHashSet(StringComparer.Ordinal);
        var registry = VariableTypeRegistry.All.Select(t => t.Tag).ToHashSet(StringComparer.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(covered.SetEquals(registry), Is.True,
                $"missing: [{string.Join(", ", registry.Except(covered))}]  extra: [{string.Join(", ", covered.Except(registry))}]");
            Assert.That(registry, Has.Count.EqualTo(21), "the registry is the 21 types the measurement covered");
        });
    }

    // Stored values are rendered, not just the defaults — otherwise the formatter could ignore its input entirely
    // and still pass the table above.
    [Test]
    public void StoredValues_AreRendered_NotJustTheDefaults()
    {
        static Func<string, string?> Attrs(params (string Key, string Value)[] pairs) =>
            key => pairs.FirstOrDefault(p => p.Key == key).Value;

        Assert.Multiple(() =>
        {
            Assert.That(VariableValueFormat.For("resource_flag", Attrs(("inivalue", "on"))), Is.EqualTo("ON"));
            Assert.That(VariableValueFormat.For("resource_integer", Attrs(("inivalue", "42"))), Is.EqualTo("42"));
            Assert.That(VariableValueFormat.For("resource_floating_point", Attrs(("inivalue", "1.5"))), Is.EqualTo("1,50"));
            Assert.That(VariableValueFormat.For("resource_temperature", Attrs(("inivalue", "21.5"))), Is.EqualTo("21,5 °C"));
            Assert.That(VariableValueFormat.For("kW", Attrs(("inivalue", "2.25"))), Is.EqualTo("2,250kW"));
            Assert.That(VariableValueFormat.For("resource_date", Attrs(("day", "24"), ("month", "12"), ("year", "2026"))),
                Is.EqualTo("24:12"), "the year is stored but never rendered");
            Assert.That(VariableValueFormat.For("resource_time", Attrs(("hour", "7"), ("minute", "30"), ("second", "5"))),
                Is.EqualTo("07:30:05"));
            Assert.That(VariableValueFormat.For("resource_timer",
                    Attrs(("hour", "0"), ("minute", "0"), ("second", "1"), ("millisecond", "500"))),
                Is.EqualTo("00:00:01,500"));
            // A weekday's inivalue is a TOKEN, not an index. This case used to assert ("inivalue", "6") → Søndag,
            // an assumption the format contradicts: the DTD declares `inivalue (monday | … | sunday) "monday"`,
            // and the reference application stores and shows the token (measured 2026-08-11 — committing Torsdag
            // made its own tree row read "Ugedag = Torsdag"). Parsing a token as an integer failed and fell back
            // to index 0, so EVERY weekday rendered "Mandag" whatever it held (alignment F-43).
            Assert.That(VariableValueFormat.For("resource_weekday", Attrs(("inivalue", "sunday"))), Is.EqualTo("Søndag"));
            Assert.That(VariableValueFormat.For("resource_weekday", Attrs(("inivalue", "thursday"))), Is.EqualTo("Torsdag"));
            // The format OMITS an attribute at its declared default, so an ABSENT inivalue is Monday — a weekday
            // saved as Mandag carries no inivalue at all — and an unrecognised token falls to the same default
            // rather than to a wrong day.
            Assert.That(VariableValueFormat.For("resource_weekday", Attrs(("inivalue", "bogus"))), Is.EqualTo("Mandag"));
            Assert.That(VariableValueFormat.For("resource_enum", Unset, stateName: "Værdi2"), Is.EqualTo("Værdi2"));
        });
    }
}
