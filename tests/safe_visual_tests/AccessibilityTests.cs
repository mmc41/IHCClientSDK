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
            Assert.That(unlinked.AccessibleName, Does.Contain("not linked"), "and the unlinked state is announced");
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

        // The root "Localities" node has children, so it is a genuine expand/collapse target.
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

        var product = harness.Session.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
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
        var window = new PropertiesWindow { Title = "Edit properties" };
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
}
