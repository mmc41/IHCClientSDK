#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
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
            foreach ((ProjectValidationFinding finding, ProjectElement subject) in DocumentationValidator.CheckWithSubjects(project))
            {
                bool terminalLevel = subject.Tag is "dataline_input" or "dataline_output";
                ProjectElement? product = terminalLevel ? index.Parent(subject) : subject;
                ProjectElement? locality = product is null ? null : index.Parent(product);
                rows.Add(ImmutableArray.Create<ReportCell>(
                    ReportText.SingleLine(locality?.GetAttribute("name")),
                    ReportText.SingleLine(product?.GetAttribute("name")),
                    terminalLevel ? ReportText.SingleLine(subject.GetAttribute("name")) : string.Empty,
                    finding.Message));
            }
            return rows.ToImmutable();
        }
    }
}
