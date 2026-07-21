using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// US-070 — an edit must not collapse the tree. Every project mutation fires <c>StateChanged</c> → the
/// view-model rebuilds both panes from scratch; the rebuild MUST carry each surviving node's expand/collapse
/// state across, so linking two pins, deleting a link, or any other edit leaves the tree open exactly as the
/// installer left it. A node revealing its FIRST child still opens by default (US-006), and a deliberate MODE
/// switch (config ⇄ a block's programming view) still starts fresh. Regression guard for the "whole tree
/// collapses on every change" defect.
/// </summary>
public class TreeExpansionTests : AvaloniaTestBase
{
    // A product with a dataline input in a locality + a function block with an input — the smallest shape with an
    // expandable product row (Installation pane) and an expandable FB + section (Functions pane).
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId productId, ElementId productInputId, ElementId fbId, ElementId fbInputId)>
        ProductAndBlockAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var productId = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var productInputId = harness.Session.Current!.FindById(productId)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;
        var fbInputId = harness.Session.Current!.FindById(fbId)!.FindChild("inputs")!.ChildrenOrEmpty().First().Id!.Value;
        return (harness, vm, productId, productInputId, fbId, fbInputId);
    }

    // The Input section node's id under a function block (the section node addresses its <inputs> container).
    private static ElementId InputsSectionId(ShellHarness harness, ElementId fbId) =>
        harness.Session.Current!.FindById(fbId)!.FindChild("inputs")!.Id!.Value;

    private static void Expand(IEnumerable<TreeNodeViewModel> roots, ElementId id) =>
        (TreeNodes.FindById(roots, id) ?? throw new AssertionException($"node {id.ToToken()} not found")).IsExpanded = true;

    private static bool IsExpanded(IEnumerable<TreeNodeViewModel> roots, ElementId id) =>
        (TreeNodes.FindById(roots, id) ?? throw new AssertionException($"node {id.ToToken()} not found after rebuild")).IsExpanded;

    // The reported trigger #1: connecting two pins by drag-drop must not collapse the branches the installer opened
    // to reach those pins. The product row, the block and its Input section all existed and were open before the link.
    [Test]
    public async Task Linking_KeepsExpandedNodesExpanded()
    {
        var (harness, vm, productId, productInputId, fbId, fbInputId) = await ProductAndBlockAsync();
        using var _ = harness;
        var inputsSectionId = InputsSectionId(harness, fbId);

        Expand(vm.InstallationNodes, productId);
        Expand(vm.FunctionNodes, fbId);
        Expand(vm.FunctionNodes, inputsSectionId);

        await vm.DragDrop.PerformDropAsync(productInputId, fbInputId);   // link the two pins (drag-drop)

        Assert.Multiple(() =>
        {
            Assert.That(IsExpanded(vm.InstallationNodes, productId), Is.True, "the product row stays open after linking");
            Assert.That(IsExpanded(vm.FunctionNodes, fbId), Is.True, "the function block stays open after linking");
            Assert.That(IsExpanded(vm.FunctionNodes, inputsSectionId), Is.True, "the Input section stays open after linking");
        });
    }

    // The reported trigger #2: deleting a link must not collapse the tree either.
    [Test]
    public async Task DeletingALink_KeepsExpandedNodesExpanded()
    {
        var (harness, vm, productId, productInputId, fbId, fbInputId) = await ProductAndBlockAsync();
        using var _ = harness;
        var inputsSectionId = InputsSectionId(harness, fbId);
        await vm.DragDrop.PerformDropAsync(productInputId, fbInputId);   // create a link to delete
        var linkRowId = harness.Session.Current!.FindById(productInputId)!.ChildrenOrEmpty()
            .First(c => c.Tag is "link_from_resource" or "link_to_resource").Id!.Value;

        Expand(vm.InstallationNodes, productId);
        Expand(vm.FunctionNodes, fbId);
        Expand(vm.FunctionNodes, inputsSectionId);

        await harness.Session.RemoveLinkAsync(linkRowId);   // delete the link

        Assert.Multiple(() =>
        {
            Assert.That(IsExpanded(vm.InstallationNodes, productId), Is.True, "the product row stays open after deleting a link");
            Assert.That(IsExpanded(vm.FunctionNodes, fbId), Is.True, "the function block stays open after deleting a link");
            Assert.That(IsExpanded(vm.FunctionNodes, inputsSectionId), Is.True, "the Input section stays open after deleting a link");
        });
    }

    // Preservation is exact, not "force everything open": a node the installer deliberately collapsed stays
    // collapsed across an unrelated edit, even though it would default to open (a locality with contents, US-006).
    [Test]
    public async Task Editing_KeepsCollapsedNodeCollapsed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);

        var localityNode = TreeNodes.FindById(vm.InstallationNodes, loc)!;
        Assert.That(localityNode.IsExpanded, Is.True, "a locality with contents defaults to open");
        localityNode.IsExpanded = false;   // the installer collapses it deliberately

        await harness.Session.AddLocalityAsync();   // an unrelated edit rebuilds the tree

        Assert.That(IsExpanded(vm.InstallationNodes, loc), Is.False,
            "a deliberately collapsed locality stays collapsed across an edit — the rebuild restores state, it does not force defaults");
    }

    // A locality gaining its FIRST child must still open to reveal it (US-006) — the "reveal" default wins over a
    // stale collapsed state, because expansion is only carried for nodes that already had children.
    [Test]
    public async Task AddingFirstProduct_OpensTheLocality()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        Assert.That(IsExpanded(vm.InstallationNodes, loc), Is.False, "an empty locality starts collapsed");
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));

        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);

        Assert.That(IsExpanded(vm.InstallationNodes, loc), Is.True,
            "adding the first product opens the locality to reveal it, not inherits its empty collapsed state");
    }

    // A MODE switch is not an in-place edit: entering a block's programming mode opens its program fresh, rather
    // than carrying over the block's collapsed state from configuration mode.
    [Test]
    public async Task EnteringProgrammingMode_StartsExpanded_IgnoringConfigState()
    {
        var (harness, vm, _, _, fbId, _) = await ProductAndBlockAsync();
        using var _ = harness;
        var fbNode = TreeNodes.FindById(vm.FunctionNodes, fbId)!;
        Assert.That(fbNode.IsExpanded, Is.False, "the block row defaults collapsed in configuration mode");

        vm.EnterProgrammingModeCommand.Execute(fbNode);

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True, "the command entered programming mode");
            Assert.That(IsExpanded(vm.FunctionNodes, fbId), Is.True,
                "programming mode opens the block's program expanded, not carried over from configuration mode");
        });
    }

    // End-to-end through the real window: the IsExpanded binding must be TwoWay so a user's expander click (a change
    // on the CONTROL) reaches the view-model — otherwise the rebuild's snapshot reads a stale default and the tree
    // still collapses. Expand the rows via the control, then make an edit, and assert a default-collapsed row is
    // still open.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task UserExpansionInWindow_SurvivesAnEdit()
    {
        var (harness, vm, productId, _, _, _) = await ProductAndBlockAsync();
        using var _ = harness;
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 4; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;   // a user expanding rows via the control
            Dispatcher.UIThread.RunJobs();
        }
        CurrentTestWindow = window;

        // The TwoWay IsExpanded binding must carry the control-side expansion into the view-model...
        Assert.That(TreeNodes.FindById(vm.InstallationNodes, productId)!.IsExpanded, Is.True,
            "expanding a row in the UI reaches the view-model (TwoWay IsExpanded binding)");

        // ...so that the rebuild an edit triggers can restore it.
        await harness.Session.AddLocalityAsync();
        Assert.That(IsExpanded(vm.InstallationNodes, productId), Is.True,
            "a row the user expanded in the UI stays open after an edit");
    }
}
