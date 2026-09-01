using System;
using System.Collections.Generic;

using Ihc.Vis.Editing;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// An edit that FAULTS inside the engine is reported by the engine, on the SDK's own fault port.
    ///
    /// <para><b>Where the responsibility sits.</b> The failing code is here: the command body threw, this layer
    /// caught it, and this layer is the only one that ever holds the exception. A host reading the fault back off
    /// the returned outcome was re-deriving "a Failed outcome means something broke" one layer above the layer
    /// that already knew — so every consumer of the facade had to re-derive it too, and the one-shot
    /// <see cref="ProjectAppService.Apply(Project, ProjectCommand)"/> door had no consumer positioned to.</para>
    ///
    /// <para><b>The outcome still carries it.</b> Reporting is ADDITIVE, exactly as it is for
    /// <c>AppServiceBase</c>'s port: <see cref="EditOutcome.Fault"/> is unchanged, so the dialog a shell shows
    /// still renders the same Danish sentence from the same value. What changed is that the fault also reaches
    /// the sink without anyone above having to notice it.</para>
    ///
    /// <para><b>The real path, not a forged outcome.</b> A command whose body throws can only be authored from
    /// inside the SDK — <see cref="ProjectCommand"/>'s mechanics are internal — which is why this fixture lives
    /// here rather than beside a shell that could only hand a hand-built <see cref="EditOutcome"/> to a
    /// publisher.</para>
    /// </summary>
    [TestFixture]
    public sealed class EditFaultReportedBySdkTests
    {
        private const string Boom = "engine boom";

        /// <summary>A command that passes its legality check and then breaks, which is what a Failed outcome IS.</summary>
        private sealed record ThrowingCommand : ProjectCommand
        {
            internal override string Describe(Project project) => "Throwing edit";

            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;

            internal override void Execute(ProjectEditor editor) => throw new InvalidOperationException(Boom);
        }

        /// <summary>The VALUE-PRODUCING shape of <see cref="ThrowingCommand"/> — every insert is one of these.</summary>
        private sealed record ThrowingValueCommand : ProjectCommand<ElementId>
        {
            internal override string Describe(Project project) => "Throwing value edit";

            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;

            internal override ElementId ExecuteCore(ProjectEditor editor) =>
                throw new InvalidOperationException(Boom);
        }

        private sealed record RefusingCommand : ProjectCommand
        {
            internal override string Describe(Project project) => "Refusing edit";

            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Refuse("Nej.");

            internal override void Execute(ProjectEditor editor) =>
                throw new InvalidOperationException("never reached: the verdict short-circuits the apply");
        }

        private static (ProjectAppService Service, List<InternalError> Reported) ServiceWithPort()
        {
            List<InternalError> reported = [];
            return (new ProjectAppService(TestSetup.Settings, reported.Add), reported);
        }

        /// <summary>Opens a document on the service and applies an edit that breaks inside the engine.</summary>
        private static EditOutcome AFaultingEditOn(ProjectAppService service) =>
            service.OpenDocument(Tree.MinimalProject()).Apply(new ThrowingCommand());

        [Test]
        public void AnEditThatFaultsOnAnOpenDocumentIsReportedToTheFaultPort()
        {
            (ProjectAppService service, List<InternalError> reported) = ServiceWithPort();

            EditOutcome outcome = AFaultingEditOn(service);

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Failed), "precondition: the edit broke");
                Assert.That(reported, Has.Count.EqualTo(1), "and the layer that caught it reported it");
                Assert.That(reported[0].Code.Value, Is.EqualTo("internal.edit-failed"));
                Assert.That(reported[0].Origin, Is.EqualTo(InternalErrorOrigin.Sdk));
                Assert.That(reported[0].Detail, Does.Contain(Boom),
                    "carrying the stack the exception still had when it was caught");
            });
        }

        /// <summary>
        /// The SAME value, not a second one minted for the port. A shell showing the outcome's fault and a sink
        /// listing the reported one must be describing one event, or the panel and the dialog disagree.
        /// </summary>
        [Test]
        public void TheReportedFaultIsTheOneTheOutcomeCarries()
        {
            (ProjectAppService service, List<InternalError> reported) = ServiceWithPort();

            EditOutcome outcome = AFaultingEditOn(service);

            Assert.That(outcome.Fault, Is.SameAs(reported[0]));
        }

        /// <summary>
        /// The one-shot facade door reports too. This is the path a host-side publisher structurally could not
        /// see: <c>Apply</c> runs on a throwaway session and returns, so nothing above it is positioned to notice
        /// the fault.
        /// </summary>
        [Test]
        public void AFaultOnTheOneShotApplyDoorIsReportedToo()
        {
            (ProjectAppService service, List<InternalError> reported) = ServiceWithPort();

            ProjectApplyResult result = service.Apply(Tree.MinimalProject(), new ThrowingCommand());

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome.Status, Is.EqualTo(EditStatus.Failed));
                Assert.That(reported, Has.Count.EqualTo(1));
                Assert.That(reported[0].Code.Value, Is.EqualTo("internal.edit-failed"));
            });
        }

        /// <summary>
        /// A REFUSED edit reports nothing. A refusal is the rules working, not the tool breaking, and a sink that
        /// collected those would bury the faults among correct behaviour.
        /// </summary>
        [Test]
        public void ARefusedEditReportsNothing()
        {
            (ProjectAppService service, List<InternalError> reported) = ServiceWithPort();

            EditOutcome outcome = service.OpenDocument(Tree.MinimalProject()).Apply(new RefusingCommand());

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused), "precondition");
                Assert.That(reported, Is.Empty);
            });
        }

        /// <summary>
        /// A service with NO port still returns the fault on the outcome. The port is additive, so a file-only
        /// caller that never wired one loses nothing it had.
        /// </summary>
        [Test]
        public void WithNoPortTheOutcomeStillCarriesTheFault()
        {
            EditOutcome outcome = AFaultingEditOn(new ProjectAppService(TestSetup.Settings));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Failed));
                Assert.That(outcome.Fault, Is.Not.Null);
                Assert.That(outcome.Fault!.Code.Value, Is.EqualTo("internal.edit-failed"));
            });
        }

        /// <summary>
        /// A VALUE-PRODUCING edit that faults carries its fault too.
        ///
        /// <para>The generic overload rebuilds the outcome to attach the produced value, and rebuilding is where
        /// a field gets left behind: <see cref="EditOutcome{T}"/> takes <c>Fault</c> as a trailing optional
        /// argument, so omitting it compiles and silently reads null. Every insert in the shell goes through this
        /// overload, and a host that shows its dialog only for an outcome that carries a fault therefore said
        /// NOTHING when one of them broke — no dialog, no status line — while the same fault raised by a
        /// non-generic edit was reported normally.</para>
        /// </summary>
        [Test]
        public void AValueProducingEditThatFaultsCarriesItsFaultToo()
        {
            (ProjectAppService service, List<InternalError> reported) = ServiceWithPort();

            EditOutcome<ElementId> outcome =
                service.OpenDocument(Tree.MinimalProject()).Apply(new ThrowingValueCommand());

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Failed), "precondition: the edit broke");
                Assert.That(outcome.Fault, Is.Not.Null,
                    "a Failed outcome with no fault is the contract violation a host cannot report around");
                Assert.That(outcome.Fault!.Code.Value, Is.EqualTo("internal.edit-failed"));
                Assert.That(outcome.Fault, Is.SameAs(reported[0]),
                    "and it is the SAME value the port was handed, as it is for the non-generic overload");
            });
        }

        /// <summary>The code travels with it: a host reads the outcome's code to title its dialog.</summary>
        [Test]
        public void AValueProducingFaultKeepsTheNonGenericOutcomesShape()
        {
            (ProjectAppService service, _) = ServiceWithPort();
            IProjectDocument document = service.OpenDocument(Tree.MinimalProject());

            EditOutcome plain = document.Apply(new ThrowingCommand());
            EditOutcome<ElementId> valued = document.Apply(new ThrowingValueCommand());

            Assert.Multiple(() =>
            {
                Assert.That(valued.Status, Is.EqualTo(plain.Status));
                Assert.That(valued.Code, Is.EqualTo(plain.Code));
                Assert.That(valued.Fault!.Code, Is.EqualTo(plain.Fault!.Code));
            });
        }

        /// <summary>
        /// A port that THROWS does not turn a reportable fault into a worse one. Fail-open, for the reason
        /// <c>AppServiceBase</c>'s port is: the caller's own outcome must arrive untouched.
        /// </summary>
        [Test]
        public void APortThatThrowsIsAbsorbed()
        {
            EditOutcome outcome = AFaultingEditOn(new ProjectAppService(
                TestSetup.Settings, _ => throw new InvalidOperationException("the sink is broken")));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Failed));
        }
    }
}
