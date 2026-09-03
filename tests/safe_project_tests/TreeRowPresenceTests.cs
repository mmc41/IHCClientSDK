using System.Linq;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Tests;

/// <summary>
/// The row-bearing predicate the reveal path asks before it claims a destination: would the projector draw a row
/// for this element? It answers from the projector's OWN ladder rather than from a live tree, so it stays right
/// for a pane that is closed, filtered or not built yet — and so the answer cannot drift from what the tree
/// actually renders.
/// <para>The four classes below are the ones that decide it: the <c>scenes</c> container (structural by tag, yet
/// drawn), an embedded <c>enum_definition</c>, a <c>*_settings</c> container together with what it holds, and a
/// resource carrying <c>setting="yes"</c>.</para>
/// </summary>
public class TreeRowPresenceTests
{
    // A minimal project shaped like the ladder's world: one locality holding one product whose body carries every
    // interesting child class. Built rather than loaded so each case is visible in one place.
    private static Project Build()
    {
        static ProjectElement Element(string tag, string id, params ProjectElement[] children) =>
            ProjectElement.Create(tag, ElementId.ParseOrNull(id), [], children);

        ProjectElement product = Element("product_dataline", "_0x100",
            Element("dataline_input", "_0x101"),
            Element("scenes", "_0x102", Element("scene_relay", "_0x103")),
            Element("enum_definition", "_0x104", Element("enum_value", "_0x105")),
            Element("dimmer_settings", "_0x106", Element("dimmer_setting_fade_rate_up", "_0x107")),
            ProjectElement.Create("resource_temperature", ElementId.ParseOrNull("_0x108"),
                [(ProductRows.SettingAttribute, ProductRows.SettingValue)], []),
            Element("airlink_shutter_up", "_0x109"));

        return new Project(Element("project", "_0x0",
            Element("groups", "_0x1", Element("group", "_0x2", product))));
    }

    private static bool HasRow(Project project, string idToken) =>
        ProjectTreeProjector.HasRow(project, ElementId.ParseOrNull(idToken)!.Value);

    [Test]
    public void ScenesContainerAndItsMembers_AreRowBearing_DespiteBeingStructural()
    {
        Project project = Build();
        Assert.Multiple(() =>
        {
            Assert.That(ProductRows.IsStructuralChild("scenes"), Is.True,
                "precondition: the tag IS structural, which is what makes this the exception");
            Assert.That(HasRow(project, "_0x102"), Is.True, "the projector tests IsScenesContainer first");
            Assert.That(HasRow(project, "_0x103"), Is.True, "so a scene member is reachable through it");
        });
    }

    [Test]
    public void EnumDefinition_AndItsValues_AreNotRowBearing()
    {
        Project project = Build();
        Assert.Multiple(() =>
        {
            Assert.That(HasRow(project, "_0x104"), Is.False);
            Assert.That(HasRow(project, "_0x105"), Is.False, "and neither is anything inside it");
        });
    }

    [Test]
    public void SettingsContainer_AndItsChildren_AreNotRowBearing()
    {
        Project project = Build();
        Assert.Multiple(() =>
        {
            Assert.That(HasRow(project, "_0x106"), Is.False, "a *_settings container is structural");
            Assert.That(HasRow(project, "_0x107"), Is.False,
                "and a setting inside it inherits that — the walk is over ancestors, not the element alone");
        });
    }

    [Test]
    public void ResourceMarkedAsASetting_IsNotRowBearing()
    {
        Project project = Build();
        Assert.Multiple(() =>
        {
            Assert.That(HasRow(project, "_0x108"), Is.False,
                "a calibration row is a genuine resource the vendor's tree declines to draw");
            Assert.That(HasRow(project, "_0x109"), Is.False, "as is a shutter direction pin, decided by tag");
        });
    }

    [Test]
    public void OrdinaryElements_AreRowBearing()
    {
        Project project = Build();
        Assert.Multiple(() =>
        {
            Assert.That(HasRow(project, "_0x2"), Is.True, "a locality");
            Assert.That(HasRow(project, "_0x100"), Is.True, "a product");
            Assert.That(HasRow(project, "_0x101"), Is.True, "an ordinary terminal");
        });
    }

    [Test]
    public void AnElementThatIsNotInTheProject_IsNotRowBearing()
    {
        Assert.That(HasRow(Build(), "_0xdead"), Is.False,
            "a vanished element has no row, so the reveal has to degrade rather than promise one");
    }

    // The predicate exists so the ladder is stated ONCE. If the projector stopped agreeing with it, the panel would
    // promise destinations the tree does not draw — so the two are compared over a real product body.
    [Test]
    public void ThePredicateAgreesWithTheRowsTheProjectorActuallyBuilds()
    {
        Project project = Build();
        TreeNodeViewModel root = new ProjectTreeProjector(project).BuildLocalitiesRoot(functions: false);
        TreeNodeViewModel productNode = root.Children[0].Children[0];

        var drawn = productNode.Children.Select(n => n.ElementId!.Value).ToHashSet();
        ProjectElement product = project.FindById(ElementId.ParseOrNull("_0x100")!.Value)!;
        Assert.Multiple(() =>
        {
            foreach (ProjectElement child in product.Children)
            {
                Assert.That(ProjectTreeProjector.HasRow(project, child.Id!.Value),
                    Is.EqualTo(drawn.Contains(child.Id!.Value)),
                    $"the predicate and the projector disagree about <{child.Tag}>");
            }
        });
    }
}
