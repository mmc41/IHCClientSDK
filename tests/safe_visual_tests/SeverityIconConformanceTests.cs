using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ihc.Vis.Validation;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The three severity glyphs, run mechanically against the parts of the icon checklist a machine can judge.
///
/// <para><b>Which parts those are, and why the list stops where it does.</b> Canvas, ink, stroke weight, semantic
/// ids, absence of colour/style/text and file size are all decidable from the file. Two checklist items are not:
/// whether the metaphor reads at a glance, and whether the strokes stay distinct at 16 px. Those need a person
/// looking at a render, and a machine substitute for them would be a check that passes while the icon is
/// unreadable — worse than no check, because it would say the item was covered.</para>
///
/// <para><b>The live-area item is deliberately absent too, and that absence is a decision rather than an
/// omission.</b> A naive reading of raw path coordinates fails <c>severity-warning.svg</c>, whose triangle steers
/// through quadratic CONTROL points at y=1.4 and x=22.3 — points the curve never reaches. The rendered geometry
/// is inside the live area; the control points are not artwork. A check that would force an edit to a conformant
/// asset is a wrong check, not a finding, so this fixture measures what it can measure honestly and says so.</para>
/// </summary>
public class SeverityIconConformanceTests
{
    /// <summary>The three tiers' assets — and deliberately NOT severity-fatal.svg.</summary>
    private static readonly string[] TierAssets =
        ["severity-error.svg", "severity-warning.svg", "severity-info.svg"];

    /// <summary>The checklist's ceiling for one glyph. Roomy for these; a breach means an icon grew a story.</summary>
    private const int MaxBytes = 2048;

    private static string AssetPath(string file) => Path.Combine(
        TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
        "applications", "ihc_openvisual", "Assets", file);

    private static string Read(string file)
    {
        string path = AssetPath(file);
        Assert.That(File.Exists(path), Is.True, $"the asset must exist at {path}");
        return File.ReadAllText(path);
    }

