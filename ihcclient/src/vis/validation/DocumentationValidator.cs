#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The eight US-072 documentation-completeness checks (R10): every wired dataline product under the
    /// project's localities and every terminal beneath it — reached by DESCENT, the same scope the report
    /// bodies use (U5/U8/U12), so the appendix that lists the documentation errors cannot omit ones the body
    /// documents in full. One <see cref="ValidationCategory.Documentation"/> finding per missing/blank
    /// documentation item. Five product-level checks (Id-kode, Lysgruppe, Kabeltype, Kabelnummer, Placering) and three
    /// terminal-level checks (not linked, Ledningsfarve, unparseable data-line address), each with a stable
    /// kebab-case rule id and its fixed Danish label as the message. All findings carry
    /// <see cref="ValidationSeverity.Warning"/> — documentation gaps are advisory and never block a
    /// save/upload. Order is deterministic: document-scan order of the subject element, then the fixed
    /// per-element check order above — the order the vendor's "Fejl i dokumentation" appendix witnesses.
    /// </summary>
    internal static class DocumentationValidator
    {
        /// <summary>A documentation finding paired with its subject element, so a consumer (the report
        /// builder) can resolve ancestry (locality/product/terminal cells) without re-running the checks.</summary>
        internal readonly record struct SubjectFinding(ProjectValidationFinding Finding, ProjectElement Subject);

        /// <summary>Runs the eight checks and returns the findings in the pinned deterministic order.</summary>
        public static ImmutableArray<ProjectValidationFinding> Check(Project project) =>
            CheckWithSubjects(project).Select(s => s.Finding).ToImmutableArray();

        /// <summary>Runs the eight checks, keeping each finding's subject element alongside it.</summary>
        public static ImmutableArray<SubjectFinding> CheckWithSubjects(Project project)
        {
            ArgumentNullException.ThrowIfNull(project);
            var findings = ImmutableArray.CreateBuilder<SubjectFinding>();
            void Add(string ruleId, ProjectElement subject, string label) =>
                findings.Add(new SubjectFinding(
                    new ProjectValidationFinding(ValidationSeverity.Warning, ruleId,
                        FindingCollector.Locate(subject), label)
                    { Category = ValidationCategory.Documentation },
                    subject));

            foreach (ProjectElement product in DatalineProducts(project))
            {
                void Product(string attribute, string ruleId, string label)
                {
                    if (Blank(product, attribute)) { Add(ruleId, product, label); }
                }
                Product("documentation_tag", "doc-documentation-tag", "Mangler Id-kode");
                Product("power_group", "doc-power-group", "Mangler Lysgruppe");
                Product("cabletype", "doc-cabletype", "Mangler Kabeltype");
                Product("cablenumber", "doc-cablenumber", "Mangler Kabelnummer");
                Product("position", "doc-position", "Mangler Placering");

                foreach (ProjectElement terminal in product.Descendants()
                    .Where(c => c.Tag is "dataline_input" or "dataline_output"))
                {
                    bool linked = terminal.Children.Any(c => c.Tag is ReciprocalTags.FollowLinkFromTag or ReciprocalTags.FollowLinkToTag);
                    if (!linked) { Add("doc-not-linked", terminal, "Ikke forbundet"); }
                    if (Blank(terminal, "cable_colour")) { Add("doc-cable-colour", terminal, "Mangler Ledningsfarve"); }
                    if (!DatalineAddress.TryParse(terminal.GetAttribute("address_dataline"), terminal.Tag == "dataline_output", out _))
                    {
                        Add("doc-address", terminal, "Mangler Adresse");
                    }
                }
            }
            return findings.ToImmutable();
        }

        /// <summary>
        /// Every wired dataline product under the project's localities, in document order. This is the report
        /// BODY's scope, which the checks used to undercut on both axes: only top-level groups counted as
        /// localities (U5 flattens nested ones) and only a group's DIRECT product children were visited (U8/U12
        /// reach a product by descent). A single descendant scan visits each product exactly once — what
        /// per-locality iteration achieves only by nearest-group ownership — and its order is document order,
        /// which is this validator's pinned finding order.
        /// </summary>
        private static IEnumerable<ProjectElement> DatalineProducts(Project project) =>
            project.Child("groups") is { } groups
                ? groups.Descendants().Where(e => e.Tag == "product_dataline")
                : Enumerable.Empty<ProjectElement>();

        private static bool Blank(ProjectElement element, string attribute) =>
            string.IsNullOrWhiteSpace(element.GetAttribute(attribute));
    }
}
