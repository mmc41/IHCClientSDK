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

    // D13 core (uxparity S-28): a LOCKED library block — the context menu OFFERS Cut/Delete/Show program,
    // the menu bar GREYS all three with a reason.
    [Test]
    public async Task LockedBlock_ContextOffers_BarGreys_CutDeleteShowProgram()
    {
        var (harness, vm, _, lockedFb) = await BuildAsync();
        using var _1 = harness;
        ShellContext ctx = vm.Context with
        {
            Node = Node(lockedFb, TreeNodeKind.FunctionBlock, isLockedBlock: true, canCut: true, canCopy: true),
        };

        Assert.Multiple(() =>
        {
            foreach (string id in new[] { "edit.cut", "edit.delete", "view.showProgram" })
            {
                Availability context = At(vm, id, ctx, Surface.ContextMenu);
                Availability bar = At(vm, id, ctx, Surface.MenuBar);
                Assert.That(context.Visible && context.Enabled, Is.True, $"{id}: the flyout offers it on a locked block");
                Assert.That(bar.Visible, Is.True, $"{id}: the bar keeps the item visible");
                Assert.That(bar.Enabled, Is.False, $"{id}: the bar greys it on a locked block (US-044)");
                Assert.That(bar.Reason, Is.Not.Null, $"{id}: the grey explains itself (QC-06)");
            }
        });
    }

    // D13: Copy is bar-enabled on ANY pin, but the flyout offers it only on a PRODUCT terminal (an FB pin's
    // context menu has no Copy) — measured vendor behaviour, encoded as SurfacePolicy data.
    [Test]
    public async Task Pins_CopyBarEnabledOnAnyPin_ContextOnlyOnProductTerminal()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext fbPin = vm.Context with { Node = Node(loc, TreeNodeKind.Pin, isPin: true) };
        ShellContext productTerminal = vm.Context with
        {
            Node = Node(loc, TreeNodeKind.Pin, isPin: true, isProductTerminal: true),
        };

        Assert.Multiple(() =>
        {
            Assert.That(At(vm, "edit.copy", fbPin, Surface.MenuBar), Is.EqualTo(Availability.Allow),
                "an FB pin copies from the bar");
            Assert.That(At(vm, "edit.copy", fbPin, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "an FB pin's flyout has no Copy");
            Assert.That(At(vm, "edit.copy", productTerminal, Surface.ContextMenu), Is.EqualTo(Availability.Allow),
                "a product terminal's flyout offers Copy");
            Assert.That(At(vm, "edit.cut", fbPin, Surface.ContextMenu), Is.EqualTo(Availability.Hidden),
                "a pin is never cuttable — the flyout omits");
            Assert.That(At(vm, "edit.cut", fbPin, Surface.MenuBar).Enabled, Is.False,
                "…and the bar greys Cut with a reason");
        });
    }

    // review F05: the Delete gate asks the SDK's delete COMMAND, not a boolean — so a US-044 grey carries the
    // reason the engine already computed (which pin, which rule) instead of a generic literal the GUI would have
    // to keep in step with the SDK by hand. Two non-deletable shapes, two DIFFERENT reasons.
    [Test]
    public async Task Delete_BarGrey_CarriesTheSdkRefusalReason_PerRefusalKind()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        ProjectElement placed = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().First(c => c.Tag.StartsWith("product_"));
        ElementId catalogPin = placed.ChildrenOrEmpty().First(c => c.Tag == "dataline_input").Id!.Value;
        ElementId deletableProduct = placed.Id!.Value;

        Availability pinBar = At(vm, "edit.delete", vm.Context with { Node = Node(catalogPin, TreeNodeKind.Pin, isPin: true) }, Surface.MenuBar);
        Availability productBar = At(vm, "edit.delete",
            vm.Context with { Node = Node(deletableProduct, TreeNodeKind.Product, canCut: true) }, Surface.MenuBar);

        Assert.Multiple(() =>
        {
            Assert.That(pinBar.Enabled, Is.False, "a catalog-declared pin cannot be deleted on its own");
            Assert.That(pinBar.Reason, Does.Contain("catalog-declared pin"),
                "the grey names the SDK's specific reason, not a generic 'cannot be deleted'");
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
    public async Task ProgrammingModeRows_ContainerKinds_And_LockedWithdrawal()
    {
        var (harness, vm, loc, _) = await BuildAsync();
        using var _1 = harness;
        ShellContext prog = vm.Context with { IsProgrammingMode = true, ProgrammingBlockLocked = false };
        ShellContext lockedProg = prog with { ProgrammingBlockLocked = true };
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
        ShellContext open = vm.Context;
        ShellContext closed = vm.Context with { ProjectOpen = false };
        string[] projectGated = { "file.saveAs", "file.close", "project.info", "project.dataTables",
                                  "project.moduleMap", "controller.send",
                                  "reports.functions", "reports.installation", "reports.functionBlocks" };
        string[] alwaysOn = { "file.new", "file.open", "file.save", "app.exit", "view.toggleToolbar",
                              "view.toggleStatusBar", "controller.retrieve", "catalog.importFile",
                              "catalog.importFolder", "help.about", "app.settings", "app.telemetryDiagnostics" };

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
            Assert.That(At(vm, "controller.send", vm.Context, Surface.Toolbar), Is.EqualTo(Availability.Allow),
                "Send is toolbar-placed and enabled with a project open");
            Assert.That(At(vm, "controller.send", vm.Context with { ProjectOpen = false }, Surface.Toolbar).Visible,
                Is.True, "…and greys rather than hides without one");
            Assert.That(At(vm, "controller.retrieve", vm.Context, Surface.Toolbar), Is.EqualTo(Availability.Allow),
                "Retrieve is toolbar-placed");
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
            Assert.That(liveVm.UndoMenuHeader, Does.StartWith("_Undo ").And.Not.EqualTo("_Undo"),
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
