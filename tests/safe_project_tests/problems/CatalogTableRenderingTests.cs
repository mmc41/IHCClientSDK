using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
        /// Both gates above compare a rendered region — always LF — against the file as the WORKING TREE holds
        /// it, and that is the checkout's business, not the catalogue's: a clone made with
        /// <c>core.autocrlf=true</c>, which is the Windows CI runner's default, hands back every line with a CR
        /// the declarations never rendered. Without normalization the two gates report "the checked-in copy is
        /// stale" on a fresh Windows clone and pass on the checkout whose own regeneration wrote the file — a
        /// verdict about git configuration wearing the words of a governance failure.
        /// </summary>
        [Test]
        public void TheGatesReadTheSameRegionWhicheverLineEndingsTheCheckoutHas()
        {
            string document = File.ReadAllText(CataloguePath);
            string crlf = document.Replace("\r\n", "\n").Replace("\n", "\r\n");

            Assert.Multiple(() =>
            {
                Assert.That(ReadGeneratedRegion(crlf, BeginMarker),
                    Is.EqualTo(ReadGeneratedRegion(document, BeginMarker)), "the catalogue index");
                Assert.That(ReadGeneratedRegion(crlf, CountsBeginMarker),
                    Is.EqualTo(ReadGeneratedRegion(document, CountsBeginMarker)), "the category counts");
            });
        }

        /// <summary>
        /// §4's <b>Blocks</b> column, on the same footing as the two tables above: rendered from each row's
        /// <see cref="ProblemCatalogEntry.RefusedOperations"/> and compared against the checked-in cells.
        ///
        /// <para><b>Why this column and not the whole section.</b> §4's other columns are prose — the finding,
        /// why it matters, the reclassification notes — and stay hand-written for the reason the index's
        /// doc-comment gives. Blocks is not prose: it is a fact about the declarations, and it is the one cell in
        /// the section that has drifted from them more than once.</para>
        ///
        /// <para><b>The renderer publishes labels, not heads — one label per head, and no head without one.</b>
        /// The map was briefly narrower than the declaration, and that was a mistake worth recording: with
        /// <c>edit.open</c> rendering as nothing, <c>id-duplicate-token</c> published <c>—</c> here while the
        /// panel listed it as fatal and the export wrote <c>blocks="edit.open"</c> — one declaration, three
        /// published answers, which is the ambiguity generating the column was meant to END. A view may be
        /// briefer than what it renders; it may not be unable to express one of six enumerated values.</para>
        ///
        /// <para>The two controller directions carry their own words for the same reason. Publishing
        /// <c>bridge.upload</c> as "Export" put one word on two unrelated operations three rows apart in one
        /// table, beside <c>io.save</c>'s "Save · Export" — a reader could not tell a file write from a
        /// controller transfer.</para>
        /// </summary>
        [Test]
        public void TheBlocksColumnMatchesWhatTheRowsDeclare()
        {
            ImmutableArray<(string Id, string Blocks)> published =
                [.. SectionFourRows.Select(row => (row.Id, row.Blocks))];
            ImmutableArray<(string Id, string Blocks)> expected =
                [.. published.Select(row => (row.Id, BlocksCell(row.Id)))];

            Assert.Multiple(() =>
            {
                // The expected side is DERIVED from the published side, so an empty parse would satisfy the
                // comparison rather than fail it. A change to the table's markup that stopped the reader
                // matching would then read as "no drift" — the one way this gate can go quietly meaningless.
                Assert.That(published, Is.Not.Empty, "§4's rows could not be read at all");

                Assert.That(published, Is.EqualTo(expected).AsCollection,
                    "problem-catalogue.md §4's Blocks column no longer matches the declarations — run the "
                    + "[Explicit] Regenerate_SectionFoursGeneratedCells test and explain every cell the diff moves");
            });
        }

        /// <summary>
        /// §4's <b>Severity</b> column, generated on the same footing as Blocks beside it — and for the same
        /// reason. It is a fact about the declarations, not prose, and it is the cell that has drifted from them
        /// most: <c>root-version</c> published "Fatal error | Open" for a row that refuses nothing, and
        /// <c>attr-required</c> published "Error | —" for a row that has always refused the save.
        ///
        /// <para><b>The rule this encodes is §2's, not a new one.</b> §4's <i>Fatal error</i> wording is about
        /// the FILE LIFECYCLE and the two controller transfers: the operation cannot be carried through, so
        /// nothing is opened, written or sent. A row refusing only <c>edit.open</c> is deliberately NOT worded
        /// that way here — the file opens, saves and uploads perfectly well, and only the editor declines it, so
        /// the section calls it an Error and names the refusal in Blocks. That is why
        /// <c>id-duplicate-token</c> reads "Error | Edit-open" and is nonetheless a Fatal ROW in the panel:
        /// the two views ask different questions, and each answers its own.</para>
        ///
        /// <para><b>The panel is not this.</b> Its tier is <c>Severity == Error &amp;&amp; refuses something</c>,
        /// which includes the edit-open row. Generating this column does not unify the two — it makes the
        /// difference DERIVED and reviewable instead of retyped.</para>
        /// </summary>
        [Test]
        public void TheSeverityColumnMatchesWhatTheRowsDeclare()
        {
            ImmutableArray<(string Id, string Severity)> published =
                [.. SectionFourRows.Select(row => (row.Id, row.Severity))];
            ImmutableArray<(string Id, string Severity)> expected =
                [.. published.Select(row => (row.Id, SeverityCell(row.Id)))];

            Assert.Multiple(() =>
            {
                // Same non-vacuity guard as the Blocks gate, for the same reason: both sides are derived from
                // one parse, so a reader that stopped matching would read as agreement.
                Assert.That(published, Is.Not.Empty, "§4's rows could not be read at all");

                Assert.That(published, Is.EqualTo(expected).AsCollection,
                    "problem-catalogue.md §4's Severity column no longer matches the declarations — run the "
                    + "[Explicit] Regenerate_SectionFoursGeneratedCells test and explain every cell the diff moves");
            });
        }

        /// <summary>
        /// A row that refuses a PUBLISHED operation is published. The comparison above holds every §4 row's cell
        /// to its declaration, but says nothing about a row that has left §4 altogether — both of its sides come
        /// from the same parsed table, so a deleted row takes its own assertion with it.
        /// <para>
        /// Derived from the declarations rather than pinned as a count, so it needs no hand-maintained number and
        /// covers exactly what this column is for: a fatal row cannot vanish from the published table unnoticed.
        /// Every head now has a label, so the exemption this once carried — a row refusing only
        /// <c>edit.open</c>, with nothing to publish — no longer applies to anything.
        /// </para>
        /// <para>
        /// A <see cref="ProblemCodeStatus.RuledOut"/> row is excluded, and that is a statement about §4 rather
        /// than a loophole: the section documents the conditions a user can MEET, and a ruled-out row describes
        /// one nothing reports. <c>load-truncated</c> is the case — it names <c>io.load</c> because that is the
        /// operation its condition would stop, while §6 records why the condition is never separately decided.
        /// Publishing it would put a row in the fatal table that no file can ever produce.
        /// </para>
        /// </summary>
        [Test]
        public void EveryRowThatRefusesAPublishedOperationAppearsInSectionFour()
        {
            IEnumerable<string> shouldBePublished = ProblemCatalog.Current.Entries
                .Where(e => e.Status != ProblemCodeStatus.RuledOut)
                .Where(e => e.RefusedOperations.Any(op => PublishedAs(op) is not null))
                .Select(e => e.Code.Value);

            Assert.That(SectionFourRows.Select(row => row.Id), Is.SupersetOf(shouldBePublished),
                "a row declaring a refusal §4 has a word for is missing from that section");
        }

        /// <summary>
        /// The published vocabulary is closed, and EVERY head has a word in it. The subset half stops a
        /// generated column inventing documentation; the total half stops it going quietly lossy, which is how
        /// one declaration came to have three published answers.
        /// </summary>
        [Test]
        public void EveryOperationHeadHasExactlyOnePublishedLabel()
        {
            string?[] labels = [.. OperationCodes.All.Select(PublishedAs)];

            Assert.Multiple(() =>
            {
                Assert.That(labels, Has.None.Null,
                    "a head with no label cannot be published, so §4 would silently under-report the row");
                Assert.That(labels, Is.Unique,
                    "two heads sharing a word make the column ambiguous — a reader cannot tell which operation");
                // Split first: a row refusing two operations renders one cell holding both words, so the cell
                // is a list of labels rather than a label.
                Assert.That(
                    SectionFourRows
                        .SelectMany(row => row.Blocks.Split(", ", StringSplitOptions.None))
                        .Distinct(),
                    Is.SubsetOf([.. labels, NoRefusal]),
                    "§4 publishes a word the head map does not produce");
            });
        }

        /// <summary>
        /// §7's first MUST, read back off the table: "A <b>Fatal error</b> aborts the operation, naming which one
        /// was refused". A row published as Fatal with an empty Blocks cell breaks that requirement on the page.
        /// <para>
        /// The pairing is only checkable now that the cell is rendered rather than typed, and generating it
        /// produced exactly one: <c>root-version</c> was published as Fatal while declaring no refusal, so its
        /// Severity cell was the wrong half. The declaration says <c>Error</c>, and it now reads Error.
        /// </para>
        /// </summary>
        [Test]
        public void EveryRowPublishedAsFatalNamesTheOperationItRefuses()
        {
            Assert.Multiple(() =>
            {
                foreach ((string id, string severity, string blocks) in SectionFourRows)
                {
                    if (!string.Equals(severity, Fatal, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.That(blocks, Is.Not.EqualTo(NoRefusal),
                        $"{id} is published as a Fatal error but names no refused operation, which §7 "
                        + "requires. Either the row declares a refusal it does not, or its Severity cell does");
                }
            });
        }

        /// <summary>
        /// Rewrites BOTH of §4's generated cells in place — Severity and Blocks, in one pass, because they are
        /// two halves of one statement and rewriting either alone can leave the pair contradicting §7.
        /// <see cref="ExplicitAttribute"/> for the same reason as its two siblings: a test that rewrites what it
        /// compares against passes unconditionally.
        /// </summary>
        [Test]
        [Explicit("Rewrites problem-catalogue.md §4's Severity and Blocks columns; run deliberately and review the diff.")]
        public void Regenerate_SectionFoursGeneratedCells()
        {
            string path = CataloguePath;
            string[] lines = File.ReadAllLines(path);
            int changed = 0;

            foreach (int index in SectionFourRowIndexes(lines))
            {
                string[] cells = SplitRow(lines[index]);
                string id = IdOf(cells);
                (int Column, string Rendered)[] generated = [(2, SeverityCell(id)), (3, BlocksCell(id))];
                bool moved = false;

                foreach ((int column, string rendered) in generated)
                {
                    if (string.Equals(cells[column], rendered, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    TestContext.Out.WriteLine($"  {id} [{column}]: '{cells[column]}' -> '{rendered}'");
                    cells[column] = rendered;
                    moved = true;
                }

                if (moved)
                {
                    lines[index] = "| " + string.Join(" | ", cells) + " |";
                    changed++;
                }
            }

            File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
            TestContext.Out.WriteLine($"rewrote {changed} §4 row(s) in {path}");
        }

        /// <summary>U+2014 EM DASH — the cell §4 uses for a row that refuses nothing.</summary>
        private const string NoRefusal = "—";

        /// <summary>§4's word for a row whose condition stops the operation outright.</summary>
        private const string Fatal = "Fatal error";

        /// <summary>
        /// One row's Severity cell, as §4 words it — derived from the row's disposition and, for the Error rows,
        /// from WHICH operations it refuses.
        /// <para>
        /// The <c>edit.open</c> exclusion is the whole subtlety and it is §2's rule, not this renderer's: §4
        /// reserves <i>Fatal error</i> for a condition that stops the file lifecycle or a controller transfer.
        /// A row that only stops the EDITOR leaves the file openable, savable and sendable, so the section words
        /// it Error and lets Blocks name the refusal.
        /// </para>
        /// </summary>
        private static string SeverityCell(string id)
        {
            if (!ProblemCatalog.Current.TryGet(new ProblemCode(id), out ProblemCatalogEntry entry))
            {
                Assert.Fail($"§4 publishes '{id}', which no catalogue entry declares");
            }

            bool stopsMoreThanTheEditor =
                entry.RefusedOperations.Any(op => op.Value != OperationCodes.EditOpen.Value);

            return entry.Disposition switch
            {
                CatalogDisposition.Refusal => Fatal,
                CatalogDisposition.Error when stopsMoreThanTheEditor => Fatal,
                CatalogDisposition.Error => "Error",
                CatalogDisposition.Warning => "Warning",
                CatalogDisposition.Info => "Information",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(id), entry.Disposition, "Unknown disposition"),
            };
        }

        /// <summary>
        /// One row's Blocks cell, as §4 words it. The order is the section's own: the file lifecycle read
        /// left to right, so a row refusing two operations reads the same way every time.
        /// </summary>
        private static string BlocksCell(string id)
        {
            if (!ProblemCatalog.Current.TryGet(new ProblemCode(id), out ProblemCatalogEntry entry))
            {
                Assert.Fail($"§4 publishes '{id}', which no catalogue entry declares");
            }

            // Head order, not alphabetical: the file lifecycle first, then the edit boundary, then the two
            // transfers — so a row refusing two operations reads the same way every time.
            string[] labels =
            [
                .. OperationCodes.All
                    .Where(head => entry.RefusedOperations.Any(op => op.Value == head.Value))
                    .Select(head => PublishedAs(head)!),
            ];

            return labels.Length == 0 ? NoRefusal : string.Join(", ", labels);
        }

        /// <summary>
        /// How §4 words each operation head — one word per head, all six, none shared.
        /// <para>
        /// <c>Download</c> and <c>Upload</c> rather than the file words <c>Import</c> and <c>Export</c>: the
        /// controller directions are not file operations, they are the two ends of a transfer, and §4's own
        /// finding text for them already says "download" and "uploaded". <c>Edit-open</c> is the phrase the
        /// entries' own doc-comments use ("also refused at edit-open").
        /// </para>
        /// <para>
        /// A head with no word returns null and fails <see cref="EveryOperationHeadHasExactlyOnePublishedLabel"/>
        /// rather than silently publishing an em dash, so adding a seventh head is a decision about how the
        /// section words it, not something a renderer settles by omission.
        /// </para>
        /// </summary>
        private static string? PublishedAs(ProblemCode operation) => operation.Value switch
        {
            "io.load" => "Open",
            "io.save" => "Save · Export",
            "edit.open" => "Edit-open",
            "import.catalog" => "Import",
            "bridge.download" => "Download",
            "bridge.upload" => "Upload",
            _ => null,
        };

        /// <summary>
        /// §4's rows, parsed once: the code, the Severity cell and the Blocks cell. The page is a checked-in
        /// file that no test writes while one is reading, so the four gates over it share one parse rather
        /// than re-reading and re-splitting it apiece.
        /// </summary>
        private static ImmutableArray<(string Id, string Severity, string Blocks)> SectionFourRows =>
            LazySectionFourRows.Value;

        private static readonly Lazy<ImmutableArray<(string Id, string Severity, string Blocks)>>
            LazySectionFourRows = new(() =>
            {
                string[] lines = File.ReadAllLines(CataloguePath);
                return
                [
                    .. SectionFourRowIndexes(lines)
                        .Select(index => SplitRow(lines[index]))
                        .Select(cells => (IdOf(cells), cells[2], cells[3])),
                ];
            });

        private static IEnumerable<int> SectionFourRowIndexes(string[] lines)
        {
            int start = Array.FindIndex(lines, l => l.StartsWith("## 4. ", StringComparison.Ordinal));
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the catalogue has no §4 heading");

            int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("## 5. ", StringComparison.Ordinal));
            Assert.That(end, Is.GreaterThan(start), "§4 has no following section, so its table has no end");

            return Enumerable.Range(start, end - start)
                .Where(i => lines[i].StartsWith("| `", StringComparison.Ordinal));
        }

        private static string[] SplitRow(string line) =>
            [.. line.Trim().Trim('|').Split('|').Select(c => c.Trim())];

        /// <summary>The row's code. Some Id cells carry a trailing reclassification marker after the token.</summary>
        private static string IdOf(string[] cells) => cells[0][1..cells[0].IndexOf('`', 1)];

        /// <summary>
        /// The counts table carries a column per FINDING disposition, so a row is counted wherever it lands. It
        /// had four columns while <see cref="CatalogDisposition"/> had three finding-producing members and one
        /// refusal; the fourth member arrived with nowhere to be counted.
        /// </summary>
        [Test]
        public void TheCategoryTableCountsEveryFindingDisposition()
        {
            string[] lines = RenderCategoryCounts(ProblemCatalog.Current).Split('\n');
            string header = lines.First(l => l.StartsWith("| Code |", StringComparison.Ordinal));

            Assert.That(header, Is.EqualTo("| Code | Fatal error | Error | Warning | Information | Total |"),
                "the columns run down the tier order, with the refusal column first under the name §2 gives it");
        }

        /// <summary>
        /// The Information column is proven WIRED rather than hard-coded, by seeding a row that declares
        /// <see cref="CatalogDisposition.Info"/> in a category where no shipped row does and watching that
        /// category's cell move. The seeded catalogue is local to the test, so nothing it counts reaches the
        /// checked-in table.
        /// <para>EVERY assertion here is a DELTA, and none may be a literal cell. A literal would pin this
        /// wiring test to the catalogue's own population — it broke twice that way already, once when the first
        /// Information row landed and once when a Scenes Warning did — and it would duplicate, worse, what
        /// <see cref="TheRenderedCategoryTableMatchesTheCheckedInCopy"/> compares properly. What this test is
        /// about is that seeding ONE Info row moves the Information cell, that category's total and the
        /// Information grand total by exactly one each; the absolute numbers are not its business.</para>
        /// </summary>
        [Test]
        public void TheInformationColumnCountsARowThatDeclaresIt()
        {
            string shipped = RenderCategoryCounts(ProblemCatalog.Current);
            ProblemCatalog seeded = ProblemCatalog.From(
            [
                .. ProblemCatalog.Current.Entries,
                new ProblemCatalogEntry(new ProblemCode("seeded-information-row"),
                    ProblemCatalogSection.ProjectFindings, ValidationCategory.Scenes, CatalogDisposition.Info,
                    RuleKind.UserContentRule, RuleFaces.WholeProject, default, FindingShape.OneFinding, default,
                    "Syntetisk oplysning."),
            ]);
            string counted = RenderCategoryCounts(seeded);
            string before = CategoryRow(shipped, "SCN");
            string after = CategoryRow(counted, "SCN");

            Assert.Multiple(() =>
            {
                Assert.That(Cell(after, InformationColumn),
                    Is.EqualTo(Cell(before, InformationColumn) + 1),
                    "the seeded row is counted in its own category's Information cell");
                Assert.That(Cell(after, TotalColumn), Is.EqualTo(Cell(before, TotalColumn) + 1),
                    "and in that category's total");
                Assert.That(Cell(TotalRow(counted), InformationColumn),
                    Is.EqualTo(Cell(TotalRow(shipped), InformationColumn) + 1),
                    "and in the Information grand total");
                Assert.That(Cell(before, FatalColumn), Is.EqualTo(Cell(after, FatalColumn)),
                    "and moves NOTHING else: an Info row is not a refusal");
            });
        }

        /// <summary>Which count each cell of a rendered counts row holds; cell 0 is the row's own label.</summary>
        private const int FatalColumn = 1;

        private const int InformationColumn = 4;

        private const int TotalColumn = 5;

        private static string CategoryRow(string table, string shortCode) =>
            table.Split('\n').First(l => l.StartsWith($"| **{shortCode}** |", StringComparison.Ordinal));

        private static string TotalRow(string table) =>
            table.Split('\n').First(l => l.StartsWith("| **Total**", StringComparison.Ordinal));

        /// <summary>One count out of a rendered counts row, bold markers and padding stripped.</summary>
        /// <param name="row">The rendered row, pipes and all.</param>
        /// <param name="column">Which cell, by the column constants above.</param>
        private static int Cell(string row, int column) =>
            int.Parse(
                row.Split('|', StringSplitOptions.RemoveEmptyEntries)[column].Trim().Trim('*'),
                CultureInfo.InvariantCulture);

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

        /// <summary>
        /// Reads one generated region, with the line endings normalized to the renderer's LF. The region is
        /// compared for CONTENT: what the file holds in the working tree is the checkout's business, and a
        /// clone made with <c>core.autocrlf=true</c> — the Windows CI runner's default — hands every line back
        /// with a CR the declarations never rendered.
        /// </summary>
        private static string ReadGeneratedRegion(string document, string beginMarker)
        {
            int begin = document.IndexOf(beginMarker, StringComparison.Ordinal);
            Assert.That(begin, Is.GreaterThanOrEqualTo(0),
                $"the catalogue has no generated region; expected the marker {beginMarker}");

            int body = begin + beginMarker.Length;
            int end = document.IndexOf(EndMarker, body, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(body), "the generated region has no end marker");

            return document[body..end].Replace("\r\n", "\n").Trim('\n', '\r');
        }

        /// <summary>
        /// The counts table — one column per <see cref="CatalogDisposition"/>, so every row is counted somewhere.
        /// The <b>Fatal error</b> column is <see cref="CatalogDisposition.Refusal"/> under the name §2 already
        /// gives it — the code has no <c>Fatal</c> disposition and inventing one so a table could keep its heading
        /// would be the document dictating the model.
        /// <para>
        /// The grand total is summed from the four columns rather than from the row count, so it and the per-row
        /// totals cannot disagree. They could before the fourth column existed: a row's own Total came from
        /// <c>rows.Count</c> and counted every disposition, while the grand total added three of them.
        /// </para>
        /// </summary>
        private static string RenderCategoryCounts(ProblemCatalog catalog)
        {
            StringBuilder page = new();
            page.Append("\nThe **Fatal error** column is the `Refusal` disposition under the name §2 gives it; the code has\n");
            page.Append("no `Fatal` value. Only CATEGORISED entries are counted, so this total is smaller than the\n");
            page.Append("catalogue's own: the operation-outcome heads carry no category, by design.\n\n");
            page.Append("**Information** is the advisory tier below Warning: those rows report a fact worth "
                + "knowing about\na correct project, so they ask for no repair and no judgement.\n\n");
            page.Append("| Code | Fatal error | Error | Warning | Information | Total |\n");
            page.Append("| --- | --- | --- | --- | --- | --- |\n");

            int fatal = 0, error = 0, warning = 0, information = 0;
            foreach (ValidationCategory category in Enum.GetValues<ValidationCategory>())
            {
                IReadOnlyList<ProblemCatalogEntry> rows = [.. catalog.Entries.Where(e => e.Category == category)];
                int refusals = rows.Count(e => e.Disposition == CatalogDisposition.Refusal);
                int errors = rows.Count(e => e.Disposition == CatalogDisposition.Error);
                int warnings = rows.Count(e => e.Disposition == CatalogDisposition.Warning);
                int infos = rows.Count(e => e.Disposition == CatalogDisposition.Info);
                fatal += refusals;
                error += errors;
                warning += warnings;
                information += infos;

                page.Append(CultureInfo.InvariantCulture, $"| **{category.ShortCode}** | {refusals} | {errors} | {warnings} | {infos} ");
                page.Append(CultureInfo.InvariantCulture, $"| {refusals + errors + warnings + infos} |\n");
            }

            page.Append(CultureInfo.InvariantCulture, $"| **Total** | **{fatal}** | **{error}** | **{warning}** | **{information}** ");
            page.Append(CultureInfo.InvariantCulture, $"| **{fatal + error + warning + information}** |\n");
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

                page.Append(CultureInfo.InvariantCulture, $"\n### {Title(section)} ({rows.Count})\n\n");
                page.Append("| Id | Cat | Costs | Kind | Status | Danish label |\n");
                page.Append("| --- | --- | --- | --- | --- | --- |\n");
                foreach (ProblemCatalogEntry row in rows)
                {
                    page.Append(CultureInfo.InvariantCulture, $"| `{row.Code.Value}` | {(row.Category?.ShortCode is { Length: > 0 } c ? c : "—")} ");
                    page.Append(CultureInfo.InvariantCulture, $"| {row.Disposition} | {row.Kind} | {row.Status} ");
                    page.Append(CultureInfo.InvariantCulture, $"| {(row.MessageTemplate.Length == 0 ? "*(to author)*" : row.MessageTemplate)} |\n");
                }
            }

            page.Append(CultureInfo.InvariantCulture, $"\n**Total: {catalog.Total} entries.** ");
            page.Append(CultureInfo.InvariantCulture, $"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.Active)} active, ");
            page.Append(CultureInfo.InvariantCulture, $"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.Retired)} retired, ");
            page.Append(CultureInfo.InvariantCulture, $"{catalog.Entries.Count(e => e.Status == ProblemCodeStatus.RuledOut)} ruled out.\n");
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
