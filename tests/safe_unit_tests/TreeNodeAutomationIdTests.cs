using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace safe_unit_tests;

/// <summary>
/// UX review SPEC-01 / USE-02: what a tree row publishes to UI Automation.
/// <para>
/// A row's <c>AutomationProperties.AutomationId</c> is a LOCATOR, and UI Automation's contract for a locator is that
/// it is unique among its siblings. The row used to publish its <see cref="TreeNodeViewModel.NodeKind"/> there, so
/// ten sibling localities all answered to <c>locality</c> and no client could address one of them: a driver that
/// asked for "the locality" got whichever the search happened to reach first, and a rename or reorder silently
/// re-pointed it. The kind is still published (it is the prefix), but the element's own id now qualifies it.
/// </para>
/// <para>
/// The accessible NAME is the other half: it is spoken, so it belongs to the application's language. The unlinked
/// suffix was English inside an otherwise Danish tree.
/// </para>
/// The projector is Avalonia-free (project in, nodes out), so these run headlessly here — no App needed.
/// </summary>
public class TreeNodeAutomationIdTests
{
    private static Task<Project> Project1Oracle() =>
        new ProjectAppService(new IhcSettings()).Load(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Project1-SimpelWired.vis"));

    private static IEnumerable<TreeNodeViewModel> Flatten(TreeNodeViewModel node)
    {
        yield return node;
        foreach (TreeNodeViewModel descendant in node.Children.SelectMany(Flatten))
        {
            yield return descendant;
        }
    }

    // Both panes of a real project: every parent's children must be distinguishable by id alone.
    [TestCase(false, TestName = "Installation pane")]
    [TestCase(true, TestName = "Functions pane")]
    public async Task TreeRowAutomationIds_AreUniqueAmongSiblings(bool functions)
    {
        Project project = await Project1Oracle();
        TreeNodeViewModel root = new ProjectTreeProjector(project).BuildLocalitiesRoot(functions);

        var collisions = new List<string>();
        foreach (TreeNodeViewModel parent in Flatten(root))
        {
            foreach (var group in parent.Children.GroupBy(c => c.AutomationId).Where(g => g.Count() > 1))
            {
                collisions.Add($"{parent.DisplayName}: {group.Count()}x '{group.Key}' "
                               + $"({string.Join(", ", group.Select(c => c.DisplayName))})");
            }
        }

        Assert.That(collisions, Is.Empty,
            "no two sibling rows may publish the same AutomationId:\n  " + string.Join("\n  ", collisions));
    }

    // The kind stays readable in the id — a client that partitions rows by type still can, without a second property.
    [Test]
    public async Task TreeRowAutomationId_KeepsTheNodeKindAsItsPrefix()
    {
        Project project = await Project1Oracle();
        TreeNodeViewModel root = new ProjectTreeProjector(project).BuildLocalitiesRoot(functions: false);

        Assert.Multiple(() =>
        {
            Assert.That(root.AutomationId, Is.EqualTo("localitiesRoot"),
                "a row that stands for no element is identified by its kind alone");
            Assert.That(Flatten(root).Where(n => n.Kind == TreeNodeKind.Locality).Select(n => n.AutomationId),
                Has.All.StartWith("locality#"), "an element-backed row qualifies its kind with the element id");
        });
    }

    // USE-02: the spoken name is Danish, like the "!" marker's tooltip beside it.
    [Test]
    public void UnlinkedRow_AnnouncesItsStateInDanish()
    {
        var unlinked = new TreeNodeViewModel("Trykknap", "/Assets/product.svg", isUnlinked: true);
        var linked = new TreeNodeViewModel("Trykknap", "/Assets/product.svg");

        Assert.Multiple(() =>
        {
            Assert.That(unlinked.AccessibleName, Is.EqualTo("Trykknap, ikke linket til controlleren"));
            Assert.That(linked.AccessibleName, Is.EqualTo("Trykknap"), "a linked row announces its label alone");
        });
    }
}
