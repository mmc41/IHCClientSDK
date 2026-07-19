using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Session;

namespace safe_visual_tests;

/// <summary>US-050: the read-only Wired module address map — addressed terminals appear with their occupying
/// product terminal; unaddressed terminals do not; the view mutates nothing.</summary>
public class ModuleMapTests
{
    private static ElementId? FindTagged(IEnumerable<ProjectElement> roots, string tag)
    {
        foreach (var e in roots)
        {
            if (e.Tag == tag && e.Id is { } id)
                return id;
            if (FindTagged(e.ChildrenOrEmpty(), tag) is { } found)
                return found;
        }
        return null;
    }

    [Test]
    public async Task ModuleMap_EmptyProject_HasNoEntries()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        var map = harness.Session.GetModuleAddressMap();

        Assert.Multiple(() =>
        {
            Assert.That(map.InputModules, Is.Empty);
            Assert.That(map.OutputModules, Is.Empty);
        });
    }

    [Test]
    public async Task ModuleMap_ListsAddressedWiredInput_WithOccupyingTerminal()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.Session.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        var pinId = FindTagged(harness.Session.Current!.Groups, "dataline_input")!.Value;

        await harness.Session.UpdatePinAsync(pinId, new PinPropertiesResult(1, 5, "red", string.Empty, false));

        var beforeProject = harness.Session.Current;
        var map = harness.Session.GetModuleAddressMap();
        var entry = map.InputModules.FirstOrDefault(e => e.Address == "1.5");
        Assert.Multiple(() =>
        {
            Assert.That(entry, Is.Not.Null, "the addressed terminal appears on input module 1, terminal 5");
            Assert.That(entry!.Product, Is.Not.Empty, "it names the occupying product");
            Assert.That(entry.Terminal, Is.Not.Empty, "it names the occupying product terminal");
            Assert.That(harness.Session.Current, Is.SameAs(beforeProject), "reading the map mutates nothing");
        });
    }

    [Test]
    public async Task ModuleMapCommand_OpensTheReadOnlyView()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        await vm.ModuleMapCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.ShowModuleMapCalls, Is.EqualTo(1));
            Assert.That(harness.Dialogs.LastModuleMap, Is.Not.Null);
        });
    }
}
