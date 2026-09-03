using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests;

/// <summary>US-047/US-048: hover tooltips carry a node's documentation note and its IHC resource id; nodes with
/// neither (the Localities root, an empty locality) show no tooltip.</summary>
public class TooltipTests
{
    [Test]
    public async Task Tooltip_LocalitiesRootAndEmptyLocality_ShowNoTooltip()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes[0].Tooltip, Is.Null, "the Localities root has no note and no resource id");
            Assert.That(vm.InstallationNodes[0].Children[0].Tooltip, Is.Null, "an empty locality with no note shows no tooltip");
        });
    }

    // US-048: a function block's tooltip shows its IHC resource id on plain hover.
    [Test]
    public async Task Tooltip_FunctionBlock_ShowsResourceId()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var blockId = vm.FunctionNodes[0].Children[0].Children[0].ElementId!.Value;

        var tooltip = TreeNodes.FindById(vm.FunctionNodes, blockId)!.Tooltip;

        Assert.That(tooltip, Does.Contain($"Resource ID: {blockId.Value}"), "the block shows its resource id");
    }

    // US-047/US-048: a product input carrying a note shows the note and its resource id, each on its own line(s).
    [Test]
    public async Task Tooltip_ProductInput_ShowsNoteThenResourceId()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        var pinId = FindTagged(harness.Session.Current!.Groups, "dataline_input")!.Value;
        await harness.Session.UpdatePinAsync(pinId, new PinPropertiesResult(1, 1, "red", "Presence from the PIR sensor.", false));

        var tooltip = TreeNodes.FindById(vm.InstallationNodes, pinId)!.Tooltip;

        Assert.Multiple(() =>
        {
            Assert.That(tooltip, Does.Contain("Presence from the PIR sensor."), "the tooltip carries the note");
            Assert.That(tooltip, Does.Contain($"Resource ID: {pinId.Value}"), "and the resource id");
            Assert.That(tooltip!.IndexOf("Presence", StringComparison.Ordinal), Is.LessThan(tooltip.IndexOf("Resource ID:", StringComparison.Ordinal)), "note before the id line");
        });
    }

    private static ElementId? FindTagged(IEnumerable<ProjectElement> roots, string tag)
    {
        foreach (var e in roots)
        {
            if (e.Tag == tag && e.Id is { } id)
                return id;
            if (FindTagged(e.Children, tag) is { } found)
                return found;
        }
        return null;
    }
}
