#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Builds the "Funktionsdokumentation" (functions / end-user) report as a shape document (spec R4):
    /// every locality as a top-level tree section in document order (U5 — nested localities flatten), its
    /// end-user-flagged dataline/airlink products (A4; descendant scope with locality = nearest ancestor
    /// group, U12), each product's terminals — dataline inputs before outputs regardless of document order
    /// (A6), airlink inputs — and under each terminal one note row per wired link, the note read from the
    /// far half's FB pin (A5). A dangling link emits nothing; an empty note still emits its row (U2). Rows
    /// carry the Full-only id chips and the differing-locality note suffix (name EQUALITY, B9) — the mode
    /// filter strips both for Standard.
    /// </summary>
    internal static class FunctionsReportBuilder
    {
        private const string Title = "Funktionsdokumentation";

        public static ReportShapeDocument Build(Project project, DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project.Root);
            var rows = ImmutableArray.CreateBuilder<ReportTreeRow>();

            foreach (ProjectElement locality in Localities(project))
            {
                rows.Add(new NamedTreeRow(0, Name(locality), Detail: null, locality.GetAttribute("id")));
                foreach (ProjectElement product in EndUserProducts(locality))
                {
                    string? position = product.GetAttribute("position");
                    rows.Add(new NamedTreeRow(1, Name(product),
                        string.IsNullOrWhiteSpace(position) ? null : ReportText.SingleLine(position),
                        product.GetAttribute("id")));
                    foreach (ProjectElement terminal in Terminals(product))
                    {
                        rows.Add(new PlainTreeRow(2, Name(terminal), terminal.GetAttribute("id")));
                        AddNoteRows(terminal, Name(locality), index, rows);
                    }
                }
            }

            var shapes = ImmutableArray.CreateBuilder<ReportShape>();
            shapes.Add(FullModeShapes.MetaLine(project, generatedAt));
            shapes.Add(FullModeShapes.ProjektBlock(project));
            shapes.Add(new TreeShape(rows.ToImmutable()));
            shapes.AddRange(FullModeShapes.FindingsAppendix(project, index));
            return new ReportShapeDocument(ReportKind.Functions, Title, shapes.ToImmutable());
        }

        // U5: every group renders as a top-level locality in document order, nesting flattened.
        private static IEnumerable<ProjectElement> Localities(Project project) =>
            project.Child("groups") is { } groups
                ? groups.Descendants().Where(e => e.Tag == "group")
                : Enumerable.Empty<ProjectElement>();

        // A4 + U12: end-user-flagged dataline/airlink products anywhere under the locality whose NEAREST
        // ancestor group is this locality — a nested group's subtree belongs to that nested locality.
        private static IEnumerable<ProjectElement> EndUserProducts(ProjectElement locality)
        {
            foreach (ProjectElement child in locality.ChildrenOrEmpty())
            {
                if (child.Tag == "group")
                {
                    continue;   // its own top-level locality (U5/U12 nearest-ancestor scope)
                }
                if (child.Tag is "product_dataline" or "product_airlink" && child.GetAttribute("enduser_report") == "yes")
                {
                    yield return child;
                }
                foreach (ProjectElement nested in EndUserProducts(child))
                {
                    yield return nested;
                }
            }
        }

        // A6: dataline inputs always precede outputs; airlink products list their inputs (vendor scope).
        private static IEnumerable<ProjectElement> Terminals(ProjectElement product)
        {
            ImmutableArray<ProjectElement> children = product.ChildrenOrEmpty();
            return product.Tag == "product_airlink"
                ? children.Where(c => c.Tag == "airlink_input")
                : children.Where(c => c.Tag == "dataline_input").Concat(children.Where(c => c.Tag == "dataline_output"));
        }

        // A5/U2/B9: one note row per wired link, note = the far half's FB pin @note; dangling link → no row;
        // empty note → row with empty text. The Full-only suffix is the linked FB's locality name when it
        // differs from the product's locality by name equality (empty/no locality → no suffix).
        private static void AddNoteRows(ProjectElement terminal, string localityName, TreeIndex index,
            ImmutableArray<ReportTreeRow>.Builder rows)
        {
            string linkTag = terminal.Tag == "dataline_output" ? "link_to_resource" : "link_from_resource";
            foreach (ProjectElement linkRow in terminal.ChildrenOrEmpty().Where(c => c.Tag == linkTag))
            {
                if (index.ById(linkRow.GetAttribute("link")) is not { } target)
                {
                    continue;   // dangling IDREF — the vendor's id(@link) empty node set (U2)
                }
                ProjectElement? pin = index.Parent(target);
                string note = ReportText.SingleLine(pin?.GetAttribute("note"));
                string fbLocality = pin is { } p && index.NearestAncestorOrSelf(p, "group") is { } fbGroup
                    ? Name(fbGroup)
                    : string.Empty;
                string? suffix = fbLocality.Length > 0 && fbLocality != localityName ? fbLocality : null;
                rows.Add(new NoteTreeRow(3, note, suffix));
            }
        }

        private static string Name(ProjectElement element) => ReportText.SingleLine(element.GetAttribute("name"));
    }
}
