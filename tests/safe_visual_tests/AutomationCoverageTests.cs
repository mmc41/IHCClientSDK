using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The automation-surface coverage net: whole-assembly, not spot checks.
/// <para>
/// Avalonia keeps ONE tree — <c>AutomationPeer</c> feeds the platform accessibility API (UIA on Windows,
/// NSAccessibility, AT-SPI2) <em>and</em> every UI-automation driver. So an element a Windows automation client
/// cannot find is an element a screen reader cannot announce, and vice versa. These tests are the cheap CI
/// enforcement point for that: they walk the real peer tree of every window the app can open and assert the two
/// properties a driver and a screen reader both need — a non-empty accessible <b>name</b> (WCAG 4.1.2 / EN 301 549
/// clause 11) and a stable, locale-independent <b>AutomationId</b>.
/// </para>
/// <para>
/// The name assertion reads the PEER, not the attached property, so Avalonia's own resolution chain counts: a
/// Button whose Content is a string is already named, a field named through <c>LabeledBy</c> is already named.
/// Only what the chain leaves blank is reported.
/// </para>
/// <para>
/// AutomationId matters separately from Name because every visible string in this app is Danish: a driver keyed
/// on "Gem projekt som…" breaks the moment the wording changes. It also must not silently rest on the
/// <c>?? Owner.Name</c> fallback for anything a driver targets by contract — x:Name is a private detail that a
/// rename tool changes without a word.
/// </para>
/// </summary>
public class AutomationCoverageTests : AvaloniaTestBase
{
    /// <summary>Every dialog window the app can open. Each has a parameterless constructor (its data is applied by
    /// the static <c>ShowAsync</c>), so the whole set is walkable without a controller or a loaded project.</summary>
    private static readonly Type[] DialogWindows =
    {
        typeof(AboutWindow), typeof(AdvancedDimmerWindow),
        typeof(EnumDefinitionWindow), typeof(EnumTypeManagerWindow), typeof(ModemPropertiesWindow),
        typeof(ModuleMapWindow), typeof(NamePromptWindow), typeof(PinPropertiesWindow),
        typeof(ProductPropertiesWindow), typeof(ProjectInfoWindow), typeof(PropertiesWindow),
        typeof(ReportPickerWindow), typeof(SceneContainerWindow), typeof(SceneValueWindow),
        typeof(VariablePropertiesWindow),
    };

    // ── The roster itself ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The audits below walk <see cref="DialogWindows"/>, a hand-maintained list — so every one of them is only as
    /// complete as that list is, and a window added later is not reported as failing but silently never looked at.
    /// This closes the loop by reflecting the windows the app actually declares: a 17th dialog fails HERE, naming
    /// itself, rather than shipping with no accessible names, no ids, and no construction check at all.
    /// </summary>
    [Test]
    public void DialogWindowRoster_NamesEveryWindowTheAppDeclares()
    {
        var declared = typeof(MainWindow).Assembly.GetTypes()
            .Where(type => typeof(Window).IsAssignableFrom(type) && !type.IsAbstract)
            .ToList();
        Assert.That(declared, Is.Not.Empty, "sanity: the app declares window types");

        Assert.That(declared, Is.EquivalentTo(DialogWindows.Append(typeof(MainWindow))),
            "every window the app declares must be in this fixture's roster — a window missing from it is not audited "
            + "for accessible names, automation ids, or even whether it can be opened at all");
    }

    // ── Constructability ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A window that throws while loading its XAML is not merely inaccessible — it is unopenable, and no
    /// automation client or user ever reaches it. This caught <c>SceneContainerWindow</c> binding
    /// <c>ColumnDefinitions</c> to a <c>{StaticResource}</c> string (no conversion happens on that path, so the
    /// Scenarier dialog raised <c>InvalidCastException</c> every time it was opened).</summary>
    [AvaloniaTest]
    public void EveryDialogWindow_CanBeConstructedAndShown()
    {
        var failures = new List<string>();
        foreach (var type in DialogWindows)
        {
            try
            {
                var window = (Window)Activator.CreateInstance(type)!;
                CurrentTestWindow = window;
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.Close();
            }
            catch (Exception ex)
            {
                failures.Add($"{type.Name}: {ex.GetBaseException().Message}");
            }
        }

        Assert.That(failures, Is.Empty,
            "every dialog window loads and shows:\n  " + string.Join("\n  ", failures));
    }

