using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Session;

namespace Ihc.Vis.Tests;

/// <summary>
/// US-089 — <i>Vis ▸ Links ▸ Etiketter / Pile</i>. The alternative rendering of a wire: instead of a child row
/// under each pin naming the opposite end, the wire is drawn as one directed arrow between the two pin ROWS.
///
/// <para>These are the projection-side facts, which is where the mode is actually decided: a wire is either a row
/// or an arrow, never both, and the arrow plan is what the overlay draws. The drawing itself (geometry, panes,
/// clipping) is <c>safe_visual_tests/LinkArrowOverlayTests</c> — it needs realized controls to have coordinates.</para>
/// </summary>
public class LinkArrowModeTests
{
    /// <summary>The fixture the wire is made on: a product with a data-line input in the <i>Installation</i> pane
    /// and a function block with an input in <i>Funktioner</i> — placed, but NOT yet wired, because two of these
    /// tests have to do something (switch the mode, open the block) between placing and wiring.</summary>
    private sealed record Placed(ShellHarness Harness, ihc_openvisual.ViewModels.MainWindowViewModel Vm,
        ElementId ProductPin, ElementId BlockPin, ElementId Block);

    private static async Task<Placed> PlacedAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ProductDefinition product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal)
                        && p.Resources.Any(r => r.Tag == "dataline_input"));
        FunctionBlockDefinition block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        ElementId blockId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        TreeNodeViewModel productPin = vm.InstallationNodes[0].Children[0].Children[0].Children
            .First(c => c.NodeKind == "pin:dataline_input");
        TreeNodeViewModel blockPin = vm.FunctionNodes[0].Children[0].Children[0].Children
            .First(s => s.NodeKind == "section:inputs").Children.First(p => p.IsPin);
        return new Placed(harness, vm, productPin.ElementId!.Value, blockPin.ElementId!.Value, blockId);
    }

    /// <summary>The same fixture with the wire already made — the corpus's ordinary product→block wire.</summary>
    private static async Task<Placed> WiredAsync()
    {
        Placed placed = await PlacedAsync();
        await placed.Harness.Session.LinkPinsAsync(placed.ProductPin, placed.BlockPin);
        return placed;
    }

    /// <summary>The wired fixture already switched to <i>Pile</i> — where every selection test starts.</summary>
    private static async Task<Placed> ArrowsShownAsync()
    {
        Placed placed = await WiredAsync();
        placed.Vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        return placed;
    }

    /// <summary>…and with its one wire selected, which is the state <c>Delete</c> acts on.</summary>
    private static async Task<Placed> SelectedWireAsync()
    {
        Placed placed = await ArrowsShownAsync();
        placed.Vm.SelectLinkArrowAt([placed.Vm.LinkArrows[0]]);
        return placed;
    }

    private static IEnumerable<TreeNodeViewModel> AllRows(ihc_openvisual.ViewModels.MainWindowViewModel vm) =>
        TreeNodes.FindAll(vm.InstallationNodes, _ => true).Concat(TreeNodes.FindAll(vm.FunctionNodes, _ => true));

    // ── The mode itself ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Labels is the default: nothing about an existing install changes until the mode is chosen.</summary>
    [Test]
    public async Task TheDefaultIsLabels_AndBothHalvesKeepTheirRows()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentLinkDisplay, Is.EqualTo(LinkDisplay.Labels), "the mode a fresh shell starts in");
            Assert.That(AllRows(vm).Count(n => n.IsLinkRow), Is.EqualTo(2),
                "the wire is two reciprocal link rows, one under each pin (US-022)");
            Assert.That(vm.LinkArrows, Is.Empty, "and nothing is drawn between them");
        });
    }

    /// <summary>Choosing <i>Pile</i> takes the rows away and puts ONE arrow in their place — not two, because a
    /// wire is one thing however many halves the file writes it in.</summary>
    [Test]
    public async Task ChoosingArrows_ReplacesBothLinkRowsWithOneArrow()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);

        Assert.Multiple(() =>
        {
            Assert.That(AllRows(vm).Count(n => n.IsLinkRow), Is.Zero, "no link row survives in either pane");
            Assert.That(vm.LinkArrows, Has.Count.EqualTo(1), "one wire, one arrow");
        });
        LinkArrowEdge edge = vm.LinkArrows[0];
        Assert.Multiple(() =>
        {
            // Orientation is the file's, not the tree's: the PRODUCER owns the from-half (US-022's "which half is
            // which"), so a product input drives the block input and the arrow points that way.
            Assert.That(edge.From.Element, Is.EqualTo(placed.ProductPin), "the arrow leaves the producer");
            Assert.That(edge.To.Element, Is.EqualTo(placed.BlockPin), "and arrives at the consumer");
            Assert.That(edge.From.Pane, Is.EqualTo(LinkArrowPane.Installation));
            Assert.That(edge.To.Pane, Is.EqualTo(LinkArrowPane.Functions));
            Assert.That(edge.Kind, Is.EqualTo(LinkArrowKind.Follow));
        });
    }

    /// <summary>Each endpoint carries the ancestor chain that leads to its row, so the overlay can anchor on the
    /// nearest ancestor that IS realized when the row's own subtree is collapsed.</summary>
    [Test]
    public async Task AnEndpointCarriesItsAncestorChain_OutermostFirst()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ElementId product = vm.InstallationNodes[0].Children[0].Children[0].ElementId!.Value;

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);

        IReadOnlyList<ElementId> path = vm.LinkArrows[0].From.Path;
        Assert.Multiple(() =>
        {
            Assert.That(path[^1], Is.EqualTo(placed.ProductPin), "the chain ends at the row the arrow anchors on");
            Assert.That(path, Does.Contain(product));
            Assert.That(path, Does.Contain(locality));
            Assert.That(path.ToList().IndexOf(locality), Is.LessThan(path.ToList().IndexOf(product)),
                "outermost first, so a collapsed ancestor is found by walking the chain backwards");
        });
    }

    /// <summary>And back: the mode is a view, so returning to <i>Etiketter</i> restores exactly what was there.</summary>
    [Test]
    public async Task ChoosingLabelsAgain_RestoresTheRows()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Labels);

        Assert.Multiple(() =>
        {
            Assert.That(AllRows(vm).Count(n => n.IsLinkRow), Is.EqualTo(2));
            Assert.That(vm.LinkArrows, Is.Empty);
        });
    }

    /// <summary>A later edit keeps the mode: the reconciler re-projects through the same decision, so a wire added
    /// while <i>Pile</i> is chosen arrives as an arrow rather than as a row nobody asked for.</summary>
    [Test]
    public async Task AWireCreatedWhileArrowsIsChosen_ArrivesAsAnArrow()
    {
        Placed placed = await PlacedAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        await placed.Harness.Session.LinkPinsAsync(placed.ProductPin, placed.BlockPin);

        Assert.Multiple(() =>
        {
            Assert.That(AllRows(vm).Count(n => n.IsLinkRow), Is.Zero, "the new wire is not a row");
            Assert.That(vm.LinkArrows, Has.Count.EqualTo(1), "it is an arrow");
        });
    }

    // ── Where the mode does NOT apply ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Programming mode keeps its labels whatever the menu says. Both panes there show ONE block — its variables
    /// and its program — so a wire's opposite end (a product pin) is on neither, and an arrow would point at
    /// nothing. A row that names the far end is the only rendering that still carries the fact.
    /// </summary>
    [Test]
    public async Task ProgrammingMode_KeepsLinkRows_BecauseTheFarEndIsOnNeitherPane()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;
        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);

        vm.EnterProgrammingModeCommand.Execute(TreeNodes.FindById(vm.FunctionNodes, placed.Block));

        Assert.Multiple(() =>
        {
            Assert.That(vm.IsProgrammingMode, Is.True, "precondition: the block is open");
            Assert.That(AllRows(vm).Count(n => n.IsLinkRow), Is.EqualTo(1),
                "the block's own input still shows the wire it is on, as a row");
            Assert.That(vm.LinkArrows, Is.Empty, "and nothing is drawn over a view that holds only one end");
        });
    }

    /// <summary>
    /// The rule that keeps the two renderings from BOTH losing a wire: a half is only taken away when the arrow
    /// that replaces it has a row at each end. A wire into an element the tree does not draw keeps its label.
    /// </summary>
    [Test]
    public async Task AWireWhoseFarEndHasNoRow_KeepsItsLabel()
    {
        Placed placed = await WiredAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;
        Project project = placed.Harness.Session.Current!;
        ProjectElement half = project.FindById(placed.ProductPin)!.Children.First(c => c.IsLinkHalf);

        Assert.That(LinkArrowPlanner.DrawsAsArrow(project, half), Is.True,
            "precondition: this wire's far end IS drawn, so it becomes an arrow");

        // The armed control: the same predicate over a half whose `link` resolves to nothing at all.
        ProjectElement dangling = Tree.Node(half.Tag, half.Id!.Value.ToToken(), [("link", "_0xdead")]);
        Assert.That(LinkArrowPlanner.DrawsAsArrow(project, dangling), Is.False,
            "a half pointing at nothing cannot become an arrow, so its label has to stay");
    }

    // ── Scene links ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A scenario link (US-024) is a wire too: the block's scene output drives the product's
    /// <i>Scenarier</i> container, so the arrow runs from the block pin to that container.</summary>
    [Test]
    public async Task AScenarioLink_DrawsFromTheBlockOutputToTheProductsScenesContainer()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        ProductDefinition product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Body.Children.Any(c => c.Tag == "scenes"));
        FunctionBlockDefinition block = harness.ProjectService.GetAvailableFunctionBlocks()
            .First(f => f.Outputs.Any(o => o.Tag == "resource_scene"));
        ElementId pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        ElementId fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        ProjectElement scenes = harness.Session.Current!.FindById(pid)!.Children.First(c => c.Tag == "scenes");
        ElementId scenesId = scenes.Id!.Value;
        ElementId sceneOutId = harness.Session.Current!.FindById(fbId)!.FindChild("outputs")!.Children
            .First(c => c.Tag == "resource_scene").Id!.Value;
        bool isDimmer = ElementId.TryParse(scenes.GetAttribute("scene_resource"), out ElementId target)
                        && harness.Session.Current!.FindById(target)?.Tag?.Contains("dimmer", StringComparison.Ordinal) == true;
        await harness.Session.LinkSceneAsync(sceneOutId, scenesId,
            isDimmer ? new SceneValueResult(true, 50, 0, 1) : new SceneValueResult(true, 0, 0, 0), isDimmer);

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);

        LinkArrowEdge edge = vm.LinkArrows.Single();
        Assert.Multiple(() =>
        {
            Assert.That(edge.Kind, Is.EqualTo(LinkArrowKind.Scene), "a scenario link is its own family of wire");
            Assert.That(edge.From.Element, Is.EqualTo(sceneOutId), "the block's scene output drives it");
            Assert.That(edge.To.Element, Is.EqualTo(scenesId), "onto the product's Scenarier container");
            Assert.That(edge.From.Pane, Is.EqualTo(LinkArrowPane.Functions));
            Assert.That(edge.To.Pane, Is.EqualTo(LinkArrowPane.Installation));
            // The scene MEMBER row under the container is not a link half and keeps its row: it carries the
            // stored value ("= 50% / 1s"), which no arrow can render.
            Assert.That(TreeNodes.FindAll(vm.InstallationNodes, n => n.Kind == TreeNodeKind.SceneMember).Count(),
                Is.EqualTo(1), "the scene membership itself is still listed, with its value");
        });
    }

    // ── The two renderings agree ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The completeness invariant, over a real corpus project rather than a two-element fixture: every link row
    /// that <i>Pile</i> takes away is accounted for by an arrow, and every arrow replaced at least one row. A
    /// wire that fell between the two would be silently unrenderable in arrows mode — the one failure a
    /// per-fixture test cannot see.
    /// </summary>
    [Test]
    public async Task OverTheCorpus_EveryRowArrowsModeTakesAway_IsCoveredByAnArrow()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.OpenAsync(TestData.PathOf("projects", "Project1-SimpelWired.vis"));

        var labelled = AllRows(vm).Where(n => n.IsLinkRow).Select(n => n.ElementId!.Value).ToHashSet();
        Assert.That(labelled, Is.Not.Empty, "precondition: the corpus project is wired");

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        var remaining = AllRows(vm).Where(n => n.IsLinkRow).Select(n => n.ElementId!.Value).ToHashSet();
        Project project = harness.Session.Current!;
        var takenAway = labelled.Except(remaining).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(takenAway, Is.Not.Empty, "arrows mode did take rows away");
            foreach (ElementId id in takenAway)
            {
                Assert.That(LinkArrowPlanner.DrawsAsArrow(project, project.FindById(id)!), Is.True,
                    $"the half {id.Value} lost its row, so an arrow must stand for it");
            }
            foreach (ElementId id in remaining)
            {
                Assert.That(LinkArrowPlanner.DrawsAsArrow(project, project.FindById(id)!), Is.False,
                    $"the half {id.Value} kept its row, so no arrow may duplicate it");
            }
            Assert.That(vm.LinkArrows, Is.Not.Empty);
            Assert.That(vm.LinkArrows.Count, Is.LessThanOrEqualTo(takenAway.Count),
                "one arrow stands for one wire — never more arrows than the halves they replaced");
        });
    }

    // ── Selecting a wire, and deleting it ────────────────────────────────────────────────────────────────────

    /// <summary>An arrow is selected the way a link row is, and it IS the selection: a row and a wire are never
    /// both selected, so <c>Delete</c> is never ambiguous about which one it means.</summary>
    [Test]
    public async Task SelectingAnArrow_ClearsTheTreeSelection()
    {
        Placed placed = await ArrowsShownAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;
        vm.SelectedInstallationNode = TreeNodes.FindById(vm.InstallationNodes, placed.ProductPin);
        Assert.That(vm.SelectedNode, Is.Not.Null, "precondition: a row is selected");

        vm.SelectLinkArrowAt([vm.LinkArrows[0]]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedLinkArrow, Is.EqualTo(vm.LinkArrows[0].Half), "the wire is selected by its own row's id");
            Assert.That(vm.SelectedNode, Is.Null, "and the tree selection let go");
            Assert.That(vm.SelectedInstallationNode, Is.Null);
        });
    }

    /// <summary>And back: selecting a row lets go of the wire.</summary>
    [Test]
    public async Task SelectingARow_ClearsTheSelectedArrow()
    {
        Placed placed = await SelectedWireAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SelectedInstallationNode = TreeNodes.FindById(vm.InstallationNodes, placed.ProductPin);

        Assert.That(vm.SelectedLinkArrow, Is.Null);
    }

    /// <summary>
    /// A click that could mean either of two overlapping wires selects NEITHER. Picking the nearest would make
    /// the next <c>Delete</c> remove a wire the installer never pointed at, so the refusal is the safe answer —
    /// and it says so, rather than reading as a click that did not register.
    /// </summary>
    [Test]
    public async Task AnAmbiguousClick_SelectsNothing_AndSaysWhy()
    {
        Placed placed = await SelectedWireAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SelectLinkArrowAt([vm.LinkArrows[0], vm.LinkArrows[0]]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedLinkArrow, Is.Null, "an ambiguous click also lets go of what was selected");
            Assert.That(vm.StatusText, Does.Contain("oven i hinanden"), "and explains the refusal");
        });
    }

    /// <summary>
    /// Delete on a selected arrow removes the wire — the same command, the same SDK verdict and the same
    /// reciprocal-pair removal as selecting its row in <i>Etiketter</i> mode (US-057). The row is simply not on
    /// screen to select.
    /// </summary>
    [Test]
    public async Task DeleteOnASelectedArrow_RemovesBothHalvesOfTheWire()
    {
        Placed placed = await SelectedWireAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        Assert.That(vm.DeleteCommand.CanExecute(null), Is.True, "Slet is offered while a wire is selected");
        await vm.DeleteCommand.ExecuteAsync(null);

        Project project = placed.Harness.Session.Current!;
        Assert.Multiple(() =>
        {
            Assert.That(project.FindById(placed.ProductPin)!.Children.Any(c => c.IsLinkHalf), Is.False,
                "the producer's half is gone");
            Assert.That(project.FindById(placed.BlockPin)!.Children.Any(c => c.IsLinkHalf), Is.False,
                "and so is the reciprocal half");
            Assert.That(vm.LinkArrows, Is.Empty, "so there is no arrow left to draw");
            Assert.That(vm.SelectedLinkArrow, Is.Null, "and the selection went with it");
            Assert.That(vm.StatusText, Is.EqualTo("Link fjernet."));
        });
    }

    /// <summary>Undo brings it back, like any other edit (US-052) — and the restored wire is an arrow again.</summary>
    [Test]
    public async Task DeletingAnArrow_IsUndoable()
    {
        Placed placed = await SelectedWireAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;
        await vm.DeleteCommand.ExecuteAsync(null);

        await vm.UndoCommand.ExecuteAsync(null);

        Assert.That(vm.LinkArrows, Has.Count.EqualTo(1), "the wire is back, and back as an arrow");
    }

    /// <summary>With nothing selected at all, <i>Slet</i> stays withheld — an arrow selection is what enables it,
    /// not the mode.</summary>
    [Test]
    public async Task WithNoSelection_DeleteStaysWithheld()
    {
        Placed placed = await ArrowsShownAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        Assert.That(vm.DeleteCommand.CanExecute(null), Is.False);
        Assert.That(vm.Registry.Explain("edit.delete"), Is.Not.Null.And.Not.Empty, "and says why");
    }

    /// <summary>Leaving arrows mode lets go of the wire: the thing that was selected is not on screen any more.</summary>
    [Test]
    public async Task ReturningToLabels_ClearsTheSelectedArrow()
    {
        Placed placed = await SelectedWireAsync();
        using ShellHarness owned = placed.Harness;
        var vm = placed.Vm;

        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Labels);

        Assert.That(vm.SelectedLinkArrow, Is.Null);
    }
}
