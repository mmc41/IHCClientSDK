using System.IO;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The finding-catalogue gate for <c>Project6-Errors.vis</c> — the vendor-authored fixture that carries a
    /// deliberate instance of every <em>non-fatal</em> condition in
    /// <c>ihcclient/docs/problem-catalogue.md</c> that IHC Visual will let a user author, plus the
    /// deliberate non-findings that must stay silent.
    ///
    /// Two things are pinned here, and they are different in kind:
    ///
    /// <list type="bullet">
    /// <item><description><b>Structural silence.</b> Every condition in the fixture is user-sourced and non-fatal
    /// by construction, so the structural checklist (ids, IDREFs, bijections, FB shape, schema) must report
    /// <em>nothing</em>. A structural finding here means the fixture drifted into file-level damage — the one
    /// thing a vendor-authored oracle can never legitimately contain.</description></item>
    /// <item><description><b>Documentation completeness.</b> The eight implemented US-072 checks must fire exactly
    /// where the fixture was authored to provoke them, and — the part that actually catches over-reporting — must
    /// stay off the issue-free control product.</description></item>
    /// </list>
    ///
    /// The remaining ~70 catalogue rows are not asserted: the SDK does not implement them yet. This fixture is the
    /// oracle they will be built against, so a row moving from "unimplemented" to "implemented" should add its
    /// assertion here rather than a new fixture.
    /// </summary>
    public class ErrorsFixtureFindingsTests
    {
        private static IhcSettings Settings => TestSetup.Settings;

        private const string Fixture = "projects/Project6-Errors.vis";

        /// <summary>The issue-free control: every documentation field filled, addressed, coloured, named.</summary>
        private const string CleanProduct = "Lampeudtag";

        private static Project Load()
        {
            using var ms = new MemoryStream(TestData.ReadBytes(Fixture));
            return new ProjectAppService(Settings).Load(ms).GetAwaiter().GetResult();
        }

        private static ProjectValidationResult Validated() =>
            new ProjectAppService(Settings).ValidateCategorized(Load());

        /// <summary>
        /// The WIRING conditions this fixture legitimately carries, and the count of each. It is a vendor-authored
        /// DEFECT fixture, so unconnected inputs, an undriven output, a doubly-driven output, an empty block a wire
        /// runs into and two cross-locality links are content it was built to hold — reporting them is the eight
        /// WIR rows working, not a regression.
        /// <para>Pinned as exact counts rather than as "some": a count is what would catch a predicate that started
        /// firing per pin instead of per block, which is the way these two rows are most likely to go wrong.</para>
        /// </summary>
        private static readonly (string RuleId, int Count)[] WiringConditions =
        [
            ("link-crosses-locality", 2),
            ("link-fb-input-unfed", 1),
            ("link-fb-output-unused", 1),
            ("link-input-unconnected", 8),
            ("link-output-multidriven", 1),
            ("link-output-undriven", 3),
            ("link-through-empty-block", 1),
        ];

        /// <summary>
        /// Nothing about the FILE's integrity or structure is wrong with this fixture: every condition it carries
        /// is user-sourced content. The claim used to be "no finding outside Documentation", which the wiring rows
        /// made false — so the categories it excludes are now named, and the wiring findings are pinned separately
        /// below rather than folded into an "is empty" that would hide them.
        /// </summary>
        /// <summary>
        /// The SCENE conditions this fixture carries, and the count of each. Its author built three scenes to be
        /// wrong on purpose — <i>Tom scene</i> (empty), <i>Alt slukket</i> (every member off), <i>Modstrid</i>
        /// (contradicting rows) — plus a thirty-minute ramp and two outputs a scene and a link both drive. Every
        /// one of these is content the fixture exists to hold.
        /// </summary>
        private static readonly (string RuleId, int Count)[] SceneConditions =
        [
            ("scene-all-off", 1),
            ("scene-empty", 3),
            ("scene-long-delay", 1),
            ("scene-output-also-linked", 2),
            ("scene-unreferenced", 3),
        ];

        /// <summary>The scene conditions the fixture carries, each exactly as often as it is present.</summary>
        [Test]
        public void Fixture_CarriesExactlyTheseSceneConditions()
        {
            var scenes = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Scenes)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in SceneConditions)
                {
                    Assert.That(scenes.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(scenes.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(SceneConditions.Select(c => c.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no scene row fires on it that this list does not name");
                Assert.That(scenes.All(f => f.Severity == ValidationSeverity.Warning), Is.True);
            });
        }

        /// <summary>
        /// The ADDRESSING conditions the fixture carries: an LED dimmer placed but not commissioned (both channels
        /// on the catalog's own unassigned channel id), three wireless products still carrying the placeholder
        /// serial, and a modem whose thirty phone slots are all blank. Every one of those is the planned-but-not-yet-
        /// commissioned state its row describes — which is why all three are Warnings, and why the fixture is a
        /// good witness for them.
        /// <para>No channel COLLISION is listed, and that absence is asserted below: two unassigned dimmer channels
        /// do not collide, and neither do two uncommissioned wireless products sharing the catalog's channel 1.</para>
        /// </summary>
        private static readonly (string RuleId, int Count)[] AddressingConditions =
        [
            ("addr-dimmer-channel-unassigned", 2),
            ("addr-modem-phonenumber-blank", 1),
            ("addr-wireless-not-commissioned", 3),
        ];

        /// <summary>The addressing conditions the fixture carries, each exactly as often as it is present.</summary>
        [Test]
        public void Fixture_CarriesExactlyTheseAddressingConditions()
        {
            var addressing = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Addressing)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in AddressingConditions)
                {
                    Assert.That(addressing.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(addressing.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(AddressingConditions.Select(c => c.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no addressing row fires on it that this list does not name — in particular no "
                    + "channel-COLLISION, since two unassigned channels do not collide");
                Assert.That(addressing.All(f => f.Severity == ValidationSeverity.Warning), Is.True);
            });
        }

        /// <summary>
        /// The DEVICE conditions the fixture carries, and it carries one of each on purpose: an LED dimmer
        /// configured with minimum 80 % above maximum 40 %; a <i>Startværdi</i> flag whose stored <c>inivalue</c> a
        /// Powerup program re-asserts at every start; three half-commissioned settings; and seven block variables
        /// left unmarked in blocks that DO mark another one — the contrast §8 says the fixture was built to hold.
        /// <para>Note what does NOT fire: the dimmer's load mode is stored as <c>rc</c>, so the automatic-load row
        /// stays silent, and its fade rates are 201/200 ms rather than zero.</para>
        /// </summary>
        private static readonly (string RuleId, int Count)[] DeviceConditions =
        [
            ("dev-backup-missing", 7),
            ("dev-dimmer-range-inverted", 1),
            ("dev-inivalue-overwritten", 1),
            ("dev-setting-default", 3),
        ];

        /// <summary>The device-setting conditions the fixture carries, each exactly as often as it is present.</summary>
        [Test]
        public void Fixture_CarriesExactlyTheseDeviceConditions()
        {
            var device = Validated().Findings
                .Where(f => f.Category == ValidationCategory.DeviceSettings)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in DeviceConditions)
                {
                    Assert.That(device.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(device.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(DeviceConditions.Select(c => c.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no device row fires on it that this list does not name — in particular nothing about a "
                    + "setting that merely stores no value");
                Assert.That(device.All(f => f.Severity == ValidationSeverity.Warning), Is.True);
            });
        }

        [Test]
        public void Fixture_HasNoFileIntegrityOrStructureFindings()
        {
            var structural = Validated().Findings
                .Where(f => f.Category is not (ValidationCategory.Documentation or ValidationCategory.Wiring
                    or ValidationCategory.Scenes or ValidationCategory.Addressing
                    or ValidationCategory.DeviceSettings or ValidationCategory.Logic
                    or ValidationCategory.ProjectStructure))
                .ToArray();

            Assert.That(structural, Is.Empty,
                "Project6-Errors.vis carries only user-sourced, non-fatal conditions, so the structural checklist "
                + "must stay silent. Reported: "
                + string.Join(" | ", structural.Select(f => f.ToString())));
        }

        /// <summary>
        /// The LOGIC conditions the fixture carries. §4's <c>Zoo</c> was authored to hold them, and the list is
        /// counted for the same reason the wiring and device lists are: "fires at least once" passes both a rule
        /// that collapsed onto one element and one that fans out over the project.
        /// </summary>
        private static readonly (string RuleId, int Count)[] LogicConditions =
        [
            // T054's enum-definition rows. FOUR unused, not one: the fixture's record (M-14) measures that IHC
            // Visual cannot bind a user-created enumerator type to a variable at all — `Indsæt ▸ Variable` offers a
            // fixed 21 entries and none is an enumerator — so every authored type here is necessarily unreferenced,
            // `Brugt` included, whose name describes the intent rather than the file.
            ("enum-def-empty", 1),
            ("enum-def-single-value", 1),
            ("enum-def-unused", 4),
            // T055's function-block shape rows. `logic-block-empty` twice: §3 records that BOTH `Tom blok` and
            // `Kobling` had their default `Program` deleted. `logic-block-no-pins` once, on `Tom blok` alone —
            // `Kobling` has pins, which is what makes it a link-through-empty-block witness. The renamed library
            // block is the master-modified witness, and `Zoo` carries the one duplicated program pair in the corpus.
            // `logic-block-locked-content` was the one row this fixture witnessed with no rule behind it, until
            // D27 gave the engine a library to compare against — the `Timer` §3 records as edited from 3 to
            // 5 minutes under `locked="yes"`, on the same block the master-modified row reports.
            ("logic-block-empty", 2),
            ("logic-block-no-pins", 1),
            ("logic-duplicate-program", 1),
            ("logic-master-block-modified", 1),
            ("logic-block-locked-content", 1),
            // T056's program-shape rows. The two "empty program" rows do NOT overlap: `Zoo`'s one program with no
            // events is the events row's finding, and the one with events and no commands is the other's.
            ("logic-program-no-events", 1),
            ("logic-program-no-actions", 1),
            ("logic-subprogram-no-conditions", 1),
            ("logic-case-no-branches", 2),
            // T057's variable-usage rows, over the shared program read model. `enum-value-unused` counts the five
            // values of the four AUTHORED types — M-14 again: the application cannot bind a user-created type to a
            // variable, so its values can never be referenced.
            ("logic-variable-unused", 4),
            ("logic-variable-write-only", 3),
            ("logic-variable-read-only", 1),
            ("enum-value-unused", 5),
            // T058's dataflow rows, all six witnessed here.
            ("logic-output-never-assigned", 3),
            ("logic-flag-never-cleared", 2),
            ("logic-counter-never-reset", 1),
            ("logic-timer-unused", 1),
            ("logic-self-trigger", 1),
            ("logic-contending-writers", 1),
        ];

        [Test]
        public void Fixture_CarriesExactlyTheseLogicConditions()
        {
            var logic = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Logic)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in LogicConditions)
                {
                    Assert.That(logic.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(logic.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(LogicConditions.Select(c => c.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no logic row fires on it that this list does not name — in particular neither ⊘ duplicate "
                    + "row, which no gesture in the enum editor can produce");
                Assert.That(logic.All(f => f.Severity == ValidationSeverity.Warning), Is.True,
                    "the one Error of the enum set is the duplicate index, and this fixture cannot carry it");
            });
        }

        /// <summary>
        /// The PROJECT-STRUCTURE conditions the fixture carries. Counted for the same reason as the wiring, device
        /// and logic lists: "fires at least once" passes both a rule that collapsed onto one element and one that
        /// fans out over the project.
        /// </summary>
        private static readonly (string RuleId, int Count)[] StructureConditions =
        [
            // T060's rows. `struct-product-no-terminals` is the SMS Modem, which §2 names as its witness — the
            // dimmer and the logging sensors are NOT reported, because channels and measured values are wirable.
            ("struct-locality-empty", 1),
            ("struct-locality-no-devices", 1),
            ("struct-product-no-terminals", 1),
            ("struct-orphan-block", 2),
        ];

        [Test]
        public void Fixture_CarriesExactlyTheseStructureConditions()
        {
            var structure = Validated().Findings
                .Where(f => f.Category == ValidationCategory.ProjectStructure)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in StructureConditions)
                {
                    Assert.That(structure.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(structure.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(StructureConditions.Select(c => c.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no structure row fires on it that this list does not name — in particular no capacity row: "
                    + "this fixture holds ONE modem, and the three controller rows are not evaluated without a "
                    + "declared capability profile");
                Assert.That(structure.All(f => f.Severity == ValidationSeverity.Warning), Is.True);
            });
        }

        /// <summary>The wiring conditions the fixture carries, each exactly as often as it is present.</summary>
        [Test]
        public void Fixture_CarriesExactlyTheseWiringConditions()
        {
            var wiring = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Wiring)
                .ToArray();

            Assert.Multiple(() =>
            {
                foreach ((string ruleId, int count) in WiringConditions)
                {
                    Assert.That(wiring.Count(f => f.RuleId == ruleId), Is.EqualTo(count), ruleId);
                }

                Assert.That(wiring.Select(f => f.RuleId).Distinct().OrderBy(id => id, StringComparer.Ordinal),
                    Is.EqualTo(WiringConditions.Select(w => w.RuleId).OrderBy(id => id, StringComparer.Ordinal))
                        .AsCollection,
                    "and no wiring row fires on it that this list does not name");
                Assert.That(wiring.All(f => f.Severity == ValidationSeverity.Warning), Is.True,
                    "every wiring row is advisory — none of them may block a save");
            });
        }

        [Test]
        public void Fixture_IsValid_BecauseEveryConditionIsNonFatal()
        {
            Assert.That(Validated().IsValid, Is.True,
                "No condition in this fixture may block a save — every row it witnesses is an advisory or a "
                + "user-sourced error that still serializes.");
        }

        /// <summary>The eight documentation checks implemented today, and how often each fires on this fixture.</summary>
        private static readonly string[] ImplementedDocumentationRules =
        [
            "doc-documentation-tag", "doc-power-group", "doc-cabletype", "doc-cablenumber",
            "doc-position", "doc-not-linked", "doc-cable-colour", "doc-address",
            // T052's five NAMING rows joined the same category, and this fixture witnesses ALL FIVE.
            "name-empty", "name-default", "name-duplicate-siblings", "name-id-code-duplicate",
            "name-cable-number-duplicate",
            // T053's four, of which this fixture witnesses three; `doc-no-enduser-products` is unwitnessable
            // here and is listed so it does not read as an unaccounted-for rule if a later change makes it fire.
            "name-note-missing", "name-power-group-variant", "doc-project-info-blank", "doc-no-enduser-products",
        ];

        /// <summary>
        /// The eight implemented documentation checks all fire, and fire exactly as often as the fixture was
        /// authored to provoke them. Authored gaps: the five product-level fields are blank on
        /// <c>LK FUGA Tryk 4 tast 2 dioder</c>; one of its terminals is unaddressed, one carries no wire
        /// colour, and one owns no link.
        ///
        /// <para>The counts are pinned rather than merely "not empty", because "not empty" is satisfied by both
        /// failure modes that matter: a check that collapsed onto a single element, and one that fanned out over
        /// every element in the project. Note the two different units — the five product-level rules count
        /// <em>products</em> (a six-button product carries one missing Id-kode, not six), while the three
        /// terminal-level rules count <em>terminals</em>. 44 findings in total.</para>
        ///
        /// <para>Independently corroborated: the third-party <c>jemi.dk/ihc/docs</c> reporter, run over this same
        /// fixture, names the same elements for all eight kinds and reports no ninth kind. Its own totals are
        /// larger on the five product-level rules only because it repeats them under each terminal — see the
        /// appendix of <c>problem-catalogue.md</c>. That tool is unofficial and has no severity model, so it corroborates
        /// <em>detection</em> only; the numbers below are this implementation's and remain the thing to defend.</para>
        /// </summary>
        [TestCase("doc-documentation-tag", 4)]
        [TestCase("doc-power-group", 4)]
        [TestCase("doc-cabletype", 4)]
        [TestCase("doc-cablenumber", 5)]
        [TestCase("doc-position", 4)]
        [TestCase("doc-not-linked", 10)]
        [TestCase("doc-cable-colour", 8)]
        [TestCase("doc-address", 5)]
        // T052's naming rows. The fixture carries one witness of every kind: a product with no name at all, a
        // duplicated identification code and cable number on the SAME product (a half-copied product, which is how
        // both collide at once), four sibling pairs sharing a name plus one duplicated pin, and two blocks left at
        // their insert name.
        [TestCase("name-empty", 1)]
        [TestCase("name-default", 2)]
        [TestCase("name-duplicate-siblings", 5)]
        [TestCase("name-id-code-duplicate", 1)]
        [TestCase("name-cable-number-duplicate", 1)]
        // T053's four remaining DOC rows, three of which this fixture witnesses: five hand-authored block inputs
        // with no note, one light group spelled `Stue` and `stue`, and all three masthead blocks CLEARED (the
        // catalogue records that IHC Visual pre-fills `programmer`, so a blank project block is deliberate).
        // `doc-no-enduser-products` is the fourth and CANNOT be witnessed here: every shutter product is flagged
        // for the end-user report at insert and no dialog clears it, and this fixture needs a shutter for
        // `dev-shutter-traveltime-zero`. The synthetic corpus trees witness it instead.
        [TestCase("name-note-missing", 5)]
        [TestCase("name-power-group-variant", 1)]
        [TestCase("doc-project-info-blank", 1)]
        public void Fixture_ReportsDocumentationRule(string ruleId, int expectedCount)
        {
            var findings = Validated().Findings.Where(f => f.RuleId == ruleId).ToArray();

            Assert.That(findings, Is.Not.Empty, $"'{ruleId}' has a deliberate witness in the fixture but did not fire.");
            Assert.That(findings, Has.Length.EqualTo(expectedCount),
                $"'{ruleId}' fires on a fixed, authored set of elements. Reported: "
                + string.Join(" | ", findings.Select(f => f.Locator)));
            Assert.That(findings.All(f => f.Category == ValidationCategory.Documentation), Is.True,
                $"'{ruleId}' is a documentation check, not a structural one.");
            Assert.That(findings.All(f => f.Severity == ValidationSeverity.Warning), Is.True,
                $"'{ruleId}' is advisory — the user judges it, so it must never be an Error.");
        }

        /// <summary>
        /// No documentation check beyond the implemented seventeen says anything about this fixture. This is the scope
        /// guard: when a catalogue row moves from unimplemented to implemented, it surfaces here first as a failure
        /// naming the new id — which is the prompt to give it a counted assertion above and a witness in the
        /// authoring record, rather than letting it appear unnoticed and un-mapped.
        /// </summary>
        [Test]
        public void Fixture_ReportsNoDocumentationRuleBeyondTheImplementedSeventeen()
        {
            string[] unexpected = Validated().Findings
                .Where(f => f.Category == ValidationCategory.Documentation)
                .Select(f => f.RuleId)
                .Distinct()
                .Where(id => !ImplementedDocumentationRules.Contains(id))
                .OrderBy(id => id)
                .ToArray();

            Assert.That(unexpected, Is.Empty,
                "A documentation rule fired that this fixture does not yet account for: "
                + string.Join(", ", unexpected)
                + ". Add its expected count to Fixture_ReportsDocumentationRule and record its witness in "
                + "Project6-Errors.md.");
        }

        /// <summary>
        /// The over-reporting guard. The control product has every documentation field filled and its terminal
        /// addressed, coloured and linked, so no documentation finding may name it. Without this, a check that
        /// fires on everything would pass every test above.
        /// </summary>
        [Test]
        public void Fixture_CleanControlProduct_ProducesNoDocumentationFinding()
        {
            Project project = Load();
            var result = new ProjectAppService(Settings).ValidateCategorized(project);

            // A finding names its subject by that element's raw `id` attribute (FindingCollector.Locate),
            // so the control's own id plus every id beneath it is the set that must never appear.
            string[] cleanIds = project.Root.DescendantsAndSelf()
                .Where(e => e.GetAttribute("name") == CleanProduct)
                .SelectMany(e => e.DescendantsAndSelf())
                .Select(e => e.GetAttribute("id"))
                .Where(id => id is not null)
                .Distinct()
                .ToArray()!;

            Assert.That(cleanIds, Is.Not.Empty, $"The control product '{CleanProduct}' is missing from the fixture.");

            // DOCUMENTATION findings only, which is what this control is about and what its name says. The
            // product is documentation-complete; it is not required to be wiring-perfect, and since T047 it is
            // legitimately named by a scene row (its output is driven by both a scenario and a follow-link, which
            // is a fact about the installation rather than about its paperwork).
            var onClean = result.Findings
                .Where(f => f.Category == ValidationCategory.Documentation
                    && f.Locator is not null && cleanIds.Contains(f.Locator))
                .ToArray();

            Assert.That(onClean, Is.Empty,
                $"'{CleanProduct}' is the issue-free control and must appear in no finding. Reported: "
                + string.Join(" | ", onClean.Select(f => f.ToString())));
        }
    }
}
