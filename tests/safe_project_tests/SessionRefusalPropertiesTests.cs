using System.Threading.Tasks;

using Ihc.Vis.Problems;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// Two properties of the SESSION doors that were written beside the retired command-evaluator face and are not
    /// about it: they are about <see cref="ProjectDocumentSession.CanApply"/> and
    /// <see cref="ProjectDocumentSession.Apply"/>, which stay.
    ///
    /// <para><b>(a) The gate and the door agree.</b> What <c>CanApply</c> refuses, <c>Apply</c> refuses; what it
    /// allows, <c>Apply</c> commits. This is the property a menu gate rests on — a greyed command that would
    /// actually have worked, or an enabled one that refuses on click, are the two ways a frontend lies to its
    /// user.</para>
    ///
    /// <para><b>(b) A refusal leaves NO TRACE.</b> No undo entry, no version bump, no dirty flag. A refused action
    /// that silently marks a document dirty is how a user ends up saving a change they were told did not
    /// happen — and it is why a host may re-ask the gate on every pointer event without paying for it in
    /// state.</para>
    ///
    /// <para><b>Why they are here rather than where they were written.</b> Their old home was a fixture whose
    /// subject — the retired command-evaluator face — is gone, so they would have been deleted with it and nothing
    /// would have failed. Nothing else in the repository asserts either one. Rehomed FIRST, deliberately, so the
    /// coverage never passes through a state where it is absent.</para>
    /// </summary>
    [TestFixture]
    public sealed class SessionRefusalPropertiesTests : SessionCommandFixture
    {
        private static Task<Project> LoadFixture() => App.Load("testdata/projects/project3-KompleksWired.vis");

        /// <summary>A command refused because its target does not exist.</summary>
        private static ProjectCommand RefusedCommand() =>
            new RenameLocality(TestData.Id("_0x7ffffe"), "Nyt navn", string.Empty);

        /// <summary>A command that is allowed: renaming the first real locality.</summary>
        private static ProjectCommand AllowedCommand(Project project) =>
            new RenameLocality(project.Groups[0].Id!.Value, "Nyt navn", string.Empty);

        /// <summary>Property (a), in both directions — a one-sided test would pass on a gate that refused everything.</summary>
        [Test]
        public async Task WhatTheGateRefusesTheApplyRefusesAndWhatItAllowsTheApplyCommits()
        {
            Project project = await LoadFixture();
            ProjectCommand refused = RefusedCommand();
            ProjectCommand allowed = AllowedCommand(project);
            ProjectDocumentSession session = Session(project);

            Assert.Multiple(() =>
            {
                Assert.That(session.CanApply(refused).Ok, Is.False, "the gate refuses it");
                Assert.That(session.Apply(refused).Status, Is.EqualTo(EditStatus.Refused),
                    "and applying it refuses rather than committing");

                Assert.That(session.CanApply(allowed).Ok, Is.True, "the gate allows it");
                Assert.That(session.Apply(allowed).Status, Is.EqualTo(EditStatus.Committed),
                    "and what it allows, an apply commits");
            });
        }

        /// <summary>
        /// Property (c), and the one this run OWES: a refusal carries its CODE through every door, so agreement
        /// between the gate and the doors is checkable by identity rather than by comparing two Danish sentences
        /// that happen to match today.
        /// <para>It was previously asserted against the retired command-evaluator envelope, whose whole content
        /// was this equality. Re-established here, on the doors that remain — otherwise the retirement would have
        /// traded a code-equality contract for a weaker boolean one.</para>
        /// </summary>
        [Test]
        public async Task ARefusedApplyAndPreviewCarryTheSameCodeTheGateReturns()
        {
            Project project = await LoadFixture();
            ProjectDocumentSession session = Session(project);
            ProjectCommand refused = RefusedCommand();

            EditVerdict verdict = session.CanApply(refused);
            PreviewOutcome preview = session.Preview(refused);
            EditOutcome outcome = session.Apply(refused);

            Assert.Multiple(() =>
            {
                Assert.That(verdict.Code, Is.EqualTo(EditRefusalCodes.TargetMissing),
                    "the refusing site's own code — not an umbrella, and not the default");
                Assert.That(outcome.Code, Is.EqualTo(verdict.Code), "the apply door carries it through unchanged");
                Assert.That(preview.Code, Is.EqualTo(verdict.Code), "and so does the preview door");
                Assert.That(outcome.Reason, Is.EqualTo(verdict.Reason), "and the sentence is the same one, whole");
                Assert.That(preview.Reason, Is.EqualTo(verdict.Reason));
            });
        }

        /// <summary>The other half: an outcome that did not refuse carries NO refusal identity.</summary>
        [Test]
        public async Task ACommittedOutcomeCarriesNoRefusalCode()
        {
            Project project = await LoadFixture();
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(AllowedCommand(project));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(outcome.Code, Is.EqualTo(default(ProblemCode)),
                    "a committed edit has no refusal to identify");
            });
        }

        /// <summary>Property (b): a refusal leaves no trace at all.</summary>
        [Test]
        public async Task ARefusedApplyLeavesNoUndoEntryNoVersionBumpAndNoDirtyFlag()
        {
            Project project = await LoadFixture();
            ProjectDocumentSession session = Session(project);
            int versionBefore = session.Version;
            bool dirtyBefore = session.IsDirty;
            bool canUndoBefore = session.CanUndo;

            EditOutcome outcome = session.Apply(RefusedCommand());

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(session.Version, Is.EqualTo(versionBefore), "no version bump");
                Assert.That(session.IsDirty, Is.EqualTo(dirtyBefore), "no dirty flag");
                Assert.That(session.CanUndo, Is.EqualTo(canUndoBefore), "no undo entry");
            });
        }
    }
}
