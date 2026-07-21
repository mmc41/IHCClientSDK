using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// refac3 T003: the stateless command-execution surface relocated onto <see cref="ProjectAppService"/>
    /// (<c>Apply</c>/<c>Apply&lt;T&gt;</c>/<c>CanApply</c>/<c>Preview</c>, D02). Pins the
    /// <see cref="ProjectApplyResult"/> Project-snapshot contract (D03): a committed apply returns the CHANGED
    /// project (the edit is visible in it) and, for a value command, a project in which the produced id resolves;
    /// a non-committing apply (NoChange / Refused) returns the ORIGINAL input project, reference-identical and never
    /// null — so the caller always has a valid snapshot and commits only on Committed. Also pins that the outcome
    /// status, produced value and change set are preserved, and the read-only <c>CanApply</c>/<c>Preview</c> probes.
    /// </summary>
    public class ProjectAppServiceApplyTests
    {
        private static ProjectAppService NewApp() => new(TestSetup.Settings);
        private static Project NewProject(ProjectAppService app) =>
            app.CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

        // An id no element in a fresh project carries, so a command targeting it is refused by the existence guard.
        private static readonly ElementId MissingId = new(9_999_999, 0);

        [Test]
        public void Apply_Committed_ReturnsChangedProject_WithChangeSet_AndDoesNotMutateInput()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);
            ElementId loc = project.Groups[0].Id!.Value;

            ProjectApplyResult result = app.Apply(project, new RenameLocality(loc, "Renamed", "a note"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(result.Project.FindById(loc)!.GetAttribute("name"), Is.EqualTo("Renamed"),
                    "the committed edit is visible in the returned project");
                Assert.That(result.Outcome.Changes, Is.Not.Null, "a committed edit carries its change set");
                Assert.That(result.Project, Is.Not.SameAs(project), "the changed project is a new snapshot");
                Assert.That(project.FindById(loc)!.GetAttribute("name"), Is.Not.EqualTo("Renamed"),
                    "the immutable input project is untouched");
            });
        }

        [Test]
        public void ApplyOfT_Committed_SurfacesProducedValue_ThatResolvesInReturnedProject()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);
            int groupsBefore = project.Groups.Count;

            ProjectApplyResult<ElementId> result = app.Apply(project, new AddLocality("Extra room"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(result.Outcome.Value, Is.Not.EqualTo(default(ElementId)), "the produced id is surfaced");
                Assert.That(result.Project.FindById(result.Outcome.Value), Is.Not.Null,
                    "the produced id resolves to a real element in the returned project");
                Assert.That(result.Project.Groups.Count, Is.EqualTo(groupsBefore + 1));
            });
        }

        [Test]
        public void Apply_NoChange_ReturnsOriginalInputProject_ReferenceIdentical()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);
            ElementId loc = project.Groups[0].Id!.Value;
            // Commit definite values first, so the identical re-apply below is a guaranteed no-op.
            Project renamed = app.Apply(project, new RenameLocality(loc, "Room", "note")).Project;

            ProjectApplyResult result = app.Apply(renamed, new RenameLocality(loc, "Room", "note"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome.Status, Is.EqualTo(EditStatus.NoChange));
                Assert.That(result.Project, Is.SameAs(renamed), "a no-op returns the original input, reference-identical");
            });
        }

        [Test]
        public void Apply_Refused_ReturnsOriginalInputProject_ReferenceIdentical()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);

            ProjectApplyResult result = app.Apply(project, new RenameLocality(MissingId, "X", "Y"));

            Assert.Multiple(() =>
            {
                Assert.That(result.Outcome.Status, Is.EqualTo(EditStatus.Refused));
                Assert.That(result.Project, Is.SameAs(project), "a refused edit returns the original input, reference-identical");
            });
        }

        [Test]
        public void CanApply_AllowsValidCommand_RefusesMissingTarget()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);

            Assert.Multiple(() =>
            {
                Assert.That(app.CanApply(project, new AddLocality("X")).Ok, Is.True, "a locality insert is always allowed");
                Assert.That(app.CanApply(project, new RenameLocality(MissingId, "X", "Y")).Ok, Is.False,
                    "renaming a missing element is refused");
            });
        }

        [Test]
        public void Preview_WouldChangeForRealEdit_NoChangeForNoOp()
        {
            ProjectAppService app = NewApp();
            Project project = NewProject(app);
            ElementId loc = project.Groups[0].Id!.Value;
            Project renamed = app.Apply(project, new RenameLocality(loc, "Room", "note")).Project;

            PreviewOutcome wouldChange = app.Preview(project, new AddLocality("X"));
            PreviewOutcome noChange = app.Preview(renamed, new RenameLocality(loc, "Room", "note"));

            Assert.Multiple(() =>
            {
                Assert.That(wouldChange.Status, Is.EqualTo(PreviewStatus.WouldChange));
                Assert.That(wouldChange.Changes!.Added, Is.Not.Empty, "the preview names the new locality id");
                Assert.That(noChange.Status, Is.EqualTo(PreviewStatus.NoChange), "an identical re-apply previews no change");
            });
        }
    }
}
