using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

using Ihc.Tests.Shared;

using static Ihc.Vis.Tests.Tree;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The characterization oracle for the validation engine: for every rule id the SDK emits today, the
    /// COMPLETE ordered finding tuple — severity, rule id, category, locator, message — recorded over a fixed
    /// corpus and pinned byte-for-byte in <c>tests/testdata/validation/findings/</c>, one file per case. It is the
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
    /// <para><b>How an intended change lands.</b> There is no declaration file and no remap columns: the gate
    /// is byte equality per case, so a rule that changes moves bytes and the only way through is to regenerate
    /// the oracles, diff them, explain every changed line by a rule that changed in the same edit, and adopt
    /// them. A map that declared renames could not coexist with that — if the bytes must match, a declared
    /// rename fails anyway — so the map was retired with the recording it policed.</para>
    ///
    /// <para><b>The recording was re-made ONCE</b>, at the task that switched the pipeline from the shipped
    /// validators to the engine, and that is the only time it moves. What changed in that diff, and nothing
    /// else: 207 findings before and 207 after, every (case, rule id, locator) triple identical except the three
    /// declared splits of one id; the message on the 27 structural ids became the Danish fixed label with the
    /// English sentence relocated to the diagnostic; the category moved off the transitional <c>Structural</c>
    /// value onto the catalogue's own; and the ORDER became the executor's — document position, then ordinal
    /// rule id — instead of the old pipeline's pass order. It moved a second time when the recording became
    /// these XML files, and that move was proved rather than reviewed: a temporary test zipped the two
    /// artifacts and required all 618 findings to agree, per case, in order.</para>
    /// </summary>
    public class ValidationCharacterizationTests
    {
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
        /// THE parity gate: each corpus case's export reproduces its oracle BYTE FOR BYTE.
        ///
        /// <para><b>Per case rather than over one flattened list</b>, so a rule that moves produces a diff in
        /// the one file it affects instead of a shifted comparison across all eighteen. It is also stricter
        /// than the tab-separated recording it replaces: that recorded six cells per finding, while these files
        /// carry the arguments, the related sites, the exact node paths and the run's own caveats — so a change
        /// in any of them now fails here instead of passing unnoticed.</para>
        ///
        /// <para><b>An intended change is adopted, not asserted around.</b> Run the <c>[Explicit]</c>
        /// <see cref="Regenerate_TheFindingsOracles"/> test, diff the emitted files, explain every changed line
        /// by a rule that changed in the same edit, and copy them over.</para>
        /// </summary>
        [TestCaseSource(typeof(FindingOracles), nameof(FindingOracles.Cases), new object?[] { null })]
        public void Corpus_ReproducesItsOracleByteForByte(string oracleFile, string caseName)
        {
            Func<Project> build = Corpus.Single(c => c.Case == caseName).Build;
            var app = new ProjectAppService(Settings, new BuiltInCatalog(), FindingOracles.Clock());

            using MemoryStream stream = new();
            app.ExportFindings(build(), stream, FindingExportOptions.Default with { SourceName = caseName })
                .GetAwaiter().GetResult();

            FindingOracles.AssertMatchesOracle(
                File.ReadAllBytes(Path.Combine(FindingOracles.DefaultRoot, oracleFile)),
                stream.ToArray(),
                oracleFile);
        }

        /// <summary>
        /// The tripwire: how many distinct codes the corpus witnesses. A 94th is new work and surfaces here
        /// first, before it reaches an oracle diff nobody was expecting.
        ///
        /// <para><b>What used to be here, and why it is gone.</b> This assertion sat inside a check that the
        /// rule-id MAP governed exactly these codes — the map being the mechanism that let an intended rename
        /// land green without regenerating the recording in the same commit. Byte equality and a rename
        /// declaration are mutually exclusive: if the bytes must match, a declared rename fails anyway. So the
        /// map went, and the workflow it existed for is now regenerate, diff, explain, adopt. The map was in no
        /// case an independent observation — the same regenerator wrote it and the recording from one array —
        /// and with the rename columns gone its whole remaining content was the number below, spelled 93
        /// times.</para>
        /// </summary>
        [Test]
        public void TheCorpusWitnessesExactlyTheBaselineCodeCount()
        {
            string[] witnessed =
                [.. FindingOracles.ReadAll().Select(f => f.Code).Distinct().OrderBy(id => id, StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(witnessed, Has.Length.EqualTo(BaselineRuleIdCount),
                    $"the corpus witnesses {BaselineRuleIdCount} distinct codes across the 18 oracle files");
                Assert.That(witnessed, Is.Unique, "a code is counted once however many findings carry it");
            });
        }

        /// <summary>
        /// Writes one findings-export oracle per corpus case into <c>findings.generated/</c> beside the test
        /// binary. <see cref="ExplicitAttribute"/> for the same reason the recording's regenerator is: adopting
        /// the output is the deliberate act of copying it over <c>tests/testdata/validation/findings/</c>.
        ///
        /// <para><b>The export is a DEFAULT export but for the source name.</b> A project carries no filename,
        /// so <c>SourceName</c> is the one thing the SDK cannot supply and the one option this sets — everything
        /// else (the production order, all three tiers, the not-run caveat) is what an ordinary export writes.
        /// That is what keeps the oracle an example of the real format rather than a shape only the test
        /// produces.</para>
        ///
        /// <para><b>The clock is pinned</b>, so <c>@generated</c> is the same byte on every machine. It is the
        /// same instant the report oracles use, borrowed rather than redeclared.</para>
        /// </summary>
        [Test]
        [Explicit("Regenerates the checked-in findings oracles. Run deliberately, then copy the emitted files "
            + "over tests/testdata/validation/findings/ and review the diff.")]
        [Category("OracleRegeneration")]
        public void Regenerate_TheFindingsOracles()
        {
            var app = new ProjectAppService(Settings, new BuiltInCatalog(), FindingOracles.Clock());
            string? directory = null;
            foreach ((string name, Func<Project> build) in Corpus)
            {
                using MemoryStream stream = new();
                app.ExportFindings(build(), stream, FindingExportOptions.Default with { SourceName = name })
                    .GetAwaiter().GetResult();
                directory = Path.GetDirectoryName(
                    FindingOracles.WriteGenerated(FindingOracles.FileNameFor(name), stream.ToArray()));
            }

            TestContext.Out.WriteLine($"Wrote {Corpus.Length} findings oracles to {directory}.");
        }

        // ----- rendering, reading and comparison -----

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
