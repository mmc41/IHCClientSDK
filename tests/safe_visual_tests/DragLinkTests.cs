using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// A-33 (US-022 + US-023) — dragging one pin onto another creates a link, reaching the identical result as the
/// two-step <i>Link from here / Link to here</i> supplement, under the SDK's shipped legality and orientation
/// (<c>Ihc.Vis.Schema.LinkRoles</c> / <see cref="ProjectEditor.CanLink"/> — A-16's 15-cell matrix, A-16amd's self-link,
/// F-066's orientation). The view-model asks the SDK; it never re-encodes the matrix.
/// </summary>
public class DragLinkTests : AvaloniaTestBase
{
    private static bool HasChildTag(ShellHarness harness, ElementId pinId, string tag) =>
        harness.Session.Current!.FindById(pinId)!.ChildrenOrEmpty().Any(c => c.Tag == tag);

    // A product with a dataline input plus a function block that has an input — the smallest US-023 pair.
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId productInputId, ElementId fbInputId)>
        ProductAndBlockAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var productInputId = harness.Session.Current!.FindById(pid)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;
        var fbInputId = harness.Session.Current!.FindById(fbId)!.FindChild("inputs")!.ChildrenOrEmpty().First().Id!.Value;
        return (harness, vm, productInputId, fbInputId);
    }

    // US-023 (product input → FB input) AND US-022 (FB output → another FB input): one drag gesture creates both link
    // families, with the vendor orientation (dragged pin = source = link_from half; target = sink = link_to half) — the
    // exact link the two-step supplement produces (both route through the same LinkPinsAsync op).
    [Test]
    public async Task DropPinOnPin_CreatesLink_SameAsTwoStep()
    {
        // US-023 — product input dragged onto a function-block input.
        var (harness, vm, productInputId, fbInputId) = await ProductAndBlockAsync();
        using var _ = harness;

        Assert.That(vm.CanDropOn(productInputId, fbInputId).Effect, Is.EqualTo(DropEffect.Link), "a legal pin pair shows a Link");
        await vm.PerformDropAsync(productInputId, fbInputId);

        // US-022 — a block output dragged onto a second block's input.
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Outputs.Count > 0);
        var block2 = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var fbSrcId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var fbDstId = (await harness.Session.AddFunctionBlockAsync(loc, block2.MasterType))!.Value;
        var fbOutputId = harness.Session.Current!.FindById(fbSrcId)!.FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;
        var fbDstInputId = harness.Session.Current!.FindById(fbDstId)!.FindChild("inputs")!.ChildrenOrEmpty().First().Id!.Value;

        await vm.PerformDropAsync(fbOutputId, fbDstInputId);

        Assert.Multiple(() =>
        {
            // US-023 orientation: the dragged product input owns the from-half, the target block input the to-half.
            Assert.That(HasChildTag(harness, productInputId, "link_from_resource"), Is.True, "US-023: source owns the link_from half");
            Assert.That(HasChildTag(harness, fbInputId, "link_to_resource"), Is.True, "US-023: target owns the link_to half");
            // US-022 orientation: the dragged block output owns the from-half, the target block input the to-half.
            Assert.That(HasChildTag(harness, fbOutputId, "link_from_resource"), Is.True, "US-022: source owns the link_from half");
            Assert.That(HasChildTag(harness, fbDstInputId, "link_to_resource"), Is.True, "US-022: target owns the link_to half");
            Assert.That(vm.StatusText, Does.StartWith("Linked"), "the drop reports the link");
        });
    }

    // A measured negative from the matrix: two product input pins cannot link directly (a product input is a source,
    // never a sink) — the drop is refused, with a reason, and creates nothing.
    [Test]
    public async Task DropPin_IllegalPair_Refused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var pidA = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var pidB = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var inputA = harness.Session.Current!.FindById(pidA)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;
        var inputB = harness.Session.Current!.FindById(pidB)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;

        DropVerdict verdict = vm.CanDropOn(inputA, inputB);
        await vm.PerformDropAsync(inputA, inputB);

        Assert.Multiple(() =>
        {
            Assert.That(verdict.Ok, Is.False, "two product inputs cannot be linked directly");
            Assert.That(verdict.Effect, Is.EqualTo(DropEffect.None));
            Assert.That(verdict.Reason, Is.Not.Null.And.Not.Empty, "the refusal says why");
            Assert.That(HasChildTag(harness, inputA, "link_from_resource"), Is.False, "nothing was linked");
        });
    }

    // A-16amd: a block output linked to its OWN input is a legitimate feedback pattern the vendor allows — the drag
    // must create it, not reintroduce a same-block refusal.
    [Test]
    public async Task DropPin_SelfLink_Allowed()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var block = harness.Session.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0 && f.Outputs.Count > 0);
        var fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        var ownOutputId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.ChildrenOrEmpty().First().Id!.Value;
        var ownInputId = harness.Session.Current!.FindById(fbId)!.FindChild("inputs")!.ChildrenOrEmpty().First().Id!.Value;

        Assert.That(vm.CanDropOn(ownOutputId, ownInputId).Effect, Is.EqualTo(DropEffect.Link), "a self-link (output→own input) is allowed");
        await vm.PerformDropAsync(ownOutputId, ownInputId);

        Assert.Multiple(() =>
        {
            Assert.That(HasChildTag(harness, ownOutputId, "link_from_resource"), Is.True, "the block output owns the from-half");
            Assert.That(HasChildTag(harness, ownInputId, "link_to_resource"), Is.True, "its own input owns the to-half");
        });
    }

    // The highlight follows the SDK legality: DragOver a legal target pin shows Link, an illegal one shows None.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task DragOver_HighlightsLegalPinsOnly()
    {
        var (harness, vm, productInputId, fbInputId) = await ProductAndBlockAsync();
        using var _ = harness;
        // A second product input — an illegal link target for the first product input.
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var pidB = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        var inputB = harness.Session.Current!.FindById(pidB)!.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;

        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        for (int i = 0; i < 4; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Dispatcher.UIThread.RunJobs();
        }
        CurrentTestWindow = window;

        var productInput = TreeNodes.FindById(vm.InstallationNodes, productInputId)!;
        var fbInput = TreeNodes.FindById(vm.FunctionNodes, fbInputId)!;
        var otherProductInput = TreeNodes.FindById(vm.InstallationNodes, inputB)!;

        var overLegal = window.DragOverEffect(productInput, fbInput);
        var overIllegal = window.DragOverEffect(productInput, otherProductInput);

        Assert.Multiple(() =>
        {
            Assert.That(overLegal, Is.EqualTo(DragDropEffects.Link), "a legal target pin highlights as a Link");
            Assert.That(overIllegal, Is.EqualTo(DragDropEffects.None), "an illegal target pin is not highlighted");
        });
    }
}
