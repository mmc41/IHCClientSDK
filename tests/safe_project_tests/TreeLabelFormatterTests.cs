using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Tests;

/// <summary>
/// T021: the shared presentation formatter the tree projector, the properties-dialog coordinator and the reconciler
/// all render through (previously each kept its own copy of ProductLabel / SceneMemberValue / the opposite-end walk).
/// Pins its public contract; the projector/coordinator/reconciler suites are the behavior guard for the wired paths.
/// </summary>
public class TreeLabelFormatterTests
{
    private static Project NewBaseProject() =>
        new ProjectAppService(new IhcSettings()).CreateNew(new ProjectDetails(string.Empty, string.Empty, string.Empty));

    [Test]
    public void ProductLabel_AppendsPlacementDescriptor_OrBareName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TreeLabelFormatter.ProductLabel("Stikkontakt", "A1"), Is.EqualTo("Stikkontakt (A1) "),
                "name (position) with the vendor's trailing space (F-003)");
            Assert.That(TreeLabelFormatter.ProductLabel("Stikkontakt", null), Is.EqualTo("Stikkontakt"));
            Assert.That(TreeLabelFormatter.ProductLabel("Stikkontakt", ""), Is.EqualTo("Stikkontakt"));
        });
    }

    [Test]
    public void LinkOpposite_OnANonLinkElement_IsUnresolved()
    {
        Project project = NewBaseProject();
        ProjectElement group = project.Groups[0];   // a locality has no `link` attribute to resolve a partner from

        Assert.Multiple(() =>
        {
            Assert.That(TreeLabelFormatter.LinkPartnerChain(project, group), Is.Empty);
            Assert.That(TreeLabelFormatter.LinkOppositeParts(project, group), Is.Empty);
            Assert.That(TreeLabelFormatter.LinkOppositePath(project, group), Is.EqualTo("(unresolved)"));
        });
    }
}
