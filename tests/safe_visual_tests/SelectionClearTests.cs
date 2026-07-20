using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;

namespace safe_visual_tests;

/// <summary>
/// C4 (review): a pane's two-way <c>SelectedInstallationNode</c>/<c>SelectedFunctionsNode</c> can be cleared to null
/// by the framework when the selected item leaves the tree (delete / undo / project-switch reconciliation). When the
/// ACTIVE pane's selection is cleared, the shared <see cref="MainWindowViewModel.SelectedNode"/> must follow so it no
/// longer points at a detached node and the mutation gates (Delete/Paste) disable.
/// </summary>
public class SelectionClearTests
{
    [Test]
    public async Task ClearingActivePaneSelection_NullsSelectedNode_AndDisablesDeleteAndPaste()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        TreeNodeViewModel locality = vm.InstallationNodes[0].Children[0];   // a room locality (deletable, NodeKind "locality")
        vm.CopyCommand.Execute(locality);          // arm the clipboard so Paste is offered on a locality
        vm.SelectedInstallationNode = locality;    // select in the Installation pane — the active pane

        // Preconditions: the active-pane selection drives SelectedNode and the mutation gates are enabled.
        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedNode, Is.SameAs(locality));
            Assert.That(vm.CanDeleteSelected, Is.True, "a locality is deletable");
            Assert.That(vm.CanPaste, Is.True, "clipboard armed + locality selected");
        });

        vm.SelectedInstallationNode = null;        // the pane's selection is cleared (delete / undo / project-switch)

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedNode, Is.Null, "clearing the active pane's selection nulls SelectedNode");
            Assert.That(vm.CanDeleteSelected, Is.False, "a null selection cannot be deleted");
            Assert.That(vm.CanPaste, Is.False, "a null selection cannot be a paste target");
        });
    }

    // The guard is pane-scoped: clearing the INACTIVE pane's stale selection must not disturb the active selection.
    [Test]
    public async Task ClearingInactivePaneSelection_LeavesActiveSelectionIntact()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        TreeNodeViewModel installationNode = vm.InstallationNodes[0].Children[0];
        vm.SelectedInstallationNode = installationNode;   // Installation pane selection (older)
        TreeNodeViewModel functionsNode = vm.FunctionNodes[0];
        vm.SelectedFunctionsNode = functionsNode;         // Functions pane becomes the active selection

        vm.SelectedInstallationNode = null;               // the now-inactive pane's selection clears

        Assert.That(vm.SelectedNode, Is.SameAs(functionsNode),
            "clearing the inactive pane must not null the active pane's SelectedNode");
    }

    // C5 (review): the programming-mode branch of Refresh() rebuilds both panes (BuildProgrammingTrees clears them),
    // so a block-program edit must capture+restore the tree selection the same way the config-mode fallback already
    // does — otherwise the selected container is dropped to an orphaned instance mid-authoring.
    [Test]
    public async Task ProgrammingModeEdit_PreservesTreeSelection()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);

        ElementId settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;   // the block's Settings section
        vm.SelectedInstallationNode = TreeNodes.FindById(vm.InstallationNodes, settingsId);

        await harness.Session.AddVariableAsync(settingsId, "resource_flag", "Away");   // a program edit → Refresh rebuild

        Assert.That(vm.SelectedInstallationNode, Is.SameAs(TreeNodes.FindById(vm.InstallationNodes, settingsId)),
            "the selected section survives a programming-mode edit — restored to the rebuilt instance (C5)");
    }
}
