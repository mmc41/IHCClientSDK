using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// fablerefac W2-2: the ProjectIndex lookup substrate agrees with the engine's own Project.FindById /
    /// FindParent for every id in an oracle project, and reports absent for an unknown id.
    /// </summary>
    public class ProjectIndexTests
    {
        private static ProjectAppService App => new(TestSetup.Settings);
        private static Task<Project> Load(string file) => App.Load("testdata/projects/" + file);

        [Test]
        public async Task Index_AgreesWithFindByIdAndFindParent_ForEveryId()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectIndex index = ProjectIndex.Build(project);

            var ids = project.Root.DescendantsAndSelf()
                .Where(e => e.Id is not null).Select(e => e.Id!.Value).Distinct().ToList();
            Assert.That(ids, Is.Not.Empty, "the oracle has id-bearing elements to check");

            Assert.Multiple(() =>
            {
                foreach (ElementId id in ids)
                {
                    Assert.That(index.FindById(id), Is.SameAs(project.FindById(id)), $"FindById {id.ToToken()}");
                    Assert.That(index.FindParent(id), Is.SameAs(project.FindParent(id)), $"FindParent {id.ToToken()}");
                }
            });
        }

        [Test]
        public async Task Index_MissingId_IsAbsent()
        {
            Project project = await Load("project3-KompleksWired.vis");
            ProjectIndex index = ProjectIndex.Build(project);
            var missing = new ElementId(0x7FFFFF, 0x99);

            Assert.Multiple(() =>
            {
                Assert.That(index.FindById(missing), Is.Null);
                Assert.That(index.FindParent(missing), Is.Null);
            });
        }
    }
}
