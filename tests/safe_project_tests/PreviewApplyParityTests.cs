using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-13: the Preview→confirm→Apply contract. <c>session.Preview(command)</c> returns exactly the
    /// structural delta the subsequent <c>session.Apply(command)</c> commits, so the GUI can show a delete's cascade
    /// impact (US-009) before committing — with no dialog below the session.
    /// </summary>
    public class PreviewApplyParityTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task Preview_ReturnsTheSameDeltaAsTheSubsequentApply()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ElementId locality = project.Groups.First(g => g.ChildrenOrEmpty().Any()).Id!.Value;
            var command = new DeleteLocality(locality);   // a cascading delete: locality + contents + references
            var session = new ProjectDocumentSession();
            session.Open(project);

            ProjectChangeSet? preview = session.Preview(command);
            EditOutcome outcome = session.Apply(command);
            ProjectChangeSet applied = outcome.Changes!;

            Assert.Multiple(() =>
            {
                Assert.That(outcome.Status, Is.EqualTo(EditStatus.Committed));
                Assert.That(preview, Is.Not.Null, "a cascading delete previews a non-null change set");
                Assert.That(preview!.Removed, Is.EquivalentTo(applied.Removed), "same removed ids");
                Assert.That(preview.Added, Is.EquivalentTo(applied.Added), "same added ids");
                Assert.That(preview.Changed, Is.EquivalentTo(applied.Changed), "same changed ids");
                Assert.That(preview.ChildListChanged, Is.EquivalentTo(applied.ChildListChanged), "same child-list changes");
                Assert.That(preview.MetadataChanged, Is.EqualTo(applied.MetadataChanged), "same metadata flag");
                Assert.That(preview.Removed, Is.Not.Empty, "the cascade removed multiple ids");
            });
        }
    }
}
