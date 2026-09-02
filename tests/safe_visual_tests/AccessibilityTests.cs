using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.VisualTree;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;
using Ihc;
using Ihc.Vis;
using Ihc.Vis.Catalog;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace safe_visual_tests;

/// <summary>
/// Accessibility regression coverage (Avalonia accessibility guidelines): every meaningful control exposes an
/// accessible name to assistive technology, form fields are programmatically label-associated, and the About
/// "source" link is a keyboard-operable control — not an unreachable pointer-only glyph. These lock in the
/// semantics that screen readers (UI Automation / NSAccessibility / AT-SPI2) rely on.
/// </summary>
public class AccessibilityTests : AvaloniaTestBase
{
    // Icon-only toolbar buttons carry meaning through their glyph alone; each must expose an accessible name so a
    // screen reader announces "New project" rather than an unnamed button.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Toolbar_IconButtons_AllHaveAccessibleNames()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var toolButtons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("tool")).ToList();

        Assert.That(toolButtons, Is.Not.Empty, "the toolbar renders its icon buttons");
        Assert.That(toolButtons.Select(AutomationProperties.GetName),
            Has.All.Not.Null.And.All.Not.Empty,
            "every icon-only toolbar button exposes an accessible name");
    }

    // The unlinked state is shown visually by a "!" glyph + colour; the accessible name must also carry it so a
    // screen-reader user learns the node is not linked to the controller.
    [Test]
    public void AccessibleName_ReflectsUnlinkedState()
    {
        var linked = new TreeNodeViewModel("PIR sensor", "/Assets/product-sensor.svg");
        var unlinked = new TreeNodeViewModel("PIR sensor", "/Assets/product-sensor.svg", isUnlinked: true);

        Assert.Multiple(() =>
        {
            Assert.That(linked.AccessibleName, Is.EqualTo("PIR sensor"), "a linked node's name is just its label");
            Assert.That(unlinked.AccessibleName, Does.StartWith("PIR sensor"), "the label is preserved");
            Assert.That(unlinked.AccessibleName, Does.Contain("ikke linket"),
                "and the unlinked state is announced — in the application's own language, since this is spoken");
        });
    }

    // The rendered tree rows must actually publish AccessibleName to the automation tree (binding wired up).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TreeRows_PublishAccessibleName_ToAutomationTree()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var localityName = viewModel.InstallationNodes[0].Children[0].DisplayName;
        var names = window.GetVisualDescendants().OfType<StackPanel>()
            .Select(AutomationProperties.GetName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        Assert.That(names, Does.Contain(localityName),
            "a locality row publishes its display name as an accessible name");
    }

    // Accessible names are English display strings that change under localization; a UI-Automation driver (screen
    // reader script, WinAppDriver/Appium E2E) needs a stable, locale-independent key. Every icon-only toolbar
    // button must therefore also expose an AutomationId, and specific buttons must keep their agreed ids.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Toolbar_IconButtons_HaveStableAutomationIds()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var toolButtons = window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("tool")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(toolButtons.Select(AutomationProperties.GetAutomationId),
                Has.All.Not.Null.And.All.Not.Empty,
                "every toolbar button exposes a stable, locale-independent AutomationId");

            var ids = toolButtons.Select(AutomationProperties.GetAutomationId).ToList();
            Assert.That(ids, Does.Contain("ToolbarNew"), "the New button keeps its agreed AutomationId");
            Assert.That(ids, Does.Contain("ToolbarSave"), "the Save button keeps its agreed AutomationId");
        });
    }

    // Each top-level menu must expose a stable AutomationId so a driver can target "the Insert menu" without
    // depending on the localized header text ("Insert" vs "Indsæt").
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TopLevelMenus_HaveStableAutomationIds()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var menu = window.GetVisualDescendants().OfType<Menu>().First();
        var topLevel = menu.Items.OfType<MenuItem>().ToList();

        Assert.That(topLevel.Select(AutomationProperties.GetAutomationId),
            Has.All.Not.Null.And.All.Not.Empty,
            "every top-level menu exposes a stable AutomationId");
        Assert.That(topLevel.Select(AutomationProperties.GetAutomationId),
            Does.Contain("MenuFile").And.Contains("MenuInsert"),
            "the agreed menu ids are present");
    }

    // A tree row's accessible name must sit on the TreeViewItem container itself, so a UIA client reading
    // TreeItem.Name gets the label directly (Avalonia otherwise leaves the container Name blank and pushes the
    // name onto an inner Text peer, which every driver then has to work around).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TreeItemContainers_PublishAccessibleName()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var localityName = viewModel.InstallationNodes[0].Children[0].DisplayName;
        var containerNames = window.GetVisualDescendants().OfType<TreeViewItem>()
            .Select(AutomationProperties.GetName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        Assert.That(containerNames, Does.Contain(localityName),
            "a locality's TreeViewItem container publishes its accessible name directly");
    }

    // A tree node's automation peer must expose the ExpandCollapse pattern so assistive tech can announce
    // "collapsed/expanded" and expand or collapse the node the standard way, and automation clients can drive
    // it. Avalonia's stock TreeViewItem peer omits it (expansion is only reachable through the visual chevron
    // button, which reads as an unrelated control), so the app supplies AccessibleTreeView whose peer implements
    // IExpandCollapseProvider.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TreeItem_AutomationPeer_ExposesExpandCollapse()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        // The root "Lokaliteter" node has children, so it is a genuine expand/collapse target.
        var root = window.GetVisualDescendants().OfType<TreeViewItem>().First(c => c.ItemCount > 0);
        var peer = ControlAutomationPeer.CreatePeerForElement(root);

        Assert.That(peer, Is.InstanceOf<IExpandCollapseProvider>(),
            "a tree node's peer exposes the ExpandCollapse pattern to UI Automation");
        var provider = (IExpandCollapseProvider)peer;

        provider.Collapse();
        Assert.Multiple(() =>
        {
            Assert.That(root.IsExpanded, Is.False, "Collapse() closes the node");
            Assert.That(provider.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed));
        });
        provider.Expand();
        Assert.Multiple(() =>
        {
            Assert.That(root.IsExpanded, Is.True, "Expand() opens the node");
            Assert.That(provider.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Expanded));
        });

        // A childless locality is a leaf — assistive tech must not present it as expandable.
        var leaf = window.GetVisualDescendants().OfType<TreeViewItem>().First(c => c.ItemCount == 0);
        var leafProvider = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(leaf);
        Assert.That(leafProvider.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.LeafNode),
            "a node with no children reports LeafNode");
    }

    // A tree row's tooltip must be exposed to UI Automation as HelpText so assistive tech and automation
    // clients can read it without hovering — Avalonia's ToolTip.Tip alone is a visual popup that never
    // reaches the automation tree. The row therefore mirrors its tooltip into AutomationProperties.HelpText.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task TreeRows_ExposeTooltip_AsHelpText()
    {
        // A placed product nests resource pins, and every pin row carries a "Resource ID: N" tooltip —
        // a fresh project's bare localities have none, so insert one to get genuinely tooltipped rows.
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie", StringComparison.Ordinal) && p.Resources.Count > 0);
        var localityId = viewModel.InstallationNodes[0].Children[0].ElementId!.Value;   // "Living room"
        await harness.Session.AddProductAsync(localityId, product.ProductIdentifier);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();

        // Realize the deep rows (locality ▸ product ▸ pins) so their tooltips render.
        for (int pass = 0; pass < 6; pass++)
        {
            foreach (var tvi in window.GetVisualDescendants().OfType<TreeViewItem>())
                tvi.IsExpanded = true;
            window.CaptureRenderedFrame();
        }

        // Every rendered row that carries a tooltip must surface that exact text to automation as HelpText.
        var rowsWithTooltip = window.GetVisualDescendants().OfType<StackPanel>()
            .Where(sp => ToolTip.GetTip(sp) is string t && t.Length > 0)
            .ToList();

        Assert.That(rowsWithTooltip, Is.Not.Empty, "at least one tree row carries a tooltip");
        Assert.That(rowsWithTooltip.Select(sp => AutomationProperties.GetHelpText(sp)),
            Is.EqualTo(rowsWithTooltip.Select(sp => ToolTip.GetTip(sp) as string)),
            "every tooltipped tree row exposes that exact text to automation as HelpText");
    }

    // Dialog form fields must be programmatically associated with their visible labels (LabeledBy), not just
    // placed next to them, so a screen reader announces "Name, edit" instead of an unlabeled edit box.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PropertiesWindow_Fields_AreLabelAssociated()
    {
        var window = new PropertiesWindow { Title = "Rediger egenskaber" };
        var name = window.FindControl<TextBox>("NameBox");
        var note = window.FindControl<TextBox>("NoteBox");
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(AutomationProperties.GetLabeledBy(name!), Is.Not.Null, "the Name field is label-associated");
            Assert.That(AutomationProperties.GetLabeledBy(note!), Is.Not.Null, "the Note field is label-associated");
        });
    }

    // A-9/A-10: a code-built confirm dialog must be keyboard-operable — the safe (negative, last) button holds focus
    // on open, and Escape closes the dialog (which its Closed handler resolves to that safe default). Exercises the
    // exact wiring AvaloniaDialogService applies to every confirm dialog.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ConfirmDialog_EscClosesAndSafeButtonFocused()
    {
        bool closed = false;
        var safeButton = new Button { Content = "No", MinWidth = 84 };
        var dialog = new Window
        {
            Content = new StackPanel { Children = { new Button { Content = "Yes" }, safeButton } }
        };
        dialog.Closed += (_, _) => closed = true;
        AvaloniaDialogService.WireKeyboardDismissal(dialog, safeButton);
        CurrentTestWindow = dialog;
        dialog.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(safeButton.IsFocused, Is.True, "the safe (negative) button holds keyboard focus on open");

        dialog.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None,
            Avalonia.Input.PhysicalKey.Escape, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(closed, Is.True, "Escape dismisses the dialog (which resolves to the safe default)");
    }

    // A-28 (F-083): F6 moves keyboard focus between the two tree panes — a real focus move, not a no-op.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task F6_SwitchesPaneFocus()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var tv1 = window.FindControl<TreeView>("InstallationTree")!;
        var tv2 = window.FindControl<TreeView>("FunctionsTree")!;
        tv1.GetVisualDescendants().OfType<TreeViewItem>().First().Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(tv1.IsKeyboardFocusWithin, Is.True, "the Installation pane starts focused");

        window.KeyPress(Avalonia.Input.Key.F6, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F6, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(tv2.IsKeyboardFocusWithin, Is.True, "F6 moves keyboard focus to the Functions pane");

        window.KeyPress(Avalonia.Input.Key.F6, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F6, null);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(tv1.IsKeyboardFocusWithin, Is.True, "F6 again returns focus to the Installation pane");
    }

    // T038 / US-045: F6 swaps keyboard focus but leaves each pane's SELECTION untouched — the caret crosses panes,
    // the highlighted rows do not move.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task F6_PreservesEachPaneSelection()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var tv1 = window.FindControl<TreeView>("InstallationTree")!;
        var tv2 = window.FindControl<TreeView>("FunctionsTree")!;
        vm.SelectedInstallationNode = vm.InstallationNodes[0].Children[0];
        vm.SelectedFunctionsNode = vm.FunctionNodes[0].Children[0];
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var installSelection = tv1.SelectedItem;
        var functionsSelection = tv2.SelectedItem;
        Assert.That(installSelection, Is.Not.Null.And.Not.SameAs(functionsSelection), "each pane starts with its own selection");

        tv1.GetVisualDescendants().OfType<TreeViewItem>().First().Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyPress(Avalonia.Input.Key.F6, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F6, null);   // to Functions
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyPress(Avalonia.Input.Key.F6, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.F6, null);   // back to Installation
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(tv1.SelectedItem, Is.SameAs(installSelection), "the Installation pane keeps its selection across F6");
            Assert.That(tv2.SelectedItem, Is.SameAs(functionsSelection), "the Functions pane keeps its selection across F6");
        });
    }

    // The About "source" link must be a real, keyboard-focusable and keyboard-activatable control with an
    // accessible name — not a TextBlock that only responds to a mouse click.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AboutWindow_SourceLink_IsKeyboardAccessible()
    {
        var about = new AboutWindow();
        CurrentTestWindow = about;
        about.Show();

        var link = about.FindControl<Button>("RepoLink");

        Assert.Multiple(() =>
        {
            Assert.That(link, Is.Not.Null, "the source link is a Button (keyboard-activatable via Enter/Space)");
            Assert.That(link!.Focusable, Is.True, "the link is reachable by keyboard focus");
            Assert.That(AutomationProperties.GetName(link), Is.Not.Null.And.Not.Empty,
                "the link exposes an accessible name");
        });
    }

    // ── Live region and landmarks (accessibility review BP-07/BP-09) ──────────────────────────────────────────

    /// <summary>The status bar is a POLITE live region. This is not a checkbox item here: the shell deliberately
    /// routes a refused shortcut's explanation to the status bar, precisely because a disabled control shows no
    /// tooltip (the T016 spike verdict behind T021 branch B). Without a live setting that text is announced to
    /// nobody, so a screen-reader user is the one user who gets NO explanation for a refusal.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task StatusBar_IsAPoliteLiveRegion()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        TextBlock status = StatusTextBlock(window);

        Assert.That(AutomationProperties.GetLiveSetting(status), Is.EqualTo(AutomationLiveSetting.Polite),
            "the status bar announces its changes without interrupting (Polite, not Assertive/Off)");
    }

    /// <summary>The same property tied to the behaviour that needs it: press a gated-off shortcut, and the text
    /// the registry writes must land in the live region — not merely somewhere on screen.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task RefusedGesture_ExplanationLandsInTheLiveRegion()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(viewModel.Registry.Bar["edit.undo"].Enabled, Is.False,
            "precondition: a fresh history gates Undo off");

        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Z, Avalonia.Input.RawInputModifiers.Control);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        TextBlock status = StatusTextBlock(window);
        Assert.Multiple(() =>
        {
            Assert.That(status.Text, Is.EqualTo(viewModel.StatusText).And.Not.Empty,
                "the refusal reason is what the status bar shows");
            Assert.That(AutomationProperties.GetLiveSetting(status), Is.EqualTo(AutomationLiveSetting.Polite),
                "and that control is the live region, so the reason is actually announced");
        });
    }

    /// <summary>The shell's major regions are landmarks, each at an accessibility view Narrator will read: a
    /// landmark below <see cref="AccessibilityView.Control"/> is silently ignored (review AP-04), which would make
    /// the annotation look present while doing nothing.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MajorRegions_DeclareLandmarks()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // "No landmark" is null, not a None member — AutomationProperties.GetLandmarkType returns a nullable.
        var landmarks = window.GetVisualDescendants().OfType<Control>()
            .Where(c => AutomationProperties.GetLandmarkType(c) is not null)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(landmarks, Has.Count.GreaterThanOrEqualTo(4),
                "the toolbar, the status bar and both tree panes are landmarks");
            Assert.That(landmarks.Select(AutomationProperties.GetAccessibilityView),
                Has.All.GreaterThanOrEqualTo(AccessibilityView.Control),
                "every landmark sits at Control or above — Narrator ignores landmarks below that view (AP-04). "
                + "Note the enum's default is Default(0), which is BELOW Control, so this must be set explicitly");
            Assert.That(landmarks.Select(AutomationProperties.GetName), Has.All.Not.Null.And.All.Not.Empty,
                "and each is named, since a landmark the user cannot tell apart is not navigable");
        });
    }

    // The status bar's text control: the one bound to the view-model's StatusText inside the named StatusBar border.
    private static TextBlock StatusTextBlock(MainWindow window) =>
        window.FindControl<Border>("StatusBar")!.GetVisualDescendants().OfType<TextBlock>().First();
}
