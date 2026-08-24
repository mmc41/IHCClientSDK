using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

using Ihc.Vis.Projects;
using Ihc.Vis.Session;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// T061 — the gate that makes "all of them" a FACT rather than a claim: every catalogue entry has something
    /// behind it, every implementation has an entry, and every entry's declared severity and category are the ones
    /// its findings actually carry.
    ///
    /// <para><b>The totals are READ, never typed.</b> <see cref="ProblemCatalog.Total"/> and the registered rule
    /// set are the only sources; a hard-coded 134 is exactly the assumption that made this invariant false before
    /// the run started, because codes shipped outside the original draft.</para>
    ///
    /// <para><b>Two kinds of "behind it", and the test knows the difference.</b> A content row is implemented by a
    /// REGISTERED RULE. An operation outcome — an edit refusal, a load or save refusal, an import or bridge
    /// refusal, a catalog-definition finding — is minted by CODE, and its evidence is that its identity is
    /// referenced in the SDK sources outside the declaration file it is declared in. Neither kind is allowed to be
    /// missing, and nothing implemented is allowed to be undeclared.</para>
    ///
    /// <para><b>Required context is coverage, not absence (D21).</b> A capacity row that needs a controller profile
    /// IS registered and implemented; what the profile does is decline to EVALUATE it. The test asserts that pair
    /// directly — registered, and excluded from the default profile — rather than treating the row as uncovered.
    /// </para>
    ///
    /// <para><b>And a reclassification stays a decision.</b> The two rows moved to deliberate non-findings keep
    /// their ids reserved with <see cref="ProblemCodeStatus.RuledOut"/> and no rule. If either were flipped back to
    /// Active without a rule, <see cref="EveryActiveEntryHasSomethingBehindIt"/> fails — which is the difference
    /// between a decision and a hole.</para>
    /// </summary>
    [TestFixture]
    public sealed class CatalogCompletenessTests
    {
        /// <summary>The declaration files: a code's appearance HERE is a declaration, not an implementation.</summary>
        private static readonly string[] DeclarationFiles =
        [
            "ProblemCatalogEntries.cs",
            "ProblemCatalogEntries.ProjectFindings.cs",
            "ProblemCatalogEntries.EditRefusals.cs",
            "ProblemCatalogEntries.CatalogDefinitions.cs",
        ];

        private static ProblemCatalog Catalog => ProblemCatalog.Current;

        // ── the two directions of coverage ──────────────────────────────────────────────────────────

        /// <summary>
        /// The Active entries with nothing behind them, each with the reason and the task that owns it. This list
        /// is the whole point of the test: it is asserted EXACTLY, in both directions, so a new gap fails the gate
        /// and a gap that gets closed fails it too until the line is removed — which is precisely what happened
        /// when D27 unblocked <c>logic-block-locked-content</c>: the row gained a rule, this test went red, and the
        /// line came out. One remains.
        /// </summary>
        private static readonly (string Code, string Why)[] KnownUnimplemented =
        [
            ("import-catalog-wrong-kind",
                "No refusal exists to give an identity to: reading a .ifb as a product SUCCEEDS today (measured — "
                + "an empty product_identifier and a functionblock body), so the row and the code disagree until a "
                + "product ruling closes the gap. Recorded in the entry and in the severity-times-operation "
                + "matrix; introducing a refusal is a product decision, not a rule-authoring one."),
        ];

        [Test]
        public void EveryActiveEntryHasSomethingBehindIt()
        {
            IReadOnlyCollection<ProblemCode> implemented = Implemented();

            EquatableArray<CatalogDefect> defects = CatalogInvariants.Check(Catalog, implemented);
            string[] uncovered =
            [
                .. defects.Where(d => d.Violation == CatalogViolation.EntryWithoutRule)
                    .Select(d => d.Code.Value)
                    .OrderBy(code => code, StringComparer.Ordinal),
            ];

            Assert.Multiple(() =>
            {
                Assert.That(Catalog.Total, Is.GreaterThan(100),
                    "the totals are read from the catalogue, never typed — a shrunken catalogue would otherwise "
                    + "make this whole suite vacuous");
                Assert.That(implemented, Is.Not.Empty);

                Assert.That(defects.Where(d => d.Violation != CatalogViolation.EntryWithoutRule), Is.Empty,
                    "nothing implemented may be undeclared, and no entry may be malformed. Defects: "
                    + string.Join(" | ", defects.Select(d => $"{d.Code.Value}: {d.Violation}")));

                // EXACTLY the two known gaps: a third one fails here, and so does a gap that has been closed but
                // whose line is still in the list.
                Assert.That(uncovered,
                    Is.EqualTo(KnownUnimplemented.Select(k => k.Code).OrderBy(c => c, StringComparer.Ordinal))
                        .AsCollection,
                    "every Active entry needs a registered rule or a coded origin. The only permitted exceptions "
                    + "are the two named in KnownUnimplemented, each with its reason and its owning task — if this "
                    + "fails, either a new row was left unimplemented or a listed one was implemented and its "
                    + "line should go.");

                foreach ((string code, string why) in KnownUnimplemented)
                {
                    Assert.That(why, Has.Length.GreaterThan(80),
                        $"{code}'s exception has to carry its reasoning, or the list becomes a mute allow-list");
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True, code);
                    Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.Active),
                        $"{code} is a real finding with no implementation yet — a RuledOut status would claim the "
                        + "condition is not a finding, which is false");
                    Assert.That(entry.MessageTemplate, Is.Empty,
                        $"{code} has no user-facing text yet, which is what an unimplemented row looks like");
                }
            });
        }

        /// <summary>
        /// The count nobody may type: what the catalogue holds is what its declarations hold. Asserted as a
        /// relationship between the two sides rather than as a number, so adding a row cannot break it and
        /// dropping one silently cannot pass.
        /// </summary>
        [Test]
        public void TheCatalogueTotalIsTheSumOfItsSections()
        {
            int bySection = Enum.GetValues<ProblemCatalogSection>()
                .Sum(section => Catalog.Entries.Count(e => e.Section == section));

            Assert.Multiple(() =>
            {
                Assert.That(bySection, Is.EqualTo(Catalog.Total));
                Assert.That(Catalog.Entries.Select(e => e.Code.Value).Distinct().Count(), Is.EqualTo(Catalog.Total),
                    "ids are unique across every section, not per section");
            });
        }

        // ── the declared severity and category are the ones findings carry ──────────────────────────

        /// <summary>
        /// The agreement check, over REAL output: every row of the characterization recording carries the severity
        /// and category its entry declares. A rule whose entry says Warning and whose finding says Error would
        /// otherwise pass every other test in this suite.
        /// </summary>
        [Test]
        public void EveryRecordedFindingCarriesItsEntrysSeverityAndCategory()
        {
            ImmutableArray<(string RuleId, string Severity, string Category)> rows = Recording();

            Assert.Multiple(() =>
            {
                Assert.That(rows, Is.Not.Empty, "the recording is the evidence; an empty read proves nothing");
                foreach ((string ruleId, string severity, string category) in rows.Distinct())
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(ruleId), out ProblemCatalogEntry entry), Is.True,
                        ruleId);
                    Assert.That(severity, Is.EqualTo(Expected(entry.Disposition)), ruleId);
                    Assert.That(category, Is.EqualTo(entry.Category?.ToString()), ruleId);
                }
            });
        }

        /// <summary>
        /// The definition findings' severities, which nothing else checks. A catalog-definition row is raised as a
        /// LITERAL <c>ValidationSeverity</c> beside a literal code inside a builder — the pre-catalogue style, kept
        /// because a builder may not read the catalogue (L4) — so its severity and its entry's disposition are two
        /// independent copies of one decision. The recording cannot cover them: these findings are about
        /// <c>.def</c>/<c>.ifb</c> files, which the project corpus never validates.
        /// </summary>
        [Test]
        public void EveryDefinitionFindingsRaisedSeverityMatchesItsEntry()
        {
            var raised = new List<(string Code, string Severity, string File)>();
            foreach (string path in SourceFiles())
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(path),
                    @"ValidationSeverity\.(\w+),\s*""([a-z][a-z0-9-]+)"""))
                {
                    raised.Add((match.Groups[2].Value, match.Groups[1].Value, Path.GetFileName(path)));
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(raised, Is.Not.Empty,
                    "the builders raise their findings as literal severity + literal code pairs; matching none "
                    + "would make this rule vacuous");
                foreach ((string code, string severity, string file) in raised)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True,
                        $"{file} raises '{code}', which needs an entry");
                    Assert.That(severity, Is.EqualTo(Expected(entry.Disposition)),
                        $"{file} raises '{code}' as {severity} while its entry declares "
                        + $"{entry.Disposition} — the entry is the truth");
                }
            });
        }

        // ── required context is coverage, not absence ───────────────────────────────────────────────

        [Test]
        public void ARowNeedingAControllerProfileIsCoveredAndDeclaredRatherThanAbsent()
        {
            ImmutableArray<ProblemCatalogEntry> needsController =
            [
                .. Catalog.Entries.Where(e => e.RequiresControllerLimits),
            ];
            HashSet<string> registered = RegisteredCodes();

            Assert.Multiple(() =>
            {
                Assert.That(needsController, Is.Not.Empty, "five capacity rows declare the requirement");
                foreach (ProblemCatalogEntry entry in needsController)
                {
                    Assert.That(registered, Does.Contain(entry.Code.Value),
                        $"{entry.Code.Value} is registered — required context is not an excuse for absence");
                    Assert.That(ValidationProfile.Categorized.Includes(entry), Is.False,
                        $"{entry.Code.Value} is not EVALUATED without a declared controller");
                    Assert.That(
                        (ValidationProfile.Categorized with
                        {
                            Controller = ControllerCapabilityLimits.VendorDocumented,
                        }).Includes(entry),
                        Is.True, $"{entry.Code.Value} IS evaluated once one is named");
                }
            });
        }

        // ── a reclassification stays a decision ─────────────────────────────────────────────────────

        [Test]
        public void TheTwoReclassifiedRowsKeepTheirIdsAndHaveNoRule()
        {
            string[] reclassified = ["name-helpfile-missing", "struct-modified-stale"];
            HashSet<string> registered = RegisteredCodes();

            Assert.Multiple(() =>
            {
                foreach (string code in reclassified)
                {
                    Assert.That(Catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry), Is.True,
                        $"{code} keeps its id reserved, which is what stops it being reused");
                    Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.RuledOut), code);
                    Assert.That(registered, Does.Not.Contain(code), code);
                }
            });
        }

        /// <summary>
        /// The armed control, from BOTH sides, because a completeness test that cannot fail is a decoration: an
        /// Active entry nothing implements must be reported, and an implementation with no entry must be reported.
        /// </summary>
        [Test]
        public void TheCompletenessCheckFailsWhenEitherSideLosesAnEntry()
        {
            ProblemCode absent = new("brand-new-row-with-no-rule");
            ProblemCatalog withExtraEntry = ProblemCatalog.From(
            [
                .. Catalog.Entries,
                new ProblemCatalogEntry(absent, ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic,
                    CatalogDisposition.Warning, RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                    FindingShape.OneFinding, default, "Label"),
            ]);
            IReadOnlyCollection<ProblemCode> implemented = Implemented();

            Assert.Multiple(() =>
            {
                Assert.That(
                    CatalogInvariants.Check(withExtraEntry, implemented)
                        .Select(d => (d.Code.Value, d.Violation)),
                    Does.Contain((absent.Value, CatalogViolation.EntryWithoutRule)),
                    "an entry with nothing behind it must be reported");

                Assert.That(
                    CatalogInvariants.Check(Catalog, [.. implemented, new ProblemCode("undeclared-implementation")])
                        .Select(d => (d.Code.Value, d.Violation)),
                    Does.Contain(("undeclared-implementation", CatalogViolation.RuleWithoutEntry)),
                    "and an implementation with no entry must be reported");
            });
        }

        // ── one family, checked whole ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Every code the session can refuse with is a governed catalogue entry, and governed the RIGHT way — the
        /// section, kind and disposition an edit outcome must carry, a Danish sentence to render, no content
        /// category, and the <c>edit</c> family.
        ///
        /// <para><b>Rehomed here, and not merely moved.</b> It was written beside the command-evaluator face and
        /// would have been deleted with it, but it is not about that face at all: it is a completeness gate over
        /// <c>EditRefusalCodes.All</c>, which is this fixture's subject. A family governed nowhere is the exact
        /// defect this catalogue was created after finding — ten codes already shipping with no entry behind any
        /// of them — so it runs over the WHOLE family rather than over one example.</para>
        /// </summary>
        [Test]
        public void EveryEditRefusalCodeIsAGovernedCatalogueEntry()
        {
            Assert.Multiple(() =>
            {
                Assert.That(EditRefusalCodes.All, Is.Not.Empty);
                foreach (ProblemCode code in EditRefusalCodes.All)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(code, out ProblemCatalogEntry entry), Is.True, code.Value);
                    Assert.That(entry.Section, Is.EqualTo(ProblemCatalogSection.OperationOutcomes), code.Value);
                    Assert.That(entry.Kind, Is.EqualTo(RuleKind.EditPrecondition), code.Value);
                    Assert.That(entry.Disposition, Is.EqualTo(CatalogDisposition.Refusal), code.Value);
                    Assert.That(entry.MessageTemplate, Is.Not.Empty, code.Value + ": the Danish sentence");
                    Assert.That(entry.Category, Is.Null, code.Value + ": an edit outcome has no content category");
                    Assert.That(code.Family, Is.EqualTo(ProblemFamily.Edit), code.Value);
                }
            });
        }

        // ── the two sources of "implemented" ────────────────────────────────────────────────────────

        /// <summary>Every code something actually implements: a registered rule, or a minting site in the SDK.</summary>
        private static IReadOnlyCollection<ProblemCode> Implemented()
        {
            HashSet<string> codes = RegisteredCodes();
            codes.UnionWith(MintedCodes());
            return [.. codes.Select(c => new ProblemCode(c))];
        }

        private static HashSet<string> RegisteredCodes() =>
            [.. ProjectRules.All(Catalog).Select(r => r.Entry.Code.Value)];

        /// <summary>
        /// The codes the SDK MINTS: an operation outcome is raised by a code path rather than detected by a rule, so
        /// its evidence is a reference to its identity outside the declaration files — either the literal code text
        /// (how the catalog-definition findings are raised) or the registry member the entry and the raising site
        /// share (<c>EditRefusalCodes.TargetMissing</c> and its five sibling registries).
        /// </summary>
        private static HashSet<string> MintedCodes()
        {
            string sources = string.Join('\n', SourceFiles().Select(File.ReadAllText));
            HashSet<string> minted = new(StringComparer.Ordinal);

            foreach (ProblemCatalogEntry entry in Catalog.Entries)
            {
                if (sources.Contains($"\"{entry.Code.Value}\"", StringComparison.Ordinal))
                {
                    minted.Add(entry.Code.Value);
                }
            }

            foreach ((string code, string reference) in RegistryMembers())
            {
                if (sources.Contains(reference, StringComparison.Ordinal))
                {
                    minted.Add(code);
                }
            }

            return minted;
        }

        /// <summary>
        /// Every SDK source file that could MINT a code, which is every file outside the validation layer.
        /// <para>
        /// THE VALIDATION LAYER IS EXCLUDED WHOLESALE, and finding out why was the arming pass: a rule file names
        /// its own codes as string literals when it registers them
        /// (<c>Rule(catalog, "struct-locality-empty", …)</c>), so a scan that reads those files accepts a rule
        /// family that is NOT registered — unregistering one and watching this test stay green is exactly what
        /// happened. Registration is checked directly against the rule set instead; a literal inside the layer is
        /// bookkeeping, never evidence.
        /// </para>
        /// </summary>
        private static IEnumerable<string> SourceFiles() =>
            Directory.EnumerateFiles(Path.Combine(TestRepository.RequireRoot(), "ihcclient", "src"), "*.cs",
                    SearchOption.AllDirectories)
                .Where(path => !path.Contains(Path.Combine("vis", "validation"), StringComparison.Ordinal))
                .Where(path => !DeclarationFiles.Contains(Path.GetFileName(path), StringComparer.Ordinal));

        /// <summary>
        /// Every <c>ProblemCode</c> a registry class exposes, as (code value, the reference a raising site writes).
        /// Reflected rather than listed, so a new registry or member is covered the day it appears.
        /// </summary>
        private static IEnumerable<(string Code, string Reference)> RegistryMembers()
        {
            foreach (Type registry in typeof(ProblemCode).Assembly.GetTypes()
                .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && t.Name.EndsWith("Codes", StringComparison.Ordinal)))
            {
                foreach (PropertyInfo property in registry
                    .GetProperties(BindingFlags.Public | BindingFlags.Static)
                    .Where(p => p.PropertyType == typeof(ProblemCode)))
                {
                    if (property.GetValue(null) is ProblemCode code && code.Value is { Length: > 0 } value)
                    {
                        yield return (value, $"{registry.Name}.{property.Name}");
                    }
                }

                foreach (FieldInfo field in registry
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(f => f.FieldType == typeof(ProblemCode)))
                {
                    if (field.GetValue(null) is ProblemCode code && code.Value is { Length: > 0 } value)
                    {
                        yield return (value, $"{registry.Name}.{field.Name}");
                    }
                }
            }
        }

        /// <summary>The characterization recording, as (rule id, severity, category) triples.</summary>
        private static ImmutableArray<(string RuleId, string Severity, string Category)> Recording() =>
        [
            .. File.ReadAllLines(Path.Combine(TestRepository.RequireRoot(), "tests", "testdata", "validation",
                    "rule-characterization.txt"))
                .Where(line => line.Length > 0 && !line.StartsWith('#'))
                .Select(line => line.Split('\t'))
                .Where(cells => cells.Length >= 4)
                .Select(cells => (cells[2], cells[1], cells[3])),
        ];

        /// <summary>The severity a disposition produces, as the recording spells it.</summary>
        private static string Expected(CatalogDisposition disposition) => disposition switch
        {
            CatalogDisposition.Error => nameof(ValidationSeverity.Error),
            _ => nameof(ValidationSeverity.Warning),
        };
    }
}
