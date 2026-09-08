using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using ihc_openvisual.ViewModels;
using Ihc;

namespace Ihc.Vis.Tests;

/// <summary>
/// Which way each arrow glyph points, measured off the artwork rather than trusted from its file name.
///
/// <para><b>Why the file name is not evidence.</b> The six arrow assets are named after the vendor ELEMENT they
/// stand for (<c>link_from_resource</c> → <c>link-from</c>), and "from" reads as an incoming direction in English
/// while naming the OUTGOING half in the schema: the <c>link_from_resource</c> row is the one the signal departs
/// from. Nothing in the key→asset mapping can catch a glyph drawn on the English reading, because the mapping is
/// right either way — only the artwork is wrong. So this fixture reads the coordinates.</para>
///
/// <para><b>The expected directions are the vendor's, read off IHC Visual's own tree.</b> A link row draws the
/// signal's travel with the row's own pin on the left and the labelled opposite end on the right: the source half
/// points AT the target (<c>→</c>), the sink half points back from it (<c>←</c>). Pin and section arrows keep the
/// separate, category-marker grammar of <c>icons_design.md</c> §4 (<c>→</c> = IN, <c>←</c> = OUT), which is why
/// both families are listed here — the two grammars are easy to conflate, and conflating them is exactly the
/// defect this fixture exists for.</para>
/// </summary>
public class ArrowIconDirectionTests
{
    private enum Pointing { Left, Right }

    /// <summary>
    /// Every arrow asset in the set, with the direction IHC Visual draws for the element it renders. Pins and
    /// sections are here as much as the links: they are the neighbours a link glyph gets confused with.
    /// </summary>
    private static readonly Dictionary<string, Pointing> ArrowGlyphs = new(StringComparer.Ordinal)
    {
        // §1 sections — the block's own perspective: the arrow enters the box for inputs, leaves it for outputs.
        ["section-input"] = Pointing.Right,
        ["section-output"] = Pointing.Left,
        // §3a pins — the same category grammar without the box.
        ["pin-in"] = Pointing.Right,
        ["pin-out"] = Pointing.Left,
        // §4 links — travel, not category: the half on the SOURCE points at the opposite end it names.
        ["link-from"] = Pointing.Right,
        ["link-to"] = Pointing.Left,
    };

    /// <summary>
    /// The arrow characters §7.1 draws on, each with the way it points. A stand-in outside this map is either not
    /// an arrow or one this fixture cannot judge; either is a finding, not a pass.
    /// </summary>
    private static readonly Dictionary<string, Pointing> StandInDirections = new(StringComparer.Ordinal)
    {
        ["→"] = Pointing.Right, ["⇢"] = Pointing.Right, ["⇥"] = Pointing.Right,
        ["←"] = Pointing.Left, ["⇠"] = Pointing.Left, ["⇤"] = Pointing.Left,
    };

    /// <summary>The horizontal centre line of the 24×24 canvas — the axis every arrow is drawn along.</summary>
    private const double Axis = 12;

    private static string AssetPath(string key) => Path.Combine(
        TestRepository.RequireRoot(), "applications", "ihc_openvisual", "Assets", key + ".svg");

    /// <summary>
    /// Which way a glyph points, read from TWO independent shapes and required to agree: the shaft's own
    /// <c>x1</c>→<c>x2</c> sense, and the side of the shaft's end on which the arrowhead's on-axis vertex sits.
    /// Reading only one of them would pass an arrow whose head was flipped against its shaft.
    /// </summary>
    private static Pointing DirectionOf(string key)
    {
        XElement root = XDocument.Load(AssetPath(key)).Root!;
        XElement shaft = Shape(root, "shaft", key);
        XElement head = Shape(root, "head", key);

        double x1 = Coordinate(shaft, "x1", key);
        double x2 = Coordinate(shaft, "x2", key);
        Assert.That(x2, Is.Not.EqualTo(x1), $"{key}.svg: the shaft is vertical, so the glyph has no direction to read");
        Pointing pointing = x2 > x1 ? Pointing.Right : Pointing.Left;

        double apex = ApexX(head, key);
        Assert.That(pointing == Pointing.Right ? apex >= x2 : apex <= x2, Is.True,
            $"{key}.svg: the arrowhead's tip (x={apex}) sits behind the shaft's end (x={x2}) — head and shaft "
            + "disagree about which way the arrow points");
        return pointing;
    }

    private static Pointing DirectionOfAsset(string? iconAsset)
    {
        Assert.That(iconAsset, Is.Not.Null.And.Not.Empty, "the row renders no icon at all");
        return DirectionOf(Path.GetFileNameWithoutExtension(iconAsset!));
    }

    private static XElement Shape(XElement root, string id, string key)
    {
        XElement[] matches = [.. root.Descendants().Where(e => (string?)e.Attribute("id") == id)];
        Assert.That(matches, Has.Length.EqualTo(1),
            $"{key}.svg: the semantic '{id}' shape icons_design.md requires is missing or duplicated");
        return matches[0];
    }

