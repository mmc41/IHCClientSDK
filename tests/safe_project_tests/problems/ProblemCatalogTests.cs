using System;
using System.Collections.Generic;
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

                // 134 live project rows, plus the 4 that were investigated and ruled out, plus the 3 that were
                // SPLIT and retired. All three kinds keep their ids occupied, which is the whole reservation
                // mechanism: an entry that stays is an id that can never be handed to a different condition.
                // The fourth ruled-out row is T048's `addr-unassigned`: its condition is what `doc-address`
                // already reports, on the same elements, which the catalogue row itself admits. The retired rows
                // are `dataline-address`, `capacity-modules-exceeded` (split into the three capacity rows under
                // D2) and `capacity-addresses` (split again, per direction, so a project over on both no longer
                // reports two findings distinguishable only by their numbers).
                Assert.That(project, Has.Count.EqualTo(141));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.Active), Is.EqualTo(134));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.RuledOut), Is.EqualTo(4));
                Assert.That(project.Count(e => e.Status == ProblemCodeStatus.Retired), Is.EqualTo(3));

                // The ten codes that already shipped with no catalogue row behind them.
                Assert.That(definitions, Has.Count.EqualTo(10));

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

            ProblemCatalog broken = ProblemCatalog.From(EquatableArray.Create<ProblemCatalogEntry>(
            [
                Row("twice", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
                Row("twice", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
                Row("io.load", ProblemCatalogSection.OperationOutcomes, ValidationCategory.FileIntegrity),
                Row("no-category", ProblemCatalogSection.ProjectFindings, null),
                Row("unimplemented", ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic),
            ]));

            CatalogViolation[] fromDeclarations = [.. CatalogInvariants.Check(broken, []).Select(d => d.Violation)];
            CatalogDefect[] withRules = [.. CatalogInvariants.Check(broken,
                [new ProblemCode("unimplemented"), new ProblemCode("nowhere-declared")])];

            Assert.Multiple(() =>
            {
                Assert.That(fromDeclarations, Does.Contain(CatalogViolation.DuplicateCode));
                Assert.That(fromDeclarations, Does.Contain(CatalogViolation.CategoryMisplaced));

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

        [Test]
        public void SeverityFollowsFromDispositionSoTheTwoCannotDisagree()
        {
            Assert.Multiple(() =>
            {
                foreach (ProblemCatalogEntry entry in Catalog.Entries)
                {
                    ValidationSeverity? expected = entry.Disposition switch
                    {
                        CatalogDisposition.Error => ValidationSeverity.Error,
                        CatalogDisposition.Warning => ValidationSeverity.Warning,
                        _ => null,
                    };
                    Assert.That(entry.Severity, Is.EqualTo(expected), entry.Code.Value);
                }
            });
        }

        /// <summary>
        /// The catalogue rates 22 project rows Fatal, but four of those also produce a finding today — three
        /// schema guards that refuse a save and report at validate, and the version check the reader does not
        /// actually perform. Those four are Errors here; the remaining 18 are refusals with no finding face at
        /// all, which is exactly the population the operation-outcome work has to give a coded refusal.
        /// </summary>
        [Test]
        public void EighteenProjectRowsAreRefusalsAndTheFourReportingFatalsAreErrors()
        {
            IReadOnlyList<ProblemCatalogEntry> project = InSection(ProblemCatalogSection.ProjectFindings);

            Assert.Multiple(() =>
            {
                Assert.That(project.Count(e => e.Disposition == CatalogDisposition.Refusal), Is.EqualTo(18));

                foreach (string reporting in new[] { "element-undeclared", "attr-undeclared", "attr-latin1", "root-version" })
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
                    "capacity-output-modules", "capacity-resources-high", "capacity-wireless-exceeded",
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
