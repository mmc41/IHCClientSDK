using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-19 — <b>reveal after insert</b>: what the tree opens when a component lands.
///
/// <para>Measured live 2026-08-11 by the procedure the finding itself prescribed, since its first reading had been
/// polluted by ancestor-expanding selects: fresh project on each side, insert one library block
/// (<c>4.1.01. AND ("Og"- blok)</c>) into an untouched locality, and read the expansion flags <b>before touching
/// anything else</b>.</para>
///
/// <code>
///   original    Entré expanded, the block expanded, Input expanded, Output expanded
///   OpenVisual  Entré expanded, the block COLLAPSED
/// </code>
///
/// <para>So the original opens the placed block all the way down to its pins, and OpenVisual opened only the
/// locality. The installer's next action is almost always to wire one of those pins, and the reveal is what puts
/// them on screen — the same reveal OpenVisual already performed for a placed PRODUCT (US-010), simply never wired
/// on the block path.</para>
///
/// <para>Every other locality stays collapsed on both sides; a reveal that opened the whole tree would be a
/// different defect, so that is asserted too.</para>
/// </summary>
public class InsertRevealParityTests
{
    private const string AndBlockMasterType = "4.1.01. AND (\"Og\"- blok)";

    [Test]
    public async Task InsertingABlock_OpensItAndItsSections_AsTheOriginalDoes()
    {
        var (harness, vm, block) = await InsertBlockAsync();
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(block.IsExpanded, Is.True,
                "the original opens the placed block, showing the sections it brought");
            Assert.That(block.Children.Select(c => c.DisplayName), Is.EquivalentTo(new[] { "Input", "Output" }),
                "…which are the two sections this template declares");
            foreach (TreeNodeViewModel section in block.Children)
            {
                Assert.That(section.IsExpanded, Is.True,
                    $"the original opens '{section.DisplayName}' too — the pins are what the installer wires next");
            }
        });
    }

    /// <summary>The locality opens as well, which OpenVisual already did — pinned so the fix above is not mistaken
    /// for the whole rule, and so a regression in either half is attributable.</summary>
    [Test]
    public async Task InsertingABlock_OpensTheHostingLocality()
    {
        var (harness, vm, _) = await InsertBlockAsync();
        using var harnessScope = harness;

        TreeNodeViewModel locality = vm.FunctionNodes[0].Children.Single(c => c.DisplayName == "Entré");
        Assert.That(locality.IsExpanded, Is.True);
    }

    /// <summary>…and nothing ELSE opens. A reveal implemented as "expand everything" would satisfy the assertions
    /// above while burying the installer in an open project, which is not what the original does: every untouched
    /// locality stayed collapsed there.</summary>
    [Test]
    public async Task InsertingABlock_LeavesEveryOtherLocalityAlone()
    {
        var (harness, vm, _) = await InsertBlockAsync();
        using var _1 = harness;

        IEnumerable<TreeNodeViewModel> others =
            vm.FunctionNodes[0].Children.Where(c => c.DisplayName != "Entré");

        Assert.That(others.Where(o => o.IsExpanded).Select(o => o.DisplayName), Is.Empty,
            "only the locality that received the block opens");
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, TreeNodeViewModel block)> InsertBlockAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        // Through the real insert path — the menu leaf's own command — so the reveal under test is the one the
        // application performs, not one a test helper arranges.
        TreeNodeViewModel locality = vm.FunctionNodes[0].Children.Single(c => c.DisplayName == "Entré");
        vm.SelectNode(locality);
        ProductMenuItemViewModel leaf = Leaves(vm.FunctionBlocksMenu)
            .First(l => l.Header == AndBlockMasterType);
        await ((IAsyncRelayCommand)leaf.Command!).ExecuteAsync(null);

        TreeNodeViewModel block = locality.Children.Single();
        return (harness, vm, block);
    }

    private static IEnumerable<ProductMenuItemViewModel> Leaves(IEnumerable<ProductMenuItemViewModel> forest)
    {
        foreach (ProductMenuItemViewModel item in forest)
        {
            if (item.Command is not null) yield return item;
            foreach (ProductMenuItemViewModel nested in Leaves(item.Children)) yield return nested;
        }
    }
}
