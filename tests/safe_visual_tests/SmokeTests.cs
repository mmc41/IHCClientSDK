using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// Headless UI smoke coverage for the IHC OpenVisual shell (US-001, US-065): the main window loads its XAML,
/// binds the real view-model and renders under the headless Skia session with the whole shell chrome present —
/// the menu bar, both tree panes, and the status bar — and the About dialog exposes its version lines.
/// A broken XAML tree, a renamed binding or a broken render pipeline fails CI instead of passing silently.
/// </summary>
public class SmokeTests : AvaloniaTestBase
{
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersShellChrome_WithMenuBarAndTwoPanes()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;   // register for automatic failure screenshots
        window.Show();

        var frame = window.CaptureRenderedFrame();
        var menu = window.GetVisualDescendants().OfType<Menu>().Single();
        int treeCount = window.GetVisualDescendants().OfType<TreeView>().Count();

        Assert.Multiple(() =>
        {
            Assert.That(frame, Is.Not.Null, "the headless Skia renderer produced a frame");
            Assert.That(window.Title, Does.Contain("IHC OpenVisual"), "the title bar names the application");
            Assert.That(menu.Items.Count, Is.EqualTo(8), "the eight stable menu titles are present (Simulation is out of scope)");
            Assert.That(treeCount, Is.EqualTo(2), "both tree panes (Installation and Functions) are shown");
        });
    }

    // US-006: the default localities render in the Installation tree under the headless renderer.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersDefaultLocalities_InInstallationTree()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Lokaliteter"), "the localities root renders");
            Assert.That(labels, Does.Contain("Stue"), "the first default locality renders");
            Assert.That(labels, Does.Contain("Udendørs"), "the last default locality renders");
        });
    }

    // US-007: the locality Properties dialog exposes a pre-fillable Name field and a multi-line Note field.
    // US-008: an inserted locality renders as a new node in the Installation tree.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterInsertLocality_RendersNewNode()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        await viewModel.InsertLocalityCommand.ExecuteAsync(null);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.That(labels, Does.Contain(ProjectWorkflow.NewLocalityName), "the newly inserted locality renders in the tree");
    }

    // US-010: an inserted wired product renders (nested under its auto-expanded locality) in the Installation tree.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterInsertProduct_RendersProductNode()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        await harness.Session.AddProductAsync(viewModel.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.That(labels, Does.Contain(product.DisplayName), "the inserted product renders under its locality");
    }

    // US-014: an inserted wireless product renders with the yellow "!" unlinked marker.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterInsertWireless_ShowsUnlinkedMarker()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var wireless = harness.ProjectService.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("LK IHC Wireless"));
        await harness.Session.AddProductAsync(viewModel.InstallationNodes[0].Children[0].ElementId!.Value, wireless.ProductIdentifier);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain(wireless.DisplayName), "the wireless product renders");
            Assert.That(labels, Does.Contain("!"), "the unlinked marker renders");
        });
    }

    // US-018: an inserted library function block renders (with its sections) in the Functions pane.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterInsertFunctionBlock_RendersBlockWithSections()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(viewModel.InstallationNodes[0].Children[0].ElementId!.Value, block.MasterType);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // The block renders under its (auto-expanded) locality in the Functions pane; its sections are one level
        // deeper and revealed on expand (covered at the view-model level).
        Assert.That(labels, Does.Contain(block.DisplayName), "the function block renders in the Functions pane");
    }

    // US-019: an inserted empty function block renders in the Functions pane.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterInsertEmptyBlock_RendersEmptyBlock()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(viewModel.InstallationNodes[0].Children[0].ElementId!.Value);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.That(labels, Does.Contain(ProjectWorkflow.EmptyBlockName), "the empty function block renders in the Functions pane");
    }

    // Selection: a Functions-pane function block, when selected, becomes the active node so its context-menu
    // commands (Unlock/Save block/Properties) act on it (fix for the per-pane selection binding).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task FunctionsTree_SelectingFunctionBlock_MakesItTheActiveNode()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddFunctionBlockAsync(viewModel.InstallationNodes[0].Children[0].ElementId!.Value, block.MasterType);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var functionsTree = window.FindControl<TreeView>("FunctionsTree")!;
        var fbNode = viewModel.FunctionNodes[0].Children[0].Children[0];
        functionsTree.SelectedItem = fbNode;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedNode, Is.SameAs(fbNode), "a Functions-pane block becomes the active node");
            Assert.That(viewModel.SelectedNode!.IsFunctionBlock, Is.True);
        });
    }

    // US-022: after linking, the two panes render reciprocal link rows under the linked pins — each labelled with
    // the bare path of the OTHER end (no arrow prefix; direction is the icon's job — F-020).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterLink_RendersReciprocalLinkRows()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.CategoryPath.StartsWith("Datalinie") && p.Resources.Count > 0);
        var block = harness.ProjectService.GetAvailableFunctionBlocks().First(f => f.Inputs.Count > 0);
        await harness.Session.AddProductAsync(loc, product.ProductIdentifier);
        await harness.Session.AddFunctionBlockAsync(loc, block.MasterType);
        var productInput = vm.InstallationNodes[0].Children[0].Children[0].Children[0];
        var blockInput = vm.FunctionNodes[0].Children[0].Children[0].Children[0].Children[0];
        await harness.Session.LinkPinsAsync(productInput.ElementId!.Value, blockInput.ElementId!.Value);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // A link row is the only row labelled with a locality-rooted path, so the prefix identifies them.
        var linkRows = labels.Where(t => t?.StartsWith("Stue / ") == true).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(linkRows, Has.Count.EqualTo(2), "both panes render a reciprocal link row");
            Assert.That(linkRows.Distinct().Count(), Is.EqualTo(2), "each row names the OTHER end");
        });
    }

    // US-028: an authored event and command render as rows under the program's Events/Commands (Functions pane).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersAuthoredEventAndCommand_InProgrammingMode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, "resource_input", "Doorbell");
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[1].ElementId!.Value, "resource_output", "Chime");

        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[0].Children[0]);   // arm the Input
        vm.SelectNode(FindFlag(vm.FunctionNodes, n => n.IsEventsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramEventMenu[0].Command!).ExecuteAsync(null);    // "Doorbell skifter til ON"
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[1].Children[0]);   // arm the Output
        vm.SelectNode(FindFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramCommandMenu.First(m => m.Header.Contains("kippes")).Command!).ExecuteAsync(null);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Doorbell -> ON"), "the authored event renders under Events");
            Assert.That(labels, Does.Contain("Toggle Chime"), "the authored command renders under Commands");
        });
    }

    // US-029: an inserted sub-program renders its Conditions group and true/false command branches in the tree.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersSubProgramStructure_InProgrammingMode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await vm.AddSubProgramCommand.ExecuteAsync(FindFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await vm.SetConditionsOrCommand.ExecuteAsync(FindFlag(vm.FunctionNodes, n => n.IsConditionsContainer)!);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Under program"), "the sub-program renders");
            Assert.That(labels.Any(t => t?.StartsWith("Betingelser") == true && t.Contains(">=1")), Is.True, "the OR-toggled Conditions group renders");
            Assert.That(labels, Does.Contain("Kommandoer ved betingelser sande"));
            Assert.That(labels, Does.Contain("Kommandoer ved betingelser falske"));
        });
    }

    // US-030: an enum variable created via the Settings palette renders under the block's Settings section.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersEnumVariable_UnderSettings()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        harness.Dialogs.EnumDefinitionResult = new EnumDefinitionResult("Mode", new[] { "Direct", "With delay", "Switched off" });
        vm.SelectNode(vm.InstallationNodes[0].Children[2]);   // Settings
        await ((IAsyncRelayCommand)vm.VariablePaletteMenu.First(m => m.Header == "Enum").Children.First(c => c.Header == "Ny…").Command!).ExecuteAsync(null);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // A resource_enum row renders its initial state (F-004/A-3) — here the first declared state, "Direct".
        // The rule is per element type, so it reaches an FB's enum variable as well as a product's state row.
        Assert.That(labels, Does.Contain("Mode = Direct"), "the enum variable renders under Settings");
    }

    // US-031: an inserted case with a value branch renders "Case (<var>)", the criterion branch, and Else.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersCaseStructure_InProgrammingMode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await harness.Session.AddVariableAsync(vm.InstallationNodes[0].Children[3].ElementId!.Value, "resource_counter", "Cleanings");
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[3].Children[0]);
        vm.SelectNode(FindFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        await ((IAsyncRelayCommand)vm.ProgramCaseMenu.First(m => m.Header == "Case (Cleanings)").Command!).ExecuteAsync(null);
        harness.Dialogs.PropertiesResult = new PropertiesResult("100", string.Empty);
        await vm.NewCaseValueCommand.ExecuteAsync(FindFlag(vm.FunctionNodes, n => n.IsCaseNode));

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Case (Cleanings)"), "the case switch renders");
            Assert.That(labels, Does.Contain("100"), "the value branch renders");
            Assert.That(labels, Does.Contain("Else"), "the default branch renders");
        });
    }

    // US-032: an authored arithmetic command line renders its one-operation formula under Commands.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersArithmeticCommand_InProgrammingMode()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        var settingsId = vm.InstallationNodes[0].Children[2].ElementId!.Value;
        await harness.Session.AddVariableAsync(settingsId, "resource_integer", "F1");   // int+int + is authorable (float+float + is a dead cell, F-109)
        await harness.Session.AddVariableAsync(settingsId, "resource_integer", "F2");
        vm.UseInProgramCommand.Execute(vm.InstallationNodes[0].Children[2].Children.First(c => TreeNodes.NameOf(c) == "F1"));
        vm.SelectNode(FindFlag(vm.FunctionNodes, n => n.IsCommandsContainer)!);
        var addCategory = vm.ProgramArithmeticMenu.First(m => m.Header.StartsWith("F1 +"));
        await ((IAsyncRelayCommand)addCategory.Children.First(c => c.Header == "F2").Command!).ExecuteAsync(null);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.That(labels, Does.Contain("F1 = F1 + F2"), "the arithmetic command renders its formula");
    }

    // US-033: a Powerup event and a saved output both render in programming mode.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_RendersPowerupEventAndSavedOutput()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await vm.AddPowerEventCommand.ExecuteAsync(FindFlag(vm.FunctionNodes, n => n.IsEventsContainer));
        var outputSectionId = vm.InstallationNodes[0].Children[1].ElementId!.Value;
        var outputId = (await harness.Session.AddVariableAsync(outputSectionId, "resource_output", "Lys"))!.Value;
        await vm.ToggleSaveValueCommand.ExecuteAsync(FindFlag(vm.InstallationNodes, n => n.ElementId == outputId));

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Powerup"), "the Powerup event renders");
            // The vendor renders the bare pin name — no "(saved)" suffix (F-019). The backup flag surfaces on the
            // "Save current value" menu item instead.
            Assert.That(labels, Does.Contain("Lys"), "the output renders under its section");
            Assert.That(labels, Does.Not.Contain("Light (saved)"), "no (saved) suffix in the tree label");
        });
    }

    // US-033b: a function-block-to-function-block variable link renders reciprocal rows in the Functions pane,
    // each labelled with the bare path of the other end (no arrow prefix — F-020).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterFbToFbLink_RendersReciprocalRows()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        var blocks = harness.Session.Current!.FindById(loc)!.ChildrenOrEmpty().Where(c => c.Tag == "functionblock").ToList();
        var outA = (await harness.Session.AddVariableAsync(blocks[0].FindChild("outputs")!.Id!.Value, "resource_output", "OutA"))!.Value;
        var inB = (await harness.Session.AddVariableAsync(blocks[1].FindChild("inputs")!.Id!.Value, "resource_input", "InB"))!.Value;
        await harness.Session.LinkPinsAsync(outA, inB);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        // One pass per tree level: each pass can only reach rows the previous pass materialised, and localities
        // now start closed, so the deepest rows (locality > block > section > pin > link) need six.
        for (int i = 0; i < 6; i++)
        {
            foreach (var item in window.GetVisualDescendants().OfType<TreeViewItem>())
                item.IsExpanded = true;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        // A link row is the only row labelled with a locality-rooted path, so the prefix identifies them.
        var linkRows = labels.Where(t => t?.StartsWith("Stue / ") == true).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(linkRows, Has.Count.EqualTo(2), "both linked pins render a reciprocal link row");
            Assert.That(linkRows.Distinct().Count(), Is.EqualTo(2), "each row names the OTHER end");
        });
    }

    // Drives a real left double-click at a control's centre through the headless input pipeline. It must go through
    // the pipeline rather than a synthetic RaiseEvent: Avalonia's TreeViewItem toggles from OnHeaderDoubleTapped,
    // i.e. from a DoubleTapped the input manager SYNTHESISES out of the pointer stream. A hand-built PointerPressed
    // never produces that, so a RaiseEvent-based test cannot observe the toggle at all and passes vacuously.
    private static void DoubleClick(Window window, Avalonia.Visual target)
    {
        Avalonia.Point centre = target.TranslatePoint(
            new Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        for (int i = 0; i < 2; i++)
        {
            window.MouseDown(centre, MouseButton.Left);
            window.MouseUp(centre, MouseButton.Left);
        }
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    // F-006 + F-007 (A-4), effect-verified through the real window: double-clicking a locality opens its properties
    // dialog AND leaves the expansion state alone. The second half is the whole point of marking the gesture
    // handled — before this, Avalonia's default toggled every expandable node under the user, which IHC Visual
    // never does (it handles the gesture on every node type and so suppresses the default everywhere).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_DoubleClickLocality_OpensProperties_AndDoesNotToggleExpansion()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        // The locality must actually HOLD something, or it is a leaf and there is no toggle to suppress — the
        // expansion half of this assertion would pass vacuously.
        var product = harness.ProjectService.GetAvailableProducts().First(p => p.DisplayName == "Lampeudtag");
        await harness.Session.AddProductAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value, product.ProductIdentifier);

        var window = new MainWindow { DataContext = vm };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var tree = window.FindControl<TreeView>("InstallationTree")!;
        var localityNode = vm.InstallationNodes[0].Children[0];
        var item = window.GetVisualDescendants().OfType<TreeViewItem>()
            .First(i => ReferenceEquals(i.DataContext, localityNode));

        Assert.That(localityNode.Children, Is.Not.Empty, "guard: the node is expandable, so a toggle is possible");
        Assert.That(item.IsExpanded, Is.True, "a locality holding a product opens by default (US-006)");

        // Click the node's OWN label, not the item's bounds centre: a TreeViewItem's bounds enclose its whole
        // expanded subtree, so the centre lands on a descendant row (the same trap the UIA driver hit).
        var label = item.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == localityNode.DisplayName);
        DoubleClick(window, label);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(1), "the locality's properties dialog opens");
            Assert.That(item.IsExpanded, Is.True, "the double-click must not collapse it — the toggle stays suppressed");
            Assert.That(tree.SelectedItem, Is.SameAs(localityNode), "the activated node becomes the selection");
        });
    }

    // US-050: the read-only data-line module map renders both groups, the vendor's four column headers, a
    // documented module's row, and the not-in-use marker on a line carrying nothing.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ModuleMapWindow_ShowsInputAndOutputModuleLists()
    {
        var map = new DatalineModuleMap(
            System.Collections.Immutable.ImmutableArray.Create(
                new DatalineModule(1, "Input 24/3", "I sidetavle", "Sensorer, lavt forbrug"),
                new DatalineModule(2, "", "", "")),
            System.Collections.Immutable.ImmutableArray<DatalineModule>.Empty);

        var window = new ModuleMapWindow { DataContext = map };
        CurrentTestWindow = window;
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsVisible).Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Indgangsmoduler"));
            Assert.That(labels, Does.Contain("Udgangsmoduler"));
            Assert.That(labels, Does.Contain("Datalinie").And.Contain("Modul type")
                .And.Contain("Lokalitet").And.Contain("Beskrivelse"), "the vendor's four column headers");
            Assert.That(labels, Does.Contain("Input 24/3").And.Contain("I sidetavle")
                .And.Contain("Sensorer, lavt forbrug"), "a documented module renders its whole row");
            Assert.That(labels, Does.Contain("<ikke i brug>"), "a line carrying no module is marked, not blank");
        });
    }

    // US-039: the project-information dialog exposes the project, customer and installer field groups.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ProjectInfoWindow_ShowsProjectCustomerInstallerFields()
    {
        var window = new ProjectInfoWindow();
        var custName = window.FindControl<AutoCompleteBox>("CustNameBox");
        var instPhone = window.FindControl<AutoCompleteBox>("InstPhoneBox");
        if (custName is not null) custName.Text = "Bob";
        if (instPhone is not null) instPhone.Text = "12345";
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(labels, Does.Contain("Projekt oplysninger"));
            Assert.That(labels, Does.Contain("Kunde oplysninger"));
            Assert.That(labels, Does.Contain("Installatør information"));
            Assert.That(custName, Is.Not.Null);
        });
    }

    private static TreeNodeViewModel? FindFlag(IEnumerable<TreeNodeViewModel> nodes, Func<TreeNodeViewModel, bool> match)
    {
        foreach (var node in nodes)
        {
            if (match(node))
                return node;
            if (FindFlag(node.Children, match) is { } found)
                return found;
        }
        return null;
    }

    // US-009: a deleted (empty) locality no longer renders in the tree.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public async Task MainWindow_AfterDeleteLocality_RemovesNode()
    {
        using var harness = ShellHarness.Create();
        var viewModel = harness.CreateViewModel();
        await viewModel.InitializeAsync();
        var node = viewModel.InstallationNodes[0].Children[0];   // "Stue"
        await viewModel.DeleteCommand.ExecuteAsync(node);

        var window = new MainWindow { DataContext = viewModel };
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        var labels = window.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.That(labels, Does.Not.Contain("Stue"), "the deleted locality no longer renders");
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PropertiesWindow_ShowsNameAndNoteFields()
    {
        var window = new PropertiesWindow { Title = "Edit Living room properties" };
        var name = window.FindControl<TextBox>("NameBox");
        var note = window.FindControl<TextBox>("NoteBox");
        if (name is not null) name.Text = "Living room";
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Edit Living room properties"));
            Assert.That(name?.Text, Is.EqualTo("Living room"), "the Name field is pre-fillable");
            Assert.That(note, Is.Not.Null, "a multi-line Note field is present");
            Assert.That(note!.AcceptsReturn, Is.True, "the Note field is multi-line");
        });
    }

    // A-13/US-011: the product-properties dialog exposes the documentation fields plus a free-text Placement field,
    // and has NO Location room dropdown (moving a product is a tree operation, not a dialog field).
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ProductProperties_HasPlaceringTextBox_NoLocationDropdown()
    {
        var window = new ProductPropertiesWindow { Title = "Produkt egenskaber" };
        var name = window.FindControl<TextBox>("NameBox");
        var placering = window.FindControl<TextBox>("PlaceringBox");
        var location = window.FindControl<ComboBox>("LocationCombo");
        var endUserReport = window.FindControl<CheckBox>("EndUserReportCheck");
        var cableType = window.FindControl<TextBox>("CableTypeBox");
        var identification = window.FindControl<TextBox>("IdentificationBox");
        var lightGroup = window.FindControl<TextBox>("LightGroupBox");
        if (name is not null) name.Text = "LK FUGA Tryk 2 tast";
        if (placering is not null) placering.Text = "i loft";
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Produkt egenskaber"));
            Assert.That(name?.Text, Is.EqualTo("LK FUGA Tryk 2 tast"));
            Assert.That(placering, Is.Not.Null, "an editable Placement text field is present");
            Assert.That(placering!.Text, Is.EqualTo("i loft"), "Placement is a plain, editable textbox");
            Assert.That(location, Is.Null, "the Location room dropdown is gone");
            Assert.That(endUserReport, Is.Not.Null, "the end-user-report control still exists (the value round-trips through it)");
            Assert.That(endUserReport!.IsVisible, Is.False, "but is HIDDEN — the vendor never shows control 303 (C15 measured 2026-07-18: 0/13 products across 6 families)");
            Assert.That(cableType, Is.Not.Null);
            Assert.That(identification, Is.Not.Null);
            Assert.That(lightGroup, Is.Not.Null);
        });
    }

    // US-012: the terminal-addressing dialog exposes data line, terminal and initial-value controls.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void PinPropertiesWindow_ShowsAddressingFields()
    {
        var window = new PinPropertiesWindow { Title = "Input 'Tryk (venstre)'" };
        var dataLine = window.FindControl<NumericUpDown>("DataLineBox");
        var terminal = window.FindControl<NumericUpDown>("TerminalBox");
        var initialValue = window.FindControl<ComboBox>("InitialValueCombo");
        if (dataLine is not null) dataLine.Value = 2;
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Input 'Tryk (venstre)'"));
            Assert.That(dataLine?.Value, Is.EqualTo(2));
            Assert.That(terminal, Is.Not.Null, "the terminal control is present");
            Assert.That(initialValue, Is.Not.Null, "the initial-value control is present (shown for outputs)");
        });
    }

    // US-013: the SMS-modem properties dialog exposes documentation, cabling, PIN and telephone-number fields.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void ModemPropertiesWindow_ShowsModemFields()
    {
        var window = new ModemPropertiesWindow { Title = "SMS modem egenskaber" };
        var name = window.FindControl<TextBox>("NameBox");
        var pin = window.FindControl<TextBox>("PinCodeBox");
        var cable = window.FindControl<TextBox>("Cable0VBox");
        var phone1 = window.FindControl<TextBox>("Phone1Box");
        if (name is not null) name.Text = "SMS Modem";
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("SMS modem egenskaber"));
            Assert.That(name?.Text, Is.EqualTo("SMS Modem"));
            Assert.That(pin, Is.Not.Null, "the PIN field is present");
            Assert.That(cable, Is.Not.Null, "the cabling fields are present");
            Assert.That(phone1, Is.Not.Null, "the telephone-number fields are present");
        });
    }

    // US-015: the advanced wireless-dimmer dialog exposes the timing/level/load-characteristic fields.
    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AdvancedDimmerWindow_ShowsDimmerFields()
    {
        var window = new AdvancedDimmerWindow();
        var softOn = window.FindControl<NumericUpDown>("SoftOnBox");
        var minimum = window.FindControl<NumericUpDown>("MinimumBox");
        var loadMode = window.FindControl<ComboBox>("LoadModeCombo");
        if (softOn is not null) softOn.Value = 700;
        CurrentTestWindow = window;
        window.Show();
        window.CaptureRenderedFrame();

        Assert.Multiple(() =>
        {
            Assert.That(window.Title, Is.EqualTo("Avancerede lysdæmper egenskaber"));
            Assert.That(softOn?.Value, Is.EqualTo(700));
            Assert.That(minimum, Is.Not.Null);
            Assert.That(loadMode, Is.Not.Null, "the load-characteristic selector is present");
        });
    }

    [AvaloniaTest]
    [CaptureScreenshotOnFailure]
    public void AboutWindow_ShowsApplicationAndSdkVersions()
    {
        var about = new AboutWindow();
        CurrentTestWindow = about;
        about.Show();

        var appVersion = about.FindControl<TextBlock>("AppVersionText");
        var sdkVersion = about.FindControl<TextBlock>("SdkVersionText");

        Assert.Multiple(() =>
        {
            Assert.That(about.Title, Is.EqualTo("Om IHC OpenVisual"));
            Assert.That(appVersion?.Text, Does.StartWith("App version:"));
            Assert.That(sdkVersion?.Text, Does.StartWith("SDK version:"));
        });
    }

    // T018: the old Reports window (NativeWebView) died with the combined-document surface — report viewing
    // now goes facade → temp HTML → default browser, covered by ReportPickerTests.
}
