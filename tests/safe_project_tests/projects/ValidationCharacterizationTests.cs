using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The characterization oracle for the validation engine: for every rule id the SDK emits today, the
    /// COMPLETE ordered finding tuple — severity, rule id, category, locator, message — recorded over a fixed
    /// corpus and pinned byte-for-byte in <c>tests/testdata/validation/rule-characterization.txt</c>. It is the
    /// contract every engine-migration task asserts against, so a migration that silently drops a finding,
    /// reorders two, widens a locator or reworded a message fails here rather than in a reviewer's memory.
    ///
    /// <para><b>Why this exists beside <see cref="ValidatorCoverageTests"/>.</b> That suite asserts by message
    /// SUBSTRING (<c>Errors.Any(e =&gt; e.Contains("link1"))</c>), which stays green while the severity flips, the
    /// category changes, the locator moves to a different element, a second finding appears beside the expected
    /// one or the surrounding sentence is rewritten. Substring coverage proves a rule still fires; it cannot
    /// prove a migration was behaviour-preserving. This test pins the whole tuple and the whole ordered list.</para>
    ///
    /// <para><b>The corpus, argued.</b> Three sources, each answering a failure mode the others cannot:</para>
    /// <list type="bullet">
    /// <item><description><b>Authentic vendor files</b> (<c>Project0-Tomt</c>, <c>Project1-SimpelWired</c>,
    /// <c>project2-CustomBlock</c>, <c>project3-KompleksWired</c>, <c>project5-Dokumentation</c>) — the
    /// over-reporting guard. A rule that grew a false positive shows up as a new line on a file IHC Visual
    /// authored and accepts, which no synthetic tree can witness. They also carry the real shapes (nested
    /// localities, function-block libraries, scene rows, wired data lines) a hand-built tree never reproduces
    /// faithfully.</description></item>
    /// <item><description><b><c>Project6-Errors.vis</c></b> — the vendor-authored defect fixture, and the only
    /// corpus member that exercises the eight <see cref="ValidationCategory.Documentation"/> checks on real
    /// content. Its counts are separately defended in <see cref="ErrorsFixtureFindingsTests"/>; here they are
    /// pinned as ordered tuples, which additionally fixes the document-scan order the report appendix
    /// renders.</description></item>
    /// <item><description><b>Synthetic trees</b> — the only way to reach the 27 structural rules at all. Every
    /// condition they provoke is file-level damage IHC Visual will not author (a duplicate id token, an
    /// undeclared element, a broken bijection, a 24-bit counter overflow), so no authentic fixture can carry
    /// one; a corpus without them would pin 8 of the 35 rules and call it parity. Each tree is minimal and
    /// deterministic: fixed id counters, fixed attribute order, no clock and no allocator, so the recorded
    /// order is a property of the validator rather than of the fixture.</description></item>
    /// </list>
    ///
    /// <para><b>The map.</b> <c>tests/testdata/validation/rule-id-map.txt</c> declares, per recorded rule id,
    /// the id(s) the engine may emit in its place, the message it may carry and the category it may move to. It
    /// is the mechanism that lets an INTENDED change pass while an undeclared one still fails.</para>
    ///
    /// <para><b>The recording was re-made ONCE</b>, at the task that switched the pipeline from the shipped
    /// validators to the engine, and that is the only time it moves. What changed in that diff, and nothing
    /// else: 207 findings before and 207 after, every (case, rule id, locator) triple identical except the three
    /// declared splits of one id; the message on the 27 structural ids became the Danish fixed label with the
    /// English sentence relocated to the diagnostic; the category moved off the transitional <c>Structural</c>
    /// value onto the catalogue's own; and the ORDER became the executor's — document position, then ordinal
    /// rule id — instead of the old pipeline's pass order. Every map row reads "=" again as a result, which is
    /// the post-migration state rather than a disabled gate: the next undeclared change still fails.</para>
    /// </summary>
    public class ValidationCharacterizationTests
    {
        private const string OracleFile = "validation/rule-characterization.txt";
        private const string MapFile = "validation/rule-id-map.txt";

        /// <summary>The map cell meaning "unchanged" — the same id, or the message as recorded.</summary>
        private const string Unchanged = "=";

        /// <summary>The rendered stand-in for a finding that names no element.</summary>
        private const string NoLocator = "-";

        /// <summary>
        /// The rule ids the SDK emits over the corpus. It was 35 before the engine took over — 27 structural plus
        /// 8 documentation — 37 after, because one id that covered three distinct conditions SPLIT into three, and
        /// 44 once the eight WIRING rows landed (seven witnessed; <c>link-pass-through</c> needs a bypassable
        /// one-in-one-out block, which no corpus file carries), and 49 with the seven SCENARIO rows — five of those
        /// witnessed, since no corpus file carries a scene container that binds no output or two member rows on one
        /// output — and 53 with the eight ADDRESSING rows of T048 and T049. Six of those eight are witnessed here:
        /// the nearly-empty module, the unassigned dimmer channel, the uncommissioned wireless product and the
        /// modem with no number; the two that are not are the two COLLISION rows, and their absence is the
        /// measurement rather than a gap — no vendor file commissions a device, so nothing in the corpus can share
        /// a real address. Those asymmetries are deliberate: this corpus is the over-reporting guard, not the
        /// per-rule test set, and each unwitnessed row is covered by its own suite (<c>WiringRulesTests</c>,
        /// <c>ScenarioRulesTests</c>, <c>ModuleAddressRulesTests</c>, <c>DeviceAddressRulesTests</c>,
        /// <c>DeviceSettingRulesTests</c>).
        /// <para>54 with T050's five DIMMER and SHUTTER rows, of which the corpus witnesses ONE: the error
        /// fixture's dimmer configured with its minimum above its maximum. The other four need a value STORED as
        /// zero, and no vendor file stores one — the catalog stores no value at all on these settings, which is the
        /// distinction those predicates rest on.</para>
        /// <para>57 with T051's four remaining DEVICE rows: three are witnessed here, on authentic files as well as
        /// on the error fixture, at one to seven findings per project — which is what a Warning qualified by "the
        /// author has shown intent" should look like. The fourth, <c>dev-write-to-read-only</c>, needs a resource
        /// declared read-only, and no vendor file or catalog definition declares one.</para>
        /// <para>62 with T052's five NAMING rows, and ALL FIVE are witnessed — the first content task where that
        /// happens, because the error fixture carries a seeded duplicate identification code and cable number
        /// beside the unnamed product, and the authentic files carry blocks and a locality still at their insert
        /// names. It is also the first task whose findings leave the engine: the DOCUMENTATION category is what the
        /// Fuld report's appendix renders, so these 24 findings moved 12 committed report oracles.</para>
        /// <para>66 with T053's four remaining DOCUMENTATION rows, and all four are witnessed — two of them only
        /// by the SYNTHETIC trees, which is the point of having them in this corpus: a hand-built tree carries no
        /// masthead metadata and no end-user flag, so it witnesses the two project-level rows that no authentic
        /// fixture can (the catalogue records WHY for the end-user one — every shutter product is flagged at insert
        /// and no dialog clears it).</para>
        /// <para>69 with T054's five ENUM-DEFINITION rows, of which the corpus witnesses THREE — the two shape
        /// rows on an authentic project as well as on the error fixture, and the unused row wherever an authored
        /// type lost its last reference. The two the corpus cannot witness are the ⊘ pair: the enum editor refuses
        /// a duplicate name and offers no index field at all, so a duplicate of either kind arrives only by
        /// hand-editing.</para>
        /// <para>73 with FOUR of T055's five function-block shape rows — every one of them witnessed. The fifth,
        /// <c>logic-block-locked-content</c>, was not implemented yet: an attribute edited under a lock looks exactly
        /// like a library default, so deciding it needed the block's library definition, and the id-ordering proxy
        /// was refuted by measurement. T055a's paragraph below is where it lands.</para>
        /// <para>77 with FOUR of T056's five program-shape rows. The fifth, <c>logic-case-duplicate-value</c>, is
        /// implemented and unwitnessed: the vendor's own insert gesture writes a case branch under the left pane's
        /// caret instead of into the selected case node, so no committed project carries two branches testing one
        /// value — only a hand-edited file can.</para>
        /// <para>81 with FOUR of T057's five variable-usage rows — the first set evaluated over the shared PROGRAM
        /// READ MODEL rather than a traversal of its own. The jump in line count (61 findings) is dominated by
        /// <c>project2</c>, which declares one variable of every kind on purpose: 23 of them are dead by design.
        /// The fifth row, <c>logic-case-value-foreign</c>, is implemented and unwitnessed — no committed project
        /// tests a value outside its switch's type.</para>
        /// <para>87 with ALL SIX of T058's dataflow rows, every one witnessed. The row that decided the shape of
        /// this set is <c>logic-contending-writers</c>: comparing trigger VARIABLES reports 24 findings on
        /// <c>project3</c> and 9 on <c>Project1</c> — the ON/OFF shape of every library block — while comparing the
        /// transitive trigger ANCESTRIES reports 4 and 2, which are the real ones.</para>
        /// <para>91 with FOUR of T060's five project-structure rows. The 75-line jump is dominated by
        /// <c>struct-locality-empty</c> (41), because a new project ships TEN named localities and an installer
        /// fills the rooms the building has — the row is true about the rest, and its own disagreement column says
        /// so. The fifth row, <c>struct-icon-default</c>, is implemented and unwitnessed: no dialog offers an icon
        /// picker, so no committed project carries an element whose kind otherwise has icons.</para>
        /// <para>92 with T055a's <c>logic-block-locked-content</c>, the row T055 could not write and D27 unblocked:
        /// it compares a locked block's stored values against the LIBRARY body the caller supplies, so it is
        /// witnessed here only because <c>ProjectAppService</c> has a catalog to supply. Nine findings — the error
        /// fixture's timer, edited from 3 to 5 minutes under its lock, and eight settings configured on locked
        /// library blocks in the two authentic projects, which is the same state the row names and its
        /// disagreement column dismisses.</para>
        /// <para>93 with the first DECLARATIVE row, <c>addr-modem-phonenumber-malformed</c>: the repository's
        /// first registered <c>Constrain</c>, and the first id here whose findings come out of
        /// <c>RunConstraints</c> rather than a traversal. It is witnessed ONLY by <c>synthetic/modem-phone</c>,
        /// and that is the measurement rather than a thin corpus — the committed files carry three telephone
        /// numbers in total and all three are well-formed, so no authentic fixture can witness the row without
        /// being edited into a defect fixture. Two findings: one number too short, one with no country code.</para>
        /// <para>Each later content row surfaces here first, exactly as these did.</para>
        /// </summary>
        private const int BaselineRuleIdCount = 93;

        private static IhcSettings Settings => TestSetup.Settings;

        // ----- the corpus -----

        /// <summary>
        /// The characterization corpus. <see langword="internal"/> rather than private so
        /// <see cref="PerfBaselineBenchmark"/> measures validation latency and allocation over exactly the
        /// documents this oracle pins, instead of a second list that could drift from it.
        /// </summary>
        internal static readonly ImmutableArray<(string Case, Func<Project> Build)> Corpus =
        [
            ("authentic/Project0-Tomt", () => Authentic("Project0-Tomt.vis")),
            ("authentic/Project1-SimpelWired", () => Authentic("Project1-SimpelWired.vis")),
            ("authentic/project2-CustomBlock", () => Authentic("project2-CustomBlock.vis")),
            ("authentic/project3-KompleksWired", () => Authentic("project3-KompleksWired.vis")),
            ("authentic/project5-Dokumentation", () => Authentic("project5-Dokumentation.vis")),
            ("fixture/Project6-Errors", () => Authentic("Project6-Errors.vis")),
            ("synthetic/ids", SyntheticIds),
            ("synthetic/schema", SyntheticSchema),
            ("synthetic/fb-locality", SyntheticFunctionBlockLocality),
            ("synthetic/fb-shape", SyntheticFunctionBlockShape),
            ("synthetic/bijection", SyntheticBijection),
            ("synthetic/dataline-address", SyntheticDatalineAddress),
            ("synthetic/enum", SyntheticEnum),
            ("synthetic/root-version", SyntheticRootVersion),
            ("synthetic/luid-ceiling", SyntheticLuidCeiling),
            ("synthetic/luid-low", SyntheticLuidLow),
            ("synthetic/containment", SyntheticContainment),
            ("synthetic/modem-phone", SyntheticModemPhone),
        ];

        // ----- the pinned recording -----

        /// <summary>
        /// The whole corpus reproduces the recorded findings: same count, same order, same tuple — modulo the
        /// changes <c>rule-id-map.txt</c> declares. This is the parity gate the migration tasks assert against.
        /// </summary>
        [Test]
        public void Corpus_ReproducesTheRecordedFindings()
        {
            ImmutableArray<string> actual = Produce();
            ImmutableArray<string> recorded = ReadRecording();
            ImmutableDictionary<string, RuleMapping> map = ReadMap();

            var problems = new List<string>();
            for (int i = 0; i < Math.Min(recorded.Length, actual.Length); i++)
            {
                if (Difference(recorded[i], actual[i], map) is { } difference)
                {
                    problems.Add($"  #{i + 1}: {difference}");
                }
            }
            for (int i = actual.Length; i < recorded.Length; i++)
            {
                problems.Add($"  #{i + 1}: no longer reported — recorded '{recorded[i]}'");
            }
            for (int i = recorded.Length; i < actual.Length; i++)
            {
                problems.Add($"  #{i + 1}: newly reported — '{actual[i]}'");
            }

            Assert.That(problems, Is.Empty,
                $"The validation output diverged from the recording ({recorded.Length} recorded, "
                + $"{actual.Length} produced). An INTENDED change is declared in {MapFile}; an unintended one is "
                + "a regression in the migration. Differences:" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Take(40))
                + (problems.Count > 40 ? $"{Environment.NewLine}  … and {problems.Count - 40} more" : string.Empty));
        }

        /// <summary>
        /// The map governs exactly the recorded rule ids — no unmapped id (which would let an undeclared remap
        /// through) and no stale entry for an id the corpus no longer witnesses. The count is pinned at 35
        /// because that is the migration baseline: a 36th rule id is new work, and it surfaces here first.
        /// </summary>
        [Test]
        public void Map_GovernsExactlyTheRecordedRuleIds()
        {
            string[] recorded = [.. ReadRecording().Select(RuleIdOf).Distinct().OrderBy(id => id, StringComparer.Ordinal)];
            string[] mapped = [.. ReadMap().Keys.OrderBy(id => id, StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(mapped, Is.EqualTo(recorded).AsCollection,
                    "every recorded rule id needs a map entry, and the map may not carry an id the corpus does not witness");
                Assert.That(recorded, Has.Length.EqualTo(BaselineRuleIdCount),
                    "the corpus witnesses the 27 structural plus 8 documentation rule ids of the migration baseline");
            });
        }

        /// <summary>
        /// Rewrites the recording and, when it is absent, seeds the identity map. <see cref="ExplicitAttribute"/>
        /// so it never runs in the gate: it writes the files next to the test binary, and adopting them is the
        /// deliberate act of copying them over <c>tests/testdata/validation/</c> — which the Definition of Done
        /// only permits in a task that names its oracle impact up front.
        /// <para><b>The map half is seeded as an ALL-IDENTITY map</b>, so copying it over a map that declares a
        /// real remapping would erase that declaration — which is exactly the change the map exists to record.
        /// Diff it; adopt the rows that are new and keep the ones that say something.</para>
        /// </summary>
        [Test]
        [Explicit("Regenerates the checked-in characterization oracle. Run deliberately, then copy the emitted "
            + "files over tests/testdata/validation/ and review the diff.")]
        [Category("OracleRegeneration")]
        public void Regenerate_TheRecording()
        {
            ImmutableArray<string> lines = Produce();
            string directory = TestContext.CurrentContext.TestDirectory;

            Write(Path.Combine(directory, "rule-characterization.generated.txt"),
                [
                    "# The validation characterization oracle: every finding the SDK produces over the corpus in",
                    "# ValidationCharacterizationTests, in production order. One finding per line, tab-separated:",
                    "#",
                    "#   case <TAB> severity <TAB> rule-id <TAB> category <TAB> locator <TAB> message",
                    "#",
                    "# The locator is '-' when the finding names no element. This is the PRE-migration recording and",
                    "# is never rewritten to make a migration pass: an intended rule-id or message change is declared",
                    "# in rule-id-map.txt instead. Regenerate with the [Explicit] Regenerate_TheRecording test.",
                    "",
                    .. lines,
                ]);

            string mapPath = Path.Combine(directory, "rule-id-map.generated.txt");
            Write(mapPath,
                [
                    "# The old-to-new rule-id map for the validation-engine migration. Tab-separated:",
                    "#",
                    "#   recorded-rule-id <TAB> emitted-rule-id(s) <TAB> emitted-message <TAB> emitted-category",
                    "#",
                    "# '=' means unchanged: the same id, or the message and category exactly as rule-characterization.txt",
                    "# records them. A rule that SPLITS lists its successors comma-separated; one whose user-facing message is",
                    "# translated to its Danish fixed label puts that label in the third cell, and one that moves category names",
                    "# its new one in the fourth. Declaring a change here is what makes it intended; an undeclared one fails",
                    "# Corpus_ReproducesTheRecordedFindings.",
                    "#",
                    "# Every row reads '=' right now, and that is the post-migration state rather than a disabled gate: the",
                    "# recording was re-made once, at the task that switched the pipeline to the engine, so recorded and produced",
                    "# agree again. The columns stay because the NEXT change needs them -- and because a change made without",
                    "# declaring it here still fails.",
                    "",
                    .. lines.Select(RuleIdOf).Distinct().OrderBy(id => id, StringComparer.Ordinal)
                        .Select(id => string.Join('\t', id, Unchanged, Unchanged, Unchanged)),
                ]);

            TestContext.Out.WriteLine($"Wrote {lines.Length} findings to {directory}.");
        }

        // ----- rendering, reading and comparison -----

        private static ImmutableArray<string> Produce()
        {
            var app = new ProjectAppService(Settings);
            var lines = ImmutableArray.CreateBuilder<string>();
            foreach ((string name, Func<Project> build) in Corpus)
            {
                foreach (ProjectValidationFinding finding in app.ValidateCategorized(build()).Findings)
                {
                    lines.Add(string.Join('\t', name, finding.Severity, finding.RuleId, finding.Category,
                        finding.Locator ?? NoLocator, finding.Message));
                }
            }
            return lines.ToImmutable();
        }

        /// <summary>
        /// One declared mapping: which id(s) may replace <paramref name="RecordedRuleId"/>, the message that
        /// replaces the recorded one, and the category it moves to. Null in either slot means the recorded value
        /// still stands.
        /// <para>
        /// The CATEGORY cell exists because every migrated rule changes it. Today's rules all carry the
        /// transitional <c>Structural</c> value, which has no single successor — the catalogue distributes them
        /// across the eight real categories — so a migration that could not declare a category change would have
        /// to choose between failing this gate and leaving the classification wrong.
        /// </para>
        /// </summary>
        private sealed record RuleMapping(
            string RecordedRuleId,
            ImmutableArray<string> EmittedRuleIds,
            string? EmittedMessage,
            string? EmittedCategory);

        /// <summary>
        /// The one recorded-versus-produced comparison, or null when they agree. Case, severity, category and
        /// locator must match exactly; the rule id must be one the map declares for the recorded id; the message
        /// must be the recorded one unless the map declares a replacement.
        /// </summary>
        private static string? Difference(string recorded, string produced, ImmutableDictionary<string, RuleMapping> map)
        {
            string[] was = recorded.Split('\t');
            string[] now = produced.Split('\t');
            if (was.Length != 6 || now.Length != 6)
            {
                return $"malformed row (recorded {was.Length} cells, produced {now.Length}, expected 6)";
            }
            if (!map.TryGetValue(was[2], out RuleMapping? mapping))
            {
                return $"rule id '{was[2]}' has no entry in {MapFile}";
            }

            string[] labels = ["case", "severity", "rule id", "category", "locator", "message"];
            for (int cell = 0; cell < 6; cell++)
            {
                bool agrees = cell switch
                {
                    2 => mapping.EmittedRuleIds.Contains(now[2], StringComparer.Ordinal),
                    3 => now[3] == (mapping.EmittedCategory ?? was[3]),
                    5 => now[5] == (mapping.EmittedMessage ?? was[5]),
                    _ => now[cell] == was[cell],
                };
                if (!agrees)
                {
                    string expected = cell switch
                    {
                        2 => string.Join(" or ", mapping.EmittedRuleIds),
                        3 => mapping.EmittedCategory ?? was[3],
                        5 => mapping.EmittedMessage ?? was[5],
                        _ => was[cell],
                    };
                    return $"{was[0]} {was[2]}: {labels[cell]} expected '{expected}', produced '{now[cell]}'";
                }
            }
            return null;
        }

        private static string RuleIdOf(string line) => line.Split('\t')[2];

        private static ImmutableArray<string> ReadRecording() => [.. Payload(OracleFile)];

        private static ImmutableDictionary<string, RuleMapping> ReadMap()
        {
            var map = ImmutableDictionary.CreateBuilder<string, RuleMapping>(StringComparer.Ordinal);
            foreach (string line in Payload(MapFile))
            {
                string[] cells = line.Split('\t');
                Assert.That(cells, Has.Length.EqualTo(4), $"{MapFile}: expected four tab-separated cells in '{line}'");
                ImmutableArray<string> emitted = cells[1] == Unchanged
                    ? [cells[0]]
                    : [.. cells[1].Split(',').Select(id => id.Trim())];
                map.Add(cells[0], new RuleMapping(
                    cells[0],
                    emitted,
                    cells[2] == Unchanged ? null : cells[2],
                    cells[3] == Unchanged ? null : cells[3]));
            }
            return map.ToImmutable();
        }

        /// <summary>The file's content rows: comments and blank lines dropped, so both artifacts stay
        /// self-documenting without the readers carrying that knowledge twice.</summary>
        private static IEnumerable<string> Payload(string name)
        {
            string path = TestData.PathOf(name);
            Assert.That(File.Exists(path), Is.True,
                $"the checked-in artifact '{name}' is missing — regenerate it with the [Explicit] "
                + $"{nameof(Regenerate_TheRecording)} test and copy it into tests/testdata/validation/");
            return File.ReadAllLines(path, Encoding.UTF8)
                .Where(line => line.Length > 0 && !line.StartsWith('#'));
        }

        /// <summary>Writes an artifact as UTF-8 without BOM and LF endings — the form the checked-in copies
        /// carry, so regenerating one produces a diff of the findings that moved and nothing else. The readers
        /// are line-based, so a checkout that converts the endings changes nothing.</summary>
        private static void Write(string path, IEnumerable<string> lines) =>
            File.WriteAllText(path, string.Join('\n', lines) + '\n',
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // ----- corpus builders -----

        private static Project Authentic(string file)
        {
            using var bytes = new MemoryStream(TestData.ReadBytes("projects/" + file));
            return new ProjectAppService(Settings).Load(bytes).GetAwaiter().GetResult();
        }

        private static string T(string tag, int counter) =>
            new ElementId(counter, TypeCode.ForTag(tag) ?? 0).ToToken();

        private static (string, string)[] A(params (string, string)[] attrs) => attrs;

        /// <summary>A name carrying U+20AC EURO SIGN, which ISO-8859-1 cannot encode. Built from the code point
        /// rather than written as a literal, so this source file's own encoding cannot alter the fixture.</summary>
        private static readonly string NonLatin1Name = "Pris " + (char)0x20ac;

        private static Project Root(string versionMajor, string lastUniqueId, params ProjectElement[] children) =>
            new(Node("utcs_project", null,
                A(("version_major", versionMajor), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                    ("last_unique_id", lastUniqueId)),
                children));

        private static Project WithRoot(params ProjectElement[] children) => Root("4", "_0xfff", children);

        /// <summary>A well-shaped function block: the five containers in the fixed order, one program skeleton.</summary>
        private static ProjectElement FunctionBlock(int at, string name, ProjectElement[] inputs,
            ProjectElement[] events, ProjectElement[] actions) =>
            Node("functionblock", T("functionblock", at), A(("name", name)),
                Node("inputs", T("inputs", at + 1), A(("name", "I")), inputs),
                Node("outputs", T("outputs", at + 2), A(("name", "O"))),
                Node("settings", T("settings", at + 3), A(("name", "S"))),
                Node("internalsettings", T("internalsettings", at + 4), A(("name", "IS"))),
                Node("programs", T("programs", at + 5), A(("name", "P")),
                    Node("program_simple", T("program_simple", at + 6), A(("name", "PS")),
                        Node("events", T("events", at + 7), A(("name", "E")), events),
                        Node("actions", T("actions", at + 8), A(("name", "A")), actions))));

        /// <summary>id-duplicate-token, id-duplicate-counter, id-typecode, id-wellformed.</summary>
        private static Project SyntheticIds() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "First"))),
                Node("group", T("group", 0x21), A(("name", "Same token"))),
                // Same counter under a different type code: a distinct token, so the duplicate-counter and
                // type-code checks both reach it where the duplicate-token check would have short-circuited.
                Node("group", T("groups", 0x21), A(("name", "Foreign type code"))),
                Node("group", "_0xzz", A(("name", "Unparseable")))));

        /// <summary>element-undeclared, attr-required, attr-latin1, attr-undeclared, idref-dangling, attr-enum-range.</summary>
        private static Project SyntheticSchema() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    // No product_identifier (#REQUIRED); locked outside its enumeration; an attribute no DTD block declares.
                    Node("product_dataline", T("product_dataline", 0x51),
                        A(("name", "Missing and undeclared"), ("locked", "maybe"), ("bogus", "x")),
                        Node("scenes", T("scenes", 0x53), A(("name", "S"), ("scene_resource", "_0xdead52")))),
                    // A name the ISO-8859-1 writer cannot encode.
                    Node("product_dataline", T("product_dataline", 0x55),
                        A(("product_identifier", "_0x2202"), ("name", NonLatin1Name))),
                    Node("bogus_element", null, A(("name", "No such element type"))))));

        /// <summary>fb-local-ref, inline-constant.</summary>
        private static Project SyntheticFunctionBlockLocality()
        {
            ProjectElement foreignPin = Node("resource_input", T("resource_input", 0x70), A(("name", "Kip")));
            ProjectElement localPin = Node("resource_input", T("resource_input", 0x89), A(("name", "Lokal")));
            // link1 reaches a pin in the other function block: a programming reference must stay local.
            ProjectElement crossBlockEvent = Node("event", T("event", 0x90),
                A(("name", "E1"), ("link1", T("resource_input", 0x70))));
            // The embedded constant's own id is not what its parent's link2 names.
            ProjectElement strayConstant = Node("condition", T("condition", 0x91),
                A(("name", "C1"), ("link2", T("resource_input", 0x89))),
                Node("resource_enum", T("resource_enum", 0x92),
                    A(("name", "Konst"), ("typedef", ElementId.NullToken), ("inivalue", ElementId.NullToken))));
            return WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        FunctionBlock(0x60, "Owner", [foreignPin], [], []),
                        FunctionBlock(0x80, "Borrower", [localPin], [crossBlockEvent], [strayConstant]))));
        }

        /// <summary>fb-shape, fb-pin-container, fb-programs, program-shape.</summary>
        private static Project SyntheticFunctionBlockShape() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    // Two of the five containers, and an input pin parked in the output container.
                    Node("functionblock", T("functionblock", 0x60), A(("name", "Truncated")),
                        Node("inputs", T("inputs", 0x61), A(("name", "I"))),
                        Node("outputs", T("outputs", 0x62), A(("name", "O")),
                            Node("resource_input", T("resource_input", 0x70), A(("name", "Misplaced"))))),
                    // A full skeleton whose programs container holds the one program kind it may not.
                    Node("functionblock", T("functionblock", 0x80), A(("name", "Sub program")),
                        Node("inputs", T("inputs", 0x81), A(("name", "I"))),
                        Node("outputs", T("outputs", 0x82), A(("name", "O"))),
                        Node("settings", T("settings", 0x83), A(("name", "S"))),
                        Node("internalsettings", T("internalsettings", 0x84), A(("name", "IS"))),
                        Node("programs", T("programs", 0x85), A(("name", "P")),
                            Node("program_sub", T("program_sub", 0x86), A(("name", "PSub"))))))));

        /// <summary>link-bijection, scene-bijection (each beside the dangling-IDREF finding the same link earns).</summary>
        private static Project SyntheticBijection() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("product_dataline", T("product_dataline", 0x51),
                        A(("product_identifier", "_0x2202"), ("name", "P")),
                        Node("dataline_output", T("dataline_output", 0x52), A(("name", "Udgang")),
                            Node("link_from_resource", T("link_from_resource", 0x54),
                                A(("name", "F"), ("link", "_0xdead49")))),
                        Node("scenes", T("scenes", 0x53),
                            A(("name", "Scenarier"), ("scene_resource", T("dataline_output", 0x52))),
                            Node("scene_link", T("scene_link", 0x55), A(("name", "S"), ("link", "_0xdead49"))))))));

        /// <summary>dataline-address, all three of its faults: unparseable, out of range, duplicated.</summary>
        private static Project SyntheticDatalineAddress() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("product_dataline", T("product_dataline", 0x51),
                        A(("product_identifier", "_0x2101"), ("name", "P1")),
                        Node("dataline_input", T("dataline_input", 0x52), A(("name", "A"), ("address_dataline", "_0x5"))),
                        Node("dataline_input", T("dataline_input", 0x53), A(("name", "B"), ("address_dataline", "_0x5"))),
                        Node("dataline_input", T("dataline_input", 0x54), A(("name", "C"), ("address_dataline", "_0x9c"))),
                        Node("dataline_input", T("dataline_input", 0x56), A(("name", "D"), ("address_dataline", "_0xzz")))))));

        /// <summary>enum-inivalue, enum-typedef.</summary>
        private static Project SyntheticEnum() => WithRoot(
            Node("enum_definitions", T("enum_definitions", 0x30), A(("name", "E")),
                Node("enum_definition", T("enum_definition", 0x41), A(("name", "A")),
                    Node("enum_value", T("enum_value", 0x42), A(("name", "A1")))),
                Node("enum_definition", T("enum_definition", 0x43), A(("name", "B")),
                    Node("enum_value", T("enum_value", 0x44), A(("name", "B1"))))),
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    // inivalue belongs to the other enum definition.
                    Node("resource_enum", T("resource_enum", 0x91),
                        A(("name", "Valg"), ("typedef", T("enum_definition", 0x41)), ("inivalue", T("enum_value", 0x44)))),
                    // typedef resolves to an element that is not an enum definition at all.
                    Node("resource_enum", T("resource_enum", 0x93),
                        A(("name", "Fejl"), ("typedef", T("group", 0x21)), ("inivalue", ElementId.NullToken))))));

        /// <summary>root-version, luid-malformed (mutually exclusive with the other two last_unique_id faults).</summary>
        private static Project SyntheticRootVersion() => Root("5", "_0xzz",
            Node("groups", T("groups", 0x20), A(("name", "L"))));

        /// <summary>luid-ceiling: a high-water mark past the 24-bit counter ceiling.</summary>
        private static Project SyntheticLuidCeiling() => Root("4", "_0x1000000",
            Node("groups", T("groups", 0x20), A(("name", "L"))));

        /// <summary>luid-low: a high-water mark below a counter the document already uses.</summary>
        private static Project SyntheticLuidLow() => Root("4", "_0x1",
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")))));

        /// <summary>containment: a product parked where the model admits only localities.</summary>
        private static Project SyntheticContainment() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("product_dataline", T("product_dataline", 0x51),
                    A(("product_identifier", "_0x2202"), ("name", "Loose product")))));

        /// <summary>
        /// addr-modem-phonenumber-malformed: three telephone slots, one the modem can dial and two it cannot.
        /// <para>SYNTHETIC because the committed corpus is measured CLEAN of this condition — the only three
        /// numbers it carries (<c>project5-Dokumentation</c>) are well-formed <c>+45…</c> ones, and
        /// <c>Project6-Errors</c>'s thirty slots are all blank. The two malformed slots are the two distinct ways
        /// the predicate fails that a reader would otherwise have to guess at: one too short, and one whose only
        /// fault is the missing country code — the strictness the vendor itself does not enforce, and the reason
        /// authentic files can now warn.</para>
        /// <para>ONE slot is left well-formed on purpose: it keeps the modem out of
        /// <c>addr-modem-phonenumber-blank</c>, so this member witnesses the malformed row alone rather than both
        /// rows at once.</para>
        /// </summary>
        private static Project SyntheticModemPhone() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("product_rs485_sms_modem", T("product_rs485_sms_modem", 0x51),
                        A(("product_identifier", "_0x9f10"), ("name", "SMS modem")),
                        Node("sms_modem_settings", T("sms_modem_settings", 0x52), A(("name", "Indstillinger")),
                            Node("sms_modem_phonenumber", T("sms_modem_phonenumber", 0x53),
                                A(("address", "1"), ("phonenumber", "+4512345678"))),
                            Node("sms_modem_phonenumber", T("sms_modem_phonenumber", 0x54),
                                A(("address", "2"), ("phonenumber", "+4"))),
                            Node("sms_modem_phonenumber", T("sms_modem_phonenumber", 0x55),
                                A(("address", "3"), ("phonenumber", "4512345678"))))))));
    }
}