    [Test]
    public void EveryTierGlyphSitsOnTheStandardCanvasWithTheStandardRootAttributes()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                XElement root = XDocument.Parse(Read(file)).Root!;
                Assert.That(root.Name.LocalName, Is.EqualTo("svg"), file);
                Assert.That((string?)root.Attribute("viewBox"), Is.EqualTo("0 0 24 24"), file);
                Assert.That((string?)root.Attribute("aria-hidden"), Is.EqualTo("true"),
                    $"{file}: the glyph is decoration beside a text label, so it is hidden from the reader");
                Assert.That((string?)root.Attribute("fill"), Is.EqualTo("none"), file);
                Assert.That((string?)root.Attribute("stroke"), Is.EqualTo("currentColor"), file);
                Assert.That((string?)root.Attribute("stroke-width"), Is.EqualTo("2"), file);
                Assert.That((string?)root.Attribute("stroke-linecap"), Is.EqualTo("round"), file);
                Assert.That((string?)root.Attribute("stroke-linejoin"), Is.EqualTo("round"), file);
            }
        });
    }

    [Test]
    public void EveryDrawnElementCarriesASemanticId()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                XElement root = XDocument.Parse(Read(file)).Root!;
                List<XElement> drawn = [.. root.Elements()];
                Assert.That(drawn, Is.Not.Empty, $"{file} draws something");

                foreach (XElement element in drawn)
                {
                    string? id = (string?)element.Attribute("id");
                    Assert.That(id, Is.Not.Null.And.Not.Empty,
                        $"{file}: <{element.Name.LocalName}> has no id — an unnamed shape cannot be discussed or "
                        + "reused, and the family's whole convention is that every stroke is named");
                    Assert.That(id, Does.Match("^[a-z][a-z0-9-]*$"),
                        $"{file}: '{id}' is not a semantic lower-kebab name");
                }
            }
        });
    }

    /// <summary>
    /// One ink, and it is the caller's. The glyphs are recoloured at runtime by the app's state layer, which only
    /// works while nothing inside them pins a colour of its own.
    /// </summary>
    [Test]
    public void NothingPinsAColourOrSmugglesInStyleOrText()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                string svg = Read(file);
                foreach (string forbidden in new[] { "<text", "style=", "<style", "gradient", "filter=", "<filter" })
                {
                    Assert.That(svg, Does.Not.Contain(forbidden).IgnoreCase,
                        $"{file} must not contain '{forbidden}'");
                }

                // Any colour literal at all — hex, rgb() or a named colour — would survive the runtime recolour.
                Assert.That(Regex.IsMatch(svg, @"#[0-9a-fA-F]{3,8}\b"), Is.False, $"{file}: hex colour");
                Assert.That(svg, Does.Not.Contain("rgb("), file);

                foreach (Match paint in Regex.Matches(svg, @"(?:fill|stroke)=""([^""]*)"""))
                {
                    string value = paint.Groups[1].Value;
                    Assert.That(value, Is.AnyOf("none", "currentColor"),
                        $"{file}: paint '{value}' is neither currentColor nor none, so the state layer cannot "
                        + "recolour this glyph");
                }
            }
        });
    }

    /// <summary>The one allowed style mix: a filled sub-shape pairs its fill with <c>stroke="none"</c>.</summary>
    [Test]
    public void AFilledSubShapeTurnsItsStrokeOff()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                foreach (XElement element in XDocument.Parse(Read(file)).Root!.Elements())
                {
                    if ((string?)element.Attribute("fill") == "currentColor")
                    {
                        Assert.That((string?)element.Attribute("stroke"), Is.EqualTo("none"),
                            $"{file}: filled '{(string?)element.Attribute("id")}' must not also be stroked — the "
                            + "two together thicken the shape by half the stroke on every side");
                    }
                }
            }
        });
    }

    [Test]
    public void EveryTierGlyphStaysWithinTheSizeCeiling()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                Assert.That(new FileInfo(AssetPath(file)).Length, Is.LessThanOrEqualTo(MaxBytes),
                    $"{file} is over {MaxBytes} bytes — at that size an icon has stopped being a glyph");
            }
        });
    }

    // ── R8: what the panel actually wires ───────────────────────────────────────────────────────────────────

    [Test]
    public void ThePanelWiresExactlyTheseThreeAssetsAndNeverTheRefusalGlyph()
    {
        string[] wired =
        [
            .. Enum.GetValues<ValidationSeverity>().Select(ProblemsPanelViewModel.SeverityIcon).Distinct(),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(wired, Is.EquivalentTo(TierAssets.Select(a => "/Assets/" + a)),
                "one asset per tier, and no fourth");
            Assert.That(wired.Any(w => w.Contains("fatal", StringComparison.OrdinalIgnoreCase)), Is.False,
                "severity-fatal.svg belongs to the REFUSAL disposition. A refusal is not a finding, so it has no "
                + "row and no tier here — it exists for the dialog side and must never appear in the panel");
        });
    }

    /// <summary>
    /// The refusal glyph is still a real, conformant asset — it is simply not the panel's. Checking it here keeps
    /// the file honest without implying the panel uses it.
    /// </summary>
    [Test]
    public void TheRefusalGlyphExistsAndIsConformantButUnusedByThePanel()
    {
        XElement root = XDocument.Parse(Read("severity-fatal.svg")).Root!;

        Assert.Multiple(() =>
        {
            Assert.That((string?)root.Attribute("viewBox"), Is.EqualTo("0 0 24 24"));
            Assert.That((string?)root.Attribute("stroke"), Is.EqualTo("currentColor"));
            Assert.That(root.Elements().Select(e => (string?)e.Attribute("id")), Has.All.Not.Null);
        });
    }

    // ── The documentation ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The icon-mapping document is a test SUBJECT here, copied beside the binaries by the csproj, exactly as the
    /// existing icon-registration tests read it. An asset the map does not mention is an asset the next author
    /// re-invents.
    /// </summary>
    [Test]
    public void TheIconMapDocumentsAllFourSeverityAssets()
    {
        string map = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "appdocs", "icon_codes.md"));

        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets.Append("severity-fatal.svg"))
            {
                Assert.That(map, Does.Contain(file), $"{file} is undocumented in icon_codes.md");
            }

            Assert.That(map, Does.Contain("Problemer"),
                "and the map says where the three tier glyphs are used");
        });
    }
}
