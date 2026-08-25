#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The five remaining PROJECT-STRUCTURE rows: an empty room, a room with logic but no hardware, a product
    /// nothing can be wired to, a block nothing reaches, and an element left with no icon where others have one.
    ///
    /// <para><b><c>struct-modified-stale</c> is the sixth and is deliberately absent.</b> The <c>modified</c> block
    /// is re-stamped on every save and no edit route touches it, so the condition cannot hold in a saved file — a
    /// check would fire on nothing. T011 moved it to the catalogue's deliberate non-findings, and a test asserts no
    /// rule is registered for it.</para>
    ///
    /// <para><b>Two of the five are quiet by construction and say why.</b> <c>struct-product-no-terminals</c> asks
    /// for a product with NOTHING wirable — no terminal, no channel, no bus resource — which across the corpus is
    /// the SMS modem and nothing else; a naive "no <c>dataline_*</c> child" reading would also report every RS485
    /// dimmer and every bus sensor, whose channels and measured values are exactly what gets wired. And
    /// <c>struct-icon-default</c> reports an element whose icon is the format's null token where another element of
    /// the same tag carries a real one — no committed project contains that, which is what its ⊘ mark means.</para>
    /// </summary>
    public static class ProjectStructureRules
    {
        /// <summary>The tag every locality carries.</summary>
        private const string LocalityTag = "group";

        /// <summary>The attribute holding an element's icon, and the token that means "none".</summary>
        private const string IconAttribute = "icon";

        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "struct-locality-empty", LocalityEmpty),
                Rule(catalog, "struct-locality-no-devices", LocalityWithoutDevices),
                Rule(catalog, "struct-product-no-terminals", ProductWithoutTerminals),
                Rule(catalog, "struct-orphan-block", OrphanBlock),
                Rule(catalog, "struct-icon-default", IconLeftDefault));
        }

        /// <summary>
        /// A locality holding neither a product nor a block: an empty room in the tree and in the reports.
        /// <para>MEASURED, and the number is worth knowing before reading a report: 8 to 10 per project, because a
        /// new project ships with ten named localities and an installer fills the ones the building has. The row is
        /// still true — those rooms are empty in the tree and in the reports, and deleting them is what a careful
        /// author does before handing documentation over — which is exactly what its "room planned but not yet
        /// fitted" disagreement is for.</para>
        /// </summary>
        private static void LocalityEmpty(IProjectInspection inspection)
        {
            foreach (ProjectElement locality in Localities(inspection.Analyses))
            {
                if (!Devices(locality).Any() && !Blocks(locality).Any())
                {
                    inspection.Report(locality, Arguments(("locality", Name(locality))));
                }
            }
        }

        /// <summary>
        /// A locality holding blocks and no hardware: the room has logic but nothing to act on — often a mis-drop.
        /// </summary>
        private static void LocalityWithoutDevices(IProjectInspection inspection)
        {
            foreach (ProjectElement locality in Localities(inspection.Analyses))
            {
                if (Blocks(locality).Any() && !Devices(locality).Any())
                {
                    inspection.Report(locality, Arguments(("locality", Name(locality))));
                }
            }
        }

        /// <summary>
        /// A product with nothing wirable on it at all: nothing on the product can be connected.
        /// <para>
        /// WIRABLE IS WIDER THAN "TERMINAL", and the measurement is why: an RS485 LED dimmer exposes
        /// <c>rs485_led_dimmer_channel</c> children and a bus sensor exposes <c>resource_*</c> measurements, and
        /// both are what an author wires. Counting only <c>dataline_*</c>/<c>airlink_*</c> children reports every
        /// dimmer and every sensor in the corpus; counting anything wirable reports the SMS modem and nothing else,
        /// which is the witness the error fixture was built to carry.
        /// </para>
        /// </summary>
        private static void ProductWithoutTerminals(IProjectInspection inspection)
        {
            foreach (ProjectElement product in inspection.Analyses.Elements
                .Where(e => ProductClassifier.IsProduct(e.Tag)))
            {
                if (!product.Children.Any(c => IsWirable(c.Tag)))
                {
                    inspection.Report(product, Arguments(("product", Name(product))));
                }
            }
        }

        /// <summary>
        /// A block nothing links to and nothing references: it is isolated from the rest of the installation.
        /// <para>
        /// TWO WAYS TO BE REACHED, and both are checked: a link half anywhere inside the block (the half exists
        /// only once the wire is made), or an id inside the block named by an attribute OUTSIDE it — a program in
        /// another block, a scene, a documentation reference.
        /// </para>
        /// <para>
        /// MEASURED: 0 in <c>Project1</c>, 1 in <c>project5</c>, 8 of 9 blocks in <c>project3</c>. That last figure
        /// is not a false positive: <c>project3</c> carries only three wired pin pairs in total, so its library
        /// blocks really were placed for the report fixtures and never wired.
        /// </para>
        /// </summary>
        private static void OrphanBlock(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            ImmutableArray<ProjectElement> blocks =
            [
                .. inspection.Analyses.WithTag("functionblock"),
            ];

            // id -> the block that owns it, for every id inside a block
            Dictionary<string, ProjectElement> owner = new(StringComparer.Ordinal);
            foreach (ProjectElement block in blocks)
            {
                foreach (ProjectElement element in block.DescendantsAndSelf())
                {
                    if (element.GetAttribute("id") is { Length: > 0 } id)
                    {
                        owner[id] = block;
                    }
                }
            }

            HashSet<ProjectElement> reached = new(ReferenceEqualityComparer.Instance);
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                ProjectElement? host = topology.NearestAncestorOrSelf(element, "functionblock");
                if (ReciprocalTags.CrossBoundaryHalfTags.Contains(element.Tag) && host is not null)
                {
                    reached.Add(host);   // the block is wired
                }

                foreach ((string name, string value) in element.Attrs)
                {
                    if (name != "id" && owner.TryGetValue(value, out ProjectElement? target)
                        && !ReferenceEquals(target, host))
                    {
                        reached.Add(target);   // something outside the block names something inside it
                    }
                }
            }

            foreach (ProjectElement block in blocks.Where(b => !reached.Contains(b)))
            {
                inspection.Report(block, Arguments(("block", Name(block))));
            }
        }

        /// <summary>
        /// An element left with the format's null icon where other elements of the same kind carry a real one.
        /// <para>
        /// THE DEFAULT IS THE NULL TOKEN, from the DTD itself: <c>icon CDATA "_0x0"</c> on nearly every element, and
        /// the canonicalizer omits an attribute equal to its default — so "left with the default icon" is "carries
        /// <c>_0x0</c>, or carries none at all".
        /// </para>
        /// <para>
        /// THE CONTRAST IS WHAT "OTHERWISE CHOSEN" MEANS: another element of the SAME TAG carries a real icon.
        /// Without it the row would report every element of a kind the format never gives an icon to. RECLASSIFIED
        /// (⊘): no element-properties dialog in the application carries an icon picker, so this state arrives by
        /// hand-editing or import — and no committed project contains it.
        /// </para>
        /// <para>SUBJECT: the elements a person authors and reads back — see <see cref="AuthoredElements"/>. A
        /// container's icon and a program operand's are furniture.</para>
        /// </summary>
        private static void IconLeftDefault(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            ImmutableArray<ProjectElement> authored =
            [
                .. inspection.Analyses.Elements
                    .Where(e => AuthoredElements.IsAuthored(e, topology)),
            ];
            HashSet<string> tagsWithChosenIcons =
            [
                .. authored.Where(e => HasIcon(e)).Select(e => e.Tag),
            ];

            foreach (ProjectElement element in authored)
            {
                if (!HasIcon(element) && tagsWithChosenIcons.Contains(element.Tag))
                {
                    inspection.Report(element, Arguments(("element", Name(element))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>Whether the element carries a real icon, as opposed to the DTD's null default.</summary>
        private static bool HasIcon(ProjectElement element) =>
            element.GetAttribute(IconAttribute) is { Length: > 0 } icon && icon != ElementId.NullToken;

        /// <summary>Whether a product member is something an author wires: a terminal, a channel, or a bus value.</summary>
        private static bool IsWirable(string tag) =>
            AuthoredElements.IsTerminal(tag)
            || tag.StartsWith("resource_", StringComparison.Ordinal)
            || tag.EndsWith("_channel", StringComparison.Ordinal);

        private static IEnumerable<ProjectElement> Localities(IProjectAnalyses analyses) =>
            analyses.WithTag(LocalityTag);

        private static IEnumerable<ProjectElement> Devices(ProjectElement locality) =>
            locality.Children.Where(c => ProductClassifier.IsProduct(c.Tag));

        private static IEnumerable<ProjectElement> Blocks(ProjectElement locality) =>
            locality.Children.Where(c => c.Tag == "functionblock");
    }
}
