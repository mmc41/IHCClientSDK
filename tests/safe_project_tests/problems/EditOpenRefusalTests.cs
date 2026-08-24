using System;
using System.Linq;

using Ihc.Vis.Editing;
using Ihc.Vis.Problems;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// RF-12: a document that cannot be opened for editing refuses with a CODED identity.
    ///
    /// <para>The read-to-write boundary runs the guards a save would fail on, once, before a user invests any
    /// work. One of them — an attribute no schema declares — had no operation head to name, so it threw a bare
    /// <see cref="InvalidOperationException"/> and the session mapped it to <see cref="EditStatus.Failed"/>
    /// carrying the ENGLISH engine sentence. The same condition at SAVE says which attribute, in Danish, under
    /// <c>attr-undeclared</c>. One condition, two operations, and only one of them was answerable.</para>
    ///
    /// <para>Two halves are fixed here and both are asserted: the guard now refuses under <c>edit.open</c> over
    /// the same published <c>attr-undeclared</c> cause, and the session recognises ANY coded refusal raised below
    /// the gate as a refusal rather than a failure — the one catch shape the problem contract promises.</para>
    /// </summary>
    [TestFixture]
    public sealed class EditOpenRefusalTests
    {
        /// <summary>A minimal project whose root carries an attribute the registry does not declare.</summary>
        private static Project WithUndeclaredAttribute() =>
            new(new ProjectElement("utcs_project", null,
                [
                    ("version_major", "4"), ("version_minor", "0"),
                    ("id1", "_0x1"), ("id2", "_0x2"), ("last_unique_id", "_0x3"),
                    ("no_such_attribute", "x"),
                ],
                []));

        [Test]
        public void OpeningForEditRefusesWithTheAttrUndeclaredCauseUnderEditOpen()
        {
            RefusedOperationException refusal =
                Assert.Throws<RefusedOperationException>(() => WithUndeclaredAttribute().Edit())!;

            Assert.Multiple(() =>
            {
                Assert.That(refusal, Is.InstanceOf<InvalidOperationException>(),
                    "the base type is unchanged, so every existing caller still catches it");
                Assert.That(refusal.Problems, Is.Not.Null, "the refusal carries an identity");
                Assert.That(refusal.Problems!.Operation.Code, Is.EqualTo(OperationCodes.EditOpen));
                Assert.That(refusal.Problems.Cause.Code.Value, Is.EqualTo("attr-undeclared"),
                    "the cause keeps the id the catalogue published, whichever operation it refuses");
                Assert.That(refusal.Problems.Cause.Message,
                    Is.EqualTo("Ukendt attribut 'no_such_attribute' på <utcs_project>."),
                    "and the Danish sentence names the attribute, bound from the row's own template");
            });
        }

        /// <summary>
        /// The session half. The document opens fine for READING, so the refusal only appears when an edit is
        /// applied — and it must arrive as Refused with its code, not as a generic Failed carrying English.
        /// </summary>
        [Test]
        public void TheSessionReportsItAsARefusalWithItsCodeRatherThanAGenericFailure()
        {
            ProjectDocumentSession session = new();
            session.Open(WithUndeclaredAttribute());

            EditOutcome outcome = session.Apply(
                new UpdateProjectInfo(ProjectInfoData.Empty with { Description = "Ny beskrivelse" }));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "a coded refusal below the gate is a refusal, not a failure");
                Assert.That(outcome.Code.Value, Is.EqualTo("attr-undeclared"),
                    "and it carries the cause's published id");
                Assert.That(outcome.Reason, Does.StartWith("Ukendt attribut"),
                    "the reason is the Danish sentence, not the English engine diagnostic");
            });
        }

        /// <summary>
        /// The drift gate's subject for this family: the registry's label is the catalogue row's template, so the
        /// copy the editing layer carries cannot drift from the copy the catalogue governs.
        /// </summary>
        [Test]
        public void TheFamilysLabelsAreTheirEntriesTemplates()
        {
            Assert.Multiple(() =>
            {
                foreach (RefusalIdentity identity in EditOpenRefusalCodes.All)
                {
                    Assert.That(ProblemCatalog.Current.TryGet(identity.Cause, out ProblemCatalogEntry cause),
                        Is.True, identity.Cause.Value);
                    Assert.That(identity.CauseLabel, Is.EqualTo(cause.MessageTemplate), identity.Cause.Value);

                    Assert.That(ProblemCatalog.Current.TryGet(identity.Operation, out ProblemCatalogEntry head),
                        Is.True, identity.Operation.Value);
                    Assert.That(identity.OperationLabel, Is.EqualTo(head.MessageTemplate), identity.Operation.Value);
                }
            });
        }

        /// <summary>
        /// T033: the session's ONE catch site honours BOTH carrier shapes. A refusal answering with an AGGREGATE —
        /// a validation refusal, whose <c>Problems</c> is null by design — must still be reported as Refused with its
        /// head's code, not as a generic failure carrying the engine's English.
        /// <para>
        /// Driven through a command that throws one, because no shipped command does: the point is the CATCH, and a
        /// site that tests for one shape and forgets the other is the defect the widened interface exists to prevent.
        /// </para>
        /// </summary>
        [Test]
        public void TheSessionAlsoReportsAnAggregateCarrierAsARefusal()
        {
            ProjectDocumentSession session = new();
            session.Open(new Project(Tree.Node("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0x3")], [])));

            EditOutcome outcome = session.Apply(new ThrowsAnAggregate());

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "an aggregate carrier is a coded refusal like any other");
                Assert.That(outcome.Code, Is.EqualTo(OperationCodes.Save), "reported under the aggregate's head");
                Assert.That(outcome.Reason, Does.StartWith("Projektet kunne ikke gemmes"),
                    "with the head's Danish, not the exception's English");
            });
        }

        /// <summary>A command whose Execute raises an aggregate-carrying refusal.</summary>
        private sealed record ThrowsAnAggregate : ProjectCommand
        {
            internal override string Describe(Project project) => "Test";

            internal override EditVerdict Evaluate(EditContext context) => EditVerdict.Allow;

            internal override void Execute(Ihc.Vis.Editing.ProjectEditor editor) =>
                throw new ProjectValidationException(OperationCodes.Save, ProjectValidationResult.FromFindings(
                    [new ProjectValidationFinding(ValidationSeverity.Error, "attr-required", "_0x1", "Mangler")]));
        }

        /// <summary>The control: a document with nothing undeclared still opens and still edits.</summary>
        [Test]
        public void AWellFormedDocumentStillOpensForEditing()
        {
            Project project = new(new ProjectElement("utcs_project", null,
                [("version_major", "4"), ("version_minor", "0"), ("id1", "_0x1"), ("id2", "_0x2"),
                 ("last_unique_id", "_0x3")], []));

            Assert.That(() => project.Edit(), Throws.Nothing);
        }
    }
}
