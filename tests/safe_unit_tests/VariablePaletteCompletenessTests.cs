using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Schema;

namespace safe_unit_tests;

/// <summary>
/// M3 / ADR-002 / D07 (T008): the OpenVisual "Insert variable" palette is a PROJECTION of the SDK-authoritative
/// <see cref="VariableTypeRegistry"/>, so every variable type the engine supports is presented (with an app label) —
/// a supported type can never silently vanish from the UI (the drift that had dropped six types:
/// resource_light_level/resource_humidity_level and kW/kWh/W/Wh, all now offered — T017 un-suppressed the four
/// power/energy types, D03).
/// </summary>
public class VariablePaletteCompletenessTests
{
    [Test]
    public void Palette_ProjectsEverySdkVariableType_NoneSilentlyDropped()
    {
        var registry = VariableTypeRegistry.All.Select(t => t.Tag).ToHashSet();
        var presented = VariablePalette.Entries.Select(e => e.Tag).ToHashSet();

        Assert.Multiple(() =>
        {
            var uncovered = registry.Where(t => !presented.Contains(t)).ToList();
            Assert.That(uncovered, Is.Empty,
                "every SDK variable type must be presented; unaccounted: " + string.Join(", ", uncovered));
            Assert.That(presented.SetEquals(registry), Is.True, "the palette presents exactly the SDK registry — no suppression");
        });
    }

    // The six types M3 found silently dropped are now all OFFERED: the two sensor value types and (T017, D03) the
    // four power/energy meter-unit types.
    [Test]
    public void PreviouslyDroppedTypes_AreNowAllOffered()
    {
        var presented = VariablePalette.Entries.Select(e => e.Tag).ToHashSet();
        Assert.Multiple(() =>
        {
            Assert.That(presented, Does.Contain("resource_light_level"), "light level is offered");
            Assert.That(presented, Does.Contain("resource_humidity_level"), "humidity is offered");
            foreach (string unit in new[] { "kW", "kWh", "W", "Wh" })
                Assert.That(presented, Does.Contain(unit), $"{unit} is now a user-insertable variable type (T017)");
        });
    }

    // The palette's 'I'/'O'/'V' section kind is derived from the SDK role, not independently re-hardcoded.
    [Test]
    public void Palette_DerivesSectionKind_FromRegistryRole()
    {
        char Kind(string tag) => VariablePalette.Entries.First(e => e.Tag == tag).Kind;
        Assert.Multiple(() =>
        {
            Assert.That(Kind("resource_input"), Is.EqualTo('I'));
            Assert.That(Kind("resource_output"), Is.EqualTo('O'));
            Assert.That(Kind("resource_flag"), Is.EqualTo('V'));
        });
    }
}
