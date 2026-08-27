using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The problem catalogue: every governed code, as compiled declarations rather than a parsed table.
    ///
    /// <para><b>What "schema" means here.</b> There is no parser, no artifact path and no drift gate pointed at a
    /// markdown file. An entry is a C# declaration, so a malformed one cannot exist, argument arity and type are
    /// the compiler's problem, and review happens in the diff of a <c>.cs</c> file. What is left for a test is the
    /// set of invariants a diff of declarations cannot enforce on its own: uniqueness across sections, the
    /// category biconditional, and the counts that say the migration is complete.</para>
    ///
    /// <para><b>Why these tests read the catalogue's own total.</b> A hard-coded 134 was already false before this
    /// catalogue existed — ten codes shipped outside the draft — so every count here is either derived from
    /// <see cref="ProblemCatalog.Entries"/> or is a per-section number this task deliberately fixes.</para>
    /// </summary>
    [TestFixture]
    public sealed class ProblemCatalogTests
    {
        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        private static IReadOnlyList<ProblemCatalogEntry> InSection(ProblemCatalogSection section) =>
            [.. Catalog.Entries.Where(e => e.Section == section)];

        [Test]
        public void TheCatalogueTotalIsItsOwnEntryCountAcrossEverySection()
        {
            IReadOnlyList<ProblemCatalogEntry> project = InSection(ProblemCatalogSection.ProjectFindings);
            IReadOnlyList<ProblemCatalogEntry> definitions = InSection(ProblemCatalogSection.CatalogDefinitionFindings);
            IReadOnlyList<ProblemCatalogEntry> outcomes = InSection(ProblemCatalogSection.OperationOutcomes);

            Assert.Multiple(() =>
            {
                Assert.That(Catalog.Total, Is.EqualTo(Catalog.Entries.Length));
                Assert.That(Catalog.Total, Is.EqualTo(project.Count + definitions.Count + outcomes.Count),
                    "every entry is in exactly one section");

                // 161 live project rows, plus the 4 that were investigated and ruled out, plus the 3 that were
                // SPLIT and retired. All three kinds keep their ids occupied, which is the whole reservation
                // mechanism: an entry that stays is an id that can never be handed to a different condition.
                // The fourth ruled-out row is T048's `addr-unassigned`: its condition is what `doc-address`
                // already reports, on the same elements, which the catalogue row itself admits. The retired rows
                // are `dataline-address`, `capacity-modules-exceeded` (split into the three capacity rows under
                // D2) and `capacity-addresses` (split again, per direction, so a project over on both no longer
                // reports two findings distinguishable only by their numbers).
                Assert.That(project, Has.Count.EqualTo(180));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.Active), Is.EqualTo(173));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.RuledOut), Is.EqualTo(4));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.Retired), Is.EqualTo(3));

                // The ten codes that already shipped with no catalogue row behind them, plus the one MINTED
                // here: `block-identity-missing`, the function-block half of `identity-missing`'s split.
                Assert.That(definitions, Has.Count.EqualTo(11));

                // The operation-outcome section grows with the work that gives each operation a coded outcome;
                // the catch-all is here from the start because nothing else mints it.
                Assert.That(outcomes.Select(e => e.Code.Value), Does.Contain("internal.unexpected"));
            });
        }

        [Test]
        public void TheCatalogueSatisfiesItsOwnInvariants()
        {
            EquatableArray<CatalogDefect> defects = CatalogInvariants.Check(Catalog, []);

            Assert.That(defects, Is.Empty,
                "defects: " + string.Join(", ", defects.Select(d => $"{d.Code.Value}={d.Violation}")));
        }

        /// <summary>
        /// The invariants, armed. A check that only ever runs against a healthy catalogue is a check nobody knows
        /// can fail, so each violation is exercised against a deliberately broken one built here.
        /// </summary>
        [Test]
        public void TheInvariantsCatchEachViolationTheyName()
        {
            ProblemCatalogEntry Row(string code, ProblemCatalogSection section, ValidationCategory? category) =>
                new(new ProblemCode(code), section, category, CatalogDisposition.Warning,
                    RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                    FindingShape.OnePerOccurrence, default, "Label");

            // The refusal PAIR, seeded from both halves: an advisory row that nonetheless refuses, and a Refusal
            // row that names no head. Between them they say what the two checks are for — refusing is the
            // hardest thing a row can do, so it may not hide under a Warning, and it may not be unattributed.
            ProblemCatalogEntry advisoryThatRefuses =
                Row("advisory-that-refuses", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic) with
                {
                    RefusedOperations = ImmutableArray.Create(OperationCodes.Save),
                };
            ProblemCatalogEntry refusalNamingNothing =
                Row("refusal-naming-nothing", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic) with
                {
                    Disposition = CatalogDisposition.Refusal,
                };

            ProblemCatalog broken = ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(
            [
                Row("twice", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
                Row("twice", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
                Row("io.load", ProblemCatalogSection.OperationOutcomes, ValidationCategory.FileIntegrity),
                Row("no-category", ProblemCatalogSection.ProjectFindings, null),
                Row("unimplemented", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
                advisoryThatRefuses,
                refusalNamingNothing,
            ]));

            // The same two shapes made legal, plus the operation head that declares nothing BY RULE. A check
            // that reported every refusal, or every empty declaration, would fail here instead of passing.
            ProblemCatalog compliant = ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(
            [
                advisoryThatRefuses with
                {
                    Code = new ProblemCode("error-that-refuses"), Disposition = CatalogDisposition.Error,
                },
                refusalNamingNothing with
                {
                    Code = new ProblemCode("refusal-naming-its-head"),
                    RefusedOperations = ImmutableArray.Create(OperationCodes.Load),
                },
                refusalNamingNothing with
                {
                    Code = new ProblemCode("io.seeded-head"),
                    Section = ProblemCatalogSection.OperationOutcomes,
                    Category = null,
                },
            ]));

            CatalogViolation[] fromDeclarations = [.. CatalogInvariants.Check(broken, []).Select(d => d.Violation)];
            (string Code, CatalogViolation Violation)[] attributed =
                [.. CatalogInvariants.Check(broken, []).Select(d => (d.Code.Value, d.Violation))];
            CatalogDefect[] withRules = [.. CatalogInvariants.Check(broken,
                [new ProblemCode("unimplemented"), new ProblemCode("nowhere-declared")])];

            Assert.Multiple(() =>
            {
                Assert.That(fromDeclarations, Does.Contain(CatalogViolation.DuplicateCode));
                Assert.That(fromDeclarations, Does.Contain(CatalogViolation.CategoryMisplaced));

                Assert.That(attributed,
                    Does.Contain(("advisory-that-refuses", CatalogViolation.RefusedOperationOnAdvisoryDisposition)),
                    "a Warning row declaring a refusal must be reported, and reported against ITS code");
                Assert.That(attributed,
                    Does.Contain(("refusal-naming-nothing", CatalogViolation.RefusalWithoutRefusedOperation)),
                    "a Refusal content row naming no head must be reported");

                Assert.That(CatalogInvariants.Check(compliant, []), Is.Empty,
                    "an Error row may refuse, a Refusal row that names its head is what every shipped refusal "
                    + "looks like, and an operation head refuses nothing because it IS the operation");

                Assert.That(withRules.Select(d => d.Violation), Does.Contain(CatalogViolation.EntryWithoutRule));
                Assert.That(withRules.Where(d => d.Violation == CatalogViolation.RuleWithoutEntry)
                        .Select(d => d.Code.Value),
                    Does.Contain("nowhere-declared"));

                // The implemented one is not reported as an entry without a rule.
                Assert.That(withRules.Where(d => d.Violation == CatalogViolation.EntryWithoutRule)
                        .Select(d => d.Code.Value),
                    Does.Not.Contain("unimplemented"));
            });
        }

        [Test]
        public void IdsAreUniqueAcrossEverySection()
        {
            string[] duplicates = [.. Catalog.Entries
                .GroupBy(e => e.Code.Value, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            Assert.Multiple(() =>
            {
                Assert.That(duplicates, Is.Empty, "ids are unique across ALL sections, not per section");
                foreach (ProblemCatalogEntry entry in Catalog.Entries)
                {
                    Assert.That(Catalog.TryGet(entry.Code, out ProblemCatalogEntry found), Is.True, entry.Code.Value);
                    Assert.That(found, Is.EqualTo(entry));
                }
            });
        }

        [Test]
        public void CategoryIsPresentExactlyWhenTheSectionIsNotAnOperationOutcome()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCatalogEntry entry in Catalog.Entries)
                {
                    bool isOutcome = entry.Section == ProblemCatalogSection.OperationOutcomes;
                    Assert.That(entry.Category is null, Is.EqualTo(isOutcome), entry.Code.Value);
                }
            });
        }

        /// <summary>
        /// Severity follows from disposition, asserted over the DISPOSITION AXIS rather than over the shipped
        /// rows — and that choice does not depend on which dispositions happen to be declared. A row walk is only
        /// as complete as the catalogue happens to be: a member no row declares is asserted about not at all, so
        /// the walk keeps passing if that member maps to Warning, to null, or to nothing. It also has to restate
        /// the mapping to compare against, which is a second copy of the very derivation under test.
        /// <para>
        /// So every member is seeded here instead, which makes the mapping total by construction: a fifth
        /// disposition added without a severity fails this immediately, on the day it is added rather than on the
        /// day a row first declares it.
        /// </para>
        /// <para>
        /// Written when <see cref="CatalogDisposition.Info"/> was the undeclared member; rows declare it now, and
        /// the argument is unchanged — which is the point of not resting it on the catalogue's population.
        /// </para>
        /// </summary>
        [Test]
        public void EveryDispositionDerivesItsSeverityWhetherOrNotARowDeclaresIt()
        {
            (CatalogDisposition Disposition, ValidationSeverity? Severity)[] axis =
            [
                (CatalogDisposition.Error, ValidationSeverity.Error),
                (CatalogDisposition.Warning, ValidationSeverity.Warning),
                (CatalogDisposition.Info, ValidationSeverity.Info),
                (CatalogDisposition.Refusal, null),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(axis.Select(a => a.Disposition),
                    Is.EquivalentTo(Enum.GetValues<CatalogDisposition>()),
                    "a disposition was added without saying which severity it reports as");

                foreach ((CatalogDisposition disposition, ValidationSeverity? severity) in axis)
                {
                    Assert.That(Seeded(disposition).Severity, Is.EqualTo(severity), disposition.ToString());
                }
            });
        }

        /// <summary>
        /// The refusal a row causes is DECLARED, so nothing has to read a prose column to learn it. The three
        /// rows below are the ones §4's <b>Blocks</b> column got wrong while it was hand-written, and each is
        /// now published from the declaration this test pins.
        /// <para>
        /// <c>attr-undeclared</c> refuses the save AND the edit-open. Its declaration said so in a doc-comment
        /// for want of a field, at a time when the column published four file-lifecycle labels and
        /// <c>edit.open</c> was not one of them; §4 renders both today.
        /// </para>
        /// <para>
        /// <c>root-version</c> refuses NOTHING. No member of <c>LoadRefusalCodes</c> carries it, and the row is
        /// reported by <c>StructureRules</c> as an ordinary Error finding instead — so the "Fatal error | Open"
        /// the column once published was drift on both halves, and generating them corrected it to "Error | —".
        /// </para>
        /// </summary>
        [Test]
        public void ARowDeclaresTheOperationsItRefusesRatherThanLeavingThemToProse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Refused("attr-undeclared"),
                    Is.EquivalentTo(new[] { OperationCodes.Save, OperationCodes.EditOpen }),
                    "the save and the edit-open, which §4 now publishes as 'Save · Export, Edit-open'");
                Assert.That(Refused("root-version"), Is.Empty,
                    "nothing refuses an open under this cause, and §4 reads 'Error | —' accordingly");
                Assert.That(Refused("id-duplicate-token"), Is.EqualTo(new[] { OperationCodes.EditOpen }),
                    "the edit-open alone — which §4 words 'Error | Edit-open', because the file itself still "
                    + "opens and saves, while the PANEL lists the row as fatal. Two views, two questions");
            });
        }

        /// <summary>
        /// An operation head declares no refused operation: it IS the operation, not a cause of one. Declaring
        /// <c>io.save</c> on <c>io.save</c> would make the head its own cause and put the operation itself in a
        /// panel tier meant for content findings.
        /// </summary>
        [Test]
        public void AnOperationHeadRefusesNothingBecauseItIsTheOperationAndNotACause()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCatalogEntry entry in
                    Catalog.Entries.Where(e => e.Section == ProblemCatalogSection.OperationOutcomes))
                {
                    Assert.That(entry.RefusedOperations, Is.Empty, entry.Code.Value);
                }
            });
        }

        /// <summary>
        /// The vocabulary is closed: a declared refusal names one of the six operation heads. Anything else — a
        /// cause code, a typo, a host code — would name an operation no filter and no send gate can act on.
        /// Armed from both sides, since an invariant that cannot fail governs nothing.
        /// </summary>
        [Test]
        public void ADeclaredRefusalMustNameAnOperationHead()
        {
            ProblemCatalogEntry offender = Seeded(CatalogDisposition.Error) with
            {
                Code = new ProblemCode("row-refusing-a-non-head"),
                RefusedOperations = ImmutableArray.Create(new ProblemCode("name-empty")),
            };

            Assert.Multiple(() =>
            {
                Assert.That(CatalogInvariants.Check(Catalog, []), Is.Empty,
                    "the shipped catalogue declares only heads");

                Assert.That(
                    CatalogInvariants.Check(ProblemCatalog.From([.. Catalog.Entries, offender]), [])
                        .Select(d => (d.Code.Value, d.Violation)),
                    Does.Contain((offender.Code.Value, CatalogViolation.RefusedOperationNotAnOperationHead)),
                    "a refused operation that is not an operation head must be reported");

                Assert.That(
                    CatalogInvariants.Check(
                        ProblemCatalog.From([.. Catalog.Entries, offender with
                        {
                            RefusedOperations = ImmutableArray.Create(OperationCodes.BridgeUpload),
                        }]), []),
                    Is.Empty,
                    "and the same row naming a real head must not be");
            });
        }

        private static EquatableArray<ProblemCode> Refused(string code)
        {
            Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
            return entry.RefusedOperations;
        }

        /// <summary>
        /// An entry that exists only to exercise the disposition axis. It is never registered in a catalogue, so
        /// it moves no count, no oracle and no generated table.
        /// </summary>
        private static ProblemCatalogEntry Seeded(CatalogDisposition disposition) =>
            new(new ProblemCode("seeded-" + disposition.ToString().ToLowerInvariant()),
                ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic, disposition,
                RuleKind.UserContentRule, RuleFaces.WholeProject, default, FindingShape.OneFinding, default,
                "Syntetisk række.");

        /// <summary>
        /// Some project rows §4 publishes as Fatal also produce a finding today — the four schema guards that
        /// refuse a save (and, for <c>attr-undeclared</c>, an edit-open) while reporting at validate, plus the
        /// version check the reader does not actually perform. Those are <see cref="CatalogDisposition.Error"/>
        /// entries here: two faces, published honestly, not a contradiction. The 18 refusals have no finding face
        /// at all, which is exactly the population the operation-outcome work has to give a coded refusal.
        ///
        /// <para>The name carries no count for the reporting half deliberately. It said "Four" until
        /// <c>attr-required</c>'s §4 row was corrected from "Error | —" to "Fatal error | Save · Export" — the row
        /// had always refused the save, so the number was only ever a count of how many the DOCUMENT admitted.
        /// The 18 does not move with it: nothing here reclassifies an entry.</para>
        /// </summary>
        [Test]
        public void EighteenProjectRowsAreRefusalsAndThePublishedFatalsThatReportAreErrors()
        {
            IReadOnlyList<ProblemCatalogEntry> project = InSection(ProblemCatalogSection.ProjectFindings);

            Assert.Multiple(() =>
            {
                Assert.That(project.Count(e => e.Disposition == CatalogDisposition.Refusal), Is.EqualTo(18));

                foreach (string reporting in
                    new[] { "element-undeclared", "attr-undeclared", "attr-latin1", "attr-required", "root-version" })
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(reporting), out ProblemCatalogEntry entry), Is.True, reporting);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Error), reporting);
                }

                // A refusal is realised at a throw or precondition site, so no executor consumes it.
                foreach (ProblemCatalogEntry entry in Catalog.Entries.Where(e => e.Disposition == CatalogDisposition.Refusal))
                {
                    Assert.That(entry.Faces, Is.EqualTo(RuleFaces.None), entry.Code.Value);
                }

                // The implication runs ONE way, and only one way: an operation outcome always refuses, because
                // that is what "the operation cannot proceed" means. The converse is false — an EDIT PRECONDITION
                // also refuses, and an edit is not one of open/save/import/download/upload.
                foreach (ProblemCatalogEntry entry in Catalog.Entries.Where(e => e.Kind == RuleKind.OperationOutcome))
                {
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Refusal), entry.Code.Value);
                }
            });
        }

        /// <summary>
        /// The rows that left the finding set are entries, not deletions. Deleting them would lose the finding
        /// that they are NOT findings, and the next person would re-derive them from the same draft.
        /// <para>The four, and why each one left — a list worth reading before adding a fifth:</para>
        /// <list type="bullet">
        /// <item><description><c>name-helpfile-missing</c> and <c>struct-modified-stale</c>: the condition does
        /// not exist. One was falsified against the live build, the other has no decidable predicate because the
        /// stamp is current in any saved file.</description></item>
        /// <item><description><c>load-truncated</c>: not separately DECIDABLE — the parser refuses the file as
        /// malformed XML before truncation could be named, so it is already reported as
        /// <c>load-not-xml</c>.</description></item>
        /// <item><description><c>addr-unassigned</c>: not separately OBSERVABLE — <c>doc-address</c> fires on the
        /// same condition over the same elements, and the catalogue row admits it ("also reported as <i>Mangler
        /// Adresse</i>"). Two ids for one observation is two sentences and one repair.</description></item>
        /// </list>
        /// </summary>
        [Test]
        public void TheInvestigatedRowsAreKeptAsRuledOutEntries()
        {
            string[] ruledOut = [.. Catalog.Entries
                .Where(e => e.Status == ProblemCodeStatus.RuledOut)
                .Select(e => e.Code.Value)
                .OrderBy(v => v, StringComparer.Ordinal)];

            Assert.That(ruledOut, Is.EqualTo(
                new[] { "addr-unassigned", "load-truncated", "name-helpfile-missing", "struct-modified-stale" })
                .AsCollection);
        }

        /// <summary>
        /// The eight documentation labels are Danish already and are pinned byte-exact by the report oracles, so
        /// the catalogue must carry them verbatim. A drift here would move 24 oracle files.
        /// </summary>
        [Test]
        public void TheDocumentationRowsCarryTheirExistingDanishLabelsVerbatim()
        {
            (string Code, string Label)[] expected =
            [
                ("doc-documentation-tag", "Mangler Id-kode"),
                ("doc-power-group", "Mangler Lysgruppe"),
                ("doc-cabletype", "Mangler Kabeltype"),
                ("doc-cablenumber", "Mangler Kabelnummer"),
                ("doc-position", "Mangler Placering"),
                ("doc-not-linked", "Ikke forbundet"),
                ("doc-cable-colour", "Mangler Ledningsfarve"),
                ("doc-address", "Mangler Adresse"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((string code, string label) in expected)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.MessageTemplate, Is.EqualTo(label), code);
                    Assert.That(entry.Category, Is.EqualTo(ValidationCategory.Documentation), code);
                }
            });
        }

        /// <summary>
        /// The catalog-definition family is end-user text too: an installer meets these through catalog import,
        /// which is a GUI action with a Danish outcome. Their original English sentences survive as diagnostics so
        /// nothing is lost for whoever is hand-authoring a definition file.
        /// </summary>
        [Test]
        public void EveryCatalogDefinitionRowHasADanishLabelAndKeepsItsEnglishDiagnostic()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCatalogEntry entry in InSection(ProblemCatalogSection.CatalogDefinitionFindings))
                {
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, entry.Code.Value);
                    Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty, entry.Code.Value);
                    Assert.That(entry.Code.Family, Is.EqualTo(ProblemFamily.Validation),
                        entry.Code.Value + " keeps its bare id");
                }
            });
        }

        /// <summary>
        /// The vendor states a RECOMMENDATION for wireless capacity, not a hard limit, and an Error's consequence
        /// must hold whatever the author intended. Above the limit the devices still bind — the system answers
        /// more slowly — so the row is a Warning. This is the run's only severity correction.
        /// </summary>
        [Test]
        public void TheWirelessCapacityRowIsAWarningAndTheThreeCapacityRowsNeedControllerLimits()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Catalog.TryGet(new ProblemCode("capacity-wireless-exceeded"), out ProblemCatalogEntry wireless), Is.True);
                Assert.That(wireless.Disposition, Is.EqualTo(CatalogDisposition.Warning));

                string[] needLimits = [.. Catalog.Entries
                    .Where(e => e.RequiresControllerLimits)
                    .Select(e => e.Code.Value)
                    .OrderBy(v => v, StringComparer.Ordinal)];
                Assert.That(needLimits, Is.EqualTo(new[]
                {
                    "capacity-input-addresses", "capacity-input-modules", "capacity-output-addresses",
                    "capacity-output-modules", "capacity-resources-high",
                    "capacity-scenarios-per-receiver", "capacity-wireless-exceeded",
                    "capacity-wireless-links-per-unit",
                }).AsCollection, "a verdict that depends on the machine is not a property of the project file");

                // The modem row needs neither a limit nor a controller.
                Assert.That(Catalog.TryGet(new ProblemCode("capacity-modem-multiple"), out ProblemCatalogEntry modem), Is.True);
                Assert.That(modem.RequiresControllerLimits, Is.False);
            });
        }

        /// <summary>
        /// The claim that makes a separate release record unnecessary, tested rather than asserted.
        ///
        /// <para>A per-release changelog of codes added, removed and re-classified was specified on one unique
        /// justification: that a diff of declarations cannot tell a RENAME from a removal plus an addition. That
        /// justification is false here. Silent re-pointing is forbidden — a speaking id that outgrows its
        /// condition is SPLIT and the old id retired, never quietly redirected — and the retirement is recorded
        /// on the entry itself. So a split leaves a visible trail in the catalogue: the old code is still there,
        /// marked, beside the codes that replaced it. A plain addition leaves no such trail, and the two are
        /// therefore distinguishable without a second artifact to keep in sync.</para>
        /// </summary>
        [Test]
        public void ARenameIsDistinguishableFromARemovalPlusAnAddition()
        {
            ProblemCatalogEntry Row(string code, ProblemCodeStatus status) =>
                new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Addressing,
                    CatalogDisposition.Error, RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                    FindingShape.OnePerOccurrence, default, "Label", status);

            // A split: one id retired, two minted in its place. And, separately, a plain new row.
            ProblemCatalog after = ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(
            [
                Row("dataline-address", ProblemCodeStatus.Retired),
                Row("dataline-address-malformed", ProblemCodeStatus.Active),
                Row("dataline-address-range", ProblemCodeStatus.Active),
                Row("brand-new-row", ProblemCodeStatus.Active),
            ]));

            Assert.Multiple(() =>
            {
                Assert.That(after.TryGet(new ProblemCode("dataline-address"), out ProblemCatalogEntry split), Is.True,
                    "the retired id is still IN the catalogue — that is what records the event");
                Assert.That(split.Status, Is.EqualTo(ProblemCodeStatus.Retired));

                Assert.That(after.TryGet(new ProblemCode("brand-new-row"), out ProblemCatalogEntry added), Is.True);
                Assert.That(added.Status, Is.EqualTo(ProblemCodeStatus.Active),
                    "an addition carries no retirement beside it, so the two cases do not look alike");

                // And the retired id cannot be handed to a different condition later.
                EquatableArray<CatalogDefect> reused = CatalogInvariants.Check(
                    ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(
                    [
                        Row("dataline-address", ProblemCodeStatus.Retired),
                        Row("dataline-address", ProblemCodeStatus.Active),
                    ])), []);
                Assert.That(reused.Select(d => d.Violation), Does.Contain(CatalogViolation.DuplicateCode),
                    "reservation works because the retired entry still occupies the id");
            });
        }

        [Test]
        public void TheEightCategoriesCarryTheirCatalogueCodesAsData()
        {
            (ValidationCategory Category, string Code)[] expected =
            [
                (ValidationCategory.FileIntegrity, "INT"),
                (ValidationCategory.Wiring, "WIR"),
                (ValidationCategory.Logic, "LOG"),
                (ValidationCategory.Scenes, "SCN"),
                (ValidationCategory.Addressing, "ADR"),
                (ValidationCategory.DeviceSettings, "DEV"),
                (ValidationCategory.Documentation, "DOC"),
                (ValidationCategory.ProjectStructure, "PRJ"),
            ];

            Assert.Multiple(() =>
            {
                foreach ((ValidationCategory category, string code) in expected)
                {
                    Assert.That(category.ShortCode, Is.EqualTo(code));
                    Assert.That(ValidationCategory.TryParseShortCode(code, out ValidationCategory parsed), Is.True, code);
                    Assert.That(parsed, Is.EqualTo(category), code);
                }

                // Every member now has a code: the transitional one that had none is gone, which is what the
                // count below states rather than implies.
                Assert.That(Enum.GetValues<ValidationCategory>(), Has.Length.EqualTo(expected.Length));
                Assert.That(Enum.GetValues<ValidationCategory>().Select(c => c.ShortCode), Is.All.Not.Empty);
                Assert.That(ValidationCategory.TryParseShortCode("XXX", out _), Is.False);
            });
        }

        [Test]
        public void BindTemplateFillsDeclaredSlotsAndLeavesAnUnsuppliedOneVisible()
        {
            ProblemCatalogEntry entry = new(
                new ProblemCode("test-row"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("dataline_input", "address_dataline"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("address", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "Adresse {address} er optaget af {name}");

            Problem supplied = new(entry.Code, string.Empty, EquatableArray.Create<ProblemArgument>(
                [new ProblemArgument("address", 42), new ProblemArgument("name", "Stue loft")]));
            Problem partial = new(entry.Code, string.Empty, EquatableArray.Create<ProblemArgument>(
                [new ProblemArgument("address", 42)]));

            Assert.Multiple(() =>
            {
                Assert.That(entry.BindTemplate(supplied), Is.EqualTo("Adresse 42 er optaget af Stue loft"));
                Assert.That(entry.BindTemplate(partial), Is.EqualTo("Adresse 42 er optaget af {name}"),
                    "a visible gap is a defect a reader reports; a silent blank reads as intended text");
            });
        }

        [Test]
        public void TheCatalogueIsOneFrozenInstanceOrderedByCode()
        {
            ProblemCatalog first = ProblemCatalog.Current;
            ProblemCatalog second = ProblemCatalog.Current;

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.SameAs(second), "built once, shared for the process lifetime");
                string[] codes = [.. first.Entries.Select(e => e.Code.Value)];
                Assert.That(codes, Is.EqualTo(codes.OrderBy(c => c, StringComparer.Ordinal).ToArray()).AsCollection,
                    "a deterministic order is what makes a rendered table and a diff stable");
            });
        }
    }
}
