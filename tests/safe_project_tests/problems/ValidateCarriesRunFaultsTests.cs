using System.Linq;

using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// The REPORTING faces carry what the run broke, not only what it found.
    ///
    /// <para><b>The hole this closes.</b> <see cref="ProjectValidationResult"/> had one channel, so
    /// <see cref="ProjectAppService.Validate"/> and <see cref="ProjectAppService.ValidateCategorized"/> flattened
    /// a faulted run into a result that answered <c>IsValid = true</c> — a clean bill of health produced by the
    /// crash. The save and upload gates were closed against that by reading the structured run first, but every
    /// OTHER caller of the flat faces (an SDK consumer gating its own transfer, a report, a panel) still had no
    /// way to learn the checklist never finished, because the fact was discarded at the boundary.</para>
    ///
    /// <para><b>A second axis, not a harder validity.</b> <see cref="ProjectValidationResult.IsValid"/> keeps
    /// meaning "no Error-severity finding". A crashed rule is not a project defect, so it must not make a clean
    /// project invalid; and it is not nothing, so it must not leave the result looking complete. The two
    /// questions are asked separately for the same reason
    /// <c>ValidationMonitor.HasBlockingFindings</c> and <c>HasIncompleteRun</c> are.</para>
    /// </summary>
    [TestFixture]
    public sealed class ValidateCarriesRunFaultsTests
    {
        private static ProjectAppService ServiceOver(StructuredValidationResult run) =>
            Fakes.FileServiceOver(run);

        private static StructuredValidationResult Faulted(params string[] rules) => Fakes.FaultedRun(rules);

        [Test]
        public void AFaultedRunIsReportedAsIncompleteByValidate()
        {
            ProjectValidationResult result = ServiceOver(Faulted("name-empty")).Validate(Tree.MinimalProject());

            Assert.Multiple(() =>
            {
                Assert.That(result.IsComplete, Is.False, "a rule threw, so the checklist never finished");
                Assert.That(result.Faults.Length, Is.EqualTo(1));
                Assert.That(result.Faults[0].Code.Value, Is.EqualTo("internal.rule-failed"));
            });
        }

        /// <summary>
        /// The categorized face is the same run through a wider profile, so it answers the same way. Asserted
        /// separately because it is a separate door, and a fix applied to one flattening site and not the other
        /// is exactly the shape of the hole this closes.
        /// </summary>
        [Test]
        public void AFaultedRunIsReportedAsIncompleteByValidateCategorized()
        {
            ProjectValidationResult result = ServiceOver(Faulted("name-empty")).ValidateCategorized(Tree.MinimalProject());

            Assert.That(result.IsComplete, Is.False);
        }

        /// <summary>
        /// INCOMPLETE does not mean INVALID. A crashed rule says nothing about the project, so a run that found
        /// no error still reports a valid project — the caller is told the answer is partial, not that the
        /// project is broken.
        /// </summary>
        [Test]
        public void AFaultDoesNotMakeAFindingFreeProjectInvalid()
        {
            ProjectValidationResult result = ServiceOver(Faulted("name-empty")).Validate(Tree.MinimalProject());

            Assert.Multiple(() =>
            {
                Assert.That(result.IsValid, Is.True, "no Error finding was reported, and a fault is not one");
                Assert.That(result.Errors, Is.Empty);
                Assert.That(result.IsComplete, Is.False, "and the caller still learns the run was partial");
            });
        }

        [Test]
        public void EveryFaultSurvivesTheFlattening()
        {
            ProjectValidationResult result =
                ServiceOver(Faulted("name-empty", "id-duplicate")).Validate(Tree.MinimalProject());

            Assert.That(result.Faults.Select(fault => fault.Detail),
                Is.EqualTo(new[] { "Rule 'name-empty' threw", "Rule 'id-duplicate' threw" }).AsCollection,
                "the faults name which rules failed, and a caller listing them needs all of them");
        }

        /// <summary>
        /// A run that broke nothing is COMPLETE, and that is the default rather than something a caller opts
        /// into: every result built from findings alone — the definition builders' included — has no fault
        /// channel to fill and must not read as partial.
        /// </summary>
        [Test]
        public void ACleanRunIsComplete()
        {
            ProjectValidationResult result =
                ServiceOver(StructuredValidationResult.Empty).Validate(Tree.MinimalProject());

            Assert.Multiple(() =>
            {
                Assert.That(result.IsComplete, Is.True);
                Assert.That(result.Faults, Is.Empty);
                Assert.That(ProjectValidationResult.Success.IsComplete, Is.True,
                    "and the shared clean value says the same");
            });
        }

        /// <summary>
        /// The faults participate in EQUALITY. Two runs that found the same nothing but broke different rules are
        /// different answers, and a host diffing results to decide whether to redraw must not treat them as one.
        /// </summary>
        [Test]
        public void TwoResultsDifferingOnlyInTheirFaultsAreNotEqual()
        {
            ProjectValidationResult first = ServiceOver(Faulted("name-empty")).Validate(Tree.MinimalProject());
            ProjectValidationResult second = ServiceOver(Faulted("id-duplicate")).Validate(Tree.MinimalProject());

            Assert.That(first, Is.Not.EqualTo(second));
        }
    }
}
