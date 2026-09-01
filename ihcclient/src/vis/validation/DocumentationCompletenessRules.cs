using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The remaining DOCUMENTATION rows, and what they have in common: each one names something the REPORTS
    /// cannot say. A pin with no note leaves the function report unable to describe the function; a blank masthead
    /// prints <c>--</c>; an unflagged installation produces an empty end-user report; two spellings of one light
    /// group split one physical circuit across two headings.
    ///
    /// <para><b>Two of the four are scoped by their own stated CONSEQUENCE, which D2 makes the specification.</b>
    /// <c>doc-project-info-blank</c> says <i>every</i> masthead renders <c>--</c>, and that is true only when all
    /// three metadata blocks are blank — the literal "project, customer OR installer" reading reports 15 of the 20
    /// corpus files, because the vendor leaves <c>customer_info</c> blank in almost all of them. And
    /// <c>doc-no-enduser-products</c> says the end-user report comes out EMPTY, which is only a fault where there
    /// was something to put in it: a project holding no products at all is not under-documented.</para>
    ///
    /// <para><b>The other two are witnessed and quiet.</b> Every library block's inputs carry the vendor's own
    /// notes (32 of 32 in <c>project3</c>), so <c>name-note-missing</c> reports hand-authored blocks and nothing
    /// else; and one light group in the whole corpus is spelled two ways, in the error fixture, on purpose.</para>
    /// </summary>
    public static class DocumentationCompletenessRules
    {
        /// <summary>The attribute a product's light group is stored in.</summary>
        private const string LightGroupAttribute = "power_group";

        /// <summary>The attribute carrying a pin's descriptive note — the report's own note column.</summary>
        private const string NoteAttribute = "note";

        /// <summary>The flag marking a product for the end-user report.</summary>
        private const string EnduserReportAttribute = "enduser_report";

        private const string Marked = "yes";

        /// <summary>The three id-less root metadata blocks the report mastheads are rendered from.</summary>
        private static readonly ImmutableArray<string> MastheadBlocks =
            ["project_info", "customer_info", "installer_info"];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "name-power-group-variant", LightGroupSpellingVariants),
                Rule(catalog, "name-note-missing", InputNoteMissing),
                Rule(catalog, "doc-project-info-blank", MastheadsBlank),
                Rule(catalog, "doc-no-enduser-products", NoEnduserProducts));
        }

        /// <summary>
        /// Two spellings of one light group: the reports group one physical circuit under two headings.
        /// <para>
        /// SUBJECT: products carrying a light group. NORMALISATION: trimmed, inner whitespace collapsed, case
        /// folded invariantly — the three ways a re-typed group name differs from the first one without meaning
        /// anything different. <c>Stue</c> and <c>stue</c> collide; <c>Stue</c> and <c>Stuen</c> do not, because
        /// that is a different word and the row's own disagreement column allows deliberately distinct names.
        /// </para>
        /// <para>LOCATION: each product whose spelling differs from the FIRST one seen for that group, which is
        /// the set to re-type. The first spelling is not reported: it is not wrong, it is just first.</para>
        /// </summary>
        private static void LightGroupSpellingVariants(IProjectInspection inspection)
        {
            Dictionary<string, string> firstSpelling = new(StringComparer.Ordinal);
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                if (product.GetAttribute(LightGroupAttribute) is not { } group || string.IsNullOrWhiteSpace(group))
                {
                    continue;   // a missing light group is doc-power-group's finding
                }

                string key = Normalize(group);
                if (!firstSpelling.TryGetValue(key, out string? first))
                {
                    firstSpelling[key] = group;
                }
                else if (!string.Equals(first, group, StringComparison.Ordinal))
                {
                    inspection.Report(product, default);
                }
            }
        }

        /// <summary>
        /// A function-block input with no note: the function report has nothing to describe the function with.
        /// <para>
        /// SUBJECT: a <c>resource_input</c> declared in a block's <c>inputs</c> container — an INPUT, as the row
        /// says, and one the report actually prints a note column for. Outputs, settings and internal variables are
        /// not this row.
        /// </para>
        /// <para>
        /// MEASURED: every library block ships the vendor's notes on its pins (32 of 32 across <c>project3</c>), so
        /// this reports hand-authored blocks — which is where the missing description actually is.
        /// </para>
        /// </summary>
        private static void InputNoteMissing(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement pin in inspection.Analyses.WithTag("resource_input"))
            {
                if (topology.Parent(pin) is { Tag: "inputs" }
                    && string.IsNullOrWhiteSpace(pin.GetAttribute(NoteAttribute)))
                {
                    inspection.Report(pin, default);
                }
            }
        }

        /// <summary>
        /// No masthead information at all: every report masthead renders <c>--</c>.
        /// <para>
        /// PREDICATE, and why it is ALL THREE rather than the row's literal "or": the row's stated consequence is
        /// that EVERY masthead renders the placeholder, which happens only when none of the three blocks carries
        /// anything. The literal reading reports 15 of the 20 corpus files, because the vendor leaves
        /// <c>customer_info</c> entirely blank in nearly every one — an installer's own project without customer
        /// details is the ordinary state, and the row's own disagreement column says so ("internal project never
        /// handed over").
        /// </para>
        /// <para>ONE FINDING for the project, located at the root: there is no element to navigate to, because a
        /// missing block is missing.</para>
        /// </summary>
        private static void MastheadsBlank(IProjectInspection inspection)
        {
            bool anyFilled = MastheadBlocks
                .Select(inspection.Project.Root.FindChild)
                .OfType<ProjectElement>()
                .Any(block => block.Attrs.Any(a => !string.IsNullOrWhiteSpace(a.Value)));

            if (!anyFilled)
            {
                inspection.Report(inspection.Project.Root, default);
            }
        }

        /// <summary>
        /// A project whose products are all kept out of the end-user report: that report comes out empty.
        /// <para>
        /// GUARD, from the row's own consequence: the project must HOLD products. An empty report is only a fault
        /// where there was something to put in it — a project with no products yet is not under-documented, and
        /// three corpus files are exactly that.
        /// </para>
        /// <para>
        /// WHY NO AUTHENTIC FILE WITNESSES THIS: the catalogue records that IHC Visual writes
        /// <c>enduser_report="yes"</c> on every shutter product at insert and no dialog clears it, so any project
        /// carrying a shutter carries a flagged product. The state is reachable — just not in a fixture that also
        /// witnesses the shutter rows.
        /// </para>
        /// </summary>
        private static void NoEnduserProducts(IProjectInspection inspection)
        {
            ImmutableArray<ProjectElement> products = [.. AllProducts(inspection.Analyses)];
            if (products.Length > 0
                && !products.Any(p => p.GetAttribute(EnduserReportAttribute) == Marked))
            {
                inspection.Report(inspection.Project.Root, default);
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// The one spelling two light-group values share when they differ only in case or spacing: trimmed, inner
        /// runs of whitespace collapsed to one space, folded to lower case invariantly.
        /// </summary>
        private static string Normalize(string value)
        {
            string[] words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', words).ToLowerInvariant();
        }
    }
}
