using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-16: the user-defined-texts (US-049) command semantics migrated from the app-level
    /// <c>safe_visual_tests.DataTablesTests</c> onto <see cref="ProjectDocumentSession"/> — Add appends (creating the
    /// table on first use), Edit renames by id, Delete removes only that row. Read back through the
    /// <c>GetDataTables</c> projection. The app-level file keeps its view-model/dialog wiring.
    /// </summary>
    public class SessionUserTextTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        private static ProjectDocumentSession Session(Project project)
        {
            var session = new ProjectDocumentSession();
            session.Open(project);
            return session;
        }

        private static ElementId ParseId(string token)
        {
            ElementId.TryParse(token, out ElementId id);
            return id;
        }

        [Test]   // from DataTablesTests.AddUserText_AppendsToTheEditableList
        public async Task AddUserText_AppendsAndCreatesTableOnFirstUse()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            Assert.That(session.Current!.GetDataTables().UserTexts, Is.Empty, "precondition: project3 has no user texts");

            session.Apply(new AddUserText("By main door", TableExists: false));

            Assert.Multiple(() =>
            {
                Assert.That(session.IsDirty, Is.True);
                Assert.That(session.Current!.GetDataTables().UserTexts.Select(t => t.Text), Does.Contain("By main door"));
            });
        }

        [Test]   // from DataTablesTests.EditUserText_ChangesTheText
        public async Task UpdateUserText_RenamesById()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddUserText("Old text", TableExists: false));
            ElementId id = ParseId(session.Current!.GetDataTables().UserTexts.Single().Id);

            session.Apply(new UpdateUserText(id, "New text"));

            Assert.That(session.Current!.GetDataTables().UserTexts.Single().Text, Is.EqualTo("New text"));
        }

        [Test]   // from DataTablesTests.DeleteUserText_RemovesOnlyThatText
        public async Task DeleteUserText_RemovesOnlyThatRow()
        {
            ProjectDocumentSession session = Session(await Load("project3-KompleksWired.vis"));
            session.Apply(new AddUserText("Keep", TableExists: false));
            session.Apply(new AddUserText("Remove", TableExists: true));
            ElementId remove = ParseId(session.Current!.GetDataTables().UserTexts.First(t => t.Text == "Remove").Id);

            session.Apply(new DeleteUserText(remove));

            var texts = session.Current!.GetDataTables().UserTexts.Select(t => t.Text).ToList();
            Assert.That(texts, Does.Contain("Keep").And.Not.Contain("Remove"));
        }
    }
}
