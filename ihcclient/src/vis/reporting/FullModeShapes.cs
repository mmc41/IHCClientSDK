#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// The four COMMON Full-mode additions every report kind shares (spec R4): the report-meta line, the
    /// Projekt identity block, and the "Fejl i dokumentation" appendix (section break + findings table) fed
    /// by the unified verification API's Documentation findings — one row per finding in the pinned check
    /// order, Lokalitet/Produkt/Terminal resolved from the subject element's ancestry at build time, blank
    /// non-applicable cells (R10). The id chips (the fourth addition) live on the tree/table rows
    /// themselves. Built by every report builder; the mode filter drops them for Standard.
    /// </summary>
    internal static class FullModeShapes
    {
        /// <summary>The report generation-timestamp format: <c>yyyy-MM-dd HH:mm</c>, invariant.</summary>
        private const string GeneratedAtFormat = "yyyy-MM-dd HH:mm";

        private const string AppendixHeading = "Fejl i dokumentation";

        public static MetaLineShape MetaLine(Project project, DateTimeOffset generatedAt) =>
            new(generatedAt.ToString(GeneratedAtFormat, CultureInfo.InvariantCulture),
                ReportText.Display(project.Programmer));

        public static KeyValueBlockShape ProjektBlock(Project project) =>
            new("Projekt", ImmutableArray.Create(
                    new KeyValueRow("Beskrivelse", ReportText.Display(project.Description)),
                    new KeyValueRow("Nummer", ReportText.Display(project.ProjectNumber)),
                    new KeyValueRow("Programmør", ReportText.Display(project.Programmer))),
                KeyValueStyle.Meta,
                ReportMembership.FullOnly);

        /// <summary>The appendix pair: the section break and the four-column findings table.</summary>
        public static IEnumerable<ReportShape> FindingsAppendix(Project project, TreeIndex index)
        {
            yield return new SectionBreakShape(AppendixHeading, SectionBreakStyle.Flush, ReportMembership.FullOnly);
            yield return new TableShape(
                Heading: null,
                ImmutableArray.Create("Lokalitet", "Produkt", "Terminal", "Fejl"),
                FindingRows(project, index),
                TableStyle.Plain,
                ReportMembership.FullOnly);
        }

        /// <summary>
        /// The appendix rows (R10 mapping): one per Documentation finding in the verification API's pinned
        /// order; Lokalitet/Produkt from the subject's ancestry, Terminal only for terminal-level findings,
        /// Fejl = the check's fixed Danish label.
        /// </summary>
        private static ImmutableArray<ImmutableArray<ReportCell>> FindingRows(Project project, TreeIndex index)
        {
            var rows = ImmutableArray.CreateBuilder<ImmutableArray<ReportCell>>();
            foreach ((ValidationFinding finding, ProjectElement subject) in DocumentationFindings(project, index))
            {
                // Resolved by ANCESTRY, not by immediate parent: a terminal's product may be a container or
                // two above it (the vendor's sensors nest their terminals inside a settings container), and a
                // product's locality may be a nested group. Reading the immediate parent printed that
                // container as the Produkt and the real product as the Lokalitet — a wrong row, which is worse
                // than the missing one the narrow validator scope used to produce.
                bool terminalLevel = subject.Tag is "dataline_input" or "dataline_output";
                ProjectElement? product = terminalLevel ? index.NearestProduct(subject) : subject;
                ProjectElement? locality = product is null ? null : index.NearestAncestorOrSelf(product, "group");
                rows.Add(ImmutableArray.Create<ReportCell>(
                    ReportText.SingleLine(locality?.GetAttribute("name")),
                    ReportText.SingleLine(product?.GetAttribute("name")),
                    terminalLevel ? ReportText.SingleLine(subject.GetAttribute("name")) : string.Empty,
                    finding.Problem.Message));
            }
            return rows.ToImmutable();
        }

        /// <summary>
        /// The documentation findings of ONE engine run, paired with the element each is about and ordered the way
        /// the vendor appendix witnesses them.
        /// <para>
        /// The ORDER is the report's own, not the engine's. The engine orders by document position and then by
        /// code, which for the five product-level checks on one element is alphabetical and is NOT the sequence
        /// the vendor prints. The sequence is declared beside the checks themselves, and this reads it — so the
        /// appendix stays byte-identical without the engine carrying a rendering concern in its sort.
        /// </para>
        /// </summary>
        private static ImmutableArray<(ValidationFinding Finding, ProjectElement Subject)> DocumentationFindings(
            Project project, TreeIndex index)
        {
            ImmutableArray<ProblemCode> order =
                [.. DocumentationRules.ProductChecksInReportOrder, .. DocumentationRules.TerminalChecksInReportOrder];
            int RankOf(ValidationFinding finding) => order.IndexOf(finding.Code);

            var subjects = ImmutableArray.CreateBuilder<(ValidationFinding, ProjectElement, int)>();
            int arrival = 0;
            foreach (ValidationFinding finding in ProjectRules.Validator
                .Validate(project, ValidationProfile.Categorized)
                .Where(f => f.Category == ValidationCategory.Documentation))
            {
                if (Subject(project, index, finding) is { } subject)
                {
                    subjects.Add((finding, subject, arrival));
                }
                arrival++;
            }

            return
            [
                .. subjects
                    // Elements are records, so grouping "the findings about this element" must key by IDENTITY.
                    // The cast pins TKey: bare, the comparer's IEqualityComparer<object?> lets inference settle
                    // on TKey = object, which compiles and then surprises the first reader of group.Key.
                    .GroupBy(entry => entry.Item2, (IEqualityComparer<ProjectElement?>)ReferenceEqualityComparer.Instance)
                    .OrderBy(group => group.Min(entry => entry.Item3))
                    .SelectMany(group => group.OrderBy(entry => RankOf(entry.Item1)))
                    .Select(entry => (entry.Item1, entry.Item2)),
            ];
        }

        /// <summary>The element a documentation finding is about, resolved from its primary location.</summary>
        private static ProjectElement? Subject(Project project, TreeIndex index, ValidationFinding finding) =>
            finding.Primary?.Element is { } id ? project.FindById(id) : null;

    }
}
