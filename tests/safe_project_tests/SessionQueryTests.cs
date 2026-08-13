using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-12: the API-D read queries on the document session delegate to the W1-5 SDK projections over
    /// <c>Current</c> — a session query returns the same content as the projection on the same project — and return
    /// the blank models (never null, never throwing) when no project is open.
    /// </summary>
    public class SessionQueryTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task SessionQueries_EqualTheSdkProjections()
        {
            Project project = await Load("project3-KompleksWired.vis");
            var session = new ProjectDocumentSession();
            session.Open(project);
            DataTablesModel sTables = session.GetDataTables(), pTables = project.GetDataTables();
            ModuleAddressMap sMap = session.GetModuleAddressMap(), pMap = project.GetModuleAddressMap();

            Assert.Multiple(() =>
            {
                Assert.That(session.GetProjectInfo(), Is.EqualTo(project.GetProjectInfo()), "project info");
                Assert.That(session.GetUnlinkedWirelessProducts(),
                    Is.EqualTo(project.GetUnlinkedWirelessProducts()), "unlinked wireless");
                // Whole read models compared by value. This used to project scalars out and compare those,
                // because DataTableView.Rows was a raw ImmutableArray that only ever compared reference-equal;
                // EquatableArray<T> makes the records structurally comparable, so the workaround is gone — and
                // the assertion is stronger than the one it replaces, since flattening every table's rows into
                // one sequence could not tell which table a row belonged to.
                Assert.That(sTables, Is.EqualTo(pTables), "data tables");
                Assert.That(sMap, Is.EqualTo(pMap), "module address map");
            });
        }

        [Test]
        public void SessionQueries_WithNoProjectOpen_ReturnBlankModels()
        {
            var session = new ProjectDocumentSession();

            Assert.Multiple(() =>
            {
                Assert.That(session.GetProjectInfo(), Is.EqualTo(ProjectInfoData.Empty));
                Assert.That(session.GetDataTables().SystemTables, Is.Empty);
                Assert.That(session.GetDataTables().UserTexts, Is.Empty);
                Assert.That(session.GetUnlinkedWirelessProducts(), Is.Empty);
                Assert.That(session.GetModuleAddressMap().InputModules, Is.Empty);
                Assert.That(session.GetModuleAddressMap().OutputModules, Is.Empty);
            });
        }
    }
}
