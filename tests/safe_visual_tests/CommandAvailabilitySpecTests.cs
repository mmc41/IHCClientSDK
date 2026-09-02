using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T012: the data-driven US-068 inventory / US-044 grey-matrix harness over the registry rows.
/// A ShellContext is FABRICATED per spec row (node kind × pane × lock × clipboard — real element ids where a
/// gate probes the SDK) and evaluated through the ONE CommandRegistry.For evaluator; visible/enabled is
/// asserted per surface. The D13 divergences are REQUIREMENTS (bar greys what the flyout offers), never
/// reconciled. T013–T015 extend this pattern to the remaining families.
/// </summary>
public class CommandAvailabilitySpecTests : AvaloniaTestBase
{
    private static NodeContext Node(
        ElementId? id, TreeNodeKind kind,
        bool isPin = false, bool isProductTerminal = false, bool isLockedBlock = false,
        bool canCut = false, bool canCopy = false, bool isLinkRow = false, bool isLogMarkPin = false,
        bool isOutputPin = false, bool isEventsContainer = false,
        bool isCommandsContainer = false, bool isConditionsContainer = false, bool isCaseNode = false) =>
        new(id, kind, isPin, isProductTerminal, isLinkRow, IsLinkTarget: isPin,
            isLogMarkPin, isOutputPin, isEventsContainer, isCommandsContainer, isConditionsContainer, isCaseNode,
            isLockedBlock, canCut, canCopy, CanReorder: canCut);

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId loc, ElementId lockedFb)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = harness.Session.Current!.Groups[0].Id!.Value;
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        ElementId lockedFb = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        return (harness, vm, loc, lockedFb);
    }

    private static CommandSpec Row(MainWindowViewModel vm, string id) => vm.Registry.Rows.Single(r => r.Id == id);

    private static Availability At(MainWindowViewModel vm, string id, ShellContext ctx, Surface surface) =>
        CommandRegistry.For(Row(vm, id), ctx, surface);

    /// <summary>
    /// uxparity2 T016 (D15) — on a LOCKED block, <c>view.showProgram</c> is ENABLED on the MENU BAR, not greyed.
    /// <para>
    /// Both surfaces were measured in both applications on BOTH fixtures (8/8 rows):
    /// the reference application enables Cut/Copy/Delete/Show program on the bar AND the flyout for a locked block.
    /// The rule is neither fixture- nor surface-dependent, so the stricter bar reading this row used to encode does
    /// not exist. The earlier contradicting measurement was reproduced on demand as an ARMING artifact — reading the
    /// bar with nothing armed reports everything greyed — which is why it survived as long as it did.
    /// </para>
    /// </summary>
    [Test]
    public async Task LockedBlock_ShowProgram_IsEnabledOnTheMenuBar_NotOnlyInTheFlyout()
    {
        var (harness, vm, _, lockedFb) = await BuildAsync();
        using var _1 = harness;
        ShellContext ctx = vm.Context with
        {
            Node = Node(lockedFb, TreeNodeKind.FunctionBlock, isLockedBlock: true, canCut: true, canCopy: true),
        };

        Availability bar = At(vm, "view.showProgram", ctx, Surface.MenuBar);
        Availability context = At(vm, "view.showProgram", ctx, Surface.ContextMenu);

        Assert.Multiple(() =>
        {
            Assert.That(context.Visible && context.Enabled, Is.True, "the flyout already offered it — unchanged");
            Assert.That(bar.Visible, Is.True, "the bar keeps the item visible");
            Assert.That(bar.Enabled, Is.True, "D15: the bar ENABLES Show program on a locked block");
            Assert.That(bar.Reason, Is.Null, "an enabled command carries no disabled-reason");
        });
    }

    // An UNLOCKED block must stay enabled on both surfaces — otherwise the test above could pass by enabling the
    // row unconditionally, which is a different bug with the same symptom on this one fixture.
    [Test]
    public async Task UnlockedBlock_ShowProgram_StaysEnabledOnBothSurfaces()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ElementId openFb = (await harness.Session.AddEmptyFunctionBlockAsync(loc))!.Value;
        ShellContext ctx = vm.Context with { Node = Node(openFb, TreeNodeKind.FunctionBlock, isLockedBlock: false) };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "view.showProgram", ctx, Surface.MenuBar).Enabled, Is.True);
            Assert.That(At(vm, "view.showProgram", ctx, Surface.ContextMenu).Enabled, Is.True);
        });
    }

    // Selecting something that is not a block (and owns no block) must still grey the bar row — the lock state is
    // not the only thing this gate decides, and relaxing it must not relax that.
    [Test]
    public async Task NonBlockSelection_ShowProgram_IsStillGreyedOnTheBar()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext ctx = vm.Context with { Node = Node(loc, TreeNodeKind.Locality) };

        Availability bar = At(vm, "view.showProgram", ctx, Surface.MenuBar);
        Assert.Multiple(() =>
        {
            Assert.That(bar.Enabled, Is.False, "a locality has no program to show");
            Assert.That(bar.Reason, Is.Not.Null, "…and the grey explains itself (QC-06)");
        });
    }

    /// <summary>
    /// uxparity2 T017 (D15) — REPLACES <c>LockedBlock_ContextOffers_BarGreys_CutDeleteShowProgram</c>, which asserted
    /// the menu bar GREYS Cut/Delete/Show program on a locked block. That was the S-28 reading, retired by V1: the
    /// reference application enables all four cells on the bar AND the flyout, on both fixtures (8/8 rows).
    /// `view.showProgram` moved to its own test under T016; Cut and Delete are
    /// corrected here, so the bar and the flyout now agree for every locked-block structural command.
    /// </summary>
    [Test]
    public async Task LockedBlock_BarAndFlyout_BothEnable_CutAndDelete()
    {
        var (harness, vm, _, lockedFb) = await BuildAsync();
        using var _1 = harness;
        ShellContext ctx = vm.Context with
        {
            Node = Node(lockedFb, TreeNodeKind.FunctionBlock, isLockedBlock: true, canCut: true, canCopy: true),
        };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.delete" })
            {
                Availability context = At(vm, id, ctx, Surface.ContextMenu);
                Availability bar = At(vm, id, ctx, Surface.MenuBar);
                Assert.That(context.Visible && context.Enabled, Is.True, $"{id}: the flyout offers it — unchanged");
                Assert.That(bar.Visible, Is.True, $"{id}: the bar keeps the item visible");
                Assert.That(bar.Enabled, Is.True, $"{id}: D15 — the bar ENABLES it on a locked block too");
                Assert.That(bar.Reason, Is.Null, $"{id}: an enabled command carries no disabled-reason");
            }
        });
    }

    // The guards T016 established: relaxing a gate must not become a blanket enable. Cut/Delete still depend on the
    // node actually supporting them, so a selection that cannot be cut is still greyed WITH a reason.
    [Test]
    public async Task UncuttableSelection_CutAndDelete_AreStillGreyedOnTheBar()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        // A node with no id and no CanCut — nothing the structural commands can act on.
        ShellContext ctx = vm.Context with { Node = Node(null, TreeNodeKind.Locality) };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.delete" })
            {
                Availability bar = At(vm, id, ctx, Surface.MenuBar);
                Assert.That(bar.Enabled, Is.False, $"{id}: nothing actionable is selected");
                Assert.That(bar.Reason, Is.Not.Null, $"{id}: the grey explains itself (QC-06)");
            }
        });
    }

    // An UNLOCKED block stays enabled on both surfaces — the lock state is no longer consulted at all, and this
    // pins that the change did not accidentally invert the condition.
    [Test]
    public async Task UnlockedBlock_CutAndDelete_StayEnabledOnBothSurfaces()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ElementId openFb = (await harness.Session.AddEmptyFunctionBlockAsync(loc))!.Value;
        ShellContext ctx = vm.Context with
        {
            Node = Node(openFb, TreeNodeKind.FunctionBlock, isLockedBlock: false, canCut: true, canCopy: true),
        };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.delete" })
            {
                Assert.That(At(vm, id, ctx, Surface.MenuBar).Enabled, Is.True, $"{id}: bar");
                Assert.That(At(vm, id, ctx, Surface.ContextMenu).Enabled, Is.True, $"{id}: flyout");
            }
        });
    }

    // uxparity2 S2-18: a function-block variable row offers Cut and Copy in the vendor flyout. Product terminals
    // remain copy-only, while neither pin family becomes reorderable.
    [Test]
    public async Task FunctionBlockVariablePin_FlyoutEnablesCutAndCopy_ButNotReorder()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext fbPin = vm.Context with
        {
            IsProgrammingMode = true,
            Node = Node(loc, TreeNodeKind.Pin, isPin: true, canCut: true, canCopy: true),
        };
        ShellContext productTerminal = vm.Context with
        {
            Node = Node(loc, TreeNodeKind.Pin, isPin: true, isProductTerminal: true),
        };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "edit.copy", fbPin, Surface.MenuBar), Is.EqualTo(Availability.Allow),
                "an FB pin copies from the bar");
            Assert.That(At(vm, "edit.copy", fbPin, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "S2-18: an FB variable pin's flyout offers Copy");
            Assert.That(At(vm, "edit.cut", fbPin, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "S2-18: an FB variable pin's flyout offers Cut");
            Assert.That(At(vm, "edit.copy", productTerminal, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "a product terminal's flyout offers Copy");
            Assert.That(At(vm, "edit.moveUp", fbPin, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "a variable pin is editable but not structurally reorderable");
        });
    }

    [Test]
    public async Task FunctionBlockVariablePin_StructuralEditing_IsProgrammingModeOnly()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        NodeContext pin = Node(loc, TreeNodeKind.Pin, isPin: true, canCut: true, canCopy: true);
        ShellContext configuration = vm.Context with { IsProgrammingMode = false, Node = pin };
        ShellContext programming = configuration with { IsProgrammingMode = true };
        ShellContext lockedProgramming = programming with { ProgrammingBlockLocked = true };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.copy", "edit.delete" })
            {
                Assert.That(At(vm, id, configuration, Surface.MenuBar).Enabled, Is.False,
                    $"{id}: the vendor greys structural pin editing in configuration mode");
                Assert.That(At(vm, id, configuration, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                    $"{id}: the vendor omits structural pin editing from the configuration flyout");
            }

            Assert.That(At(vm, "edit.cut", configuration, Surface.Toolbar).Enabled, Is.False);
            Assert.That(At(vm, "edit.copy", configuration, Surface.Toolbar).Enabled, Is.False);
            Assert.That(At(vm, "edit.cut", programming, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.copy", programming, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.delete", programming, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.cut", lockedProgramming, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "edit.copy", lockedProgramming, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "the vendor keeps Copy available in a locked block's read-only programming view");
            Assert.That(At(vm, "edit.delete", lockedProgramming, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
        });
    }

    [Test]
    public async Task ActiveProgrammingRoot_StructuralEditing_IsConfigurationModeOnly()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        NodeContext block = Node(loc, TreeNodeKind.FunctionBlock, isLockedBlock: true, canCut: true, canCopy: true);
        ShellContext configuration = vm.Context with { IsProgrammingMode = false, Node = block };
        ShellContext programming = configuration with { IsProgrammingMode = true, ProgrammingBlockLocked = true };
        ShellContext unlockedProgramming = programming with { ProgrammingBlockLocked = false };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.copy", "edit.delete" })
            {
                Assert.That(At(vm, id, configuration, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                    $"{id}: the block itself is structurally editable in configuration mode");
                Assert.That(At(vm, id, programming, Surface.MenuBar).Enabled, Is.False,
                    $"{id}: the active programming root is not a structural tree item");
                Assert.That(At(vm, id, programming, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            }
            Assert.That(At(vm, "edit.moveDown", configuration, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.moveDown", unlockedProgramming, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "the active programming root is a projection, not a reorderable structural item");
        });
    }

    [Test]
    public async Task ProgramTreeRoot_OffersBlockCommands_ButNotStructuralDeletion()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        NodeContext root = Node(loc, TreeNodeKind.ProgramBlockRoot, isLockedBlock: true);
        ShellContext locked = vm.Context with
        {
            IsProgrammingMode = true,
            ProgrammingBlockLocked = true,
            Node = root,
        };
        ShellContext unlocked = locked with
        {
            ProgrammingBlockLocked = false,
            Node = root with { IsLockedBlock = false },
        };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "node.saveBlock", locked, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.saveBlock", locked, Surface.MenuBar).Enabled, Is.False,
                "the locked root flyout is measurable, but saving the active locked block stays disabled on the bar");
            Assert.That(At(vm, "node.unlock", locked, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.delete", locked, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.saveBlock", unlocked, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.unlock", unlocked, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "edit.delete", unlocked, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
        });
    }

    // The Delete gate uses the cheap SDK classifier first, then asks the command on refusal so a US-044 grey carries
    // the engine's specific reason instead of a generic GUI literal. Two non-deletable shapes, two DIFFERENT reasons.
    [Test]
    public async Task Delete_BarGrey_CarriesTheSdkRefusalReason_PerRefusalKind()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        ProjectElement placed = harness.Session.Current!.FindById(loc)!.Children.First(c => c.Tag.StartsWith("product_", StringComparison.Ordinal));
        ElementId catalogPin = placed.Children.First(c => c.Tag == "dataline_input").Id!.Value;
        ElementId deletableProduct = placed.Id!.Value;

        Availability pinBar = At(vm, "edit.delete", vm.Context with { Node = Node(catalogPin, TreeNodeKind.Pin, isPin: true) }, Surface.MenuBar);
        Availability productBar = At(vm, "edit.delete",
            vm.Context with { Node = Node(deletableProduct, TreeNodeKind.Product, canCut: true) }, Surface.MenuBar);

        Assert.Multiple(() =>
        {
            Assert.That(pinBar.Enabled, Is.False, "a catalog-declared pin cannot be deleted on its own");
            Assert.That(pinBar.Reason, Does.Contain("katalogdefineret"),
                "the grey names the SDK's specific reason, not a generic 'cannot be deleted' — Danish since T015. "
                + "The sentence opens on the noun rather than on a quoted name (D5), because a template has to "
                + "start with a capital or a placeholder to pass the phrasing standard the SDK holds itself to");
            Assert.That(At(vm, "edit.delete", vm.Context with { Node = Node(catalogPin, TreeNodeKind.Pin, isPin: true) },
                Surface.ContextMenu), Is.EqualTo(Availability.Hidden), "…while the transient surface omits it (US-068)");
            Assert.That(productBar, Is.EqualTo(Availability.Allow), "the product that owns the pin still deletes");
        });
    }

    // US-068: Paste needs a non-empty clipboard AND a locality target; the flyout omits, the bar greys.
    [Test]
    public async Task Paste_RequiresClipboardAndLocalityTarget()
    {
        var (harness, vm, loc, lockedFb) = await BuildAsync();
        using var _1 = harness;
        ShellContext localityNoClipboard = vm.Context with { Node = Node(loc, TreeNodeKind.Locality, canCut: true, canCopy: true), Clipboard = null };
        ShellContext localityWithClipboard = localityNoClipboard with { Clipboard = new ClipboardContext(lockedFb, IsCut: false) };
        ShellContext blockWithClipboard = localityWithClipboard with
        {
            Node = Node(lockedFb, TreeNodeKind.FunctionBlock, canCut: true, canCopy: true),
        };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "edit.paste", localityNoClipboard, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "no Paste in the flyout while the clipboard is empty (US-068: 6 items empty, 7 full)");
            Assert.That(At(vm, "edit.paste", localityNoClipboard, Surface.MenuBar).Enabled, Is.False);
            Assert.That(At(vm, "edit.paste", localityWithClipboard, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "Paste appears on a locality once the clipboard holds a node");
            Assert.That(At(vm, "edit.paste", localityWithClipboard, Surface.MenuBar), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.paste", blockWithClipboard, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "Paste targets localities only");
        });
    }

    // US-044: with nothing selected every family command is bar-visible-but-greyed and flyout-omitted.
    [Test]
    public async Task NoSelection_BarGreysAll_ContextOmitsAll()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext ctx = vm.Context with { Node = null, Clipboard = null };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.copy", "edit.paste", "edit.delete", "view.showProgram" })
            {
                Availability bar = At(vm, id, ctx, Surface.MenuBar);
                Assert.That(bar.Visible && !bar.Enabled, Is.True, $"{id}: bar visible but greyed with no selection");
                Assert.That(At(vm, id, ctx, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                    $"{id}: flyout omits with no selection");
            }
        });
    }

    /// <summary>
    /// Alignment F-1 — Indsæt ▸ Lokalitet must be ENABLED with NO selection.
    /// Measured against the vendor 2026-08-09 on a fresh unnamed project: the bar item is enabled before anything
    /// is clicked, and invoking it inserts a locality named "Lokalitet" as the LAST child of the root (verified
    /// live, then undone). The execute path here is already selection-independent (InsertLocality inserts under
    /// the groups root), so only the gate withheld it. A room selection stays refused-with-reason: the vendor
    /// silently no-ops there (measured Code=NoEffect), and the registered "unavailable commands explain
    /// themselves" enhancement prefers an explained grey over a silent nothing.
    /// </summary>
    [Test]
    public async Task NoSelection_InsertLocality_IsEnabledOnTheBar()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext noSelection = vm.Context with { Node = null };

        Availability bar = At(vm, "insert.locality", noSelection, Surface.MenuBar);
        Assert.Multiple(() =>
        {
            Assert.That(bar.Visible, Is.True, "the bar always carries Indsæt ▸ Lokalitet");
            Assert.That(bar.Enabled, Is.True, "vendor: enabled with no selection — inserts at the root");
            Assert.That(bar.Reason, Is.Null, "an enabled command carries no disabled-reason");
        });
    }

    // crudarch T013: the remaining node-scoped rows — US-068 inventory per node kind, evaluated through the
    // same fabricated-context harness. Pin rows, block rows, root rows and the Properties divergence
    // (bar enabled on a link row, flyout omits it) in one sweep.
    [Test]
    public async Task NodeScopedRows_InventoryPerNodeKind()
    {
        var (harness, vm, loc, lockedFb) = await BuildAsync();
        using var _1 = harness;
        ShellContext root = vm.Context with { Node = Node(null, TreeNodeKind.LocalitiesRoot) };
        ShellContext locality = vm.Context with { Node = Node(loc, TreeNodeKind.Locality, canCut: true, canCopy: true), InstallationPaneActive = false };
        ShellContext pin = vm.Context with { Node = Node(loc, TreeNodeKind.Pin, isPin: true, isLogMarkPin: true) };
        ShellContext linkRow = vm.Context with { Node = Node(loc, TreeNodeKind.LinkFrom, isLinkRow: true) };
        ShellContext unlockedFb = vm.Context with { Node = Node(lockedFb, TreeNodeKind.FunctionBlock, canCut: true, canCopy: true) };
        ShellContext lockedFbCtx = unlockedFb with { Node = unlockedFb.Node! with { IsLockedBlock = true } };

        Assert.Multiple(() =>
        {
            // Localities root: Insert locality only; Properties has no target (US-068).
            Assert.That(At(vm, "insert.locality", root, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "insert.locality", locality, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.properties", root, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.properties", root, Surface.MenuBar).Enabled, Is.False);

            // A locality in the Functions pane takes the empty block; the Installation pane refuses it.
            Assert.That(At(vm, "insert.emptyFunctionBlock", locality, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "insert.emptyFunctionBlock", locality with { InstallationPaneActive = true }, Surface.ContextMenu),
                Is.EqualTo(Availability.Hidden));

            // Pin rows: link-from/use-in-program/link-to/log-mark; never Move up/down (US-068, D07).
            Assert.That(At(vm, "link.startFromHere", pin, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.useInProgram", pin, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "link.toHere", pin, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.toggleLogMark", pin, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.moveUp", pin, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "edit.moveDown", pin, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));

            // Function block: Save block on any block; Unlock only on a locked one.
            Assert.That(At(vm, "node.saveBlock", unlockedFb, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.unlock", unlockedFb, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.unlock", lockedFbCtx, Surface.ContextMenu), Is.EqualTo(Availability.Allow));

            // …and both ride the Bibliotek BAR too, where the same gate GREYS instead of hiding (measured against
            // the vendor 2026-08-04: Gem Funktionsblok is live on any block, Oplås only on a locked one).
            Assert.That(At(vm, "node.saveBlock", unlockedFb, Surface.MenuBar).Enabled, Is.True);
            Assert.That(At(vm, "node.saveBlock", root, Surface.MenuBar).Enabled, Is.False, "no block selected");
            Assert.That(At(vm, "node.unlock", lockedFbCtx, Surface.MenuBar).Enabled, Is.True);
            Assert.That(At(vm, "node.unlock", unlockedFb, Surface.MenuBar).Enabled, Is.False, "already unlocked");

            // Link row: jump-to-opposite offered; Properties DIVERGES — the flyout omits it on a link row while
            // the Edit menu stays enabled (measured; reproduced, not reconciled).
            Assert.That(At(vm, "link.jumpOpposite", linkRow, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "link.jumpOpposite", locality, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.properties", linkRow, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.properties", linkRow, Surface.MenuBar), Is.EqualTo(Availability.Allow));

            // Reorderable structural node: Move offered (down — this locality is FIRST among ten, so up is a
            // T018 boundary refusal); withdrawn under a locked programming block (F-087).
            Assert.That(At(vm, "edit.moveDown", locality, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.moveDown", locality with { ProgrammingBlockLocked = true }, Surface.ContextMenu),
                Is.EqualTo(Availability.Hidden));

            // Help is an always-available bar command (F1) — never node-gated.
            Assert.That(At(vm, "help.onNode", root, Surface.MenuBar), Is.EqualTo(Availability.Allow));
        });
    }

    // crudarch T014: the programming-mode rows — authoring commands appear on their container kinds and are
    // WITHDRAWN wholesale on a locked block (A-27/F-076: missing, not greyed, in the flyout; greyed in the bar);
    // Leave-programming-mode and the Ctrl+I/U pin inserts gate on the mode itself.
    [Test]
    public async Task ProgramRow_OffersDelete_WhenSdkClassifiesTopLevelProgramAsDeletable()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ElementId block = (await harness.Session.AddEmptyFunctionBlockAsync(loc))!.Value;
        ElementId program = harness.Session.Current!.FindById(block)!.Descendants()
            .Single(e => e.Tag == "program_simple").Id!.Value;
        ShellContext context = vm.Context with
        {
            IsProgrammingMode = true,
            ProgrammingBlockLocked = false,
            Node = Node(program, TreeNodeKind.Program),
        };

        Assert.That(At(vm, "edit.delete", context, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
            "the vendor Program-row flyout offers enabled Delete for the top-level program_simple node");
    }

    [Test]
    public async Task ProgrammingModeRows_ContainerKinds_And_LockedWithdrawal()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext prog = vm.Context with { IsProgrammingMode = true, ProgrammingBlockLocked = false };
        ShellContext lockedProg = prog with { ProgrammingBlockLocked = true };
        ShellContext programs = prog with { Node = Node(loc, TreeNodeKind.Programs) };
        ShellContext events = prog with { Node = Node(loc, TreeNodeKind.Events, isEventsContainer: true) };
        ShellContext commands = prog with { Node = Node(loc, TreeNodeKind.Commands, isCommandsContainer: true) };
        ShellContext conditions = prog with { Node = Node(loc, TreeNodeKind.Conditions, isConditionsContainer: true) };
        ShellContext caseNode = prog with { Node = Node(loc, TreeNodeKind.Case, isCaseNode: true) };
        ShellContext outputPin = prog with { Node = Node(loc, TreeNodeKind.Pin, isPin: true, isOutputPin: true) };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "program.addPowerEvent", events, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.addSubProgram", commands, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.addLogicGroup", conditions, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.setConditionsAnd", conditions, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.setConditionsOr", conditions, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.newCaseValue", caseNode, Surface.ContextMenu), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.toggleSaveValue", outputPin, Surface.ContextMenu), Is.EqualTo(Availability.Allow));

            // S2-16: the bar and Ctrl+G save the active programming block even when one of its child rows is
            // selected; the transient flyout remains row-specific and therefore does not add Save block here.
            Assert.That(At(vm, "node.saveBlock", programs, Surface.MenuBar), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "node.saveBlock", programs, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "node.saveBlock", lockedProg with { Node = programs.Node }, Surface.MenuBar).Enabled,
                Is.False, "a locked active block cannot be saved back to the library");

            // The wrong container refuses (flyout omits, bar greys).
            Assert.That(At(vm, "program.addPowerEvent", commands, Surface.ContextMenu), Is.EqualTo(Availability.Hidden));
            Assert.That(At(vm, "program.addSubProgram", events, Surface.MenuBar).Enabled, Is.False);

            // A LOCKED block withdraws every authoring row (A-27): flyout omits, bar greys with a reason.
            foreach (string id in new[] { "program.addPowerEvent", "program.addSubProgram", "program.addLogicGroup",
                                          "program.newCaseValue", "program.setConditionsAnd", "program.setConditionsOr" })
            {
                ShellContext lockedCtx = id switch
                {
                    "program.addPowerEvent" => lockedProg with { Node = events.Node },
                    "program.addSubProgram" => lockedProg with { Node = commands.Node },
                    "program.newCaseValue" => lockedProg with { Node = caseNode.Node },
                    _ => lockedProg with { Node = conditions.Node },
                };
                Assert.That(At(vm, id, lockedCtx, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                    $"{id}: withdrawn (not greyed) on a locked block's flyout");
            }

            // Leave-programming-mode: bar-enabled only while IN the mode.
            Assert.That(At(vm, "program.leaveMode", prog, Surface.MenuBar), Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "program.leaveMode", vm.Context with { IsProgrammingMode = false }, Surface.MenuBar).Enabled,
                Is.False);

            // Ctrl+I / Ctrl+U pin inserts: keybinding-only (no menu surface) — the GATE takes mode + unlocked.
            Assert.That(Row(vm, "program.insertInput").Gate(prog).Ok, Is.True);
            Assert.That(Row(vm, "program.insertInput").Gate(lockedProg).Ok, Is.False);
            Assert.That(Row(vm, "program.insertOutput").Gate(vm.Context with { IsProgrammingMode = false }).Ok, Is.False);
            Assert.That(At(vm, "program.insertInput", prog, Surface.MenuBar), Is.EqualTo(Availability.Hidden),
                "no menu surface — hidden everywhere, reachable by gesture only");
        });
    }

    // crudarch T015: the app-level rows — most gate on ProjectOpen or Allow; Save stays ALWAYS-ENABLED (D07,
    // vendor parity) even with no project open.
    [Test]
    public async Task AppLevelRows_GateOnProjectOpen_SaveAlwaysEnabled()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        // Alignment F-4: the two transfer rows are no longer project-or-always rows — they gate on the CONTROLLER
        // (measured: the reference application greys Hent/Send on a fresh project and on a saved one alike), so
        // they are held connected here and their connection gate is asserted separately below. Everything else
        // this test covers is untouched.
        ShellContext open = vm.Context with { ControllerConnected = true };
        ShellContext closed = open with { ProjectOpen = false };
        string[] projectGated = { "file.saveAs", "file.close", "project.info",
                                  "project.moduleMap", "controller.send",
                                  "reports.functions", "reports.installation", "reports.functionBlocks" };
        string[] alwaysOn = { "file.new", "file.open", "file.save", "app.exit", "view.toggleToolbar",
                              "view.toggleStatusBar", "view.toggleProblems", "controller.retrieve",
                              "catalog.importFile", "catalog.importFolder", "help.about", "app.settings",
                              "app.telemetryDiagnostics" };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "file.save", closed, Surface.MenuBar), Is.EqualTo(Availability.Allow),
                "Save stays always-enabled (D07 — vendor parity), even with no project");
            foreach (string id in alwaysOn)
            {
                Assert.That(At(vm, id, closed, Surface.MenuBar), Is.EqualTo(Availability.Allow), $"{id}: always available");
            }
            foreach (string id in projectGated)
            {
                Availability bar = At(vm, id, closed, Surface.MenuBar);
                Assert.That(bar.Visible && !bar.Enabled, Is.True, $"{id}: greys without a project");
                Assert.That(bar.Reason, Is.Not.Null, $"{id}: the grey explains itself");
                Assert.That(At(vm, id, open, Surface.MenuBar), Is.EqualTo(Availability.Allow), $"{id}: enabled with a project");
            }
            // …and the transfer pair's own gate, which the two arrays above deliberately hold constant.
            foreach (string id in new[] { "controller.send", "controller.retrieve" })
            {
                Availability off = At(vm, id, open with { ControllerConnected = false }, Surface.MenuBar);
                Assert.That(off.Visible && !off.Enabled, Is.True, $"{id}: greys without a controller");
                Assert.That(off.Reason, Is.Not.Null, $"{id}: the grey explains itself");
            }
        });
    }

    // crudarch T018 (G6): Move up/down gate on TRUE reorderability — the ReorderNode factory returns null at
    // the list ends and document.CanApply confirms the middle — so the ends refuse (flyout omits, bar/keys
    // stop firing no-ops) while middles stay offered.
    [Test]
    public async Task MoveRows_BoundaryGating_EndsRefuse_MiddleAllows()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        var groups = harness.Session.Current!.Groups;
        ShellContext Ctx(ElementId id) => vm.Context with { Node = Node(id, TreeNodeKind.Locality, canCut: true) };
        ShellContext first = Ctx(groups[0].Id!.Value);
        ShellContext middle = Ctx(groups[1].Id!.Value);
        ShellContext last = Ctx(groups[^1].Id!.Value);

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "edit.moveUp", first, Surface.ContextMenu),
                Is.EqualTo(Availability.Hidden), "the first sibling cannot move up — the flyout omits");
            Assert.That(At(vm, "edit.moveDown", first, Surface.ContextMenu),
                Is.EqualTo(Availability.Allow), "…but it can move down");
            Assert.That(At(vm, "edit.moveUp", middle, Surface.ContextMenu),
                Is.EqualTo(Availability.Allow), "a middle sibling moves both ways");
            Assert.That(At(vm, "edit.moveDown", middle, Surface.ContextMenu),
                Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.moveUp", last, Surface.ContextMenu),
                Is.EqualTo(Availability.Allow), "the last sibling can move up");
            Assert.That(At(vm, "edit.moveDown", last, Surface.ContextMenu),
                Is.EqualTo(Availability.Hidden), "…but not down");
        });
    }

    // crudarch T020 (G5): the toolbar is a PERSISTENT surface — its five real buttons (Retrieve/Send/Cut/
    // Copy/Paste) grey with a reason and never hide; no Undo/Redo buttons exist (D07).
    [Test]
    public async Task ToolbarRows_PersistentSurface_GreyWithReason_NeverHide()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext noSelection = vm.Context with { Node = null, Clipboard = null };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.copy", "edit.paste" })
            {
                Availability tb = At(vm, id, noSelection, Surface.Toolbar);
                Assert.That(tb.Visible, Is.True, $"{id}: a toolbar button never hides");
                Assert.That(tb.Enabled, Is.False, $"{id}: greyed with nothing to act on");
                Assert.That(tb.Reason, Is.Not.Null, $"{id}: the grey explains itself");
            }
            // Alignment F-4: both transfer commands also need a CONNECTION, so the enabled case is measured on a
            // connected context. What this test asserts about the toolbar is unchanged — a toolbar button greys,
            // it never hides — and it now says so about the connection as well as the document.
            ShellContext connected = vm.Context with { ControllerConnected = true };
            Assert.That(At(vm, "controller.send", connected, Surface.Toolbar), Is.EqualTo(Availability.Allow),
                "Send is toolbar-placed and enabled with a project open and a controller connected");
            Assert.That(At(vm, "controller.send", connected with { ProjectOpen = false }, Surface.Toolbar).Visible,
                Is.True, "…and greys rather than hides without one");
            Assert.That(At(vm, "controller.retrieve", connected, Surface.Toolbar), Is.EqualTo(Availability.Allow),
                "Retrieve is toolbar-placed");
            foreach (string id in new[] { "controller.send", "controller.retrieve" })
            {
                Availability off = At(vm, id, vm.Context with { ControllerConnected = false }, Surface.Toolbar);
                Assert.That(off.Visible, Is.True, $"{id}: still on the bar with no controller");
                Assert.That(off.Enabled, Is.False, $"{id}: greyed with no controller, as the vendor greys it");
                Assert.That(off.Reason, Is.Not.Null, $"{id}: the grey explains itself");
            }
            Assert.That(vm.Registry.Rows, Has.None.Matches<CommandSpec>(r =>
                    r.Id is "edit.undo" or "edit.redo" && r.Placement.Contains(Surface.Toolbar)),
                "NO Undo/Redo toolbar buttons (D07)");
        });
    }

    // crudarch T017 (US-052/U-BP-07): Undo/Redo rows gate on the document's CanUndo/CanRedo — greyed with a
    // reason when the history is empty, action-named headers preserved, redo cleared by a new edit.
    [Test]
    public async Task UndoRedoRows_GateOnHistory_KeepActionNamedHeaders()
    {
        var (harness, vm, _, _) = await BuildAsync();
        using var _1 = harness;

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "edit.undo", vm.Context with { CanUndo = false }, Surface.MenuBar).Enabled, Is.False,
                "empty history greys Undo in the bar");
            Assert.That(At(vm, "edit.undo", vm.Context with { CanUndo = true }, Surface.MenuBar),
                Is.EqualTo(Availability.Allow));
            Assert.That(At(vm, "edit.redo", vm.Context with { CanRedo = false }, Surface.MenuBar).Enabled, Is.False);
            Assert.That(At(vm, "edit.redo", vm.Context with { CanRedo = true }, Surface.MenuBar),
                Is.EqualTo(Availability.Allow));
        });

        // Live flow on a PRISTINE harness (BuildAsync's block insert already armed the shared one's history):
        // fresh project -> Undo greyed; an edit arms it with the action-named header; undo arms Redo; a NEW
        // edit clears the redo history again (US-052 two-stack semantics through the rows).
        using var live = ShellHarness.Create();
        var liveVm = live.CreateViewModel();
        await liveVm.InitializeAsync();
        Assert.That(liveVm.Registry.Bar["edit.undo"].Enabled, Is.False, "fresh project: nothing to undo");
        await live.Session.AddLocalityAsync();
        Assert.Multiple(() =>
        {
            Assert.That(liveVm.Registry.Bar["edit.undo"].Enabled, Is.True, "an edit arms Undo");
            Assert.That(liveVm.UndoMenuHeader, Does.StartWith("_Fortryd ").And.Not.EqualTo("_Fortryd"),
                "the header names the action (E14)");
        });
        await live.Session.UndoAsync();
        Assert.That(liveVm.Registry.Bar["edit.redo"].Enabled, Is.True, "an undo arms Redo");
        await live.Session.AddLocalityAsync();
        Assert.Multiple(() =>
        {
            Assert.That(liveVm.Registry.Bar["edit.redo"].Enabled, Is.False, "a new edit clears the redo history");
            Assert.That(liveVm.Registry.Bar["edit.redo"].Reason, Is.Not.Null, "the grey explains itself");
        });
    }

    // D13 "and they really run": on a locked block the flyout's Cut and Delete are not decoration — the
    // registry-materialized commands execute (Cut stashes the clipboard; Delete removes after the confirm).
    [Test]
    public async Task LockedBlock_ContextCutAndDelete_ReallyRun()
    {
        var (harness, vm, _, lockedFb) = await BuildAsync();
        using var _1 = harness;
        var blockNode = TreeNodes.FindById(vm.FunctionNodes, lockedFb)!;

        vm.CutCommand.Execute(blockNode);
        Assert.That(vm.Context.Clipboard, Is.EqualTo(new ClipboardContext(lockedFb, IsCut: true)),
            "Cut on a locked block really runs (fills the clipboard)");

        harness.Dialogs.ConfirmResult = true;   // the cascade confirm
        await ((CommunityToolkit.Mvvm.Input.IAsyncRelayCommand)vm.DeleteCommand).ExecuteAsync(blockNode);
        Assert.That(harness.Session.Current!.FindById(lockedFb), Is.Null,
            "Delete on a locked block really runs (the block is gone)");
    }
}
