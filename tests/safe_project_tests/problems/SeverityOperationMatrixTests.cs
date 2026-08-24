using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

using Ihc.App;
using Ihc.Vis.Catalog;
using Ihc.Vis.Io;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE SEVERITY-TIMES-OPERATION MATRIX: every catalogue row whose DISPOSITION and OPERATION disagree.
    ///
    /// <para><b>Two independent facts, two columns.</b> A row's disposition is what the catalogue says it is —
    /// a Fatal error, an Error, a Warning. A row's operation behaviour is what the CODE does when it meets the
    /// condition: refuse, or proceed and report. They are not the same axis and were never meant to be: three
    /// of the four schema rows are catalogued Error AND refuse the save outright, which is not a contradiction
    /// but a row with two faces.</para>
    ///
    /// <para><b>Both directions, or the audit is worthless.</b> Auditing only the Fatal rows finds the ones that
    /// promise a refusal and do not deliver it; it cannot find <c>attr-required</c>, catalogued Error while the
    /// serializer refuses the save outright. That reverse case is the same catalogue-versus-code disagreement,
    /// and a matrix that could not see it would have certified the catalogue as consistent while it was not.</para>
    ///
    /// <para><b>Derived, not asserted.</b> Every column is computed — the disposition and face from the
    /// catalogue's own declarations, the operation and its site by scanning the SDK source for the refusal
    /// identity each site raises. Nothing here is a hand-copied claim that can quietly go stale, which is what
    /// makes the artifact a gate: change a disposition, code a refusal, or delete one, and the derived matrix
    /// stops matching the checked-in copy.</para>
    ///
    /// <para><b>No posture is changed by recording one.</b> Every row that would NEWLY refuse something that
    /// succeeds today is flagged as a product decision defaulting to no change (D13). The matrix is the record
    /// of that decision, not a to-do list someone may action without a ruling.</para>
    /// </summary>
    [TestFixture]
    public sealed class SeverityOperationMatrixTests
    {
        private const string MatrixFile = "validation/severity-operation-matrix.txt";

        /// <summary>The code classes that declare what the SDK can refuse. Scanned, so adding one is visible.</summary>
        private static readonly ImmutableArray<Type> RefusalSurfaces =
        [
            typeof(LoadRefusalCodes),
            typeof(SaveRefusalCodes),
            typeof(ImportRefusalCodes),
            typeof(BridgeRefusalCodes),
        ];

        private sealed record MatrixRow(
            string Code, string Published, string Declared, string Operation, string Site, string Face,
            string Divergence, string Decision)
        {
            public string ToLine() =>
                string.Join('\t', Code, Published, Declared, Operation, Site, Face, Divergence, Decision);
        }

        /// <summary>
        /// What the master artifact PUBLISHES for each row, parsed from its own tables. The third input, and the
        /// one that catches <c>root-version</c>: its entry declares Error (which is what the engine emits) while
        /// the catalogue a reader opens says Fatal error at Open. Comparing the entry with the code alone would
        /// have called that row consistent, because the disagreement is between the entry and the PUBLICATION.
        /// The generated regions are excluded — they are rendered FROM the entries, so including one would compare
        /// the declarations with themselves. ALL of them are stripped, not just the document up to the first:
        /// truncating there silently dropped every hand-written row the moment a second generated block was added
        /// ahead of the appendix, which turned this parse into an empty dictionary and the audit into a vacuous pass.
        /// </summary>
        private static ImmutableDictionary<string, string> PublishedSeverity()
        {
            string path = Path.Combine(TestRepository.RequireRoot(), "ihcclient", "docs", "problem-catalogue.md");
            string body = WithoutGeneratedRegions(File.ReadAllText(path));

            ImmutableDictionary<string, string>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (string line in body.Split('\n'))
            {
                if (!line.StartsWith("| `", StringComparison.Ordinal))
                {
                    continue;
                }
                string[] cells = line.Split('|');
                if (cells.Length < 4)
                {
                    continue;
                }
                string code = cells[1].Trim().Trim('`', ' ', '\u2714', '\u2705').Trim('`').Trim();
                string severity = cells[3].Trim();
                if (code.Length > 0 && severity.Length > 0)
                {
                    builder[code] = severity;
                }
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// The catalogue with every generated region cut out, so what remains is the hand-written publication only.
        /// </summary>
        /// <param name="document">The whole markdown document.</param>
        private static string WithoutGeneratedRegions(string document)
        {
            const string begin = "<!-- GENERATED:";
            const string end = "<!-- END GENERATED -->";

            StringBuilder kept = new();
            int at = 0;
            while (true)
            {
                int open = document.IndexOf(begin, at, StringComparison.Ordinal);
                if (open < 0)
                {
                    kept.Append(document[at..]);
                    return kept.ToString();
                }

                kept.Append(document[at..open]);
                int close = document.IndexOf(end, open, StringComparison.Ordinal);
                if (close < 0)
                {
                    return kept.ToString();   // an unterminated region swallows the rest, as truncating always did
                }

                at = close + end.Length;
            }
        }

        /// <summary>Every cause the SDK can raise, by its declaring member — the "refuses today" evidence.</summary>
        private static ImmutableDictionary<string, string> RaisableCauses()
        {
            ImmutableDictionary<string, string>.Builder builder =
                ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
            foreach (Type surface in RefusalSurfaces)
            {
                foreach (PropertyInfo property in surface.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    string member = surface.Name + "." + property.Name;
                    if (property.PropertyType == typeof(RefusalIdentity))
                    {
                        builder[((RefusalIdentity)property.GetValue(null)!).Cause.Value] = member;
                    }
                    else if (property.PropertyType == typeof(ProblemCode) && property.Name != "Operation")
                    {
                        builder[((ProblemCode)property.GetValue(null)!).Value] = member;
                    }
                }
            }
            return builder.ToImmutable();
        }

        /// <summary>
        /// Where the refusal is actually raised, found by scanning the SDK for the identity member's use outside
        /// its own declaration. A site derived this way cannot drift from the code the way a written-down one can
        /// — and "declared but never used" shows up as an empty site rather than as a claim nobody checked.
        /// </summary>
        private static ImmutableDictionary<string, string> SitesOf(IEnumerable<string> members)
        {
            string root = TestRepository.RequireRoot();
            var uses = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string member in members)
            {
                uses[member] = [];
            }

            foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "ihcclient", "src"), "*.cs",
                         SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (string member in uses.Keys)
                    {
                        // The declaration itself is `public static ... Member { get; }`; a USE names the type too.
                        if (lines[i].Contains(member, StringComparison.Ordinal))
                        {
                            uses[member].Add($"{relative}:{i + 1}");
                        }
                    }
                }
            }

            return uses.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.Count == 0 ? "-" : string.Join(' ', pair.Value.OrderBy(v => v, StringComparer.Ordinal)),
                StringComparer.Ordinal);
        }

        /// <summary>The matrix, computed from the catalogue and the source. The artifact is a copy of this.</summary>
        private static ImmutableArray<MatrixRow> Derive()
        {
            ImmutableDictionary<string, string> raisable = RaisableCauses();
            ImmutableDictionary<string, string> sites = SitesOf(raisable.Values.Distinct());
            ImmutableDictionary<string, string> published = PublishedSeverity();

            ImmutableArray<MatrixRow>.Builder rows = ImmutableArray.CreateBuilder<MatrixRow>();
            foreach (ProblemCatalogEntry entry in ProblemCatalog.Current.Entries)
            {
                // Operation heads carry no severity and no finding face, so they cannot diverge from one.
                if (entry.Section == ProblemCatalogSection.OperationOutcomes)
                {
                    continue;
                }

                bool refuses = raisable.TryGetValue(entry.Code.Value, out string? member);
                bool declaredRefusal = entry.Disposition == CatalogDisposition.Refusal;
                string publishedSeverity = published.GetValueOrDefault(entry.Code.Value, "-");
                bool publishedFatal = publishedSeverity == "Fatal error";

                // Order matters, and it is the order of what a reader needs told first. A row that refuses while
                // its entry says Error has TWO faces — that is the reverse direction, and the most specific
                // thing true of it. Only then does "promises a refusal, does not deliver one" apply, which is
                // the same statement whether the promise is the entry's or the publication's.
                (string divergence, string decision) =
                    entry.Status == ProblemCodeStatus.RuledOut && !refuses && (declaredRefusal || publishedFatal)
                        ? ("ruled-out",
                            "investigated: not separately decidable, and already reported under another id")
                    : refuses && !declaredRefusal
                        ? ("refuses-but-not-fatal",
                            "two faces: reports at validate, refuses at the operation — both are intended")
                    : !refuses && (declaredRefusal || publishedFatal)
                        ? ("fatal-but-proceeds",
                            "product decision: no posture change (D13) — a new refusal needs a ruling, not a task")
                    : ("agrees", string.Empty);
                if (divergence == "agrees")
                {
                    continue;
                }

                rows.Add(new MatrixRow(
                    entry.Code.Value,
                    publishedSeverity,
                    entry.Disposition.ToString(),
                    refuses ? "refuses" : "proceeds",
                    refuses ? sites[member!] : "-",
                    (entry.Faces & RuleFaces.WholeProject) != RuleFaces.None ? "WholeProject" : "-",
                    divergence,
                    decision));
            }

            return [.. rows.OrderBy(r => r.Code, StringComparer.Ordinal)];
        }

        private static string Render(ImmutableArray<MatrixRow> rows)
        {
            var page = new StringBuilder();
            page.Append(Header);
            foreach (MatrixRow row in rows)
            {
                page.Append(row.ToLine()).Append('\n');
            }
            return page.ToString();
        }

        private const string Header = """
            # THE SEVERITY-TIMES-OPERATION MATRIX: every catalogue row whose DISPOSITION and OPERATION disagree.
            #
            #   code <TAB> published <TAB> declared <TAB> operation <TAB> site <TAB> face <TAB> divergence <TAB> decision
            #
            # THREE INDEPENDENT FACTS, which is why there are three columns and not one severity. PUBLISHED is
            # what the master artifact's own tables say. DECLARED is what the entry in code says. OPERATION is
            # what the code DOES when it meets the condition. A row can be Error and still refuse -- that is a
            # row with two faces, not a contradiction -- and a row can be published Fatal while its entry says
            # Error, which is the disagreement that comparing entry-to-code alone cannot see.
            #
            # BOTH DIRECTIONS ARE IN SCOPE. A Fatal row that does not refuse today is one kind of disagreement; a
            # non-Fatal row that DOES refuse is the other, and auditing only the Fatal rows cannot see it.
            #
            # DERIVED, NOT WRITTEN DOWN. Disposition and face come from the catalogue's declarations; operation
            # and site come from scanning the SDK for the refusal identity each site raises. Regenerate
            # deliberately: a change here is a change in what the product does.
            #
            # A ROW MARKED fatal-but-proceeds INTRODUCES NO REFUSAL. It records that the catalogue promises one
            # and the code does not, defaulting to today's behaviour (D13). Closing that gap is a product ruling.
            #
            # ROWS THAT AGREE ARE NOT LISTED -- the matrix is the disagreement set, so an empty file would mean
            # the catalogue and the code say the same thing everywhere.

            """;

        /// <summary>
        /// The gate. The matrix is regenerated from the catalogue and the source, and must equal the checked-in
        /// copy: code a refusal for a row that proceeds today, delete one, or change a disposition, and this
        /// fails until the matrix records it.
        /// </summary>
        [Test]
        public void TheDerivedMatrixMatchesTheCheckedInCopy()
        {
            string expected = File.ReadAllText(TestData.PathOf(MatrixFile)).Replace("\r\n", "\n");
            string actual = Render(Derive());

            Assert.That(actual, Is.EqualTo(expected),
                "the checked-in severity-times-operation matrix is stale — run the [Explicit] "
                + "Regenerate_TheMatrix test and review the diff, which is the list of catalogue-versus-code "
                + "disagreements this change creates or closes");
        }

        /// <summary>
        /// ARMED, and this is the assertion that makes the gate mean something. A gate only ever run against a
        /// matching pair is a gate nobody knows can fail: here the derivation is re-run with one row's operation
        /// flipped, and the result must differ from the checked-in copy.
        /// </summary>
        [Test]
        public void TheGateFailsWhenADispositionChangesWithoutAMatrixEdit()
        {
            ImmutableArray<MatrixRow> derived = Derive();
            Assert.That(derived, Is.Not.Empty, "an empty matrix would make every assertion below vacuous");

            MatrixRow flipped = derived[0] with
            {
                Operation = derived[0].Operation == "refuses" ? "proceeds" : "refuses",
            };
            string tampered = Render([flipped, .. derived.Skip(1)]);

            Assert.That(tampered, Is.Not.EqualTo(Render(derived)),
                "a changed operation must change the rendered matrix, or the gate cannot see one");
        }

        /// <summary>
        /// The reverse direction is REALLY in the matrix, named. This is the row the Fatal-only audit would have
        /// missed: catalogued Error, and the serializer refuses the save outright.
        /// </summary>
        [Test]
        public void TheReverseDirectionIsRecordedForAttrRequired()
        {
            MatrixRow row = Derive().Single(r => r.Code == "attr-required");

            Assert.Multiple(() =>
            {
                Assert.That(row.Published, Is.EqualTo("Error"), "published as an Error");
                Assert.That(row.Declared, Is.EqualTo("Error"));
                Assert.That(row.Operation, Is.EqualTo("refuses"));
                Assert.That(row.Divergence, Is.EqualTo("refuses-but-not-fatal"));
                Assert.That(row.Site, Does.Contain("ProjectSerializer.cs"), "with the site it refuses at");
                Assert.That(row.Face, Is.EqualTo("WholeProject"), "and it reports at validate as well");
            });
        }

        /// <summary>
        /// Every row that would newly refuse something carries the product decision, so nobody reads the matrix
        /// as a backlog. D13 is explicit: this work introduces no new refusal.
        /// </summary>
        [Test]
        public void EveryRowThatWouldNewlyRefuseIsFlaggedAsAProductDecision()
        {
            MatrixRow[] wouldRefuse = [.. Derive().Where(r => r.Divergence == "fatal-but-proceeds")];

            Assert.Multiple(() =>
            {
                Assert.That(wouldRefuse.Select(r => r.Code), Does.Contain("root-version"),
                    "the known instance: published Fatal at Open, while the reader does not check version_major");
                foreach (MatrixRow row in wouldRefuse)
                {
                    Assert.That(row.Decision, Does.Contain("D13"), row.Code);
                    Assert.That(row.Site, Is.EqualTo("-"), row.Code + " must have no refusing site");
                }
            });
        }

        /// <summary>
        /// Every identity the SDK declares is actually raised somewhere. A declared-but-unused refusal would put
        /// a code in the matrix's evidence column that no site can produce — the same defect as a catalogue row
        /// with no origin, one level down.
        /// </summary>
        [Test]
        public void EveryDeclaredRefusalIdentityIsRaisedSomewhere()
        {
            ImmutableDictionary<string, string> raisable = RaisableCauses();
            ImmutableDictionary<string, string> sites = SitesOf(raisable.Values.Distinct());

            Assert.Multiple(() =>
            {
                foreach ((string code, string member) in raisable)
                {
                    Assert.That(sites[member], Is.Not.EqualTo("-"), member + " (" + code + ") is declared but never raised");
                }
            });
        }

        /// <summary>Regenerates the artifact. Explicit: a change here is a change in what the product does.</summary>
        [Test]
        [Explicit("Regenerates the checked-in severity-times-operation matrix")]
        public void Regenerate_TheMatrix()
        {
            string path = Path.Combine(TestRepository.RequireRoot(), "tests", "testdata", "validation",
                "severity-operation-matrix.txt");
            File.WriteAllText(path, Render(Derive()), new UTF8Encoding(false));

            TestContext.Out.WriteLine("wrote " + path);
        }
    }
}