    // ── Per-control coverage ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>QC-01 + QC-06 over the shell: every control a user can focus is announced by name and addressable
    /// by a stable id.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task Shell_EveryInteractiveControl_HasNameAndAutomationId()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(Audit(window), Is.Empty, Explain("MainWindow", Audit(window)));
    }

    /// <summary>The same walk over every dialog. Dialogs are where automation coverage rots unnoticed: they are
    /// opened rarely, and a field that announces nothing is invisible in a screenshot.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void EveryDialog_EveryInteractiveControl_HasNameAndAutomationId()
    {
        var failures = new List<string>();
        foreach (var type in DialogWindows)
        {
            var window = (Window)Activator.CreateInstance(type)!;
            CurrentTestWindow = window;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            failures.AddRange(Audit(window).Select(f => $"{type.Name} → {f}"));
            window.Close();
        }

        Assert.That(failures, Is.Empty, Explain("the dialogs", failures));
    }

    // ── Menus ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Every menu command carries a stable id. Menu items are the app's primary command surface and its
    /// most localization-exposed one — the headers are Danish and carry access-key underscores ("Gem projekt _som…"),
    /// so header-text addressing is both brittle and awkward to write. Ids also have to be unique: a driver that
    /// asks for one id and gets two elements cannot proceed.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryMenuBarItem_HasAStableUniqueAutomationId()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var items = MenuItems(menu).ToList();
        Assert.That(items, Has.Count.GreaterThan(40), "the menu bar's authored items are all reachable");

        AssertMenuIds(items, "the menu bar");
    }

    /// <summary>The node context flyout is the other half of the command surface (and the only home of several
    /// commands), so it carries ids on the same terms. They are <c>ctx.</c>-prefixed because the same command
    /// appears on both surfaces and a duplicated id would make either one ambiguous to a driver.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryContextMenuItem_HasAStableUniqueAutomationId()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(window.TryFindResource("NodeContextMenu", out var resource), Is.True,
            "the shared node context flyout is a window resource");
        var flyout = (MenuFlyout)resource!;
        var items = flyout.Items.OfType<MenuItem>().SelectMany(mi => new[] { mi }.Concat(MenuItems(mi))).ToList();
        Assert.That(items, Has.Count.GreaterThan(25), "the flyout's authored items are all reachable");

        AssertMenuIds(items, "the node context flyout");
        Assert.That(items.Select(AutomationProperties.GetAutomationId), Has.All.StartsWith("ctx."),
            "context-flyout ids are prefixed so they never collide with the menu bar's ids for the same command");
    }

    // ── Menu operability ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Finding an item is only half of automation — the client has to be able to OPERATE it. Avalonia 12.1.1's
    /// stock <c>MenuItemAutomationPeer</c> implements <c>IToggleProvider</c> and nothing else, so out of the box a
    /// menu item offers UI Automation no way to be invoked and a submenu no way to be opened. Measured against the
    /// running app, every menu item reported exactly one pattern: ScrollItem. That leaves the app's primary command
    /// surface drivable only by synthesized clicks at screen coordinates, and leaves a screen-reader user an item
    /// that announces no action at all — so the shell supplies <c>AccessibleMenuItem</c>, whose peer adds Invoke and
    /// ExpandCollapse (the same treatment <c>AccessibleTreeView</c> already gives tree nodes).
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task EveryMenuItem_ExposesThePatternItsRoleNeeds()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var failures = new List<string>();
        foreach (var item in MenuItems(menu))
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(item);
            var id = AutomationProperties.GetAutomationId(item);

            // A submenu host must be openable; a leaf must be invokable. Both are asked for through GetProvider,
            // which is the exact call the Windows UIA bridge makes — a peer that merely *implements* the interface
            // but does not surface it through GetProviderCore is invisible to a driver, and this catches that.
            if (item.HasSubMenu)
            {
                if (peer.GetProvider<IExpandCollapseProvider>() is null)
                    failures.Add($"{id}: submenu host exposes no ExpandCollapse");
            }
            else if (peer.GetProvider<IInvokeProvider>() is null)
            {
                failures.Add($"{id}: command item exposes no Invoke");
            }
        }

        Assert.That(failures, Is.Empty,
            "every menu item offers UI Automation the pattern its role needs:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>A menu separator must stay a separator. It is not a menu item and must never be wrapped into one:
    /// a wrapped separator reaches an automation client as a nameless, invokable row, so a driver enumerating the
    /// File menu sees eleven "commands" of which three do nothing — and a screen reader reads the blanks out.
    /// (Avalonia's own container rule treats a Separator as its own container; a subclass that generates containers
    /// has to keep that, which is exactly what this caught.)</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MenuSeparators_StaySeparators_AndAreNotInvokableItems()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var file = MenuItems(menu).First(mi => AutomationProperties.GetAutomationId(mi) == "MenuFile");

        // Open it, because the assertion is about the realized CONTAINERS, not the declared items: the items
        // collection keeps its Separator objects either way, so asserting on `file.Items` passes even while every
        // separator is being wrapped in a menu item. What a driver sees is the container.
        ControlAutomationPeer.CreatePeerForElement(file).GetProvider<IExpandCollapseProvider>()!.Expand();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var wrapped = file.GetRealizedContainers()
            .OfType<MenuItem>()
            .Where(c => string.IsNullOrEmpty(AutomationProperties.GetAutomationId(c)))
            .ToList();

        Assert.That(wrapped, Is.Empty,
            $"a menu separator must not be realized as a menu item ({wrapped.Count} nameless, invokable rows would "
            + "reach an automation client and be read out by a screen reader)");
        Assert.That(file.GetRealizedContainers().OfType<Separator>().Count(), Is.EqualTo(4),
            "the File menu's four separators are realized as Separator controls");
    }

    /// <summary>The pattern has to actually work, not merely be advertised: invoking a leaf must run its command.
    /// Driven on the toolbar toggle because its whole effect is one observable view-model flag — no dialog, no file
    /// system, no project mutation.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task InvokingAMenuItemThroughAutomation_RunsItsCommand()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var toggle = MenuItems(menu).Single(mi => AutomationProperties.GetAutomationId(mi) == "view.toggleToolbar");
        bool before = viewModel.IsToolbarVisible;

        var invoke = ControlAutomationPeer.CreatePeerForElement(toggle).GetProvider<IInvokeProvider>();
        Assert.That(invoke, Is.Not.Null, "the item advertises Invoke");
        invoke!.Invoke();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(viewModel.IsToolbarVisible, Is.Not.EqualTo(before),
            "invoking the item through UI Automation ran its command, exactly as a click would");
    }

    /// <summary>
    /// Invoking a menu item through automation must also CLOSE the menu, exactly as clicking it does.
    /// <para>Avalonia splits the two: raising <c>MenuItem.ClickEvent</c> runs the command, while the menu is
    /// dismissed separately by the interaction handler on pointer-release. A peer that only raises the click
    /// therefore leaves the menu standing open over the app — and every automation client treats "the menu is
    /// still realized" as "the invoke did not take", so a command that genuinely ran reports as failed. The
    /// aui-openvisual driver did exactly that: `locality insert` returned MutationFailed while the locality had
    /// in fact been inserted (the next undo announced "Fortrød: Indsæt lokalitet").</para>
    /// </summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task InvokingAMenuItemThroughAutomation_ClosesTheMenu()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var view = MenuItems(menu).First(mi => AutomationProperties.GetAutomationId(mi) == "MenuView");
        ControlAutomationPeer.CreatePeerForElement(view).GetProvider<IExpandCollapseProvider>()!.Expand();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(menu.IsOpen, Is.True, "precondition: the menu is open");

        var toggle = MenuItems(menu).Single(mi => AutomationProperties.GetAutomationId(mi) == "view.toggleToolbar");
        ControlAutomationPeer.CreatePeerForElement(toggle).GetProvider<IInvokeProvider>()!.Invoke();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.That(menu.IsOpen, Is.False,
            "invoking a command item dismisses the menu, as a click does — a menu left open reads to every "
            + "automation client as an invoke that did not take");
    }

    /// <summary>And ExpandCollapse has to actually open the submenu — the step every driver needs before it can
    /// reach anything inside a menu.</summary>
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task ExpandingAMenuThroughAutomation_OpensItsSubmenu()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var menu = window.GetLogicalDescendants().OfType<Menu>().First();
        var file = MenuItems(menu).First(mi => AutomationProperties.GetAutomationId(mi) == "MenuFile");
        var provider = ControlAutomationPeer.CreatePeerForElement(file).GetProvider<IExpandCollapseProvider>();
        Assert.That(provider, Is.Not.Null, "the File menu advertises ExpandCollapse");

        Assert.That(provider!.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed),
            "a closed menu reports Collapsed, not LeafNode");

        provider.Expand();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Multiple(() =>
        {
            Assert.That(file.IsSubMenuOpen, Is.True, "Expand() opens the menu");
            Assert.That(provider.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Expanded));
        });

        provider.Collapse();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.That(file.IsSubMenuOpen, Is.False, "Collapse() closes it again");
    }

    // ── Windows themselves ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>A driver has to identify the window it just opened before it can touch anything inside it. Titles
    /// are Danish and several are set at runtime from project data ("Rediger egenskaber" becomes the node's name),
    /// so the window itself needs an id that does not move.</summary>
    [AvaloniaTest]
    public void EveryWindow_HasAStableAutomationId()
    {
        var ids = new Dictionary<string, string>();
        var missing = new List<string>();

        foreach (var type in DialogWindows.Append(typeof(MainWindow)))
        {
            var window = (Window)Activator.CreateInstance(type)!;
            CurrentTestWindow = window;
            window.Show();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            var id = ControlAutomationPeer.CreatePeerForElement(window).GetAutomationId();
            if (string.IsNullOrWhiteSpace(id))
                missing.Add(type.Name);
            else if (!ids.TryAdd(id, type.Name))
                missing.Add($"{type.Name}: id '{id}' is already used by {ids[id]}");

            window.Close();
        }

        Assert.That(missing, Is.Empty,
            "every window exposes a unique AutomationId:\n  " + string.Join("\n  ", missing));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Reports every focusable control in <paramref name="window"/> that a driver or screen reader could
    /// not resolve. Walks the LOGICAL tree, which is exactly the authored surface: control-template internals (a
    /// ComboBox's toggle button, a NumericUpDown's spinner) live in the visual tree only and are the theme's
    /// responsibility, not the app's.</summary>
    private static List<string> Audit(Window window)
    {
        var failures = new List<string>();
        foreach (var control in window.GetLogicalDescendants().OfType<Control>())
        {
            // Menu items are audited separately (by id, across both surfaces); a closed menu's items are not
            // focusable here anyway.
            if (control is MenuItem || control is not InputElement { Focusable: true })
                continue;

            var peer = ControlAutomationPeer.CreatePeerForElement(control);
            var name = peer.GetName();
            var id = peer.GetAutomationId();
            var what = $"{control.GetType().Name}(name='{name}', id='{id}')";

            if (string.IsNullOrWhiteSpace(name))
                failures.Add($"{what} has no accessible name");

            // Containers realized from an ItemsSource are DATA: their identity is the bound item, and the
            // container theme supplies whatever id the data can offer (the tree rows publish their node kind).
            // Everything authored in markup must carry its own id.
            if (string.IsNullOrWhiteSpace(id) && !IsDataRealized(control))
                failures.Add($"{what} has no AutomationId");
        }
        return failures;
    }

    private static bool IsDataRealized(Control control) =>
        control.Parent is ItemsControl { ItemsSource: not null };

    /// <summary>Every MenuItem in a subtree, including nested submenus, in authored order.</summary>
    private static IEnumerable<MenuItem> MenuItems(ILogical root)
    {
        foreach (var child in root.GetLogicalChildren())
        {
            if (child is MenuItem item)
            {
                yield return item;
                foreach (var nested in MenuItems(item))
                    yield return nested;
            }
            else
            {
                foreach (var nested in MenuItems(child))
                    yield return nested;
            }
        }
    }

    private static void AssertMenuIds(IReadOnlyList<MenuItem> items, string surface)
    {
        var unnamed = items
            .Where(mi => string.IsNullOrWhiteSpace(AutomationProperties.GetAutomationId(mi)))
            .Select(mi => $"{mi.Header}")
            .ToList();
        Assert.That(unnamed, Is.Empty,
            $"every item on {surface} carries an explicit AutomationId; these do not:\n  "
            + string.Join("\n  ", unnamed));

        var duplicates = items
            .GroupBy(AutomationProperties.GetAutomationId)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ×{g.Count()}")
            .ToList();
        Assert.That(duplicates, Is.Empty,
            $"ids on {surface} are unique:\n  " + string.Join("\n  ", duplicates));
    }

    private static string Explain(string what, IReadOnlyCollection<string> failures) =>
        $"every interactive control in {what} is announced by name and addressable by a stable AutomationId "
        + $"({failures.Count} gap(s)):\n  " + string.Join("\n  ", failures);
}
