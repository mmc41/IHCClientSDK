using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-5: the locality command family — AddLocality returns a resolvable id and matches the engine's
    /// own AddGroup; RenameLocality's label uses the pre-edit name (D10) and Evaluate refuses a missing element;
    /// DeleteLocality cascades its contents and undoes as one step.
    /// </summary>
    public class LocalityCommandTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        [Test]
        public async Task AddLocality_Commits_ReturnsResolvableId_MatchesEngineAddGroup()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);
            int before = session.Current!.Groups.Count;

            EditOutcome<ElementId> outcome = session.Apply(new AddLocality("Kitchen"));

            ProjectEditor editor = project.Edit();
            editor.AddGroup("Kitchen");
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Groups.Count, Is.EqualTo(before + 1));
                Assert.That(session.Current!.FindById(outcome.Value)?.GetAttribute("name"), Is.EqualTo("Kitchen"),
                    "the returned id resolves to the new locality");
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own AddGroup byte-for-byte");
            });
        }

        [Test]
        public async Task RenameLocality_LabelUsesPreEditName_AndSetsNameAndNote()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement group = project.Groups.First();
            ElementId id = group.Id!.Value;
            string oldName = group.GetAttribute("name") ?? "";
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new RenameLocality(id, "Renamed", "a note"));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(outcome.Label, Is.EqualTo("Omdøb " + oldName), "the label used the pre-edit name (D10)");
                Assert.That(session.Current!.FindById(id)!.GetAttribute("name"), Is.EqualTo("Renamed"));
                Assert.That(session.Current!.FindById(id)!.GetAttribute("note"), Is.EqualTo("a note"));
            });
        }

        // T018: the DeleteLocality label reads the pre-edit name through the ElementView.Name read surface
        // (project.View(element).Name), not a raw GetAttribute("name") re-typed literal.
        [Test]
        public async Task DeleteLocality_LabelUsesPreEditName()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement group = project.Groups.First(g => !g.Children.IsDefaultOrEmpty);
            string name = group.GetAttribute("name") ?? "";
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new DeleteLocality(group.Id!.Value));

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(outcome.Label, Is.EqualTo("Slet " + name),
                    "the label reads the name via the shared ElementView.Name surface");
            });
        }

        [Test]
        public async Task RenameLocality_Evaluate_RefusesAMissingElement()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new RenameLocality(new ElementId(0x7FFFFF, 0x32), "X", ""));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task DeleteLocality_CascadesContents_AndUndoReversesAsOneStep()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement group = project.Groups.First(g => !g.Children.IsDefaultOrEmpty);
            ElementId id = group.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new DeleteLocality(id));
            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.FindById(id), Is.Null, "the locality and its contents are gone");
            });

            session.Undo();
            ProjectElement? restored = session.Current!.FindById(id);
            Assert.Multiple(() =>
            {
                Assert.That(restored, Is.Not.Null, "one undo restores the locality");
                Assert.That(restored!.Children.IsDefaultOrEmpty, Is.False, "and its contents — the cascade reverses as a unit");
            });
        }
    }
}
