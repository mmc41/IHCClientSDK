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
                Assert.That(sTables.UserTexts, Is.EqualTo(pTables.UserTexts), "user texts");
                // DataTableView.Rows is itself an ImmutableArray (reference-equal only), so compare projected scalars.
                Assert.That(sTables.SystemTables.Select(t => t.Name),
                    Is.EqualTo(pTables.SystemTables.Select(t => t.Name)), "system table names");
                Assert.That(sTables.SystemTables.SelectMany(t => t.Rows),
                    Is.EqualTo(pTables.SystemTables.SelectMany(t => t.Rows)), "system table rows");
                Assert.That(sMap.InputModules, Is.EqualTo(pMap.InputModules), "input modules");
                Assert.That(sMap.OutputModules, Is.EqualTo(pMap.OutputModules), "output modules");
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
