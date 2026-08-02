using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using ihc_openvisual.ViewModels;

namespace safe_visual_tests;

/// <summary>
/// A copied locality must be pasteable somewhere (uxparity S-10). It duplicates onto the locality ROOT —
/// the container localities actually live in — appended last, exactly as IHC Visual does. A locality
/// cannot be pasted onto another locality, because a locality does not nest.
/// </summary>
public class PasteLocalityParityTests
{
    private static TreeNodeViewModel Root(MainWindowViewModel vm) => vm.InstallationNodes[0];

    [Test]
    public async Task CopyLocality_PastedOnTheRoot_IsDuplicatedLast()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        TreeNodeViewModel source = Root(vm).Children[0];
        string sourceName = source.DisplayName;
        int before = Root(vm).Children.Count;

        vm.CopyCommand.Execute(source);
        await vm.PasteCommand.ExecuteAsync(Root(vm));

        Assert.Multiple(() =>
        {
            Assert.That(Root(vm).Children, Has.Count.EqualTo(before + 1), "the copy is added");
            Assert.That(Root(vm).Children[^1].DisplayName, Is.EqualTo(sourceName), "appended last, keeping its name");
            Assert.That(Root(vm).Children[^1].ElementId, Is.Not.EqualTo(source.ElementId), "and it is an independent element");
        });
    }

    [Test]
    public async Task Paste_RevealsTheWholePastedSubtree()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // Give the source locality something to hold, so the paste brings a subtree and not just a bare row.
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Count > 0);
        await harness.Session.AddProductAsync(Root(vm).Children[0].ElementId!.Value, product.ProductIdentifier);

        vm.CopyCommand.Execute(Root(vm).Children[0]);
        await vm.PasteCommand.ExecuteAsync(Root(vm));

        TreeNodeViewModel pasted = Root(vm).Children[^1];
        Assert.Multiple(() =>
        {
            Assert.That(pasted.IsExpanded, Is.True, "the pasted locality opens so its arrival is visible");
            Assert.That(pasted.Children, Is.Not.Empty, "precondition: it brought its contents");
            Assert.That(pasted.Children.All(c => c.Children.Count == 0 || c.IsExpanded), Is.True,
                "and so does everything inside it");
        });
    }

    [Test]
    public async Task Paste_DropsEveryLink_SoTheDuplicateArrivesUnwired()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // Wire a product pin to a block pin inside ONE locality, so the pair is internal to what gets copied.
        ElementId localityId = Root(vm).Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(localityId, product.ProductIdentifier);
        await harness.Session.AddEmptyFunctionBlockAsync(localityId);
        ProjectElement block = harness.Session.Current!.FindById(localityId)!
            .ChildrenOrEmpty().First(c => c.Tag == "functionblock");
        ElementId inPin = (await harness.Session.AddVariableAsync(
            block.FindChild("inputs")!.Id!.Value, "resource_input", "InA"))!.Value;
        // Re-read the room: every edit produces a new project, so a snapshot taken before them is stale.
        ElementId productPin = harness.Session.Current!.FindById(localityId)!
            .ChildrenOrEmpty().First(c => c.Tag != "functionblock")
            .Descendants().First(d => d.Tag == "dataline_input").Id!.Value;
        await harness.Session.LinkPinsAsync(productPin, inPin);
        int linksBefore = LinkHalves(harness.Session.Current!, localityId);
        Assert.That(linksBefore, Is.GreaterThan(0), "precondition: the room is wired to itself");

        vm.CopyCommand.Execute(Root(vm).Children[0]);
        await vm.PasteCommand.ExecuteAsync(Root(vm));

        ElementId copyId = harness.Session.Current!.Groups[^1].Id!.Value;
        Assert.That(LinkHalves(harness.Session.Current!, copyId), Is.Zero,
            "a pasted duplicate arrives unwired, even for pairs wholly inside the copy");
    }

    private static int LinkHalves(Project project, ElementId subtreeRoot) =>
        project.FindById(subtreeRoot)!.DescendantsAndSelf().Count(e => e.IsLinkHalf);

    [Test]
    public async Task CopyLocality_PastedOnAnotherLocality_IsRefused()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = Root(vm).Children.Count;

        vm.CopyCommand.Execute(Root(vm).Children[0]);
        await vm.PasteCommand.ExecuteAsync(Root(vm).Children[1]);

        Assert.That(Root(vm).Children, Has.Count.EqualTo(before), "a locality does not nest inside a locality");
    }

    [Test]
    public async Task Delete_OnTheLocalityRoot_DoesNothing()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = Root(vm).Children.Count;

        await vm.DeleteCommand.ExecuteAsync(Root(vm));

        Assert.Multiple(() =>
        {
            Assert.That(vm.InstallationNodes, Has.Count.EqualTo(1), "the root survives");
            Assert.That(Root(vm).Children, Has.Count.EqualTo(before), "and so do its localities");
            Assert.That(harness.Session.IsDirty, Is.False, "nothing was edited");
        });
    }
}
