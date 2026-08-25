using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Alignment F-15/F-17: <i>Kopier</i> on a PIN follows whether the pin is a
/// signal SOURCE — not whether it belongs to a product, and not its direction alone.
///
/// <para>Measured 2026-08-11 on one project holding both families in identical state (no addressing, no links),
/// each row read on BOTH surfaces — the row's context flyout and <c>Rediger ▸ Kopier</c> with the owning pane
/// really focused. The reference application agrees with itself on every row:</para>
/// <list type="table">
/// <item><term>product INPUT (<c>LK FUGA Tryk 2 tast ▸ Tryk (venstre)</c>)</term><description>offered</description></item>
/// <item><term>product OUTPUT (<c>Lampeudtag ▸ Udgang</c>)</term><description>withheld</description></item>
/// <item><term>block INPUT (<c>AND ▸ Input ▸ Indgang 1</c>)</term><description>withheld</description></item>
/// <item><term>block OUTPUT (<c>AND ▸ Output ▸ Udgang</c>)</term><description>offered</description></item>
/// </list>
///
/// <para>The two families run OPPOSITE ways, which is why neither "a product terminal is copyable" (the pre-F-15
/// rule) nor "an input pin is copyable" (F-15's rule, derived from products alone) survives contact with both.
/// What both obey is direction of VALUE FLOW: a product's input terminal and a block's output pin are the rows the
/// system READS, and those are the rows the vendor lets you copy; a product's output and a block's input are rows
/// the system WRITES, and it withholds Kopier on both.</para>
///
/// <para>Each row is asserted on both surfaces because the two are decided in different places — a failed gate
/// HIDES on the transient flyout but GREYS on the persistent bar — so a rule encoded in a surface policy alone
/// leaves the bar disagreeing with the flyout, which is what F-15's fix did on the product output.</para>
/// </summary>
public class PinCopyParityTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }

    private static Availability CopyOn(MainWindowViewModel vm, NodeContext node, Surface surface) =>
        CommandRegistry.For(vm.Registry.Rows.Single(r => r.Id == "edit.copy"),
                            vm.Context with { Node = node }, surface);

    /// <summary>A product's catalog-declared terminal: not the installer's to cut or copy in its own right, so it
    /// reaches the copy rule with <c>CanCopy:false</c> and only the direction differs.</summary>
    private static NodeContext ProductTerminal(bool isOutput) =>
        Pin(isProductTerminal: true, isOutput: isOutput, canCut: false);

    /// <summary>A function block's section pin. These are NOT catalog-declared in the projector, so unlike a
    /// product terminal they arrive with <c>CanCut</c>/<c>CanCopy</c> already true — the reason a rule written for
    /// products alone never described them.</summary>
    private static NodeContext BlockPin(bool isOutput) =>
        Pin(isProductTerminal: false, isOutput: isOutput, canCut: true);

    private static NodeContext Pin(bool isProductTerminal, bool isOutput, bool canCut) =>
        new(new ElementId(1, 1), TreeNodeKind.Pin,
            IsPin: true, IsProductTerminal: isProductTerminal, IsLinkRow: false, IsLinkTarget: true,
            IsLogMarkPin: false,
            IsOutputPin: isOutput, IsEventsContainer: false, IsCommandsContainer: false,
            IsConditionsContainer: false, IsCaseNode: false, IsLockedBlock: false,
            CanCut: canCut, CanCopy: canCut, CanReorder: false);

    private static void AssertOffered(MainWindowViewModel vm, NodeContext node, string because)
    {
        Assert.Multiple(() =>
        {
            Assert.That(CopyOn(vm, node, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                $"flyout: {because}");
            Assert.That(CopyOn(vm, node, Surface.MenuBar), Is.EqualTo(Availability.Allow),
                $"menu bar: {because}");
        });
    }

    private static void AssertWithheld(MainWindowViewModel vm, NodeContext node, string because)
    {
        Assert.Multiple(() =>
        {
            // The flyout OMITS what does not apply; the bar keeps the row and greys it (US-068 / US-044).
            Assert.That(CopyOn(vm, node, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                $"flyout: {because}");
            Assert.That(CopyOn(vm, node, Surface.MenuBar).Enabled, Is.False,
                $"menu bar: {because}");
            Assert.That(CopyOn(vm, node, Surface.MenuBar).Visible, Is.True,
                $"menu bar keeps the row and greys it rather than hiding it: {because}");
        });
    }

    [Test]
    public async Task ProductInputTerminal_OffersCopy()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        AssertOffered(vm, ProductTerminal(isOutput: false),
            "a product's input terminal is a value the system reads, and the vendor offers Kopier on it");
    }

    [Test]
    public async Task ProductOutputTerminal_WithholdsCopy()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        AssertWithheld(vm, ProductTerminal(isOutput: true),
            "the vendor withholds Kopier on a product's output terminal on BOTH surfaces — its flyout carries "
            + "none and its Rediger ▸ Kopier reads disabled");
    }

    [Test]
    public async Task BlockInputPin_WithholdsCopy()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        AssertWithheld(vm, BlockPin(isOutput: false),
            "a block's input pin is written by its links, and the vendor withholds Kopier on it");
    }

    [Test]
    public async Task BlockOutputPin_OffersCopy()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        AssertOffered(vm, BlockPin(isOutput: true),
            "a block's output pin is the value the block produces, and the vendor offers Kopier on it — the "
            + "opposite direction to a product terminal");
    }

    /// <summary>The rule is about the two families running OPPOSITE ways, so it is stated as a contrast rather
    /// than as four independent cells: a change that made every pin copyable, or none, would pass some of the
    /// cases above and must not pass this.</summary>
    [Test]
    public async Task TheTwoPinFamiliesRunOppositeWays()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;

        Assert.Multiple(() =>
        {
            Assert.That(CopyOn(vm, ProductTerminal(isOutput: false), Surface.ContextMenu),
                Is.Not.EqualTo(CopyOn(vm, ProductTerminal(isOutput: true), Surface.ContextMenu)),
                "a product terminal's two directions must not agree");
            Assert.That(CopyOn(vm, BlockPin(isOutput: false), Surface.ContextMenu),
                Is.Not.EqualTo(CopyOn(vm, BlockPin(isOutput: true), Surface.ContextMenu)),
                "a block pin's two directions must not agree");
            Assert.That(CopyOn(vm, ProductTerminal(isOutput: true), Surface.ContextMenu),
                Is.Not.EqualTo(CopyOn(vm, BlockPin(isOutput: true), Surface.ContextMenu)),
                "the two families' OUTPUT rows must not agree with each other");
        });
    }
}
