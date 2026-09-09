using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// D-2: the two remaining schema tables a frontend used to read directly, asked of the facade instead — the
    /// sections a function block declares (identity, order and caption), and whether an insertable variable type is
    /// a block SIGNAL rather than a value it holds.
    /// </summary>
    public class BlockSectionAndVariableTypeFactsTests : SessionCommandFixture
    {
        /// <summary>
        /// The sections come back in DOCUMENT order with their identity and caption — the three things a consumer
        /// needs to lay them out. Order is part of the answer: a block's inputs precede its outputs in the file.
        /// </summary>
        [Test]
        public async Task BlockSections_CarryIdentityOrderAndCaption()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ProjectElement block = project.Root.Descendants().First(e => e.Tag == "functionblock");

            var sections = project.View(block).BlockSections;

            Assert.Multiple(() =>
            {
                Assert.That(sections.Select(s => s.Container),
                    Is.EqualTo(new[] { "inputs", "outputs", "settings", "internalsettings" }),
                    "identity and order, as the format declares them");
                Assert.That(sections.Select(s => s.Caption), Has.All.Not.Empty, "each section has a fallback caption");
                Assert.That(sections.Where(s => s.IsInternal).Select(s => s.Container),
                    Is.EqualTo(new[] { "internalsettings" }),
                    "exactly one section is internal to the block rather than part of its interface");
                Assert.That(project.View(project.Root).BlockSections, Is.Empty,
                    "an element that is not a function block declares none");
            });
        }

        /// <summary>
        /// The insertion query now answers WHAT each offered type is, not just its tag: a signal is the block's
        /// wiring in or out, a value is something it holds. A caller that groups or orders by that no longer needs
        /// the registry.
        /// </summary>
        [Test]
        public async Task InsertableVariableTypes_SayWhetherEachIsASignal()
        {
            Project project = await Load("project2-CustomBlock.vis");
            ElementId section = project.Root.Descendants()
                .First(e => e.Tag == "functionblock").Children
                .First(c => c.Tag is "variables" or "settings" or "internalsettings").Id!.Value;

            var offered = App.GetInsertableVariableTypes(project, section);

            Assert.Multiple(() =>
            {
                Assert.That(offered, Is.Not.Empty, "the section accepts variables at all");
                Assert.That(offered.Where(t => t.Tag is "resource_input" or "resource_output")
                        .All(t => t.IsSignal), Is.True,
                    "an input or an output is the block's wiring");
                Assert.That(offered.Where(t => t.Tag is "resource_integer" or "resource_flag")
                        .All(t => !t.IsSignal), Is.True,
                    "a register or a flag is a value the block holds");
            });
        }
    }
}
