using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Configuration;
using ihc_openvisual.Controls;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc.Vis;
using Ihc.Vis.Model;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// US-089 — <i>Vis ▸ Links ▸ Pile</i> on a realized window: the menu that offers it, and where the arrows
/// actually land.
///
/// <para>Geometry is asserted from <see cref="LinkArrowOverlay.BuildArrows"/> rather than from a screenshot,
/// because what makes an arrow right is where its two ends are — beside the producing row, at the row it drives,
/// inside the pane that holds each — and a rendered surface cannot be asked that. Which wires exist at all is
/// <c>safe_project_tests/LinkArrowModeTests</c>; this fixture takes that as given.</para>
/// </summary>
public class LinkArrowOverlayTests : AvaloniaTestBase
{
    // An initialized shell over an empty project, with its first locality resolved — where every fixture starts.
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, ElementId Locality)> ShellAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm, vm.InstallationNodes[0].Children[0].ElementId!.Value);
    }

    // Shows the shell's window and hands back it and its overlay. Registered for failure capture here rather
    // than in each fixture, so a test that forgets cannot silently lose its screenshot.
    private static (MainWindow Window, LinkArrowOverlay Overlay) Show(MainWindowViewModel vm)
    {
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, window.GetLogicalDescendants().OfType<LinkArrowOverlay>().Single());
    }

    // Shows the window already in Pile with every row open — the state the geometry assertions measure.
    private static (MainWindow Window, LinkArrowOverlay Overlay) ShowArrows(MainWindowViewModel vm)
    {
        (MainWindow window, LinkArrowOverlay overlay) = Show(vm);
        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        OpenEverything(vm);
        return (window, overlay);
    }

    // The first pin of a placed function block's `inputs`/`outputs` section.
    private static ElementId BlockPin(ShellHarness harness, ElementId block, string section) =>
        harness.Session.Current!.FindById(block)!.FindChild(section)!.Children.First().Id!.Value;

    // A product input wired to a function-block input, on a shown window: the ordinary cross-pane wire.
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, MainWindow Window,
        LinkArrowOverlay Overlay)> WiredWindowAsync()
    {
        (ShellHarness harness, MainWindowViewModel vm, ElementId loc) = await ShellAsync();
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        ElementId pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        ElementId fbId = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        ElementId productInput = harness.Session.Current!.FindById(pid)!.Children
            .First(c => c.Tag == "dataline_input").Id!.Value;
        await harness.Session.LinkPinsAsync(productInput, BlockPin(harness, fbId, "inputs"));

        (MainWindow window, LinkArrowOverlay overlay) = Show(vm);
        return (harness, vm, window, overlay);
    }

    // The same, already in Pile with every row open and its ONE arrow resolved — what every selection and
    // hit-test assertion starts from.
    private static async Task<(ShellHarness Harness, MainWindowViewModel Vm, MainWindow Window,
        LinkArrowOverlay Overlay, LinkArrowShape Arrow)> OneWireShownAsync()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, LinkArrowOverlay overlay) =
            await WiredWindowAsync();
        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        OpenEverything(vm);
        return (harness, vm, window, overlay, overlay.BuildArrows().Single());
    }

    // Opens every row of both panes and lets layout settle, so a row's rectangle is a real rectangle.
    private static void OpenEverything(MainWindowViewModel vm)
    {
        foreach (TreeNodeViewModel root in vm.InstallationNodes.Concat(vm.FunctionNodes))
        {
            root.ExpandSubtree();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static IReadOnlyList<string?> VisMenuIds(MainWindow window)
    {
        Menu menu = window.GetLogicalDescendants().OfType<Menu>().First();
        MenuItem view = menu.Items.OfType<MenuItem>()
            .First(mi => AutomationProperties.GetAutomationId(mi) == AutomationIds.MenuView);
        return view.Items.OfType<Control>().Select(AutomationProperties.GetAutomationId).ToList();
    }

    // ── The menu ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The row sits directly above <i>Tema</i>, and offers the two renderings as a radio pair — the same
    /// shape as the two appearance submenus it now leads.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheVisMenu_OffersLinks_DirectlyAboveTema()
    {
        (ShellHarness harness, MainWindowViewModel vm, _) = await ShellAsync();
        using ShellHarness owned = harness;
        (MainWindow window, _) = Show(vm);

        IReadOnlyList<string?> ids = VisMenuIds(window);
        Menu menu = window.GetLogicalDescendants().OfType<Menu>().First();
        MenuItem links = menu.GetLogicalDescendants().OfType<MenuItem>()
            .First(mi => AutomationProperties.GetAutomationId(mi) == "menu.links");

        Assert.Multiple(() =>
        {
            Assert.That(ids, Does.Contain("menu.links"));
            Assert.That(ids.ToList().IndexOf("menu.links"), Is.EqualTo(ids.ToList().IndexOf("menu.theme") - 1),
                "Links is the row immediately above Tema");
            Assert.That(links.Header, Is.EqualTo("Links"));
            Assert.That(links.Items.OfType<MenuItem>().Select(AutomationProperties.GetAutomationId),
                Is.EqualTo(new[] { "links.labels", "links.arrows" }), "labels first, because it is the default");
            Assert.That(links.Items.OfType<MenuItem>().Select(mi => mi.Header),
                Is.EqualTo(new object[] { "Etiketter", "Pile" }), "Danish, like the rest of the app's chrome");
            Assert.That(links.Items.OfType<MenuItem>().Select(mi => mi.ToggleType),
                Is.All.EqualTo(MenuItemToggleType.Radio), "one rendering at a time, checked like Tema's variants");
        });
    }

    /// <summary>And invoking the item through UI Automation switches the mode — the route a driver and a screen
    /// reader take, which is not the same code path as calling the command.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task InvokingPileThroughAutomation_SwitchesTheRendering()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, _) = await WiredWindowAsync();
        using ShellHarness owned = harness;
        Menu menu = window.GetLogicalDescendants().OfType<Menu>().First();
        MenuItem arrows = menu.GetLogicalDescendants().OfType<MenuItem>()
            .Single(mi => AutomationProperties.GetAutomationId(mi) == "links.arrows");

        ControlAutomationPeer.CreatePeerForElement(arrows).GetProvider<IInvokeProvider>()!.Invoke();
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentLinkDisplay, Is.EqualTo(LinkDisplay.Arrows));
            Assert.That(vm.LinkArrows, Is.Not.Empty, "the wire the panes stopped listing is now one to draw");
        });
    }

    // ── What is drawn ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Labels mode draws nothing at all — the overlay is inert until the installer asks for it.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task InLabelsMode_TheOverlayDrawsNothing()
    {
        (ShellHarness harness, MainWindowViewModel vm, _, LinkArrowOverlay overlay) = await WiredWindowAsync();
        using ShellHarness owned = harness;
        OpenEverything(vm);

        Assert.That(overlay.BuildArrows(), Is.Empty);
    }

    /// <summary>
    /// The cross-pane wire, which is the common one: it leaves the product pin's row on the RIGHT — past the
    /// label, in the free space — and arrives on the LEFT of the block pin's row, crossing the splitter once.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ACrossPaneWire_LeavesTheProducersRowAndArrivesAtWhatItDrives()
    {
        (ShellHarness harness, _, MainWindow window, LinkArrowOverlay overlay, _) = await OneWireShownAsync();
        using ShellHarness owned = harness;

        IReadOnlyList<LinkArrowShape> arrows = overlay.BuildArrows();
        Assert.That(arrows, Has.Count.EqualTo(1), "one wire, one arrow");
        LinkArrowShape arrow = arrows[0];
        Rect installation = PaneRect(window, overlay, AutomationIds.InstallationTree);
        Rect functions = PaneRect(window, overlay, AutomationIds.FunctionsTree);

        Assert.Multiple(() =>
        {
            Assert.That(arrow.CrossesPanes, Is.True);
            Assert.That(arrow.Kind, Is.EqualTo(LinkArrowKind.Follow));
            Assert.That(installation.Contains(arrow.Start), Is.True, "it starts inside the Installation pane");
            Assert.That(functions.Contains(arrow.End), Is.True, "and ends inside the Funktioner pane");
            Assert.That(arrow.End.X, Is.GreaterThan(arrow.Start.X), "left to right, once across the splitter");
            // The control points reach out horizontally from each end, which is what keeps the curve from
            // cutting diagonally across the rows between them.
            Assert.That(arrow.Control1.X, Is.GreaterThan(arrow.Start.X));
            Assert.That(arrow.Control2.X, Is.LessThan(arrow.End.X));
        });
    }

    /// <summary>
    /// A wire between two rows of the SAME pane attaches on the right of both and bows out into the space beside
    /// the labels — there is no facing side to use, and routing it through the other pane would cross the
    /// splitter twice to say nothing.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AWireInsideOnePane_AttachesOnTheRightOfBothRows()
    {
        (ShellHarness harness, MainWindowViewModel vm, ElementId loc) = await ShellAsync();
        using ShellHarness owned = harness;
        var source = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Outputs.Count > 0);
        var sink = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        ElementId sourceId = (await harness.Session.AddFunctionBlockAsync(loc, source.MasterType))!.Value;
        ElementId sinkId = (await harness.Session.AddFunctionBlockAsync(loc, sink.MasterType))!.Value;
        await harness.Session.LinkPinsAsync(BlockPin(harness, sourceId, "outputs"), BlockPin(harness, sinkId, "inputs"));
        (MainWindow window, LinkArrowOverlay overlay) = ShowArrows(vm);

        IReadOnlyList<LinkArrowShape> arrows = overlay.BuildArrows();
        Assert.That(arrows, Has.Count.EqualTo(1));
        LinkArrowShape arrow = arrows[0];
        Rect functions = PaneRect(window, overlay, AutomationIds.FunctionsTree);

        Assert.Multiple(() =>
        {
            Assert.That(arrow.CrossesPanes, Is.False, "both ends are function-block pins, so both are in Funktioner");
            Assert.That(functions.Contains(arrow.Start), Is.True);
            Assert.That(functions.Contains(arrow.End), Is.True);
            Assert.That(arrow.Control1.X, Is.GreaterThanOrEqualTo(arrow.Start.X), "the curve bows outwards, right");
            Assert.That(arrow.Control2.X, Is.GreaterThanOrEqualTo(arrow.End.X));
            Assert.That(arrow.Control1.X, Is.LessThanOrEqualTo(functions.Right), "and stays inside its own pane");
        });

        // And closing the locality folds BOTH of its ends onto that one row, which is a wire the installer has put
        // away rather than one that goes nowhere — so nothing is drawn instead of a loop leaving and re-entering
        // the same row.
        vm.FunctionNodes[0].Children[0].IsExpanded = false;
        Dispatcher.UIThread.RunJobs();
        Assert.That(overlay.BuildArrows(), Is.Empty,
            "a wire whose two ends land on the same row draws nothing");
    }

    /// <summary>
    /// A wire into a closed subtree is still drawn: the arrow anchors on the nearest row that IS on screen — the
    /// block, then the locality — so a collapsed pane says "the wire goes in there" instead of losing it. This is
    /// what the endpoint's ancestor chain is for.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task WhenTheFarEndIsInsideAClosedRow_TheArrowAnchorsOnTheNearestOpenAncestor()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, LinkArrowOverlay overlay,
            LinkArrowShape wire) = await OneWireShownAsync();
        using ShellHarness owned = harness;
        Point atThePin = wire.End;

        TreeNodeViewModel functionBlock = vm.FunctionNodes[0].Children[0].Children[0];
        functionBlock.IsExpanded = false;
        Dispatcher.UIThread.RunJobs();
        IReadOnlyList<LinkArrowShape> closedBlock = overlay.BuildArrows();

        vm.FunctionNodes[0].Children[0].IsExpanded = false;
        Dispatcher.UIThread.RunJobs();
        IReadOnlyList<LinkArrowShape> closedLocality = overlay.BuildArrows();

        Assert.Multiple(() =>
        {
            Assert.That(closedBlock, Has.Count.EqualTo(1), "closing the block does not drop the wire");
            Assert.That(closedBlock[0].End, Is.Not.EqualTo(atThePin), "it now points at the block's own row");
            Assert.That(closedLocality, Has.Count.EqualTo(1), "nor does closing the locality above it");
            Assert.That(PaneRect(window, overlay, AutomationIds.FunctionsTree).Contains(closedLocality[0].End),
                Is.True, "and the end stays inside the pane it belongs to");
        });
    }

    // The pane's viewport in the overlay's own coordinates — the frame every arrow end must fall inside.
    private static Rect PaneRect(MainWindow window, LinkArrowOverlay overlay, string automationId)
    {
        Control tree = window.GetLogicalDescendants().OfType<Control>()
            .First(c => AutomationProperties.GetAutomationId(c) == automationId);
        return new Rect(tree.TranslatePoint(default, overlay)!.Value, tree.Bounds.Size);
    }

    // ── Selecting a wire, and deleting it ────────────────────────────────────────────────────────────────────

    /// <summary>A click ON the line reaches it; one beside it does not. The tolerance is what makes a 1.7px line
    /// clickable at all, so both halves are asserted — a hit test that answered "yes" everywhere would pass the
    /// first assertion alone.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task HitTest_ReachesTheLine_AndNotTheSpaceBesideIt()
    {
        (ShellHarness harness, _, _, LinkArrowOverlay overlay, LinkArrowShape arrow) = await OneWireShownAsync();
        using ShellHarness owned = harness;

        Assert.Multiple(() =>
        {
            // Both ends are ON the curve by construction, so they are the two points a hit test cannot get wrong.
            Assert.That(overlay.WiresAt(arrow.Start), Has.Count.EqualTo(1), "the tail is on the wire");
            Assert.That(overlay.WiresAt(arrow.End), Has.Count.EqualTo(1), "and so is the head");
            Assert.That(overlay.WiresAt(arrow.Start + new Vector(0, 40)), Is.Empty,
                "40px below the tail is empty space, and a click there belongs to the pane");
        });
    }

    /// <summary>
    /// Two wires leaving one pin share their tail exactly, so a click there could mean either — and the hit test
    /// reports BOTH rather than the nearest. That is what lets the view-model refuse instead of guessing which
    /// wire the next Delete would remove; further along, where the two have separated, each is selectable again.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TwoWiresLeavingOnePin_AreBothReportedWhereTheyOverlap()
    {
        (ShellHarness harness, MainWindowViewModel vm, ElementId loc) = await ShellAsync();
        using ShellHarness owned = harness;
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        ElementId pid = (await harness.Session.AddProductAsync(loc, product.ProductIdentifier))!.Value;
        ElementId first = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        ElementId second = (await harness.Session.AddFunctionBlockAsync(loc, block.MasterType))!.Value;
        ElementId pin = harness.Session.Current!.FindById(pid)!.Children.First(c => c.Tag == "dataline_input").Id!.Value;
        await harness.Session.LinkPinsAsync(pin, BlockPin(harness, first, "inputs"));
        await harness.Session.LinkPinsAsync(pin, BlockPin(harness, second, "inputs"));
        (_, LinkArrowOverlay overlay) = ShowArrows(vm);

        IReadOnlyList<LinkArrowShape> arrows = overlay.BuildArrows();
        Assert.That(arrows, Has.Count.EqualTo(2), "one pin, two wires");
        Assert.Multiple(() =>
        {
            Assert.That(arrows[0].Start, Is.EqualTo(arrows[1].Start), "they leave the same pin from the same point");
            Assert.That(overlay.WiresAt(arrows[0].Start), Has.Count.EqualTo(2),
                "so a click there is reported as ambiguous rather than resolved to one of them");
            Assert.That(overlay.WiresAt(arrows[0].End), Has.Count.EqualTo(1),
                "while each head, where they have separated, names one wire");
        });
    }

    /// <summary>The selected wire is drawn at double width — the cue the user asked for, and the one that does
    /// not depend on colour.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TheSelectedWire_IsDrawnAtDoubleWidth()
    {
        (ShellHarness harness, MainWindowViewModel vm, _, LinkArrowOverlay overlay, LinkArrowShape wire) =
            await OneWireShownAsync();
        using ShellHarness owned = harness;
        double unselected = wire.StrokeWidth;

        vm.SelectLinkArrowAt([vm.LinkArrows[0]]);
        Dispatcher.UIThread.RunJobs();
        LinkArrowShape selected = overlay.BuildArrows().Single();

        Assert.Multiple(() =>
        {
            Assert.That(selected.IsSelected, Is.True, "the overlay learned which wire is selected");
            Assert.That(selected.StrokeWidth, Is.EqualTo(unselected * 2).Within(0.001));
        });
    }

    /// <summary>
    /// The whole gesture, driven as a person drives it: click the line, press Delete, and the connection is gone.
    /// Nothing below this asserts the route — the view-model tests take the selection as given — so this is the
    /// one place that proves a click on an arrow reaches it at all, and that Delete then means the wire.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ClickingAWireAndPressingDelete_RemovesTheConnection()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, LinkArrowOverlay overlay,
            LinkArrowShape arrow) = await OneWireShownAsync();
        using ShellHarness owned = harness;
        ElementId producer = arrow.Edge.From.Element;

        window.MouseDown(overlay.TranslatePoint(arrow.Start, window)!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.That(vm.SelectedLinkArrow, Is.Not.Null, "the click landed on the wire");

        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.FindById(producer)!.Children.Any(c => c.IsLinkHalf), Is.False,
                "the connection is gone from the document");
            Assert.That(overlay.BuildArrows(), Is.Empty, "and there is nothing left to draw");
        });
    }

    /// <summary>
    /// Clicking a wire has to leave keyboard focus INSIDE the window, because Avalonia delivers a key press to
    /// the focused element and to nothing else. Measured on the running application: with focus elsewhere —
    /// straight after using the menu, which is exactly how the mode gets turned on — the click selected the wire,
    /// <i>Rediger ▸ Slet</i> was enabled, and the Delete key reached no handler at all. The overlay takes focus
    /// itself so the selection has an owner; it is never a TAB stop, so nothing else about the shell changes.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ClickingAWire_TakesKeyboardFocus_SoTheDeleteThatFollowsIsDelivered()
    {
        (ShellHarness harness, _, MainWindow window, LinkArrowOverlay overlay, LinkArrowShape arrow) =
            await OneWireShownAsync();
        using ShellHarness owned = harness;

        window.MouseDown(overlay.TranslatePoint(arrow.Start, window)!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(window.FocusManager!.GetFocusedElement(), Is.SameAs(overlay),
                "the selected wire has a keyboard-focus owner");
            Assert.That(overlay.IsTabStop, Is.False, "but Tab never lands on a drawing");
        });
    }

    /// <summary>
    /// The pointer over a line belongs to the LINE. Without this the row underneath still lit its hover
    /// highlight while the pointer was on a wire, so aiming at a line looked like picking a row — confusing to
    /// read and an invitation to mis-click. The band is exactly the geometry the frame drew, asked through the
    /// same <c>ICustomHitTest</c> the input system asks.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ThePointerIsOverTheOverlayOnALine_AndOverTheTreeEverywhereElse()
    {
        (ShellHarness harness, _, MainWindow window, LinkArrowOverlay overlay, LinkArrowShape arrow) =
            await OneWireShownAsync();
        using ShellHarness owned = harness;
        var hitTest = (ICustomHitTest)overlay;

        Assert.Multiple(() =>
        {
            Assert.That(hitTest.HitTest(arrow.Start), Is.True, "on the line, the pointer is the overlay's");
            Assert.That(hitTest.HitTest(arrow.Start + new Vector(0, 40)), Is.False,
                "40px away it is the pane's, so rows hover and are clicked exactly as they always were");
            // What actually reaches the user: the row under a line does not light up.
            Assert.That(window.InputHitTest(overlay.TranslatePoint(arrow.Start, window)!.Value), Is.SameAs(overlay),
                "and the input system agrees, which is what stops the row underneath hovering");
        });
    }

    /// <summary>
    /// …except while a drag is in flight, when the band is handed straight back. Hit testing cannot tell a click
    /// from a drop, so a drop that landed on the few pixels of a line would otherwise reach a control that is not
    /// a drop target and fail with nothing on screen to explain it.
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task WhileADragIsInFlight_TheLineGivesItsBandBackToThePanes()
    {
        (ShellHarness harness, _, _, LinkArrowOverlay overlay, LinkArrowShape arrow) = await OneWireShownAsync();
        using ShellHarness owned = harness;
        var hitTest = (ICustomHitTest)overlay;
        Assert.That(hitTest.HitTest(arrow.Start), Is.True, "precondition: the line owns its band");

        overlay.PassThroughPointer = true;

        Assert.That(hitTest.HitTest(arrow.Start), Is.False, "a drop aimed through a line reaches the pane beneath");
    }

    /// <summary>
    /// A line drawn across a parent row must not be what stops it opening. Reported from the running application:
    /// once a wire owned the pointer along its whole length, the twisty of a row it crossed stopped answering —
    /// and the head of every arrow into a CLOSED subtree lands right beside that row's twisty, so the very rows a
    /// person wants to open are the ones an arrow reaches for.
    /// <para>Both halves are asserted, because only the pair says the rule is doing anything: the geometry really
    /// does put a wire there, and the pointer goes to the twisty anyway.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AWireAcrossATwisty_LeavesItClickable_AndTheRowStillOpens()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, LinkArrowOverlay overlay, _) =
            await OneWireShownAsync();
        using ShellHarness owned = harness;
        // Close the block the wire runs into: the arrow now ends on the block's own row, beside its twisty.
        TreeNodeViewModel block = vm.FunctionNodes[0].Children[0].Children[0];
        block.IsExpanded = false;
        Dispatcher.UIThread.RunJobs();
        ToggleButton twisty = window.GetVisualDescendants().OfType<TreeViewItem>()
            .First(i => ReferenceEquals(i.DataContext, block))
            .GetVisualDescendants().OfType<ToggleButton>().First(t => t.IsEffectivelyVisible);

        // Where the wire and the twisty actually meet — searched rather than assumed, because how wide a twisty
        // is and where it sits inside its row is the theme's business, not this test's.
        Point? crossing = TwistyPoints(twisty, overlay).FirstOrDefault(p => overlay.WiresAt(p).Count > 0);
        Assert.That(crossing, Is.Not.Null, "armed: the wire really is drawn across the twisty");

        Assert.That(overlay.WiresClaimingPointer(crossing!.Value), Is.Empty,
            "the pane's own control wins, so the press is never the overlay's to take");

        Point inWindow = overlay.TranslatePoint(crossing.Value, window)!.Value;
        window.MouseDown(inWindow, MouseButton.Left);
        window.MouseUp(inWindow, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(block.IsExpanded, Is.True, "and the click opened the row, as it always did");
            Assert.That(vm.SelectedLinkArrow, Is.Null, "without selecting the wire that crosses it");
        });
    }

    // A small grid over a twisty, in the overlay's coordinates.
    private static IEnumerable<Point> TwistyPoints(Control twisty, LinkArrowOverlay overlay)
    {
        if (twisty.TranslatePoint(default, overlay) is not { } origin)
        {
            yield break;
        }
        for (int x = 1; x < twisty.Bounds.Width; x += 2)
        {
            for (int y = 1; y < twisty.Bounds.Height; y += 2)
            {
                yield return origin + new Vector(x, y);
            }
        }
    }

    /// <summary>
    /// The same rule for the shell's own control: the pane splitter keeps its strip. EVERY cross-pane wire
    /// crosses it, and a press there is the start of a DRAG that no hit test can tell from a click — so a wire
    /// claiming the strip leaves the panes unresizable along the whole band it crosses, with the arrow taking
    /// the press instead.
    /// <para>Both halves are asserted for the same reason as the twisty above: that a wire really is drawn
    /// across the strip, and that the pointer goes to the splitter anyway.</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AWireAcrossThePaneSplitter_LeavesItDraggable()
    {
        (ShellHarness harness, _, MainWindow window, LinkArrowOverlay overlay, LinkArrowShape arrow) =
            await OneWireShownAsync();
        using ShellHarness owned = harness;
        Assert.That(arrow.CrossesPanes, Is.True, "armed: this wire is one that has to cross the strip");
        GridSplitter splitter = window.GetVisualDescendants().OfType<GridSplitter>()
            .First(s => s.ResizeDirection == GridResizeDirection.Columns);

        // Where the wire and the strip actually meet — searched rather than assumed, because how wide the
        // splitter is and where the curve crosses it are layout's business, not this test's.
        IReadOnlyList<Point> crossings = SplitterPoints(splitter, overlay)
            .Where(p => overlay.WiresAt(p).Count > 0).ToList();
        Assert.That(crossings, Is.Not.Empty, "armed: the wire really is drawn across the splitter");

        Assert.Multiple(() =>
        {
            foreach (Point crossing in crossings)
            {
                Assert.That(overlay.WiresClaimingPointer(crossing), Is.Empty,
                    "the splitter wins, so the press is never the overlay's to take");
                Assert.That(Reaches(window.InputHitTest(overlay.TranslatePoint(crossing, window)!.Value), splitter),
                    Is.True, "and the input system agrees, which is what leaves the drag to the splitter");
            }
        });
    }

    // A grid over the splitter's whole strip, in the overlay's coordinates.
    private static IEnumerable<Point> SplitterPoints(Control splitter, LinkArrowOverlay overlay)
    {
        if (splitter.TranslatePoint(default, overlay) is not { } origin)
        {
            yield break;
        }
        for (double x = 0.5; x < splitter.Bounds.Width; x++)
        {
            for (int y = 1; y < splitter.Bounds.Height; y += 2)
            {
                yield return origin + new Vector(x, y);
            }
        }
    }

    // Whether a hit-test result is the control itself or something inside its template.
    private static bool Reaches(IInputElement? hit, Visual target) =>
        hit is Visual visual && (ReferenceEquals(visual, target) || visual.GetVisualAncestors().Contains(target));

    /// <summary>A click that reaches no wire is left entirely to the panes — which is what keeps the overlay from
    /// costing the trees a click anywhere but on a line.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task AClickAwayFromEveryWire_StillReachesTheTree()
    {
        (ShellHarness harness, MainWindowViewModel vm, MainWindow window, LinkArrowOverlay overlay) =
            await WiredWindowAsync();
        using ShellHarness owned = harness;
        vm.SetLinkDisplayCommand.Execute(LinkDisplay.Arrows);
        OpenEverything(vm);
        // The producing pin itself: a LEAF in arrows mode (its link rows are what the mode took away), so its
        // container bounds are its own row rather than a whole subtree — and its label sits well left of where
        // its arrow attaches, which is the point of the test.
        TreeNodeViewModel pin = TreeNodes.FindById(vm.InstallationNodes, overlay.BuildArrows().Single().Edge.From.Element)!;
        Control row = window.GetVisualDescendants().OfType<TreeViewItem>()
            .First(i => ReferenceEquals(i.DataContext, pin));

        window.MouseDown(row.TranslatePoint(new Point(30, row.Bounds.Height / 2), window)!.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(vm.SelectedLinkArrow, Is.Null, "no wire was aimed at");
            Assert.That(vm.SelectedNode, Is.SameAs(pin), "and the row under the pointer took the click");
        });
    }
}
