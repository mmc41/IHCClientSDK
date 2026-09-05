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
        /// The PREVIEW half, which used to answer the opposite of the two Apply tests above. Preview and Apply
        /// run the same kernel, so the same editor-open guard fires for both — but Preview's catch had no arm
        /// for a coded refusal, so it reported the rules working as an engine break: status Faulted, the ENGLISH
        /// diagnostic, and an internal-error row filed for it. On a document with duplicate ids or an undeclared
        /// attribute that was EVERY command, in both spellings of the guard.
        /// </summary>
        [TestCase("attr-undeclared", "Ukendt attribut")]
        [TestCase("id-duplicate-token", "Dobbelt id")]
        public void PreviewReportsACodedEditOpenRefusalTheSameWayApplyDoes(string code, string danishPrefix)
        {
            ProjectDocumentSession session = new();
            session.Open(code == "attr-undeclared" ? WithUndeclaredAttribute() : WithDuplicateIds());
            ProjectCommand command =
                new UpdateProjectInfo(ProjectInfoData.Empty with { Description = "Ny beskrivelse" });

            PreviewOutcome preview = session.Preview(command);
            EditOutcome applied = session.Apply(command);

            Assert.Multiple(() =>
            {
                Assert.That(preview.Status, Is.EqualTo(PreviewStatus.Refused),
                    "asking whether an edit WOULD work must not report the answer as an engine bug");
                Assert.That(preview.Code.Value, Is.EqualTo(code));
                Assert.That(preview.Reason, Does.StartWith(danishPrefix),
                    "the Danish sentence a caller may show, not the engine's English diagnostic");
                Assert.That(preview.Fault, Is.Null,
                    "and no internal-error row: a refusal is the rules working, not a fault to file");
                // The parity that matters more than any single field: the two doors run one kernel, so they must
                // not disagree about the same throw.
                Assert.That(preview.Code, Is.EqualTo(applied.Code));
                Assert.That(preview.Reason, Is.EqualTo(applied.Reason));
            });
        }

        /// <summary>A minimal project whose two groups carry the SAME id under different token spellings.</summary>
        /// <remarks>
        /// '_0x532' and '_0x0532' are different token strings but the same <c>ElementId</c>, which is precisely
        /// the collision id-addressed editing cannot survive: every lookup matches by parsed id and would resolve
        /// first-match.
        /// </remarks>
        private static Project WithDuplicateIds() =>
            Tree.WithRoot(
                Tree.Node("groups", "_0x2031", [("name", "L")],
                    Tree.Node("group", "_0x532", [("name", "A")]),
                    Tree.Node("group", "_0x0532", [("name", "B")])));

        /// <summary>
        /// The session half of the duplicate-id guard — the second kind of guard at this boundary, and the one
        /// with no save-side sibling: an EDIT-MODEL precondition. A duplicate id is tolerated by save and merely
        /// REPORTED by validate, but id-addressed editing would target the wrong element, so the open refuses.
        /// The direct-throw half (identity, operation, bound Danish sentence) is asserted where the guard's own
        /// test already lived, in <c>EditorGuardTests.Edit_DuplicateIds_AsLeadingZeroTokenVariants_AreRejected</c>;
        /// this mirrors
        /// <see cref="TheSessionReportsItAsARefusalWithItsCodeRatherThanAGenericFailure"/>: applying an edit to
        /// such a document reports Refused with the code, where it used to report a generic Failed carrying the
        /// English engine sentence.
        /// </summary>
        [Test]
        public void TheSessionReportsTheDuplicateIdOpenAsARefusalWithItsCode()
        {
            ProjectDocumentSession session = new();
            session.Open(WithDuplicateIds());

            EditOutcome outcome = session.Apply(
                new UpdateProjectInfo(ProjectInfoData.Empty with { Description = "Ny beskrivelse" }));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused),
                    "a coded refusal below the gate is a refusal, not a failure");
                Assert.That(outcome.Code.Value, Is.EqualTo("id-duplicate-token"));
                Assert.That(outcome.Reason, Does.StartWith("Dobbelt id"),
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
