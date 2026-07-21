using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Editing;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-9: the programming-authoring command family — a representative authoring edit byte-round-trips
    /// against the engine, ToggleLogMark is reversible, and a command aimed at the wrong container is Refused.
    /// </summary>
    public class ProgramCommandTests
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
        public async Task AddSubProgram_OnCommandContainer_MatchesEngine()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement actions = project.Root.Descendants().First(e => e.Tag == "actions" && e.Id is not null);
            ElementId id = actions.Id!.Value;
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new AddSubProgram(id));

            ProjectEditor editor = project.Edit();
            editor.Branch(id).AddSubProgram();
            Project viaEngine = editor.ToProject();

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(session.Current!.Equals(viaEngine), Is.True, "matches the engine's own Branch.AddSubProgram");
            });
        }

        [Test]
        public async Task AddSubProgram_OnWrongContainer_IsRefused()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First().Id!.Value;   // not an "actions" container
            ProjectDocumentSession session = Session(project);

            EditOutcome outcome = session.Apply(new AddSubProgram(locality));

            Assert.That(outcome.Status, Is.EqualTo(EditStatus.Refused));
        }

        [Test]
        public async Task ToggleLogMark_IsReversible()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectElement logRow = project.Root.Descendants()
                .First(e => e.Id is not null && e.IsLogRow(project));
            ElementId id = logRow.Id!.Value;
            ProjectDocumentSession session = Session(project);
            Project before = session.Current!;

            EditOutcome toggled = session.Apply(new ToggleLogMark(id));
            session.Undo();

            Assert.Multiple(() =>
            {
                Assert.That(toggled.Status, Is.EqualTo(EditStatus.Committed), "the toggle changed the log mark");
                Assert.That(session.Current!.Equals(before), Is.True, "toggle then undo returns to the original");
            });
        }
    }
}
