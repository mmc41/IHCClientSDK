#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The eight US-072 documentation-completeness checks (R10): per locality → wired dataline product →
    /// terminal, one <see cref="ValidationCategory.Documentation"/> finding per missing/blank documentation
    /// item. Five product-level checks (Id-kode, Lysgruppe, Kabeltype, Kabelnummer, Placering) and three
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

            foreach (ProjectElement group in project.Groups)
            {
                foreach (ProjectElement product in group.ChildrenOrEmpty().Where(c => c.Tag == "product_dataline"))
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

                    foreach (ProjectElement terminal in product.ChildrenOrEmpty().Where(c => c.Tag is "dataline_input" or "dataline_output"))
                    {
                        bool linked = terminal.ChildrenOrEmpty().Any(c => c.Tag is ReciprocalTags.FollowLinkFromTag or ReciprocalTags.FollowLinkToTag);
                        if (!linked) { Add("doc-not-linked", terminal, "Ikke forbundet"); }
                        if (Blank(terminal, "cable_colour")) { Add("doc-cable-colour", terminal, "Mangler Ledningsfarve"); }
                        if (!DatalineAddress.TryParse(terminal.GetAttribute("address_dataline"), terminal.Tag == "dataline_output", out _))
                        {
                            Add("doc-address", terminal, "Mangler Adresse");
                        }
                    }
                }
            }
            return findings.ToImmutable();
        }

        private static bool Blank(ProjectElement element, string attribute) =>
            string.IsNullOrWhiteSpace(element.GetAttribute(attribute));
    }
}
