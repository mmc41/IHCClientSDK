using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The remaining PROJECT-STRUCTURE rows: a product nothing can be wired to, and an element left with no icon
    /// where others of its kind have one.
    ///
    /// <para><b><c>struct-modified-stale</c> is deliberately absent.</b> The <c>modified</c> block
    /// is re-stamped on every save and no edit route touches it, so the condition cannot hold in a saved file — a
    /// check would fire on nothing. T011 moved it to the catalogue's deliberate non-findings, and a test asserts no
    /// rule is registered for it.</para>
    ///
    /// <para><b>Both are quiet by construction and say why.</b> <c>struct-product-no-terminals</c> asks
    /// for a product with NOTHING wirable — no terminal, no channel, no bus resource — which across the corpus is
    /// the SMS modem and nothing else; a naive "no <c>dataline_*</c> child" reading would also report every RS485
    /// dimmer and every bus sensor, whose channels and measured values are exactly what gets wired. And
    /// <c>struct-icon-default</c> reports an element whose icon is the format's null token where another element of
    /// the same tag carries a real one — no committed project contains that, which is what its ⊘ mark means.</para>
    /// </summary>
    public static class ProjectStructureRules
    {
        /// <summary>The attribute holding an element's icon, and the token that means "none".</summary>
        private const string IconAttribute = "icon";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "struct-product-no-terminals", ProductWithoutTerminals),
                Rule(catalog, "struct-icon-default", IconLeftDefault));
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
    }
}
