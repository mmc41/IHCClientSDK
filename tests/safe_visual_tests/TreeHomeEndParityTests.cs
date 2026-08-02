using System.Threading.Tasks;

namespace safe_visual_tests;

/// <summary>
/// Home/End move the caret to the first / last VISIBLE row of a pane (uxparity S-29). Measured on the vendor from
/// `Stue` with every locality collapsed: End lands on `Udendørs` (the last top-level row) and Home on the
/// `Lokaliteter` root. OpenVisual did nothing for either key.
/// </summary>
public class TreeHomeEndParityTests
{
    [Test]
    public async Task Home_SelectsTheFirstRow()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);

        vm.SelectFirstRowCommand.Execute(false);

        // Assert the property the TREE binds to. SelectedNode is an aggregate that a pane change feeds, so
// asserting it passes even when nothing on screen moves — which is exactly what happened (S-29).
Assert.That(vm.SelectedInstallationNode, Is.SameAs(vm.InstallationNodes[0]), "Home lands on the tree root");
    }

    [Test]
    public async Task End_SelectsTheLastVisibleRow_NotACollapsedDescendant()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var root = vm.InstallationNodes[0];
        var lastLocality = root.Children[^1];
        lastLocality.IsExpanded = false;
        vm.SelectNode(root);

        vm.SelectLastVisibleRowCommand.Execute(false);

        Assert.That(vm.SelectedInstallationNode, Is.SameAs(lastLocality),
            "End stops at the last VISIBLE row — a collapsed locality's children are not reachable by caret");
    }

    [Test]
    public async Task End_DescendsIntoAnExpandedLastRow()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var root = vm.InstallationNodes[0];
        var last = root.Children[^1];
        if (last.Children.Count == 0)
            Assert.Ignore("the fixture's last locality has no children");
        last.IsExpanded = true;

        vm.SelectLastVisibleRowCommand.Execute(false);

        Assert.That(vm.SelectedInstallationNode, Is.SameAs(last.Children[^1]));
    }
}
