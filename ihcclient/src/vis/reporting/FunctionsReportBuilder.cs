using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Reporting
{
    /// <summary>
    /// Builds the "Funktionsdokumentation" (functions / end-user) report as a shape document (spec R4):
    /// every locality as a top-level tree section in document order (U5 — nested localities flatten), its
    /// end-user-flagged product roots (A4; descendant scope with locality = nearest ancestor group, U12),
    /// each product's terminals located by descent — dataline inputs before outputs regardless of document
    /// order (A6), airlink inputs — and under each terminal one note row per wired link, the note read from the
    /// far half's FB pin (A5). A dangling link emits nothing; an empty note still emits its row (U2). Rows
    /// carry the Full-only id chips and the differing-locality note suffix (name EQUALITY, B9) — the mode
    /// filter strips both for Standard.
    /// </summary>
    internal static class FunctionsReportBuilder
    {
        private static readonly string Title = ReportTitles.For(ReportKind.Functions);

        public static ReportShapeDocument Build(Project project, DateTimeOffset generatedAt)
        {
            ArgumentNullException.ThrowIfNull(project);
            var index = new TreeIndex(project);
            var rows = ImmutableArray.CreateBuilder<ReportTreeRow>();

            foreach (ProjectElement locality in TreeIndex.Localities(project))
            {
                rows.Add(new NamedTreeRow(0, Name(locality), Detail: null, locality.GetAttribute("id")));
                foreach (ProjectElement product in EndUserProducts(project, locality))
                {
                    string? position = product.GetAttribute("position");
                    rows.Add(new NamedTreeRow(1, Name(product),
                        string.IsNullOrWhiteSpace(position) ? null : ReportText.SingleLine(position),
                        product.GetAttribute("id")));
                    foreach (ProjectElement terminal in Terminals(product))
                    {
                        rows.Add(new PlainTreeRow(2, Name(terminal), terminal.GetAttribute("id"))
                        {
                            Membership = TerminalMembership(terminal.Tag),
                        });
                        AddNoteRows(terminal, Name(locality), index, rows);
                    }
                }
            }

            var shapes = ImmutableArray.CreateBuilder<ReportShape>();
            shapes.Add(FullModeShapes.MetaLine(project, generatedAt));
            shapes.Add(FullModeShapes.ProjektBlock(project));
            shapes.Add(new TreeShape(rows.ToImmutable()));
            shapes.AddRange(FullModeShapes.FindingsAppendix(project, index));
            return new ReportShapeDocument(Title, shapes.ToImmutable());
        }

        // A4 + U12: end-user-flagged product roots anywhere under the locality whose NEAREST ancestor group
        // is this locality — a nested group's subtree belongs to that nested locality.
        private static IEnumerable<ProjectElement> EndUserProducts(Project project, ProjectElement locality)
        {
            foreach (ProjectElement child in locality.Children)
            {
                if (child.Tag == "group")
                {
                    continue;   // its own top-level locality (U5/U12 nearest-ancestor scope)
                }
                // Admission is by product ROOT, through the shared classifier — not an exact two-tag match,
                // which refused a flagged open-world product outright even though the installation report
                // documented its terminals in full (review G7(b)).
                if (ProductClassifier.IsProduct(child.Tag) && project.View(child).EnduserReport)
                {
                    yield return child;
                }
                foreach (ProjectElement nested in EndUserProducts(project, child))
                {
                    yield return nested;
                }
            }
        }

        // A6: dataline inputs always precede outputs; airlink products list their inputs (vendor scope).
        // Located by DESCENT (U8) — the same way the installation report locates them. Reading only the
        // direct children made the two reports answer differently about one tree: the vendor's own sensor
        // products nest their terminals inside a settings container, so the installation report documented a
        // terminal the end-user report silently dropped (review G6).
        // Which KIND of terminal a product carries is decided by the same classifier that admits it, for the
        // same reason: an exact `product_airlink` match would render a flagged open-world wireless product as
        // a bare name with no children — moving G7(b)'s contradiction one level down instead of closing it.
        private static IEnumerable<ProjectElement> Terminals(ProjectElement product)
        {
            IReadOnlyList<ProjectElement> descendants = product.Descendants();
            return ProductClassifier.IsWireless(product.Tag)
                ? descendants.Where(c => c.Tag.StartsWith(AirlinkTerminalPrefix, StringComparison.Ordinal))
                : descendants.Where(c => c.Tag == "dataline_input").Concat(descendants.Where(c => c.Tag == "dataline_output"));
        }

        /// <summary>The tag prefix every wireless terminal kind shares (input, relay, dimming, the dimmer
        /// pair and the three shutter controls) — an open set, so it is matched by prefix rather than by a
        /// list that a new catalog family would silently fall out of.</summary>
        private const string AirlinkTerminalPrefix = "airlink_";

        /// <summary>
        /// Which mode a terminal row belongs to. Standard is the vendor-parity surface (C-3) and the vendor's
        /// end-user report listed only the airlink INPUTS, so every other wireless terminal kind is content
        /// the vendor loses and lands in Full alone (RL-3). Their note rows follow at greater depth and need
        /// no tag of their own — <see cref="ReportModeFilter"/> drops a Full-only row with its whole subtree.
        /// </summary>
        private static ReportMembership TerminalMembership(string tag) =>
            tag.StartsWith(AirlinkTerminalPrefix, StringComparison.Ordinal) && tag != "airlink_input"
                ? ReportMembership.FullOnly
                : ReportMembership.Common;

        // A5/U2/B9: one note row per wired link, note = the far half's FB pin @note; dangling link → no row;
        // empty note → row with empty text. The Full-only suffix is the linked FB's locality name when it
        // differs from the product's locality by name equality (empty/no locality → no suffix).
        private static void AddNoteRows(ProjectElement terminal, string localityName, TreeIndex index,
            ImmutableArray<ReportTreeRow>.Builder rows)
        {
            foreach (ProjectElement target in index.LinkTargets(terminal))
            {
                ProjectElement? pin = index.Parent(target);
                string note = ReportText.SingleLine(pin?.GetAttribute("note"));
                string fbLocality = ReportText.SingleLine(pin is null ? null : index.LocalityName(pin));
                string? suffix = fbLocality.Length > 0 && fbLocality != localityName ? fbLocality : null;
                rows.Add(new NoteTreeRow(3, note, suffix));
            }
        }

        private static string Name(ProjectElement element) => ReportText.SingleLine(element.GetAttribute("name"));
    }
}
