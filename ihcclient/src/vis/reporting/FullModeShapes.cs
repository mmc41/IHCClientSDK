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
                Display(project.Child("project_info")?.GetAttribute("programmer")));

        public static KeyValueBlockShape ProjektBlock(Project project)
        {
            ProjectElement? info = project.Child("project_info");
            return new KeyValueBlockShape("Projekt", ImmutableArray.Create(
                    new KeyValueRow("Beskrivelse", Display(info?.GetAttribute("description"))),
                    new KeyValueRow("Nummer", Display(info?.GetAttribute("number"))),
                    new KeyValueRow("Programmør", Display(info?.GetAttribute("programmer")))),
                KeyValueStyle.Meta,
                ReportMembership.FullOnly);
        }

        /// <summary>The appendix pair: the section break and the four-column findings table.</summary>
        public static IEnumerable<ReportShape> FindingsAppendix(Project project, TreeIndex index)
        {
            yield return new SectionBreakShape(AppendixHeading, ReportMembership.FullOnly);
            yield return new TableShape(
                Heading: null,
                ImmutableArray.Create("Lokalitet", "Produkt", "Terminal", "Fejl"),
                FindingRows(project, index)
                    .Select(r => ImmutableArray.Create<ReportCell>(r.Locality, r.Product, r.Terminal, r.Problem))
                    .ToImmutableArray(),
                TableStyle.Plain,
                ReportMembership.FullOnly);
        }

        /// <summary>
        /// The appendix rows (R10 mapping): one per Documentation finding in the verification API's pinned
        /// order; Lokalitet/Produkt from the subject's ancestry, Terminal only for terminal-level findings,
        /// Fejl = the check's fixed Danish label. Also feeds the legacy combined report's completeness list
        /// until that surface retires.
        /// </summary>
        public static ImmutableArray<(string Locality, string Product, string Terminal, string Problem)> FindingRows(
            Project project, TreeIndex index)
        {
            var rows = ImmutableArray.CreateBuilder<(string, string, string, string)>();
            foreach ((ProjectValidationFinding finding, ProjectElement subject) in DocumentationValidator.CheckWithSubjects(project))
            {
                bool terminalLevel = subject.Tag is "dataline_input" or "dataline_output";
                ProjectElement? product = terminalLevel ? index.Parent(subject) : subject;
                ProjectElement? locality = product is null ? null : index.Parent(product);
                rows.Add((
                    ReportText.SingleLine(locality?.GetAttribute("name")),
                    ReportText.SingleLine(product?.GetAttribute("name")),
                    terminalLevel ? ReportText.SingleLine(subject.GetAttribute("name")) : string.Empty,
                    finding.Message));
            }
            return rows.ToImmutable();
        }

        private static string Display(string? value) => ReportText.Display(value);
    }
}
