using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Svg.Skia;
using Ihc.Vis.Validation;
using ihc_openvisual.ViewModels;
using NUnit.Framework;
using SkiaSharp;
using Svg.Model;

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
    /// <summary>
    /// The four tiers' assets — every glyph the panel can show, and the whole family every check here
    /// judges. <c>severity-fatal.svg</c> joined it when Fatale fejl became a tier of its own; it was the
    /// refusal disposition's glyph before that, with no row to sit on.
    /// </summary>
    private static readonly string[] TierAssets =
        ["severity-fatal.svg", "severity-error.svg", "severity-warning.svg", "severity-info.svg"];

    /// <summary>
    /// The signal inks, and the ONLY two colour literals a severity glyph may pin.
    ///
    /// <para>These are the severity family's single documented exception to the checklist's currentColor-only rule
    /// (<c>icons_design.md</c> §5). They are values rather than tokens because an SVG file cannot reference an
    /// Avalonia resource; the pairing is kept honest by <see cref="TheSignalInksAreTheAppsOwnLightThemeTokens"/>
    /// below, which reads the literals back out of <c>App.axaml</c>.</para>
    /// </summary>
    private const string SeverityRed = "#B91C1C";
    private const string HeadingBlue = "#1E5AA8";

    /// <summary>
    /// Which shape in which glyph carries a signal ink — the artwork contract, stated once.
    ///
    /// <para>A shape absent from a file's map must stay <c>currentColor</c>, so this table pins BOTH halves: the
    /// mark that must be coloured and the surround that must keep following the theme ink. <c>severity-info.svg</c>
    /// is present with an EMPTY map on purpose — the advisory tier signals by shape alone, and an empty entry says
    /// that deliberately where a missing entry would read as an oversight.</para>
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> SignalInk = new()
    {
        // The cross is the failure; the ring stays theme ink so the glyph still reads in a dark theme.
        ["severity-error.svg"] = new() { ["cross-a"] = SeverityRed, ["cross-b"] = SeverityRed },
        // The bang is the advisory mark, in the Problemer heading's own blue; the triangle stays theme ink.
        ["severity-warning.svg"] = new() { ["bang-bar"] = HeadingBlue, ["bang-dot"] = HeadingBlue },
        ["severity-info.svg"] = [],
        // A refusal colours WHOLE — sign and cross together — because it is the one glyph that is not a row in a
        // list but a stop in a dialog, and it has no neighbouring tiers to stay distinguishable from.
        ["severity-fatal.svg"] = new()
        {
            ["stop-sign"] = SeverityRed, ["cross-a"] = SeverityRed, ["cross-b"] = SeverityRed,
        },
    };

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
    /// Two inks at most, and both of them declared: the caller's, and a signal one off the family's short palette.
    ///
    /// <para><b>Why this is not the checklist's flat currentColor-only rule.</b> A severity glyph says two things
    /// at once — WHICH mark (the shape) and HOW BAD (the ink) — and the mark that carries the badness is a
    /// sub-shape, not the whole glyph: the error ring stays theme ink while its cross goes red. One
    /// <c>CurrentColor</c> cannot express two inks, so for this family alone the signal colour is artwork. It stays
    /// bounded by being an ALLOW-LIST of two values rather than a licence to colour freely, and by
    /// <see cref="EachGlyphPinsItsSignalInkOnExactlyTheSignalShapes"/> pinning which shape may take which.</para>
    /// </summary>
    [Test]
    public void NothingPinsAnUndeclaredColourOrSmugglesInStyleOrText()
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

                Assert.That(svg, Does.Not.Contain("rgb("), file);

                foreach (Match paint in Regex.Matches(svg, @"(?:fill|stroke)=""([^""]*)"""))
                {
                    string value = paint.Groups[1].Value;
                    Assert.That(value, Is.AnyOf("none", "currentColor", SeverityRed, HeadingBlue),
                        $"{file}: paint '{value}' is neither the theme's ink nor one of the two declared signal "
                        + "inks — an undeclared colour cannot be reasoned about and never follows a theme");
                }
            }
        });
    }

    /// <summary>
    /// The artwork contract: exactly the signal shapes carry a signal ink, and everything else keeps following the
    /// theme. The second half matters as much as the first — a glyph coloured WHOLE would stop reading against a
    /// dark surface, and the ring/triangle is what keeps it visible there.
    /// </summary>
    [Test]
    public void EachGlyphPinsItsSignalInkOnExactlyTheSignalShapes()
    {
        Assert.Multiple(() =>
        {
            foreach (string file in TierAssets)
            {
                Dictionary<string, string> expected = SignalInk[file];

                foreach (XElement element in XDocument.Parse(Read(file)).Root!.Elements())
                {
                    string id = (string?)element.Attribute("id") ?? "";
                    // A shape paints through whichever of the two attributes it uses; the id says which ink it owes.
                    string paint = (string?)element.Attribute("stroke") is { } s && s != "none"
                        ? s
                        : (string?)element.Attribute("fill") ?? "currentColor";

                    if (expected.TryGetValue(id, out string? ink))
                    {
                        Assert.That(paint, Is.EqualTo(ink), $"{file}: '{id}' must carry the signal ink");
                    }
                    else
                    {
                        Assert.That(paint, Is.EqualTo("currentColor"),
                            $"{file}: '{id}' is not a signal shape, so it must keep following the theme ink — "
                            + "colouring it too would sink the glyph into a dark background");
                    }
                }
            }
        });
    }

    /// <summary>
    /// The two literals above are the app's OWN light-theme tokens, read back out of <c>App.axaml</c>. An SVG
    /// cannot reference an Avalonia resource, so the pairing is a copy — and an unwitnessed copy drifts. This is
    /// the witness: retune <c>ErrorBrush</c> or the pane-header blue and this fails until the artwork follows.
    /// </summary>
    [Test]
    public void TheSignalInksAreTheAppsOwnLightThemeTokens()
    {
        string app = File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..",
            "applications", "ihc_openvisual", "App.axaml"));

        Assert.Multiple(() =>
        {
            Assert.That(app, Does.Contain($@"x:Key=""ErrorBrush"" Color=""#FF{SeverityRed[1..]}"""),
                "the severity red is ErrorBrush's light-theme colour — the same red a refusing dialog writes in");
            Assert.That(app, Does.Contain($@"x:Key=""PaneHeaderBackgroundBrush"" Color=""#FF{HeadingBlue[1..]}"""),
                "the advisory blue is the Problemer heading's own brand blue, which is fixed in BOTH themes");
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
                    // Any fill at all, theme ink or signal ink — the rule is about the double-paint, not the colour.
                    if ((string?)element.Attribute("fill") is { } fill && fill != "none")
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

    // ── What the renderer actually draws ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The signal ink survives the app's runtime recolour and reaches the pixels — asserted on a RENDERED FRAME,
    /// not on the file and not on a property.
    ///
    /// <para><b>Why a pixel test earns its keep here and nowhere else in this file.</b> The panel hands the glyph
    /// its ink through <c>Svg.CurrentColor</c>. That contract only holds while the substitution touches the
    /// <c>currentColor</c> KEYWORD and leaves a pinned colour alone — and neither the SVG text nor the control's
    /// property can testify to that: a recolour that clobbered every paint would read back perfectly on both while
    /// drawing a uniformly grey glyph. <c>ToolbarDisabledAppearanceTests</c> records two occasions where exactly
    /// that gap bit this app, one of them a Svg.Skia release in which <c>currentColor</c> silently stopped
    /// resolving off-Windows while the property still read back correctly. This is the check that would see it.</para>
    ///
    /// <para>It also pins the half that keeps the family readable in a dark theme: the surround must still take the
    /// caller's ink, so a rendered tier glyph shows BOTH inks — and <c>severity-fatal.svg</c>, alone, shows only
    /// its own.</para>
    /// </summary>
    [Test]
    public void EverySignalInkSurvivesTheRuntimeRecolourAndReachesThePixels()
    {
        // Any ink distinct from both signal inks would do; the light theme's own is the honest choice.
        SKColor themeInk = SKColor.Parse("#303030");

        Assert.Multiple(() =>
        {
            foreach ((string file, Dictionary<string, string> signals) in SignalInk)
            {
                HashSet<SKColor> drawn = RenderGlyph(file, themeInk);

                foreach (string ink in signals.Values.Distinct())
                {
                    Assert.That(drawn, Does.Contain(SKColor.Parse(ink)),
                        $"{file}: the signal ink {ink} never reached a pixel — the runtime recolour has "
                        + "overwritten a pinned colour instead of only substituting the currentColor keyword");
                }

                bool colouredWhole = file == "severity-fatal.svg";
                Assert.That(drawn.Contains(themeInk), Is.EqualTo(!colouredWhole),
                    colouredWhole
                        ? $"{file} colours whole, so no part of it may still draw in the caller's ink"
                        : $"{file}: the surround must still take the caller's ink — that is what keeps the glyph "
                          + "visible on a dark surface, where a baked light-theme colour would sink");
            }
        });
    }

    /// <summary>The square the glyph is drawn into. A row shows it at 16; this is deliberately far larger so every
    /// stroke has an antialias-free interior to sample. It measures the INK, not the legibility.</summary>
    private const int RenderSize = 128;

    /// <summary>
    /// Renders one glyph with the app's ink supplied as <c>currentColor</c>, and returns every distinct colour in
    /// the result.
    ///
    /// <para>It goes through <see cref="SvgSource"/> and <see cref="SvgParameters"/> — the exact pair
    /// <c>Svg.CurrentColor</c> forwards to — rather than through a window, because the headless windowing needed
    /// to host the control renders only the first window shown in a test and would leave later glyphs blank
    /// (measured). Blank reads identically to a colour fault, so the shorter path is also the honest one; the
    /// picture assertion stands in front of the ink assertions to keep a load failure from ever wearing that
    /// costume. The asset's own TEXT is passed in, so this and the file checks above measure one artefact.</para>
    /// </summary>
    private static HashSet<SKColor> RenderGlyph(string file, SKColor currentColor)
    {
        var ink = System.Drawing.Color.FromArgb(currentColor.Red, currentColor.Green, currentColor.Blue);
        using SvgSource source = SvgSource.LoadFromSvg(Read(file), new SvgParameters(null, null, ink));

        SKPicture? picture = source.Picture;
        Assert.That(picture, Is.Not.Null,
            $"{file} did not load at all — every colour assertion on it would be a false alarm about the artwork");

        using var bitmap = new SKBitmap(new SKImageInfo(RenderSize, RenderSize));
        using (var canvas = new SKCanvas(bitmap))
        {
            // White, so an unpainted pixel is unmistakably neither ink.
            canvas.Clear(SKColors.White);
            SKRect box = picture!.CullRect;
            canvas.Scale(RenderSize / box.Width, RenderSize / box.Height);
            canvas.Translate(-box.Left, -box.Top);
            canvas.DrawPicture(picture);
        }

        HashSet<SKColor> distinct = [];
        for (int y = 0; y < RenderSize; y++)
        {
            for (int x = 0; x < RenderSize; x++)
            {
                distinct.Add(bitmap.GetPixel(x, y));
            }
        }

        return distinct;
    }

    // ── R8: what the panel actually wires ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// One asset per TIER, wired through the tier table. It used to enumerate severities and assert that the
    /// fatal glyph never appeared — correct while every tier was a severity, and doubly stale now: the panel
    /// lists a Fatale fejl tier that wires exactly that glyph, and enumerating severities would no longer
    /// reach every tier anyway, since two of them share one.
    /// <para>
    /// IN ORDER, against the asset list this fixture already drives its file loops from. Compared as a sequence
    /// rather than a set because the tier order is worst-first and the icon column reads down it, and because
    /// the expectation then comes from one declared list instead of a second copy of the four paths.
    /// </para>
    /// </summary>
    [Test]
    public void ThePanelWiresExactlyOneAssetPerTier()
    {
        string[] wired =
        [
            .. Enum.GetValues<ProblemsTier>().Select(ProblemsPanelViewModel.TierIcon),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(wired, Is.Unique, "two tiers sharing a glyph would make the icon column ambiguous");
            Assert.That(wired, Is.EqualTo(TierAssets.Select(a => "/Assets/" + a)).AsCollection,
                "every tier asset is wired, nothing else is, and in the tier order the panel lists in");
        });
    }

    // `severity-fatal.svg` had a test of its own here, asserting it was "conformant but unused by the panel".
    // The panel now WIRES it, as the tier gate above shows, so the claim in its name was false — and once the
    // glyph joined TierAssets its three assertions became a strict subset of the two loops above, which run
    // every root-attribute and every semantic-id check over all four assets. A renamed duplicate would have
    // been noise, so it is gone rather than restated.

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
            foreach (string file in TierAssets)
            {
                Assert.That(map, Does.Contain(file), $"{file} is undocumented in icon_codes.md");
            }

            Assert.That(map, Does.Contain("Problemer"),
                "and the map says where the four tier glyphs are used");
        });
    }
}
