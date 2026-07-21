using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Schema;

namespace safe_unit_tests;

/// <summary>
/// M3 / ADR-002 / D07 (T008): the OpenVisual "Insert variable" palette is a PROJECTION of the SDK-authoritative
/// <see cref="VariableTypeRegistry"/>, so every variable type the engine supports is either presented (with an app
/// label) or on an explicit suppression list — a supported type can never silently vanish from the UI (the drift
/// that had dropped six types: resource_light_level/resource_humidity_level and kW/kWh/W/Wh).
/// </summary>
public class VariablePaletteCompletenessTests
{
    [Test]
    public void Palette_ProjectsEverySdkVariableType_NoneSilentlyDropped()
    {
        var registry = VariableTypeRegistry.All.Select(t => t.Tag).ToHashSet();
        var presented = VariablePalette.Entries.Select(e => e.Tag).ToHashSet();
        var suppressed = VariablePalette.Suppressed.ToHashSet();

        Assert.Multiple(() =>
        {
            var uncovered = registry.Where(t => !presented.Contains(t) && !suppressed.Contains(t)).ToList();
            Assert.That(uncovered, Is.Empty,
                "every SDK variable type must be presented or explicitly suppressed; unaccounted: " + string.Join(", ", uncovered));
            Assert.That(presented.IsSubsetOf(registry), Is.True, "the palette offers only SDK-supported types");
            Assert.That(suppressed.IsSubsetOf(registry), Is.True, "the suppression list names only real SDK types");
            Assert.That(presented.Overlaps(suppressed), Is.False, "a type is either presented or suppressed, never both");
        });
    }

    // The six types M3 found silently dropped are now explicitly accounted for: the two sensor value types are
    // presented; the four power/energy meter-unit types are on the suppression list (D07).
    [Test]
    public void PreviouslyDroppedTypes_AreNowAccountedFor()
    {
        var presented = VariablePalette.Entries.Select(e => e.Tag).ToHashSet();
        Assert.Multiple(() =>
        {
            Assert.That(presented, Does.Contain("resource_light_level"), "light level is now offered");
            Assert.That(presented, Does.Contain("resource_humidity_level"), "humidity is now offered");
            foreach (string unit in new[] { "kW", "kWh", "W", "Wh" })
                Assert.That(VariablePalette.Suppressed, Does.Contain(unit), $"{unit} is explicitly suppressed, not silently dropped");
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
