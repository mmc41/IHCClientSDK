using System.Linq;
using Ihc.App;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests
{
    /// <summary>
    /// W1 / RC1 / D02-D03 (uxparity2 T013): the ENGINE owns "what may be inserted under this section", and a caller
    /// asks it rather than keeping a second copy of the rule. The vendor offers a function-block Input section its
    /// signal type plus all 19 value types (measured live), and
    /// <see cref="PlacementRules.OptionsFor"/> already models exactly that — what was missing was a public door onto
    /// it, which is what these tests pin.
    /// <para>
    /// The door deliberately returns the <b>variable</b> types only: <c>OptionsFor("outputs")</c> legitimately also
    /// offers <c>resource_scene</c>, which is not a variable type and belongs to US-024's separate scene flow. A
    /// palette rendering the raw option list would show the vendor's Output list but break that story.
    /// </para>
    /// </summary>
    public class InsertableVariableTypesTests
    {
        // A block with all four sections, reached through the public facade exactly as a GUI would.
        private static (ProjectAppService app, Project project, ElementId inputs, ElementId outputs,
                        ElementId settings, ElementId internals) BlockWithSections()
        {
            var app = new ProjectAppService(TestSetup.Settings);
            Project project = ProjectReader.Read(TestData.ReadBytes("projects/project2-CustomBlock.vis"));
            ProjectElement block = project.Root.DescendantsAndSelf()
                .First(e => e.Tag == "functionblock" && e.GetAttribute("name") == "Custom blok");
            ElementId Section(string tag) => block.Children.First(c => c.Tag == tag).Id!.Value;
            return (app, project, Section("inputs"), Section("outputs"), Section("settings"), Section("internalsettings"));
        }

        [Test]
        public void InputsSection_OffersItsSignalType_PlusAllNineteenValueTypes()
        {
            var (app, project, inputs, _, _, _) = BlockWithSections();

            var offered = app.GetInsertableVariableTypes(project, inputs);

            Assert.Multiple(() =>
            {
                Assert.That(offered, Does.Contain("resource_input"), "an Input section offers its own signal type");
                foreach (string tag in VariableTypeRegistry.ValueTypeTags)
                    Assert.That(offered, Does.Contain(tag), $"the vendor offers every value type here, including {tag}");
                Assert.That(offered, Has.Count.EqualTo(1 + VariableTypeRegistry.ValueTypeTags.Length),
                    "exactly the signal type plus the 19 value types — no more");
                Assert.That(offered, Does.Not.Contain("resource_output"), "the OTHER signal type is not insertable here");
            });
        }

        [Test]
        public void OutputsSection_OffersItsSignalType_PlusValueTypes_ButNotTheSceneEntry()
        {
            var (app, project, _, outputs, _, _) = BlockWithSections();

            var offered = app.GetInsertableVariableTypes(project, outputs);

            Assert.Multiple(() =>
            {
                Assert.That(offered, Does.Contain("resource_output"));
                Assert.That(offered, Does.Not.Contain("resource_input"));
                Assert.That(offered, Does.Not.Contain("resource_scene"),
                    "a scene is not a variable type — US-024 owns that route (uxparity2 V3 scope guard)");
                Assert.That(offered, Has.Count.EqualTo(1 + VariableTypeRegistry.ValueTypeTags.Length));
            });
        }

        // Settings and internal settings carry no signal type at all — the vendor shows 19 items there, not 20.
        [Test]
        public void SettingsSections_OfferTheValueTypesOnly_WithNoSignalType()
        {
            var (app, project, _, _, settings, internals) = BlockWithSections();

            Assert.Multiple(() =>
            {
                foreach (ElementId section in new[] { settings, internals })
                {
                    var offered = app.GetInsertableVariableTypes(project, section);
                    Assert.That(offered, Has.Count.EqualTo(VariableTypeRegistry.ValueTypeTags.Length),
                        "19 value types, no signal type");
                    Assert.That(offered, Does.Not.Contain("resource_input"));
                    Assert.That(offered, Does.Not.Contain("resource_output"));
                }
            });
        }

        // A container that is not a block section has no variable palette; the caller gets an empty list rather than
        // having to know which kinds are sections.
        [Test]
        public void NonSectionContainer_OffersNoVariableTypes()
        {
            var (app, project, _, _, _, _) = BlockWithSections();
            ElementId locality = project.Root.DescendantsAndSelf().First(e => e.Tag == "group").Id!.Value;

            Assert.That(app.GetInsertableVariableTypes(project, locality), Is.Empty,
                "a locality holds products and blocks, not variables");
        }
    }
}
