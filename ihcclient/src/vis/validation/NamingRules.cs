#nullable enable
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
    /// The five NAMING rows: whether the things in a project can be told apart, in the reports and on site.
    ///
    /// <para><b>These are DOCUMENTATION-category rows, so they REACH THE USER</b> — the Fuld report's documentation
    /// appendix renders that category — which makes this the first content task whose findings move the committed
    /// report oracles instead of living only in the engine. Their templates are therefore fixed LABELS in the
    /// register the appendix already uses (<i>Mangler Kabelnummer</i>, <i>Dobbelt id</i>), not sentences.</para>
    ///
    /// <para><b>What a "template name" is, without a catalog.</b> A placed library block's name is
    /// <c>{master_type}.{master_version}. {master_name}</c> — every part of which the block itself carries — so a
    /// block still at its insert name can be recognised from the file alone, and one the installer renamed cannot
    /// be mistaken for it. No catalog lookup, and no guessing from an identifier.</para>
    ///
    /// <para><b>Blank is not this set's business outside <c>name-empty</c>.</b> The three duplicate rows skip a
    /// blank value: an absent identification code is <c>doc-documentation-tag</c>'s finding and an absent cable
    /// number is <c>doc-cablenumber</c>'s, so two blanks are not a collision — they are two missing fields, each
    /// already reported by its own row.</para>
    /// </summary>
    public static class NamingRules
    {
        /// <summary>The empty-function-block template's name, as the catalog's own <c>fb.def</c> writes it.</summary>
        private const string EmptyBlockTemplateName = "Tom blok";

        /// <summary>The name a new locality is given, which is the format's own placeholder.</summary>
        private const string NewLocalityName = "Lokalitet";

        /// <summary>The identification code a product carries in the documentation.</summary>
        private const string IdentificationCodeAttribute = "documentation_tag";

        /// <summary>The cable number a product or terminal carries.</summary>
        private const string CableNumberAttribute = "cablenumber";

        /// <summary>
        /// The elements a person NAMES and then reads back, from the shared reader — <c>struct-icon-default</c> asks
        /// the same population whether it has an ICON, so the two rows cannot disagree about which elements a person
        /// authors. See <see cref="AuthoredElements"/> for the two measured exclusions and what including them costs.
        /// </summary>
        private static bool IsNameable(ProjectElement element, ITopologyAnalysis topology) =>
            AuthoredElements.IsAuthored(element, topology);

        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "name-empty", Empty),
                Rule(catalog, "name-default", StillTemplate),
                Rule(catalog, "name-duplicate-siblings", DuplicateSiblings),
                Rule(catalog, "name-id-code-duplicate", DuplicateAttribute(
                    IdentificationCodeAttribute, ProductClassifier.IsProduct)),
                Rule(catalog, "name-cable-number-duplicate", DuplicateAttribute(
                    CableNumberAttribute,
                    tag => ProductClassifier.IsProduct(tag) || AuthoredElements.IsTerminal(tag))));
        }

        /// <summary>
        /// A nameable element with no name: it cannot be identified in a report or on site.
        /// <para>SUBJECT: the kinds a person names and reads back. EXCLUSION: the format's structural containers,
        /// which carry names nobody authors.</para>
        /// </summary>
        private static void Empty(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement element in inspection.Analyses.Elements
                .Where(e => IsNameable(e, topology)))
            {
                if (string.IsNullOrWhiteSpace(element.GetAttribute("name")))
                {
                    inspection.Report(element, default);
                }
            }
        }

        /// <summary>
        /// An element still carrying the name it was inserted with: the reports read as unfinished.
        /// <para>
        /// SUBJECT: localities named <c>Lokalitet</c>, and function blocks named either the empty-block template's
        /// name or their own <c>{master_type}.{master_version}. {master_name}</c> — which is what a library block is
        /// named at insert, reconstructed from the block's OWN attributes so no catalog is needed and a renamed
        /// block cannot be mistaken for one.
        /// </para>
        /// <para>
        /// NOT products: a product's insert name is its catalog display name, which the file does not carry, so
        /// there is no in-file evidence to read. Reporting them would need the catalog inside the validation pass,
        /// and guessing from the identifier would report renamed products too.
        /// </para>
        /// </summary>
        private static void StillTemplate(IProjectInspection inspection)
        {
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (element.GetAttribute("name") is not { Length: > 0 } name)
                {
                    continue;
                }

                bool untouched = element.Tag switch
                {
                    "group" => name == NewLocalityName,
                    "functionblock" => name == EmptyBlockTemplateName || name == InsertName(element),
                    _ => false,
                };

                if (untouched)
                {
                    inspection.Report(element, default);
                }
            }
        }

        /// <summary>
        /// Two siblings with one name: references in the reports and on site are ambiguous.
        /// <para>SUBJECT: nameable children of one parent — two localities in one container, two products in one
        /// locality, two pins on one block. ACROSS parents is not this row: two rooms may each hold a
        /// <i>Loftlampe</i>, and that is how installations are named. LOCATION: the second holder, with the first
        /// as a related location.</para>
        /// </summary>
        private static void DuplicateSiblings(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement parent in inspection.Analyses.Elements)
            {
                if (parent.Children.Length == 0)
                {
                    continue;   // a leaf has no siblings to collide; the map below would be allocated for nothing
                }

                Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
                foreach (ProjectElement child in parent.Children.Where(c => IsNameable(c, topology)))
                {
                    if (child.GetAttribute("name") is not { } name || string.IsNullOrWhiteSpace(name))
                    {
                        continue;   // a blank name is name-empty's finding, not a collision
                    }

                    if (seen.TryGetValue(name, out ProjectElement? first))
                    {
                        inspection.ReportGroup(child, [first], default);
                    }
                    else
                    {
                        seen[name] = child;
                    }
                }
            }
        }

        /// <summary>
        /// Two elements carrying one documentation value that is supposed to identify one of them.
        /// <para>SUBJECT: the elements the given attribute belongs to, across the WHOLE project — a code or a cable
        /// number identifies its holder documentation-wide, unlike a name, which only has to distinguish siblings.
        /// EXCLUSION: a blank value, which is the matching <c>doc-*</c> row's finding.</para>
        /// </summary>
        private static ProjectInspection DuplicateAttribute(string attribute, Func<string, bool> subject) =>
            inspection =>
            {
                Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
                foreach (ProjectElement element in inspection.Analyses.Elements
                    .Where(e => subject(e.Tag)))
                {
                    if (element.GetAttribute(attribute) is not { } value || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (seen.TryGetValue(value, out ProjectElement? first))
                    {
                        inspection.ReportGroup(element, [first], default);
                    }
                    else
                    {
                        seen[value] = element;
                    }
                }
            };

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// The name a library block carries at insert, from the shared reader. <c>logic-master-block-modified</c>
        /// asks the opposite question of the same three attributes, so the reconstruction lives in one place and
        /// the two rows cannot disagree about what an insert name is.
        /// </summary>
        private static string? InsertName(ProjectElement block) => LibraryBlockIdentity.InsertName(block);
    }
}
