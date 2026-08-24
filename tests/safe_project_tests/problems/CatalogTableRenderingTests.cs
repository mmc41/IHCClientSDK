using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The catalogue's human-readable index is rendered FROM the declarations and compared against the
    /// checked-in copy, so published documentation cannot fall behind the code.
    ///
    /// <para><b>Generation runs in this direction on purpose.</b> The markdown was the bootstrap source once and
    /// is not the authority: it is a self-declared unconfirmed draft, and pointing a build gate at it would have
    /// made the draft authoritative over compiled declarations. Reversing the arrow keeps the
    /// pinned-artifact-with-a-gate benefit this repository already earns from its public-API baseline, and points
    /// it at the OUTPUT instead of the input.</para>
    ///
    /// <para><b>What is rendered, and what deliberately is not.</b> The index carries the fields the code owns —
    /// id, category, disposition, kind, status, Danish label. It does not carry the draft's evidence and
    /// rationale columns ("why it may matter", "why it may be fine"): those are prose, they live as doc-comments
    /// on the declarations where they are reviewable in the same diff, and fourteen unread string fields across
    /// 145 entries existing only to fill columns in a generated table is exactly the shape that was cut from the
    /// design. So the hand-written sections of the catalogue stay hand-written, and this index is the part that
    /// can never drift.</para>
    ///
    /// <para>The renderer lives here rather than in the SDK. Nothing but a test ever consumed it, and a docs
    /// writer, a generated-file record and a drift report in public API for a single test's benefit is API
    /// nobody asked for.</para>
    /// </summary>
    [TestFixture]
    public sealed class CatalogTableRenderingTests
    {
        private const string BeginMarker = "<!-- GENERATED: catalogue index — rendered from the declarations; do not edit by hand -->";
        private const string CountsBeginMarker = "<!-- GENERATED: category counts — rendered from the declarations; do not edit by hand -->";
        private const string EndMarker = "<!-- END GENERATED -->";

        private static string CataloguePath =>
            Path.Combine(TestRepository.RequireRoot(), "ihcclient", "docs", "problem-catalogue.md");

        [Test]
        public void TheRenderedIndexMatchesTheCheckedInCopy()
        {
            string expected = Render(ProblemCatalog.Current);
            string actual = ReadGeneratedRegion(File.ReadAllText(CataloguePath), BeginMarker);

            Assert.That(actual, Is.EqualTo(expected),
                "the checked-in catalogue index is stale — run the [Explicit] Regenerate_TheCatalogueIndex test "
                + "and review the diff, which is the list of governance changes this change makes");
        }

        /// <summary>
        /// §1's per-category counts, on the same footing as the index. They were hand-maintained, and by the time
        /// this gate was added they disagreed with the declarations in six of the eight rows and in every total —
        /// including one row that the very changeset adding this test had moved. A count is not prose; it is a
        /// fact about the declarations, so it is rendered rather than retyped.
        /// </summary>
        [Test]
        public void TheRenderedCategoryTableMatchesTheCheckedInCopy()
        {
            string expected = RenderCategoryCounts(ProblemCatalog.Current);
            string actual = ReadGeneratedRegion(File.ReadAllText(CataloguePath), CountsBeginMarker);

            Assert.That(actual, Is.EqualTo(expected),
                "the checked-in category counts are stale — run the [Explicit] Regenerate_TheCategoryTable test "
                + "and review the diff");
        }

        /// <summary>
        /// The standing constraint on generation: it must not become the ONLY way to build a problem, or the open
        /// host vocabulary closes by the back door. A host with no generated factory of its own still constructs
        /// one by hand, and nothing consults the catalogue to allow it.
        /// </summary>
        [Test]
        public void GenerationIsNotTheOnlyWayToBuildAProblem()
        {
            ProblemCode ungoverned = ProblemCode.Parse("app.openvisual.something-new");
            Problem built = new(ungoverned, "Noget gik galt", EquatableArray<ProblemArgument>.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(ProblemCatalog.Current.TryGet(ungoverned, out _), Is.False,
                    "the code is in no catalogue, SDK or host");
                Assert.That(built.Code, Is.EqualTo(ungoverned), "and it was constructed anyway");
            });
        }

        /// <summary>
        /// Regenerates the checked-in index. <see cref="ExplicitAttribute"/> so it never runs in the gate: a test
        /// that rewrites the artifact it compares against would pass unconditionally.
        /// </summary>
        [Test]
        [Explicit("Rewrites the checked-in catalogue index; run deliberately and review the diff.")]
        public void Regenerate_TheCatalogueIndex()
        {
            Rewrite(BeginMarker, Render(ProblemCatalog.Current), "catalogue index");
        }

        /// <summary>Regenerates §1's counts. <see cref="ExplicitAttribute"/> for the same reason as its sibling.</summary>
        [Test]
        [Explicit("Rewrites the checked-in category counts; run deliberately and review the diff.")]
        public void Regenerate_TheCategoryTable()
        {
            Rewrite(CountsBeginMarker, RenderCategoryCounts(ProblemCatalog.Current), "category counts");
        }

        private static void Rewrite(string beginMarker, string rendered, string what)
        {
            string path = CataloguePath;
            string document = File.ReadAllText(path);

            int begin = document.IndexOf(beginMarker, StringComparison.Ordinal);
            string updated = begin < 0
                ? document.TrimEnd('\n') + "\n\n" + beginMarker + "\n" + rendered + "\n" + EndMarker + "\n"
                : document[..begin] + beginMarker + "\n" + rendered + "\n"
                  + document[document.IndexOf(EndMarker, begin, StringComparison.Ordinal)..];

            File.WriteAllText(path, updated, new UTF8Encoding(false));
            TestContext.Out.WriteLine($"rewrote the {what} in {path}");
        }

        private static string ReadGeneratedRegion(string document, string beginMarker)
        {
            int begin = document.IndexOf(beginMarker, StringComparison.Ordinal);
            Assert.That(begin, Is.GreaterThanOrEqualTo(0),
                $"the catalogue has no generated region; expected the marker {beginMarker}");

            int body = begin + beginMarker.Length;
            int end = document.IndexOf(EndMarker, body, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(body), "the generated region has no end marker");

            return document[body..end].Trim('\n', '\r');
        }

        /// <summary>
        /// The counts table. The <b>Fatal error</b> column is <see cref="CatalogDisposition.Refusal"/> under the
        /// name §2 already gives it — the code has no <c>Fatal</c> disposition and inventing one so a table could
        /// keep its heading would be the document dictating the model.
        /// </summary>
        private static string RenderCategoryCounts(ProblemCatalog catalog)
        {
            StringBuilder page = new();
            page.Append("\nThe **Fatal error** column is the `Refusal` disposition under the name §2 gives it; the code has\n");
            page.Append("no `Fatal` value. Only CATEGORISED entries are counted, so this total is smaller than the\n");
            page.Append("catalogue's own: the operation-outcome heads carry no category, by design.\n\n");
            page.Append("| Code | Fatal error | Error | Warning | Total |\n");
            page.Append("| --- | --- | --- | --- | --- |\n");

            int fatal = 0, error = 0, warning = 0;
            foreach (ValidationCategory category in Enum.GetValues<ValidationCategory>())
            {
                IReadOnlyList<ProblemCatalogEntry> rows = [.. catalog.Entries.Where(e => e.Category == category)];
                int refusals = rows.Count(e => e.Disposition == CatalogDisposition.Refusal);
                int errors = rows.Count(e => e.Disposition == CatalogDisposition.Error);
                int warnings = rows.Count(e => e.Disposition == CatalogDisposition.Warning);
                fatal += refusals;
                error += errors;
                warning += warnings;

                page.Append($"| **{category.ShortCode}** | {refusals} | {errors} | {warnings} | {rows.Count} |\n");
            }

            page.Append($"| **Total** | **{fatal}** | **{error}** | **{warning}** | **{fatal + error + warning}** |\n");
            return page.ToString().Trim('\n');
        }

        private static string Render(ProblemCatalog catalog)
        {
            StringBuilder page = new();
            page.Append("\n## Appendix — catalogue index, generated from the declarations\n\n");
            page.Append("Every governed code, as the code itself declares it. This section is RENDERED from\n");
            page.Append("`ihcclient/src/vis/validation/ProblemCatalogEntries.*.cs` and compared by a test, so it cannot\n");
            page.Append("fall behind the declarations. Edit the declarations, not this table.\n\n");
            page.Append("The evidence and rationale columns of the sections above are deliberately absent here: they are\n");
            page.Append("prose, and they live as doc-comments on each declaration.\n");

            foreach (ProblemCatalogSection section in Enum.GetValues<ProblemCatalogSection>())
            {
                IReadOnlyList<ProblemCatalogEntry> rows =
                    [.. catalog.Entries.Where(e => e.Section == section).OrderBy(e => e.Code.Value, StringComparer.Ordinal)];
                if (rows.Count == 0)
                {
                    continue;
                }

                page.Append($"\n### {Title(section)} ({rows.Count})\n\n");
                page.Append("| Id | Cat | Costs | Kind | Status | Danish label |\n");
                page.Append("| --- | --- | --- | --- | --- | --- |\n");
                foreach (ProblemCatalogEntry row in rows)
                {
                    page.Append($"| `{row.Code.Value}` | {(row.Category?.ShortCode is { Length: > 0 } c ? c : "—")} ");
                    page.Append($"| {row.Disposition} | {row.Kind} | {row.Status} ");
                    page.Append($"| {(row.MessageTemplate.Length == 0 ? "*(to author)*" : row.MessageTemplate)} |\n");
                }
            }

            page.Append($"\n**Total: {catalog.Total} entries.** ");
            page.Append($"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.Active)} active, ");
            page.Append($"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.Retired)} retired, ");
            page.Append($"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.RuledOut)} ruled out.\n");
            return page.ToString().Trim('\n');
        }

        private static string Title(ProblemCatalogSection section) => section switch
        {
            ProblemCatalogSection.ProjectFindings => "Project findings",
            ProblemCatalogSection.CatalogDefinitionFindings => "Catalog-definition findings",
            ProblemCatalogSection.OperationOutcomes => "Operation outcomes",
            _ => section.ToString(),
        };
    }
}
