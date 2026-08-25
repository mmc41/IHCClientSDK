using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

using Ihc.App;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.Io;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// THE SEVERITY-TIMES-OPERATION MATRIX: every catalogue row whose DISPOSITION and OPERATION disagree.
    ///
    /// <para><b>Two independent facts.</b> A row's disposition is what the catalogue says it is — publication
    /// and entry both. A row's operation behaviour is what the CODE does when it meets the condition: refuse,
    /// or proceed and report. They are not the same axis and were never meant to be: the four schema rows are
    /// catalogued Error AND refuse the save outright, which is not a contradiction but a row with two faces.</para>
    ///
    /// <para><b>Both directions, or the audit is worthless.</b> Auditing only the Fatal rows finds the ones that
    /// promise a refusal and do not deliver it; it cannot find <c>attr-required</c>, catalogued Error while the
    /// serializer refuses the save outright. That reverse case is the same catalogue-versus-code disagreement,
    /// and a matrix that could not see it would have certified the catalogue as consistent while it was not.</para>
    ///
    /// <para><b>Derived, then acknowledged.</b> The disagreement set is computed — the disposition from the
    /// catalogue's declarations and the master artifact's own tables, the operation from what the SDK can
    /// actually raise — and must equal the recorded copy in
    /// <c>tests/testdata/validation/severity-operation-matrix.xml</c>. The file records only what identifies a
    /// disagreement (its code and its kind), so an edit that moves source lines or rewords a decision cannot
    /// stale it; when the set really changes, the failure prints the rows to record.</para>
    ///
    /// <para><b>No posture is changed by recording one.</b> A row that would NEWLY refuse something that
    /// succeeds today stays a product decision defaulting to no change (D13). The recorded file is the record
    /// of that decision, not a to-do list someone may action without a ruling.</para>
    /// </summary>
    [TestFixture]
    public sealed class SeverityOperationMatrixTests
    {
        private const string MatrixFile = "validation/severity-operation-matrix.xml";
        private const string RootTag = "ihc_catalog_divergences";
        private const string FormatVersion = "1";

        // The closed kind vocabulary. A fourth would be a fourth way catalogue and code can disagree, which is
        // a design change rather than a row edit.
        // The severity cell the master artifact spells for a fatal row — the one publication value this audit
        // asks about.
        private const string FatalSeverity = "Fatal error";

        private const string RefusesButNotFatal = "refuses-but-not-fatal";
        private const string FatalButProceeds = "fatal-but-proceeds";
        private const string RuledOut = "ruled-out";

        /// <summary>
        /// The code classes that declare what the SDK can refuse — the IO families plus the edit-open boundary,
        /// which refuses with the SAME causes a save does. Scanned, so adding one is visible.
        /// </summary>
        private static readonly ImmutableArray<Type> RefusalSurfaces =
        [
            typeof(LoadRefusalCodes),
            typeof(EditOpenRefusalCodes),
            typeof(SaveRefusalCodes),
            typeof(ImportRefusalCodes),
            typeof(BridgeRefusalCodes),
        ];

        /// <summary>One disagreement, as the recorded file identifies it: the row's code and its kind.</summary>
        private sealed record Divergence(string Code, string Kind)
        {
            public string ToLine() => $"   <divergence code=\"{Code}\" kind=\"{Kind}\"/>";
        }

        /// <summary>
        /// The codes the master artifact PUBLISHES as fatal, parsed from its own tables. The third input, and the
        /// one that catches <c>root-version</c>: its entry declares Error (which is what the engine emits) while
        /// the catalogue a reader opens says Fatal error at Open. Comparing the entry with the code alone would
        /// have called that row consistent, because the disagreement is between the entry and the PUBLICATION.
        /// The generated regions are excluded — they are rendered FROM the entries, so including one would compare
        /// the declarations with themselves. ALL of them are stripped, not just the document up to the first:
        /// truncating there silently dropped every hand-written row the moment a second generated block was added
        /// ahead of the appendix, which turned this parse into an empty set and the audit into a vacuous pass.
        /// That is why an EMPTY RESULT REFUSES here, at the input that can actually go silently empty — an empty
        /// disagreement set is a legitimate, if remote, state of the catalogue; an empty fatal publication is not.
        /// <para>A SET of fatal codes rather than a code-to-severity map: fatal is the only publication value the
        /// audit asks about, so carrying the others invited a reader to wonder what they were for.</para>
        /// </summary>
        private static ImmutableHashSet<string> PublishedFatalCodes()
        {
            string path = Path.Combine(TestRepository.RequireRoot(), "ihcclient", "docs", "problem-catalogue.md");
            string body = WithoutGeneratedRegions(File.ReadAllText(path));

            ImmutableHashSet<string>.Builder builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
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
                // The two check-mark characters some rows carry beside the code, spelled as code points so the
                // source stays ASCII.
                string code = cells[1].Trim().Trim('`', ' ', (char)0x2714, (char)0x2705).Trim('`').Trim();
                string severity = cells[3].Trim();
                if (code.Length > 0 && severity == FatalSeverity)
                {
                    builder.Add(code);
                }
            }

            ImmutableHashSet<string> codes = builder.ToImmutable();
            if (codes.IsEmpty)
            {
                throw new InvalidDataException(
                    "problem-catalogue.md: the hand-written tables published no Fatal rows — this parse coming "
                    + "back empty is the recorded vacuous-pass failure, not a plausible state of the catalogue.");
            }
            return codes;
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

        /// <summary>
        /// Every refusal the surfaces declare, as <c>(member, cause)</c> rows — the "refuses today" evidence.
        /// A LIST rather than a cause-keyed map, because one cause may be declared under several operations
        /// (<c>attr-undeclared</c> is refused at save and at edit-open, one row under two operations), and the
        /// usage audit must see every declaring member, not whichever one a map scanned last.
        /// </summary>
        private static ImmutableArray<(string Member, string Cause)> DeclaredRefusals()
        {
            ImmutableArray<(string Member, string Cause)>.Builder rows =
                ImmutableArray.CreateBuilder<(string Member, string Cause)>();
            foreach (Type surface in RefusalSurfaces)
            {
                foreach (PropertyInfo property in surface.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    string member = surface.Name + "." + property.Name;
                    if (property.PropertyType == typeof(RefusalIdentity))
                    {
                        rows.Add((member, ((RefusalIdentity)property.GetValue(null)!).Cause.Value));
                    }
                    else if (property.PropertyType == typeof(ProblemCode) && property.Name != "Operation")
                    {
                        rows.Add((member, ((ProblemCode)property.GetValue(null)!).Value));
                    }
                }
            }
            return rows.ToImmutable();
        }

        /// <summary>
        /// The identity members actually used somewhere in the SDK, found by scanning for the member's use
        /// outside its own declaration (the declaration reads <c>public static … Member { get; }</c>; a USE
        /// names the type too). Membership is all this needs — the recorded file carries no sites, precisely so
        /// that an edit shifting source lines cannot stale it.
        /// </summary>
        private static ImmutableHashSet<string> UsedMembers(ImmutableArray<string> members)
        {
            ImmutableHashSet<string>.Builder used = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
            foreach (string file in Directory.EnumerateFiles(
                         Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src"), "*.cs",
                         SearchOption.AllDirectories))
            {
                // Membership is the whole answer, so once every member is accounted for the remaining files
                // cannot change it. The caller passes a DISTINCT set, which is what makes the count comparable.
                if (used.Count == members.Length)
                {
                    break;
                }

                string source = File.ReadAllText(file);
                foreach (string member in members)
                {
                    if (!used.Contains(member) && source.Contains(member, StringComparison.Ordinal))
                    {
                        used.Add(member);
                    }
                }
            }

            return used.ToImmutable();
        }

        /// <summary>The disagreement set, computed from the catalogue and the code. The file is a copy of this.</summary>
        private static ImmutableArray<Divergence> Derive()
        {
            ImmutableHashSet<string> raisable =
                DeclaredRefusals().Select(r => r.Cause).ToImmutableHashSet(StringComparer.Ordinal);
            ImmutableHashSet<string> publishedFatalCodes = PublishedFatalCodes();

            ImmutableArray<Divergence>.Builder rows = ImmutableArray.CreateBuilder<Divergence>();
            foreach (ProblemCatalogEntry entry in ProblemCatalog.Current.Entries)
            {
                // Operation heads carry no severity and no finding face, so they cannot diverge from one.
                if (entry.Section == ProblemCatalogSection.OperationOutcomes)
                {
                    continue;
                }

                bool refuses = raisable.Contains(entry.Code.Value);
                bool declaredRefusal = entry.Disposition == CatalogDisposition.Refusal;
                bool publishedFatal = publishedFatalCodes.Contains(entry.Code.Value);

                // Order matters, and it is the order of what a reader needs told first. A row that refuses while
                // its entry says Error has TWO faces — that is the reverse direction, and the most specific
                // thing true of it. Only then does "promises a refusal, does not deliver one" apply, which is
                // the same statement whether the promise is the entry's or the publication's.
                string? kind =
                    entry.Status == ProblemCodeStatus.RuledOut && !refuses && (declaredRefusal || publishedFatal)
                        ? RuledOut
                    : refuses && !declaredRefusal
                        ? RefusesButNotFatal
                    : !refuses && (declaredRefusal || publishedFatal)
                        ? FatalButProceeds
                    : null;
                if (kind is not null)
                {
                    rows.Add(new Divergence(entry.Code.Value, kind));
                }
            }

            return [.. rows.OrderBy(r => r.Code, StringComparer.Ordinal)];
        }

        /// <summary>The recorded copy, in file order. Refuses a file whose shape is not this format's.</summary>
        private static ImmutableArray<Divergence> Recorded()
        {
            string path = TestData.PathOf(MatrixFile);
            XElement root = XDocument.Load(path).Root
                ?? throw new InvalidDataException($"{MatrixFile}: the document has no root element.");
            if (root.Name.LocalName != RootTag)
            {
                throw new InvalidDataException($"{MatrixFile}: the root is <{root.Name.LocalName}>, not <{RootTag}>.");
            }
            if (root.Attribute("version")?.Value != FormatVersion)
            {
                throw new InvalidDataException($"{MatrixFile}: not format version {FormatVersion}.");
            }

            return
            [
                .. root.Elements("divergence").Select(row => new Divergence(
                    row.Attribute("code")?.Value
                        ?? throw new InvalidDataException($"{MatrixFile}: a <divergence> carries no 'code'."),
                    row.Attribute("kind")?.Value
                        ?? throw new InvalidDataException($"{MatrixFile}: a <divergence> carries no 'kind'."))),
            ];
        }

        /// <summary>
        /// The gate. Change a disposition, code a refusal, or delete one, and the derived set stops matching the
        /// recorded file; the failure prints the rows to record, so acknowledging a REVIEWED change is pasting
        /// them — in the same commit as the change, which is what an acknowledgment is for.
        /// </summary>
        [Test]
        public void TheDerivedDisagreementSetMatchesTheRecordedFile()
        {
            ImmutableArray<Divergence> derived = Derive();

            Assert.That(Recorded(), Is.EqualTo(derived).AsCollection,
                "the recorded severity-times-operation matrix is stale — a catalogue disposition, a refusal or "
                + "the publication changed. Review the difference, then record it by making "
                + "tests/testdata/validation/severity-operation-matrix.xml hold exactly these rows:"
                + Environment.NewLine + string.Join(Environment.NewLine, derived.Select(d => d.ToLine())));
        }

        /// <summary>
        /// The reverse direction is REALLY in the set, named. This is the row a Fatal-only audit would have
        /// missed: catalogued Error, and the serializer refuses the save outright — two faces, both intended.
        /// </summary>
        [Test]
        public void TheReverseDirectionIsRecordedForAttrRequired() =>
            Assert.That(
                Derive().Single(r => r.Code == "attr-required").Kind, Is.EqualTo(RefusesButNotFatal));

        /// <summary>
        /// The known fatal-but-proceeds instance stays recorded: published Fatal at Open, while the reader does
        /// not check <c>version_major</c>. D13 is explicit — closing that gap is a product ruling, so a refusal
        /// appearing here would surface as this row leaving the derived set.
        /// </summary>
        [Test]
        public void TheKnownFatalButProceedsRowIsRootVersion() =>
            Assert.That(
                Derive().Single(r => r.Code == "root-version").Kind, Is.EqualTo(FatalButProceeds));

        /// <summary>
        /// Every identity the SDK declares is actually raised somewhere. A declared-but-unused refusal would put
        /// a code in the "refuses today" evidence that no site can produce — the same defect as a catalogue row
        /// with no origin, one level down.
        /// </summary>
        [Test]
        public void EveryDeclaredRefusalIdentityIsRaisedSomewhere()
        {
            ImmutableArray<(string Member, string Cause)> declared = DeclaredRefusals();
            ImmutableHashSet<string> used = UsedMembers([.. declared.Select(r => r.Member).Distinct()]);

            Assert.Multiple(() =>
            {
                foreach ((string member, string code) in declared)
                {
                    Assert.That(used, Does.Contain(member), member + " (" + code + ") is declared but never raised");
                }
            });
        }
    }
}