    private static double Coordinate(XElement shape, string name, string key)
    {
        string? raw = (string?)shape.Attribute(name);
        Assert.That(raw, Is.Not.Null, $"{key}.svg: the shaft carries no {name}");
        return double.Parse(raw!, CultureInfo.InvariantCulture);
    }

    /// <summary>The x of the arrowhead's tip: its single vertex on the canvas axis, whichever way the head is drawn.</summary>
    private static double ApexX(XElement head, string key)
    {
        string? raw = (string?)head.Attribute("points");
        Assert.That(raw, Is.Not.Null, $"{key}.svg: the arrowhead carries no points");
        double[] numbers = [.. raw!.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(n => double.Parse(n, CultureInfo.InvariantCulture))];
        Assert.That(numbers, Has.Length.EqualTo(6),
            $"{key}.svg: the arrowhead is not the three-point head the family draws");

        double[] onAxis = [.. Enumerable.Range(0, numbers.Length / 2)
            .Where(i => numbers[(2 * i) + 1] == Axis)
            .Select(i => numbers[2 * i])];
        Assert.That(onAxis, Has.Length.EqualTo(1),
            $"{key}.svg: the arrowhead must have exactly one vertex on the canvas axis (y={Axis}) — its tip");
        return onAxis[0];
    }

    [Test]
    public void EveryArrowGlyphPointsTheWayTheVendorDrawsIt()
    {
        Assert.Multiple(() =>
        {
            foreach ((string key, Pointing expected) in ArrowGlyphs)
            {
                Assert.That(DirectionOf(key), Is.EqualTo(expected), $"{key}.svg points the wrong way");
            }
        });
    }

    /// <summary>
    /// The text surfaces draw the same arrows from a different table (<see cref="DefaultReportIcons"/>), so a
    /// corrected asset with an uncorrected stand-in would leave a report contradicting the tree it describes.
    /// </summary>
    [Test]
    public void EveryArrowStandInPointsTheSameWayAsItsArtwork()
    {
        Assert.Multiple(() =>
        {
            foreach ((string key, Pointing expected) in ArrowGlyphs)
            {
                string standIn = DefaultReportIcons.StandInFor(key);
                Assert.That(StandInDirections.ContainsKey(standIn), Is.True,
                    $"'{standIn}' stands in for {key} but is not an arrow this fixture can read");
                Assert.That(StandInDirections[standIn], Is.EqualTo(expected),
                    $"the {key} stand-in '{standIn}' points against the {key}.svg artwork");
            }
        });
    }

    /// <summary>
    /// The sighting itself, on the corpus project it was seen in: every follow-link row the two panes project,
    /// with the direction its half is drawn with in IHC Visual. The screenshot pair that opened this was one row
    /// of each kind — <c>Tryk (venstre)</c>'s outgoing row in the Installation pane and the block input
    /// <c>Kip</c>'s incoming row in the Functions pane — so those two are also named individually.
    /// </summary>
    [Test]
    public async Task TheFollowLinkRowsOfProject1PointTheWayTheVendorDrawsThem()
    {
        Project project = await new ProjectAppService(new IhcSettings()).Load(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));
        var projector = new ProjectTreeProjector(project);
        TreeNodeViewModel installation = projector.BuildLocalitiesRoot(functions: false);
        TreeNodeViewModel functions = projector.BuildLocalitiesRoot(functions: true);

        TreeNodeViewModel[] linkRows = [.. TreeNodes.FindAll([installation, functions],
            n => n.Kind is TreeNodeKind.LinkFrom or TreeNodeKind.LinkTo)];

        Assert.Multiple(() =>
        {
            Assert.That(linkRows.Select(r => r.Kind).Distinct().Count(), Is.EqualTo(2),
                "the project must exercise BOTH link halves, or the sweep below proves only one direction");
            foreach (TreeNodeViewModel row in linkRows)
            {
                Pointing expected = row.Kind == TreeNodeKind.LinkFrom ? Pointing.Right : Pointing.Left;
                Assert.That(DirectionOfAsset(row.IconAsset), Is.EqualTo(expected),
                    $"the {row.Kind} row '{row.DisplayName}' draws its arrow the wrong way");
            }

            TreeNodeViewModel outgoing = Pin(installation, "Tryk (venstre)");
            Assert.That(DirectionOfAsset(outgoing.Children.Single().IconAsset), Is.EqualTo(Pointing.Right),
                "the button's row points AT the block input it names");
            TreeNodeViewModel incoming = Pin(functions, "Kip");
            Assert.That(DirectionOfAsset(incoming.Children.Single().IconAsset), Is.EqualTo(Pointing.Left),
                "the block input's row points back FROM the button it names");
        });
    }

    // The one LINKED pin of that name in the pane — the other block's identically named input carries no link row,
    // so the linked-ness is part of the address rather than a filter applied afterwards.
    private static TreeNodeViewModel Pin(TreeNodeViewModel root, string name) =>
        TreeNodes.FindAll([root], n => n.Kind == TreeNodeKind.Pin && n.DisplayName == name && n.Children.Count > 0)
            .Single();
}
