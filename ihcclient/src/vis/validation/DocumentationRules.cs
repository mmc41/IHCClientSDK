#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The DOCUMENTATION-COMPLETENESS rules: five about a data-line product (identification code, power
    /// group, cable type, cable number, position) and three about each of its terminals (not linked, cable colour,
    /// unreadable address).
    /// <para>
    /// THESE ARE THE ONLY RULES THAT ALREADY REACH A USER, through the full report's documentation appendix,
    /// and they are the only ones whose message needs no translation: their labels have always been the short
    /// Danish phrases the appendix prints. Every other migrated rule moves an English sentence into its
    /// diagnostic; these have no English sentence to move, and changing a single character of one would move
    /// every byte-pinned report oracle that renders the appendix.
    /// </para>
    /// <para>
    /// SCOPE IS BY DESCENT, matching the report BODY's scope rather than a narrower one. The checks once counted
    /// only top-level groups as localities and only a group's DIRECT product children, so the appendix listing the
    /// documentation errors could omit products the body documented in full. A single descendant scan visits each
    /// product exactly once, which is also what makes the order document order.
    /// </para>
    /// <para>
    /// All eight are WARNINGS. A documentation gap is advisory: the installation works, the paperwork is
    /// incomplete, and only the person doing the commissioning can say whether that matters yet.
    /// </para>
    /// </summary>
    public static class DocumentationRules
    {
        /// <summary>
        /// The five product-level checks, in the order the vendor's documentation appendix witnesses them. The
        /// ORDER is a report-parity fact rather than an engine one, and it is declared here because this is where
        /// the checks are: a renderer that must reproduce it reads this, rather than re-deriving it.
        /// </summary>
        public static EquatableArray<ProblemCode> ProductChecksInReportOrder { get; } =
            ImmutableArray.Create(
                new ProblemCode("doc-documentation-tag"),
                new ProblemCode("doc-power-group"),
                new ProblemCode("doc-cabletype"),
                new ProblemCode("doc-cablenumber"),
                new ProblemCode("doc-position"));

        /// <summary>The three terminal-level checks, in the order the appendix witnesses them.</summary>
        public static EquatableArray<ProblemCode> TerminalChecksInReportOrder { get; } =
            ImmutableArray.Create(
                new ProblemCode("doc-not-linked"),
                new ProblemCode("doc-cable-colour"),
                new ProblemCode("doc-address"));

        private static readonly ImmutableDictionary<string, string> ProductAttributes =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["doc-documentation-tag"] = "documentation_tag",
                ["doc-power-group"] = "power_group",
                ["doc-cabletype"] = "cabletype",
                ["doc-cablenumber"] = "cablenumber",
                ["doc-position"] = "position",
            }.ToImmutableDictionary(StringComparer.Ordinal);

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ImmutableArray<RuleDefinition>.Builder rules = ImmutableArray.CreateBuilder<RuleDefinition>();
            foreach (ProblemCode code in ProductChecksInReportOrder)
            {
                string attribute = ProductAttributes[code.Value];
                rules.Add(Rule(catalog, code, inspection =>
                {
                    foreach (ProjectElement product in DatalineProducts(inspection))
                    {
                        if (IsBlank(product, attribute))
                        {
                            inspection.Report(product, EquatableArray<ProblemArgument>.Empty);
                        }
                    }
                }));
            }

            rules.Add(Rule(catalog, new ProblemCode("doc-not-linked"), inspection =>
                Terminals(inspection, terminal => !terminal.Children.Any(c =>
                    c.Tag is ReciprocalTags.FollowLinkFromTag or ReciprocalTags.FollowLinkToTag))));

            rules.Add(Rule(catalog, new ProblemCode("doc-cable-colour"), inspection =>
                Terminals(inspection, terminal => IsBlank(terminal, "cable_colour"))));

            rules.Add(Rule(catalog, new ProblemCode("doc-address"), inspection =>
                Terminals(inspection, terminal => !DatalineAddress.TryParse(
                    terminal.GetAttribute("address_dataline"), terminal.Tag == "dataline_output", out _))));

            return rules.ToImmutable();
        }

        private static void Terminals(IProjectInspection inspection, Func<ProjectElement, bool> incomplete)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement terminal in inspection.Analyses.Elements)
            {
                if (terminal.Tag is not ("dataline_input" or "dataline_output")
                    || topology.NearestAncestorOrSelf(terminal, "product_dataline") is not { } product
                    || !InLocalities(topology, product))
                {
                    continue;
                }

                if (incomplete(terminal))
                {
                    inspection.Report(terminal, EquatableArray<ProblemArgument>.Empty);
                }
            }
        }

        /// <summary>
        /// Every data-line product under the project's localities, in document order — the report body's scope,
        /// reached by descent so a nested locality or a product two containers down is still visited exactly once.
        /// <para>
        /// Off the shared analyses, not off a fresh subtree walk: all eight rules in this file ask for the same
        /// list, and this was the one module still opening its own <c>Descendants()</c> per rule.
        /// </para>
        /// </summary>
        private static IEnumerable<ProjectElement> DatalineProducts(IProjectInspection inspection) =>
            inspection.Analyses.WithTag("product_dataline")
                .Where(product => InLocalities(inspection.Analyses.Topology, product));

        /// <summary>Whether the element sits under the project's localities, which is the report body's scope.</summary>
        private static bool InLocalities(ITopologyAnalysis topology, ProjectElement element) =>
            topology.NearestAncestorOrSelf(element, "groups") is not null;

        /// <summary>
        /// Whitespace counts as blank. This is the one place in the engine where an empty-but-present attribute is
        /// a problem: the schema's notion of required is about the attribute existing, and a documentation field
        /// of three spaces satisfies that while telling a reader nothing.
        /// </summary>
        private static bool IsBlank(ProjectElement element, string attribute) =>
            string.IsNullOrWhiteSpace(element.GetAttribute(attribute));
    }
}
