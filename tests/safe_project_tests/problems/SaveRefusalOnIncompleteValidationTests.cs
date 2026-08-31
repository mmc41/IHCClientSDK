using System;
using System.IO;
using System.Threading.Tasks;

using FakeItEasy;

using Ihc.App;
using Ihc.Envelope;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// A save or upload whose validation run did not COMPLETE is refused, and refused under its own identity.
    ///
    /// <para><b>Why a separate refusal.</b> A crashed rule leaves the findings list short by an amount nothing
    /// can measure, so a run carrying a fault says nothing about whether the project is clean. Deciding on the
    /// findings alone lets a project reach the controller — the one sink with no <c>.BAK</c> — with the checklist
    /// silently incomplete. Reporting it as the errors-found refusal would be worse than saying nothing: that
    /// sentence counts blocking errors, so a faulted run with none would tell the user to fix zero errors.</para>
    ///
    /// <para><b>The executor is substituted, not the rules.</b> The fault channel fills only when a rule throws
    /// and no registered rule does, so the condition is unreachable through the shipped rule set. The service is
    /// therefore built over an executor that returns the run this fixture describes.</para>
    /// </summary>
    [TestFixture]
    public sealed class SaveRefusalOnIncompleteValidationTests
    {
        private static StructuredValidationResult Faulted(params string[] rules) => Fakes.FaultedRun(rules);

        private static ProjectAppService FileService(StructuredValidationResult run) =>
            Fakes.FileServiceOver(run);

        private static ProjectAppService BridgeService(StructuredValidationResult run, IControllerService controller) =>
            Fakes.BridgeServiceOver(run, controller);

        /// <summary>
        /// The exposed file path: a caller that opted into validating before the write. It is the opt-in rather
        /// than the default that matters here — a plain save never validated at all, so the gate could not have
        /// been reached through it.
        /// </summary>
        [Test]
        public void AValidateBeforeSaveWhoseRunFaultedIsRefusedAsIncomplete()
        {
            ProjectAppService service = FileService(Faulted("name-empty"));
            string path = Path.Combine(Path.GetTempPath(), $"incomplete-{Guid.NewGuid():N}.vis");

            RefusedOperationException refusal = Assert.ThrowsAsync<RefusedOperationException>(
                () => service.Save(Tree.MinimalProject(), path,
                    ProjectSaveOptions.PreserveExistingMetadata with { ValidateBeforeSave = true }))!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Cause.Code, Is.EqualTo(SaveRefusalCodes.ValidationIncomplete.Cause));
                Assert.That(refusal.Problems.Operation.Code, Is.EqualTo(OperationCodes.Save));
                Assert.That(File.Exists(path), Is.False, "a refused save writes nothing");
            });
        }

        /// <summary>
        /// The unavoidable path: <c>UploadTo</c> turns validation on for every controller flash, so this gate is
        /// what stands between a faulted run and the controller's EPROM.
        /// </summary>
        [Test]
        public void AnUploadWhoseRunFaultedIsRefusedAsIncompleteAndNeverStores()
        {
            IControllerService controller = A.Fake<IControllerService>();
            ProjectAppService service = BridgeService(Faulted("name-empty"), controller);

            RefusedOperationException refusal = Assert.ThrowsAsync<RefusedOperationException>(
                () => service.UploadTo(Tree.MinimalProject(), ProjectSaveOptions.PreserveExistingMetadata))!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal.Problems!.Cause.Code, Is.EqualTo(SaveRefusalCodes.ValidationIncomplete.Cause));
                A.CallTo(() => controller.StoreProject(A<ProjectFile>._)).MustNotHaveHappened();
            });
        }

        /// <summary>
        /// The refusal is DISTINCT from the errors-found one. Both stop the same write, and telling them apart is
        /// the whole point: one says the project has defects to repair, the other says the check never finished.
        /// </summary>
        [Test]
        public void AFaultFreeRunWithErrorsStillRefusesAsAValidationFailureNotAnIncompleteOne()
        {
            StructuredValidationResult withError = new(
                EquatableArray.Create<ValidationFinding>(
                    [new ValidationFinding(
                        new Problem(new ProblemCode("name-empty"), "Navnet mangler.",
                            EquatableArray<ProblemArgument>.Empty, "The name is empty."),
                        ValidationSeverity.Error,
                        ValidationCategory.Documentation,
                        null,
                        EquatableArray<FindingLocation>.Empty)]),
                EquatableArray<InternalError>.Empty);
            ProjectAppService service = FileService(withError);
            string path = Path.Combine(Path.GetTempPath(), $"errors-{Guid.NewGuid():N}.vis");

            Assert.ThrowsAsync<ProjectValidationException>(
                () => service.Save(Tree.MinimalProject(), path, ProjectSaveOptions.PreserveExistingMetadata with { ValidateBeforeSave = true }));
        }

        /// <summary>
        /// The control: a run that neither found nor broke anything still writes. A gate that also stopped a good
        /// project would be a worse defect than the one it prevents.
        /// </summary>
        [Test]
        public async Task ACleanRunStillSaves()
        {
            ProjectAppService service = FileService(StructuredValidationResult.Empty);
            string path = Path.Combine(Path.GetTempPath(), $"clean-{Guid.NewGuid():N}.vis");

            try
            {
                await service.Save(Tree.MinimalProject(), path, ProjectSaveOptions.PreserveExistingMetadata with { ValidateBeforeSave = true });

                Assert.That(new FileInfo(path).Length, Is.GreaterThan(0));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
