using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests;

/// <summary>
/// ADR-002: the OpenVisual "Insert variable" palette is a PROJECTION of the SDK-authoritative
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

    /// <summary>
    /// Alignment F-12: the palette lists in the VENDOR's Indsæt ▸ Variable menu
    /// order, measured 2026-08-09 (armed bar dump, configuration mode). The vendor's first item, Scenarie, is not a
    /// variable (US-024 owns scenes and it never reaches this palette); Enum is OpenVisual's own extra route (the
    /// vendor menu has no Enum item) and is appended last. Everything between is the vendor's order verbatim.
    /// </summary>
    [Test]
    public void Entries_FollowTheVendorsInsertVariableMenuOrder()
    {
        var expected = new[]
        {
            "Ugedag", "Flag", "Tal", "Tæller", "Tidspunkt", "Dato", "Timer", "Indgang", "Udgang",
            "Timertid", "kW", "kWh", "W", "Wh", "Helligdag", "Kommatal", "Fugtighed", "Lys",
            "Lysniveau", "Temperatur", "Enum",
        };
        Assert.That(VariablePalette.Entries.Select(e => e.Label), Is.EqualTo(expected));
    }

    // uxparity2 T014 (W1/D03) — REPLACES Palette_DerivesSectionKind_FromRegistryRole, which pinned the 'I'/'O'/'V'
    // section kind. That concept is gone: the palette no longer decides which types a section accepts, because the
    // ENGINE does (ProjectAppService.GetInsertableVariableTypes over PlacementRules). The old test's intent — "the
    // palette does not independently encode the section rule" — is now pinned more strongly, by showing the palette
    // labels whatever it is given and applies no section rule of its own.
    [Test]
    public void LabelledTypes_LabelsExactlyWhatTheEngineReports_AndAppliesNoSectionRuleOfItsOwn()
    {
        // A signal type and a value type together: the old 'I'/'V' split would have refused to show both at once.
        var offered = VariablePalette.LabelledTypes(new[] { "resource_input", "resource_flag", "kW" }).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(offered.Select(e => e.Tag), Is.EquivalentTo(new[] { "resource_input", "resource_flag", "kW" }),
                "every tag the engine reports is labelled — the palette filters nothing by section");
            Assert.That(offered.Select(e => e.Label), Is.EqualTo(new[] { "Flag", "Indgang", "kW" }),
                "…and comes back in PALETTE (vendor-menu) order, not the caller's, so the menu reads the same wherever it is raised");
            Assert.That(VariablePalette.LabelledTypes(System.Array.Empty<string>()), Is.Empty,
                "a container that accepts no variables yields no palette");
        });
    }

    // The engine may legitimately report a tag that is not a variable type (resource_scene under an outputs section).
    // The SDK door filters those, but the palette must not render one as a raw tag if one ever arrives.
    [Test]
    public void LabelledTypes_DropsATagWithNoLabel_RatherThanShowingTheRawTag()
    {
        var offered = VariablePalette.LabelledTypes(new[] { "resource_flag", "resource_scene" }).ToList();

        Assert.That(offered.Select(e => e.Tag), Is.EqualTo(new[] { "resource_flag" }),
            "resource_scene is not a variable type (US-024 owns it) and never reaches the palette");
    }
}
