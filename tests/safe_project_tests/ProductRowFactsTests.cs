using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// D-2: three facts a frontend used to derive from the raw row-grammar helper, asked of the read view instead —
    /// whether a placed resource is a configurable SETTING, whether a product-body child is a STRUCTURAL block
    /// rather than a resource, and whether the vendor's own tree suppresses the row.
    /// <para>
    /// The third is the vendor's display CONVENTION, not a frontend policy: whether OpenVisual then shows a
    /// suppressed row anywhere else is its own decision, which is why the fact and the policy stay apart.
    /// </para>
    /// </summary>
    public class ProductRowFactsTests : SessionCommandFixture
    {
        private static ProjectElement Find(Project project, string tag) =>
            project.Root.Descendants().First(e => e.Tag == tag);

        /// <summary>A resource carrying the catalog's setting mark is a setting; an ordinary sibling is not.</summary>
        [Test]
        public async Task IsSetting_ReadsTheCatalogsSettingMark()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectElement ordinary = Find(project, "dataline_output");

            Assert.Multiple(() =>
            {
                Assert.That(project.View(ordinary).IsSetting, Is.False,
                    "an ordinary pin carries no setting mark, and the attribute's DTD default is not a setting");
                Assert.That(project.Root.Descendants()
                        .Where(e => e.GetAttribute("setting") == "yes")
                        .All(e => project.View(e).IsSetting), Is.True,
                    "every marked row reads as a setting");
            });
        }

        /// <summary>
        /// The structural blocks of a product body: the scenes container, an embedded enum typedef, and any
        /// settings/config container, whatever family suffix it carries — they are not resource rows.
        /// </summary>
        [Test]
        public async Task IsStructural_SeparatesContainersFromResourceRows()
        {
            Project project = await Load("Project1-SimpelWired.vis");

            Assert.Multiple(() =>
            {
                Assert.That(project.View(Find(project, "settings")).IsStructural, Is.True,
                    "a settings container is structure, not a resource row");
                Assert.That(project.View(Find(project, "dataline_output")).IsStructural, Is.False,
                    "a pin is a resource row");
            });
        }

        /// <summary>
        /// The vendor's tree suppresses two disjoint kinds of row, and neither signal catches the other: a shutter
        /// product's Op/Ned pins, identifiable only by tag, and any row marked as a setting, identifiable only by
        /// the mark — its tag is shared with visible siblings.
        /// </summary>
        [Test]
        public async Task IsHiddenFromTree_CoversBothVendorSuppressions()
        {
            Project project = await Load("Project1-SimpelWired.vis");
            ProjectElement ordinary = Find(project, "dataline_output");

            Assert.That(project.View(ordinary).IsHiddenFromTree, Is.False, "an ordinary pin is drawn");

            foreach (ProjectElement suppressed in project.Root.Descendants()
                         .Where(e => e.Tag is "airlink_shutter_up" or "airlink_shutter_down"
                                     || e.GetAttribute("setting") == "yes"))
            {
                Assert.That(project.View(suppressed).IsHiddenFromTree, Is.True,
                    $"{suppressed.Tag} is one of the vendor's suppressed rows");
            }
        }
    }
}
