using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The THIRD declared context, and the one that behaves OPPOSITELY to the other two.
    ///
    /// <para><b>Enabling versus narrowing.</b> <c>RequiresControllerLimits</c> and <c>RequiresLibrary</c> are
    /// ENABLING: absent context means the rule does not run and does not report, because a verdict that depends on
    /// the machine is not a property of the project file. A firmware bound is NARROWING: the row runs and reports
    /// with no target declared, and a target can only ever WITHHOLD a finding whose fix it is past — never create
    /// one.</para>
    ///
    /// <para><b>Why the difference is not an inconsistency.</b> "This project uses the affected feature" IS a
    /// property of the file; only "does the affected feature still misbehave here" depends on the target. A
    /// guardrail that stayed silent until a controller was connected would be silent exactly while the project is
    /// being designed, which is the case it exists for.</para>
    ///
    /// <para><b>And it is not the evaluability axis.</b> A row withheld by a firmware target WAS evaluated; the
    /// export's not-run list answers <c>CanEvaluate</c> and must not pick it up, or the report tells the reader the
    /// caller withheld context that this row never needed.</para>
    /// </summary>
    [TestFixture]
    public sealed class FirmwareNarrowingTests
    {
        /// <summary>The release the vendor claims fixed the v3 holiday schedule — A29's bound, as a stand-in.</summary>
        private static readonly ControllerFirmwareVersion FixedIn = new(3, 3, 21);

        private static ProblemCatalogEntry Entry(string code, DeclaredFirmwareBound? bound = null) =>
            new(new ProblemCode(code), ProblemCatalogSection.ProjectFindings, ValidationCategory.Logic,
                CatalogDisposition.Warning, RuleKind.UserContentRule, RuleFaces.WholeProject, default,
                FindingShape.OnePerOccurrence, default, "Label")
            {
                FirmwareBound = bound,
            };

        private static DeclaredFirmwareBound Bound(ControllerFirmwareVersion? fixedIn) =>
            new("holiday-schedule", fixedIn, ThresholdConfidence.VendorRecommendation, "Stand-in for a real row's citation.");

        private static (Project Project, WholeProjectValidator Validator) Fixture(
            params ProblemCatalogEntry[] entries)
        {
            ProjectElement subject = Tree.Node("dataline_input", "_0x2a", []);
            Project project = new(Tree.Node("utcs_project", null, [], subject));
            ProblemCatalog catalog = ProblemCatalog.From(entries.ToImmutableArray());
            RuleSet rules = RuleSet.Create(catalog,
                entries.Select(e => new RuleBuilder(e).Inspect(i => i.Report(subject, default)).Build()));
            return (project, new WholeProjectValidator(rules));
        }

        private static string[] Run(ValidationProfile profile, params ProblemCatalogEntry[] entries)
        {
            (Project project, WholeProjectValidator validator) = Fixture(entries);
            return [.. validator.Validate(project, profile).Select(f => f.Code.Value)];
        }

        /// <summary>
        /// The forms the evidence actually writes a firmware version in. Every one of these appears in the
        /// reverse-engineering material or in a shipped message, so a parser that reads only <c>3.3.21</c> would
        /// force the declaring session to normalize by hand and get it wrong once.
        /// </summary>
        [Test]
        public void AVersionReadsTheFormsTheEvidenceActuallyUses()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ControllerFirmwareVersion.TryParse("3.3.21", out ControllerFirmwareVersion plain), Is.True);
                Assert.That(plain, Is.EqualTo(new ControllerFirmwareVersion(3, 3, 21)));

                Assert.That(ControllerFirmwareVersion.TryParse("03.03.33", out ControllerFirmwareVersion padded), Is.True);
                Assert.That(padded, Is.EqualTo(new ControllerFirmwareVersion(3, 3, 33)),
                    "leading zeros are the vendor's own writing, not a typo to reject");

                Assert.That(ControllerFirmwareVersion.TryParse("CTR.R.03.03.44", out ControllerFirmwareVersion designated), Is.True);
                Assert.That(designated, Is.EqualTo(new ControllerFirmwareVersion(3, 3, 44)),
                    "the controller designation prefix the dimmer errata quotes");

                Assert.That(ControllerFirmwareVersion.TryParse("v3.3.21", out ControllerFirmwareVersion prefixed), Is.True);
                Assert.That(prefixed, Is.EqualTo(new ControllerFirmwareVersion(3, 3, 21)),
                    "the shipped firmware-mismatch message's own v%d.%d.%d");

                Assert.That(ControllerFirmwareVersion.TryParse("03.04.72.03", out ControllerFirmwareVersion four), Is.True);
                Assert.That(four, Is.EqualTo(new ControllerFirmwareVersion(3, 4, 72, 3)),
                    "four components, the desktop build's own form");
            });
        }

        /// <summary>
        /// The other half of the same commitment: what it cannot read it REFUSES, rather than reading a prefix and
        /// guessing the rest. A half-parsed version silently narrows a real finding away.
        /// </summary>
        [Test]
        public void AVersionRefusesWhatItCannotReadRatherThanGuessing()
        {
            string?[] unreadable = [null, "", "   ", "3", "3..1", "3.3.21-beta", "abc", "3.3.99999999999999", "."];

            Assert.Multiple(() =>
            {
                foreach (string? text in unreadable)
                {
                    Assert.That(ControllerFirmwareVersion.TryParse(text, out ControllerFirmwareVersion parsed), Is.False,
                        text ?? "<null>");
                    Assert.That(parsed, Is.EqualTo(default(ControllerFirmwareVersion)),
                        "a refused parse leaves nothing behind to be mistaken for a target");
                }
            });
        }

        [Test]
        public void VersionsOrderByComponent()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new ControllerFirmwareVersion(3, 3, 20) < new ControllerFirmwareVersion(3, 3, 21));
                Assert.That(new ControllerFirmwareVersion(3, 3, 21) < new ControllerFirmwareVersion(3, 4, 0));
                Assert.That(new ControllerFirmwareVersion(3, 4, 0) < new ControllerFirmwareVersion(4, 0, 0));
                Assert.That(new ControllerFirmwareVersion(3, 3, 21) >= new ControllerFirmwareVersion(3, 3, 21));
                Assert.That(new ControllerFirmwareVersion(3, 3, 21, 1) > new ControllerFirmwareVersion(3, 3, 21));
            });
        }

        /// <summary>
        /// THE GUARDRAIL PROPERTY. Everything else in this fixture is a refinement of it.
        /// </summary>
        [Test]
        public void AFirmwareBoundedRowReportsWhenNoTargetIsDeclared()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Run(ValidationProfile.ProjectOnly, Entry("errata-row", Bound(FixedIn))),
                    Is.EqualTo(new[] { "errata-row" }).AsCollection,
                    "a row that is silent until a controller is connected is silent while the project is designed");
                Assert.That(ValidationProfile.ProjectOnly.Firmware, Is.Null);
                Assert.That(ValidationProfile.Categorized.Firmware, Is.Null);
            });
        }

        [Test]
        public void ATargetBelowTheFixStillReportsIt()
        {
            ValidationProfile older = ValidationProfile.ProjectOnly with
            {
                Firmware = new ControllerFirmwareVersion(3, 3, 20),
            };

            Assert.That(Run(older, Entry("errata-row", Bound(FixedIn))),
                Is.EqualTo(new[] { "errata-row" }).AsCollection);
        }

        /// <summary>
        /// The bound is INCLUSIVE — "fixed in 3.3.21" means 3.3.21 itself carries the fix.
        /// </summary>
        [Test]
        public void ATargetAtOrPastTheFixWithholdsIt()
        {
            Assert.Multiple(() =>
            {
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = FixedIn }, Entry("errata-row", Bound(FixedIn))),
                    Is.Empty, "at the fix");
                Assert.That(
                    Run(ValidationProfile.ProjectOnly with { Firmware = new ControllerFirmwareVersion(3, 4, 0) },
                        Entry("errata-row", Bound(FixedIn))),
                    Is.Empty, "past the fix");
            });
        }

        /// <summary>
        /// The Error case of the severity rule, mechanically: a defect no release is known to fix cannot be
        /// narrowed away by any target, however new.
        /// </summary>
        [Test]
        public void ADefectFixedInNoKnownReleaseIsNeverWithheld()
        {
            ValidationProfile newest = ValidationProfile.ProjectOnly with
            {
                Firmware = new ControllerFirmwareVersion(9, 9, 9),
            };

            Assert.That(Run(newest, Entry("errata-row", Bound(null))),
                Is.EqualTo(new[] { "errata-row" }).AsCollection);
        }

        /// <summary>
        /// Narrowing only ever REMOVES. A row that declares no bound is untouched by any target, so supplying one
        /// can never turn a quiet project into a reported one.
        /// </summary>
        [Test]
        public void ANarrowingContextCanOnlyRemoveAFindingNeverCreateOne()
        {
            ProblemCatalogEntry plain = Entry("plain-row");
            ValidationProfile withTarget = ValidationProfile.ProjectOnly with { Firmware = FixedIn };

            Assert.That(Run(withTarget, plain), Is.EqualTo(Run(ValidationProfile.ProjectOnly, plain)).AsCollection);
        }

        /// <summary>
        /// The distinction the findings export depends on. Fold narrowing into <c>CanEvaluate</c> and every
        /// firmware-narrowed row is published as one the caller failed to supply context for.
        /// </summary>
        [Test]
        public void NarrowingIsNotEvaluability()
        {
            ProblemCatalogEntry errata = Entry("errata-row", Bound(FixedIn));
            ValidationProfile past = ValidationProfile.ProjectOnly with
            {
                Firmware = new ControllerFirmwareVersion(3, 4, 0),
            };

            Assert.Multiple(() =>
            {
                Assert.That(past.Includes(errata), Is.False, "the row does not run against this target");
                Assert.That(past.CanEvaluate(errata), Is.True,
                    "but it WAS evaluable — it needs no context the caller could have failed to supply");
                Assert.That(errata.RequiresControllerLimits, Is.False);
                Assert.That(errata.RequiresLibrary, Is.False);
            });
        }

        /// <summary>
        /// Pins the shape of the decision, the way the profile fixture pins the absence of a threshold type: an
        /// enabling flag here would SKIP the row when no target is declared, which is the opposite of a guardrail.
        /// </summary>
        [Test]
        public void ThereIsNoEnablingFlagForFirmware()
        {
            string[] properties = [.. typeof(ProblemCatalogEntry).GetProperties().Select(p => p.Name)];

            Assert.Multiple(() =>
            {
                Assert.That(properties, Does.Not.Contain("RequiresFirmware"));
                Assert.That(properties, Does.Contain(nameof(ProblemCatalogEntry.FirmwareBound)));
            });
        }

        /// <summary>
        /// The mechanism is IN USE, which is what retired its inertness tripwire.
        ///
        /// <para><c>TheMechanismIsInertUntilARowDeclaresABound</c> stood here and asserted that no catalogue
        /// entry carried a <see cref="DeclaredFirmwareBound"/>. Its own doc-comment said to delete it in the diff
        /// that added the first errata row, and <c>logic-block-recursive</c> is that row — it is satisfied by
        /// being retired, not by being weakened.</para>
        ///
        /// <para>This replaces it with the claim that is true from here on and stays true: every declared bound
        /// is REACHABLE — it belongs to an entry the catalogue holds under the code it names. A bound declared on
        /// nothing, or on a retired row, would narrow findings that never appear.</para>
        /// </summary>
        [Test]
        public void EveryDeclaredFirmwareBoundBelongsToALiveRow()
        {
            ProblemCatalogEntry[] bounded =
                [.. ProblemCatalog.Current.Entries.Where(e => e.FirmwareBound is not null)];

            Assert.Multiple(() =>
            {
                Assert.That(bounded, Is.Not.Empty,
                    "the mechanism is in use — if this is empty the errata rows have gone, not the test");
                foreach (ProblemCatalogEntry entry in bounded)
                {
                    Assert.That(entry.Status, Is.EqualTo(ProblemCodeStatus.Active), entry.Code.Value);
                    Assert.That(entry.FirmwareBound!.Evidence, Is.Not.Empty, entry.Code.Value);
                    Assert.That(entry.FirmwareBound.Name, Is.Not.Empty, entry.Code.Value);
                }
            });
        }
    }
}
