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
    /// corpus and pinned byte-for-byte in <c>tests/testdata/validation/</c>, one file per case. It is the
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
    /// artifacts and required every finding to agree, per case, in order.</para>
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
        /// <para>87 with ALL SIX of T058's dataflow rows, every one witnessed — the first set whose predicates
        /// read the transitive trigger ANCESTRIES rather than the trigger variables themselves, which is what
        /// keeps the ON/OFF shape of every library block off them.</para>
        /// <para>91 with FOUR of T060's five project-structure rows. The fifth row, <c>struct-icon-default</c>, is
        /// implemented and unwitnessed: no dialog offers an icon picker, so no committed project carries an
        /// element whose kind otherwise has icons.</para>
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
        /// <para>94 with the first INFORMATION row, <c>product-s0-instrument-only</c>: the first id here whose
        /// findings are neither errors nor advisory judgements but a datasheet fact about a correctly placed
        /// device. It is witnessed by the three authentic files that carry an <c>s0_device</c> —
        /// <c>project3-KompleksWired</c>, <c>project5-Dokumentation</c> and <c>Project6-Errors</c>, one meter
        /// each — so unlike the declarative row above it needed no synthetic case.</para>
        /// <para>95 with <c>logic-statement-unlinked</c>, whose two witnesses are both synthetic and are worth
        /// telling apart. <c>synthetic/statement-link</c> was built for it, and carries the exclusion beside the
        /// finding. <c>synthetic/fb-locality</c> witnesses it by ACCIDENT: a <c>condition</c> authored there to
        /// carry a stray <c>link2</c> has no <c>link1</c> either, so it satisfies this predicate too — which is
        /// correct, and is a second, independent confirmation that the rule reads the right attribute. No
        /// AUTHENTIC file witnesses it, and none should: the vendor editor always writes <c>link1</c>.</para>
        /// <para>96 with <c>capacity-s0-multiple</c>, witnessed by <c>synthetic/s0-multiple</c> alone: the three
        /// authentic files carrying an S0 device carry exactly one each, and the vendor refuses the second
        /// insert, so no authentic file can witness it.</para>
        /// <para>97 with <c>capacity-rs485-exceeded</c>, witnessed by <c>synthetic/rs485-bus</c> alone — 32
        /// dimmers and an SMS modem, which is 33 because the vendor's own guard sentence counts the modem.</para>
        /// <para>98 with <c>capacity-voicemodem-dimmer-conflict</c>, witnessed by
        /// <c>synthetic/voicemodem-dimmer</c>. That case necessarily also witnesses
        /// <c>element-undeclared</c> — <c>product_rs485_modem</c> is an open-world tag this SDK's registry does
        /// not carry — which is honest rather than incidental: a real project holding a voice modem draws the
        /// same finding.</para>
        /// <para>99 with <c>scene-dimming-out-of-range</c>, witnessed by <c>synthetic/scene-dimming</c>: the two
        /// <c>dimming_value</c>s the committed corpus carries are 60 and 100, both in range, and the vendor
        /// spinner cannot author one that is not.</para>
        /// <para>100 with <c>dev-inivalue-out-of-range</c>, witnessed by <c>synthetic/inivalue-range</c>: every
        /// initial value the committed corpus carries is in range (20, 75, 80 and 45.00), and nothing in the
        /// vendor tool refuses one that is not.</para>
        /// <para>101 with <c>root-version-minor</c>, witnessed by <c>synthetic/root-version-minor</c>. It needed
        /// a case of its own: <c>synthetic/root-version</c> is a 5.0 file, which this predicate excludes by
        /// design because a major ahead is the sibling row's finding. The two cases together record the whole
        /// version contract.</para>
        /// <para>102 with <c>product-wireless-phaseout</c>, and it is the first row of this batch whose witnesses
        /// are all AUTHENTIC: the three committed projects carrying wireless products report it, one finding
        /// each, with no synthetic case needed at all.</para>
        /// <para>103 with <c>product-discontinued</c>, witnessed by <c>synthetic/discontinued</c>: none of the
        /// nine withdrawn identifiers appears in any committed project, which is the ordinary case rather than a
        /// gap — the corpus was authored from current hardware.</para>
        /// <para>104 with <c>migration-untested-product</c>, witnessed by two AUTHENTIC files: <c>project3</c>
        /// carries <c>_0x2125</c> and <c>_0x2139</c>, <c>project5</c> carries <c>_0x2139</c>. The same three
        /// devices are also <c>product-sensor-pulse-input</c>'s subject once that row lands, which is two
        /// independent statements about one device rather than one fact twice.</para>
        /// <para>105 with <c>product-sensor-pulse-input</c>, on the SAME three devices
        /// <c>migration-untested-product</c> already reports — <c>project3</c>'s two sensors and
        /// <c>project5</c>'s one. Two rows over one group of devices is the intended shape: what a sensor needs
        /// in order to work, and what becomes of it in a conversion.</para>
        /// <para>106 with <c>rs485-dimmer-powerfail-level</c>, which is the LARGEST single mover of this batch:
        /// 36 findings, because every placed dimmer reports and <c>synthetic/rs485-bus</c> holds 32 of them. The
        /// three authentic files contribute one each.</para>
        /// <para>107 with <c>rs485-bus-installation</c>: 6 findings, one per case, in the three authentic files
        /// and in <c>synthetic/rs485-bus</c>, <c>synthetic/voicemodem-dimmer</c> and
        /// <c>synthetic/modem-phone</c> — the last of those because an SMS modem sits on the same bus as a
        /// dimmer does.</para>
        /// <para>108 with <c>fb-user-authored</c>: 17 findings, the largest single mover of the run. Ten are in
        /// the authentic files (project2 1, project3 3, project5 2, Project6 4) and seven in synthetic cases
        /// whose blocks are built bare — a block with no master attributes IS user-authored, so those are
        /// correct rather than incidental.</para>
        /// <para>109 with <c>fb-provenance-rewritten</c>: ONE finding, <c>project2-CustomBlock</c>'s
        /// <c>AutoProof</c> block. That file carries one block of each provenance shape — <c>AutoProof</c> with a
        /// master name and no trio, <c>Custom blok</c> with neither — which makes it the witness for the PAIR
        /// rather than for either row alone.</para>
        /// <para>110 with <c>rs485-dimmer-fault-unwired</c>: 3 findings, one per authentic file carrying a
        /// dimmer. Notably NOT the 33 bare dimmers in the synthetic RS-485 cases — those products carry no
        /// channel elements, so they expose no fault resources to leave unwired, and the rule's "has any" guard
        /// keeps them silent.</para>
        /// <para>111 with <c>backup-retained-count</c>: 4 findings, one per authentic file that marks any
        /// <c>resource_*</c> for backup. It does NOT fire on <c>DuplicatedAdressErrors.vis</c>, whose two
        /// <c>backup="yes"</c> elements are output TERMINALS rather than resources — which is why
        /// <c>ErrorSeverityFixtureTests</c>'s empty-Infos assertion still holds.</para>
        /// <para>112 with <c>logic-block-recursive</c>: 2 findings, both in <c>synthetic/block-recursion</c>, one
        /// per block of the mutually-calling pair. SYNTHETIC by measurement, and the measurement is the point —
        /// the rule's first cut reported <c>project3-KompleksWired</c>'s <c>1.2.04.e</c> library block, because
        /// it projected programs onto blocks AFTER the search and so read that block's own two programs
        /// signalling each other as a self-loop. Contracting each block to one node first made the corpus
        /// silent, which is the right answer: no authentic file recurses.</para>
        /// <para>113 with <c>product-3key-upload-abort</c>: 1 finding, in <c>synthetic/discontinued</c>. The
        /// product is in no authentic file, so the case carries it — and carries the OTHER 3-key product beside
        /// it, which is not the subject. That pairing is the oracle's job here: the two are told apart by
        /// measurement rather than by name, so an edit that drifts onto the FUGA identifier turns one finding
        /// into two under the byte compare.</para>
        /// <para>114 with <c>logic-holiday-schedule-firmware</c>: 2 findings, one each in
        /// <c>project2-CustomBlock</c> and <c>project5-Dokumentation</c> — the first row of the firmware-errata
        /// group with AUTHENTIC witnesses rather than a built fixture. project2 carries FOUR
        /// <c>resource_holiday</c> elements and still produces one finding, which is <c>OneFinding</c> measured
        /// rather than asserted. <c>Project6-Errors</c> carries none, so it does not move.</para>
        /// <para>115 with <c>fb-holiday-input-custom-block</c>: 1 finding, <c>project2-CustomBlock</c>'s
        /// <c>Custom blok</c> — the file's OTHER block, <c>AutoProof</c>, carries a master name and so is not
        /// custom. That same file also draws <c>logic-holiday-schedule-firmware</c>, which is the pair working
        /// as the source describes them: one project, two different statements about holiday behaviour.</para>
        /// <para>116 with <c>rs485-dimmer-firmware-link-errors</c>: 3 findings, one per corpus file that
        /// places the RS-485 LED dimmer — <c>project3-KompleksWired</c>, <c>project5-Dokumentation</c> and
        /// <c>Project6-Errors</c>, one dimmer each. On the fixture it is the THIRD row to fire on that one
        /// device, beside the bus row and the power-fail row, which is the deliberate overlap its entry
        /// argues for: three independent subjects, not one condition counted three times.</para>
        /// <para>117 with <c>rs485-dimmer-scenario-recall</c>: 2 findings, in
        /// <c>project5-Dokumentation</c> and <c>Project6-Errors</c>. <c>project3-KompleksWired</c> places the
        /// same dimmer and is SILENT, because its two channels' scene containers hold no member rows — an
        /// AUTHENTIC negative control, which is rare here and is why the row needed no synthetic case at all.</para>
        /// <para>Each later content row surfaces here first, exactly as these did.</para>
        /// <para><b>The count then went DOWN for the first time</b>, to 106: the 2026-08 Tier-1 campaign deleted
        /// eleven rules that condemned the normal state of healthy projects, and every one of the eleven was
        /// witnessed here — which is exactly why they were deleted. The paragraphs above are the record of how
        /// the corpus grew and are left as written; this is where it shrank.</para>
        /// <para>Then to 104, in the Tier-2 pass that followed: three more witnessed rows went —
        /// <c>link-input-unconnected</c> and <c>link-output-undriven</c> reshaped into the single per-product
        /// <c>link-product-unwired</c>, which is witnessed here in their place, and
        /// <c>logic-master-block-modified</c> deleted outright. Net minus two.</para>
        /// </summary>
        private const int BaselineRuleIdCount = 104;

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
            ("synthetic/statement-link", SyntheticStatementLink),
            ("synthetic/s0-multiple", SyntheticS0Multiple),
            ("synthetic/rs485-bus", SyntheticRs485Bus),
            ("synthetic/voicemodem-dimmer", SyntheticVoicemodemDimmer),
            ("synthetic/scene-dimming", SyntheticSceneDimming),
            ("synthetic/inivalue-range", SyntheticInivalueRange),
            ("synthetic/root-version-minor", SyntheticRootVersionMinor),
            ("synthetic/discontinued", SyntheticDiscontinued),
            ("synthetic/block-recursion", SyntheticBlockRecursion),
        ];

        // ----- the pinned recording -----

        /// <summary>
        /// THE parity gate: each corpus case's export reproduces its oracle BYTE FOR BYTE.
        ///
        /// <para><b>Per case rather than over one flattened list</b>, so a rule that moves produces a diff in
        /// the one file it affects instead of a shifted comparison across every case at once. It is also stricter
        /// than the tab-separated recording it replaces: that recorded six cells per finding, while these files
        /// carry the arguments, the related sites, the exact node paths and the run's own caveats — so a change
        /// in any of them now fails here instead of passing unnoticed.</para>
        ///
        /// <para><b>An intended change is adopted, not asserted around.</b> Run the <c>[Explicit]</c>
        /// <see cref="Regenerate_TheFindingsOracles"/> test, diff the emitted files, explain every changed line
        /// by a rule that changed in the same edit, and copy them over.</para>
        /// </summary>
        [TestCaseSource(typeof(FindingOracleHarness), nameof(FindingOracleHarness.Cases))]
        public void Corpus_ReproducesItsOracleByteForByte(string oracleFile, string caseName)
        {
            Func<Project> build = Corpus.Single(c => c.Case == caseName).Build;
            var app = new ProjectAppService(Settings, new BuiltInCatalog(), FindingOracleHarness.Clock());

            using MemoryStream stream = new();
            app.ExportFindings(build(), stream, FindingExportOptions.Default with { SourceName = caseName })
                .GetAwaiter().GetResult();

            FindingOracleHarness.AssertMatchesOracle(
                File.ReadAllBytes(Path.Combine(FindingOracleHarness.DefaultRoot, oracleFile)),
                stream.ToArray(),
                oracleFile);
        }

        /// <summary>
        /// The tripwire: how many distinct codes the corpus witnesses. One more is new work and surfaces here
        /// first, before it reaches an oracle diff nobody was expecting.
        ///
        /// <para><b>What used to be here, and why it is gone.</b> This assertion sat inside a check that the
        /// rule-id MAP governed exactly these codes — the map being the mechanism that let an intended rename
        /// land green without regenerating the recording in the same commit. Byte equality and a rename
        /// declaration are mutually exclusive: if the bytes must match, a declared rename fails anyway. So the
        /// map went, and the workflow it existed for is now regenerate, diff, explain, adopt. The map was in no
        /// case an independent observation — the same regenerator wrote it and the recording from one array —
        /// and with the rename columns gone its whole remaining content was the number below, spelled once per
        /// code.</para>
        /// </summary>
        [Test]
        public void TheCorpusWitnessesExactlyTheBaselineCodeCount()
        {
            string[] witnessed =
                [.. FindingOracleHarness.ReadAll().Select(f => f.Code).Distinct().OrderBy(id => id, StringComparer.Ordinal)];

            Assert.Multiple(() =>
            {
                Assert.That(witnessed, Has.Length.EqualTo(BaselineRuleIdCount),
                    $"the corpus witnesses {BaselineRuleIdCount} distinct codes across the {Corpus.Length} oracle files");
                Assert.That(witnessed, Is.Unique, "a code is counted once however many findings carry it");
            });
        }

        /// <summary>
        /// Writes one findings-export oracle per corpus case into <c>findings.generated/</c> beside the test
        /// binary. <see cref="ExplicitAttribute"/> for the same reason the recording's regenerator is: adopting
        /// the output is the deliberate act of copying it over <c>tests/testdata/validation/</c>.
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
            + "over tests/testdata/validation/ and review the diff.")]
        [Category("OracleRegeneration")]
        public void Regenerate_TheFindingsOracles()
        {
            var app = new ProjectAppService(Settings, new BuiltInCatalog(), FindingOracleHarness.Clock());
            string? directory = null;
            foreach ((string name, Func<Project> build) in Corpus)
            {
                using MemoryStream stream = new();
                app.ExportFindings(build(), stream, FindingExportOptions.Default with { SourceName = name })
                    .GetAwaiter().GetResult();
                directory = Path.GetDirectoryName(
                    FindingOracleHarness.WriteGenerated(FindingOracleHarness.FileNameFor(name), stream.ToArray()));
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

        /// <summary>
        /// A root at the given major and the SUPPORTED minor — the shape every case here wanted before one of
        /// them needed a minor of its own.
        /// </summary>
        private static Project Root(string versionMajor, string lastUniqueId, params ProjectElement[] children) =>
            Root(versionMajor, "0", lastUniqueId, children);

        /// <summary>
        /// A root at an explicit version PAIR.
        /// <para>An overload rather than an optional parameter on the three-argument form, and deliberately: a
        /// defaulted <c>versionMinor</c> would have to sit before the <c>params</c> array, so every existing call
        /// would bind its first child element to it. Two overloads keep the twelve cases that predate this one
        /// byte-identical by construction rather than by care.</para>
        /// </summary>
        private static Project Root(
            string versionMajor, string versionMinor, string lastUniqueId, params ProjectElement[] children) =>
            new(Node("utcs_project", null,
                A(("version_major", versionMajor), ("version_minor", versionMinor), ("id1", "_0x1"),
                    ("id2", "_0x2"), ("last_unique_id", lastUniqueId)),
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

        /// <summary>
        /// root-version-minor — a file of the SUPPORTED major written by a newer minor revision.
        /// <para>A case of its own rather than a tweak to <c>synthetic/root-version</c>, because that one is
        /// <c>Root("5", …)</c> and this predicate excludes it BY DESIGN: a major ahead is the sibling row's
        /// finding, and reporting both would say one thing twice. The two cases together record the whole version
        /// contract — 5.0 reports the major and 4.1 reports the minor.</para>
        /// </summary>
        private static Project SyntheticRootVersionMinor() => Root("4", "1", "_0xfff",
            Node("groups", T("groups", 0x20), A(("name", "L"))));

        /// <summary>
        /// product-discontinued — one withdrawn device from each of the two root elements the set spans.
        /// <para>SYNTHETIC because none of the nine discontinued identifiers appears in any committed project:
        /// the corpus was authored from current hardware, which is the ordinary case and not a gap.</para>
        /// <para><b>Both root elements are present on purpose.</b> The set is keyed on (root element,
        /// identifier) rather than on the identifier alone, and a case carrying only one of the two would leave
        /// the oracle silent about whether the other half of that key is read at all. The wireless one ALSO
        /// draws <c>product-wireless-phaseout</c>, which is correct and is the deliberate overlap between a
        /// family-wide announcement and a device-specific one.</para>
        /// </summary>
        private static Project SyntheticDiscontinued() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    // device_type is REQUIRED on a wireless root — without it the case draws an attr-required
                    // Error that refuses the save, which would make a fixture about procurement read as a
                    // corrupt file. Read in the .vis unpadded spelling, as the authentic files write it.
                    Node("product_airlink", T("product_airlink", 0x51),
                        A(("product_identifier", "_0x4104"), ("device_type", "_0x80a"),
                            ("name", "Trådløs dæmper"), ("serialnumber", "_0xaa01"))),
                    Node("product_dataline", T("product_dataline", 0x52),
                        A(("product_identifier", "_0x210c"), ("name", "IR modtager"))),
                    // The 3-key push button of product-3key-upload-abort, and beside it the OTHER 3-key
                    // product, which is not the subject. The pair is what the oracle is for: an edit that
                    // drifts onto the FUGA identifier turns one finding into two under the byte compare.
                    Node("product_dataline", T("product_dataline", 0x53),
                        A(("product_identifier", "_0x106"), ("name", "Mini Modul 3 tryk"))),
                    Node("product_dataline", T("product_dataline", 0x54),
                        A(("product_identifier", "_0x2132"), ("name", "FUGA Betjeningstryk 3 tast"))))));

        /// <summary>
        /// block-recursion: two blocks that call each other, and a third whose two programs signal each other
        /// over its own internal settings.
        /// <para>SYNTHETIC because the committed corpus is measured CLEAN of the condition: the only block the
        /// first cut of the rule reported was <c>project3-KompleksWired</c>'s
        /// <c>1.2.04.e. Trådløs / Bus lysdæmper</c>, a <c>master_schneider_electric="yes"</c> library block, and
        /// it was the rule that was wrong, not the file.</para>
        /// <para>The third block is here for exactly that reason. It carries the same ring the vendor's library
        /// blocks carry, so the oracle pins the EXCLUSION next to the finding: if the contraction is ever
        /// loosened back to a per-program graph, this case grows a third finding and the byte compare says so.
        /// </para>
        /// </summary>
        private static Project SyntheticBlockRecursion()
        {
            ProjectElement Flag(int at, string name) =>
                Node("resource_flag", T("resource_flag", at), A(("name", name)));

            ProjectElement Block(int at, string name, ProjectElement[] internals, ProjectElement[] programs) =>
                Node("functionblock", T("functionblock", at), A(("name", name)),
                    Node("inputs", T("inputs", at + 1), A(("name", "Indgange"))),
                    Node("outputs", T("outputs", at + 2), A(("name", "Udgange"))),
                    Node("settings", T("settings", at + 3), A(("name", "Indstillinger"))),
                    Node("internalsettings", T("internalsettings", at + 4), A(("name", "Interne")), internals),
                    Node("programs", T("programs", at + 5), A(("name", "Programmer")), programs));

            ProjectElement Program(int at, string name, int triggeredBy, int assigns) =>
                Node("program_simple", T("program_simple", at), A(("name", name)),
                    Node("events", T("events", at + 1), A(("name", "Hændelser")),
                        Node("event", T("event", at + 2),
                            A(("name", "%P -> ON"), ("link1", T("resource_flag", triggeredBy)),
                                ("method", "_0xa")))),
                    Node("actions", T("actions", at + 3), A(("name", "Kommandoer"), ("type", "_0x2")),
                        Node("action", T("action", at + 4),
                            A(("name", "%P = ON"), ("link1", T("resource_flag", assigns)),
                                ("method", "_0xa")))));

            return WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")),
                        Block(0x70, "Kalder B", [Flag(0x80, "Flag A")],
                            [Program(0x90, "Kald", triggeredBy: 0x80, assigns: 0x81)]),
                        Block(0xa0, "Kalder A", [Flag(0x81, "Flag B")],
                            [Program(0xb0, "Kald", triggeredBy: 0x81, assigns: 0x80)]),
                        Block(0xc0, "Taler kun med sig selv", [Flag(0xd0, "Flag C"), Flag(0xd1, "Flag D")],
                            [
                                Program(0xe0, "Første", triggeredBy: 0xd0, assigns: 0xd1),
                                Program(0xe8, "Anden", triggeredBy: 0xd1, assigns: 0xd0),
                            ]))));
        }

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

        /// <summary>
        /// logic-statement-unlinked — and, in the same program, the exclusion that makes the row safe.
        /// <para>SYNTHETIC because the committed corpus is measured CLEAN of this condition: not one of its ~800
        /// statements is missing <c>link1</c>, since the vendor editor always writes one. The state arrives only
        /// by hand-editing.</para>
        /// <para><b>THE <c>event_power</c> SIBLING IS THE POINT OF THIS CASE.</b> It carries no <c>link1</c> by
        /// design, and it shares <c>event</c>'s id type code and icon — so a rule matching statements by the id
        /// suffix or by the icon rather than by the tag reports it too. The corpus's authentic files carry 7 such
        /// elements between them and would catch that as well, but only here do the finding and its exclusion sit
        /// in one program, where the oracle records the discrimination itself rather than its absence.</para>
        /// <para>The linked <c>event</c> is kept beside them for the same reason the modem case keeps one
        /// well-formed number: it proves the walk reaches statements it then declines to report.</para>
        /// </summary>
        private static Project SyntheticStatementLink() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    FunctionBlock(0x60, "Trappelys",
                        [Node("resource_input", T("resource_input", 0x70), A(("name", "Indgang")))],
                        [
                            Node("event", T("event", 0x71),
                                A(("name", "E1"), ("link1", T("resource_input", 0x70)))),
                            Node("event_power", T("event_power", 0x72), A(("name", "Powerup"))),
                        ],
                        [Node("action", T("action", 0x73), A(("name", "A1")))]))));

        /// <summary>
        /// capacity-s0-multiple — two S0 metering products where the controller binds one.
        /// <para>SYNTHETIC because no committed project carries two: the three that carry an S0 device at all
        /// carry exactly one each, and the vendor refuses the second insert outright, so the state arrives only
        /// by import or by hand.</para>
        /// <para>Both meters carry a pulse count inside the declared range, so this case witnesses the capacity
        /// row rather than <c>addr-s0-ticks-missing</c> alongside it. The two <c>product-s0-instrument-only</c>
        /// Information findings it also produces are correct and expected: that row reports every S0 terminal,
        /// and this one reports the project holding more than one — two independent statements about the same
        /// two devices.</para>
        /// </summary>
        private static Project SyntheticS0Multiple() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("s0_device", T("s0_device", 0x51),
                        A(("product_identifier", "_0x2313"), ("name", "Måler 1"), ("ticks", "100"))),
                    Node("s0_device", T("s0_device", 0x52),
                        A(("product_identifier", "_0x2313"), ("name", "Måler 2"), ("ticks", "100"))))));

        /// <summary>
        /// capacity-rs485-exceeded — a bus one component over the vendor's stated maximum.
        /// <para>SYNTHETIC because the committed corpus carries at most two RS-485 products per file, and the
        /// vendor refuses the insert that would exceed the limit — so only import or hand-editing produces it.</para>
        /// <para><b>32 dimmers PLUS the SMS modem, rather than 33 dimmers.</b> The row's distinctive claim is the
        /// vendor's own <i>"inkl. SMS modem"</i>: the modem occupies a place on the bus like anything else. A case
        /// made of dimmers alone would witness the count and say nothing about that clause, while this one fails
        /// the moment someone excuses the modem — at 32 dimmers the finding disappears.</para>
        /// </summary>
        private static Project SyntheticRs485Bus() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    [
                        .. Enumerable.Range(0, 32).Select(i => Node("product_rs485_led_dimmer",
                            T("product_rs485_led_dimmer", 0x100 + i),
                            A(("product_identifier", "_0x9e10"), ("name", $"Dæmper {i:00}")))),
                        Node("product_rs485_sms_modem", T("product_rs485_sms_modem", 0x140),
                            A(("product_identifier", "_0x6101"), ("name", "SMS modem"))),
                    ])));

        /// <summary>
        /// capacity-voicemodem-dimmer-conflict — a Voice Modem beside an RS485 LED dimmer.
        /// <para>SYNTHETIC because the vendor refuses the insert outright, and because no committed project
        /// carries a voice modem at all: the built-in catalog ships none.</para>
        /// <para><b>The co-findings are the price of the witness, and they are honest.</b>
        /// <c>product_rs485_modem</c> is an OPEN-WORLD tag in this SDK — declared in neither <c>TypeCode.cs</c>
        /// nor <c>CanonicalDtdBlocks.dtd</c> — so the element draws <c>element-undeclared</c> and its borrowed id
        /// token draws <c>id-typecode</c>. Neither is invented for the fixture's convenience and neither is
        /// suppressed: a real project carrying a voice modem would draw exactly the same two, because this SDK
        /// genuinely does not model that product. Registering the tag to tidy the oracle would be inventing
        /// catalog knowledge the repository does not have.</para>
        /// </summary>
        private static Project SyntheticVoicemodemDimmer() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("product_rs485_modem", T("product_rs485_sms_modem", 0x51),
                        A(("product_identifier", "_0x6001"), ("name", "Talemodem"))),
                    Node("product_rs485_led_dimmer", T("product_rs485_led_dimmer", 0x52),
                        A(("product_identifier", "_0x9e10"), ("name", "Dæmper"))))));

        /// <summary>
        /// scene-dimming-out-of-range — a scene member driving a light level of 150 %.
        /// <para>SYNTHETIC because the committed corpus carries exactly two <c>dimming_value</c>s, 60 and 100,
        /// both in range — and the vendor's Lysniveau spinner cannot express an out-of-range one, so only a
        /// hand-edit or a defective writer produces it.</para>
        /// <para>The scene is otherwise WELL FORMED: a reciprocal <c>scene_link</c> half, a member row in a
        /// container that binds a real output, and a program that fires the pin. That is deliberate — a broken
        /// scene would draw <c>scene-bijection</c> or <c>scene-unreferenced</c> and bury the one finding this
        /// case exists to record.</para>
        /// </summary>
        private static Project SyntheticSceneDimming()
        {
            ProjectElement memberRow = Node("scene_dimmer", T("scene_dimmer", 0x102),
                A(("name", "Stuelampe"), ("link", T("scene_link", 0x101)), ("dimming_value", "150")));
            ProjectElement product = Node("product_dataline", T("product_dataline", 0x105),
                A(("product_identifier", "_0x2202"), ("name", "Lampeudtag")),
                Node("dataline_output", T("dataline_output", 0x100),
                    A(("name", "Udgang"), ("address_dataline", "_0x5"))),
                Node("scenes", T("scenes", 0x104),
                    A(("name", "Scenarier"), ("scene_resource", T("dataline_output", 0x100))),
                    memberRow));

            ProjectElement scenePin = Node("resource_scene", T("resource_scene", 0x74), A(("name", "Aften")),
                Node("scene_link", T("scene_link", 0x101),
                    A(("name", "Scenarie link"), ("link", T("scene_dimmer", 0x102)))));

            ProjectElement block = Node("functionblock", T("functionblock", 0x70), A(("name", "Scenarier")),
                Node("inputs", T("inputs", 0x71), A(("name", "I")),
                    Node("resource_input", T("resource_input", 0x72), A(("name", "Kip"), ("note", "N")))),
                Node("outputs", T("outputs", 0x73), A(("name", "O")), scenePin),
                Node("settings", T("settings", 0x75), A(("name", "S"))),
                Node("internalsettings", T("internalsettings", 0x76), A(("name", "IS"))),
                Node("programs", T("programs", 0x77), A(("name", "P")),
                    Node("program_simple", T("program_simple", 0x78), A(("name", "Aften")),
                        Node("events", T("events", 0x79), A(("name", "E")),
                            Node("event", T("event", 0x7a),
                                A(("name", "%P -> ON"), ("link1", T("resource_input", 0x72)), ("method", "_0xa")))),
                        Node("actions", T("actions", 0x7b), A(("name", "A"), ("type", "_0x2")),
                            Node("action", T("action", 0x7c),
                                A(("name", "%P = ON"), ("link1", T("resource_scene", 0x74)), ("method", "_0xa")))))));

            return WithRoot(
                Node("groups", T("groups", 0x20), A(("name", "L")),
                    Node("group", T("group", 0x21), A(("name", "Stue")), product, block)));
        }

        /// <summary>
        /// dev-inivalue-out-of-range — a humidity reading whose initial value is 150,00 %RH.
        /// <para>SYNTHETIC because every <c>inivalue</c> the committed corpus carries is in range (20, 75, 80 and
        /// 45.00), and nothing in the vendor tool refuses one that is not — the measured 150.00 loads, renders
        /// verbatim and survives a resave, so only an author or an importer produces it.</para>
        /// <para><b>The decimal form is the point.</b> The value is written <c>150.00</c>, not <c>150</c>, so the
        /// oracle records that the sentence prints the bytes rather than a reformatted number — which is the whole
        /// reason the slot is <c>AttributeValue</c>. The LUX-valued <c>resource_light</c> sits beside it at 5000,
        /// well past 100 and correctly silent, so the oracle records the scope as well as the finding.</para>
        /// </summary>
        private static Project SyntheticInivalueRange() => WithRoot(
            Node("groups", T("groups", 0x20), A(("name", "L")),
                Node("group", T("group", 0x21), A(("name", "Stue")),
                    Node("functionblock", T("functionblock", 0x70), A(("name", "Klima")),
                        Node("inputs", T("inputs", 0x71), A(("name", "I"))),
                        Node("outputs", T("outputs", 0x72), A(("name", "O"))),
                        Node("settings", T("settings", 0x73), A(("name", "S"))),
                        Node("internalsettings", T("internalsettings", 0x74), A(("name", "IS")),
                            Node("resource_humidity_level", T("resource_humidity_level", 0x80),
                                A(("name", "Fugtighed"), ("inivalue", "150.00"))),
                            Node("resource_light", T("resource_light", 0x81),
                                A(("name", "Lysstyrke"), ("inivalue", "5000")))),
                        Node("programs", T("programs", 0x75), A(("name", "P")))))));
    }
}
