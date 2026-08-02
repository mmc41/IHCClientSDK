using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ihc_openvisual.Views;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// US-044/US-045 (11-interaction-model §keyboard, MUST): <c>Shift+F10</c> opens the selected node's context menu
/// without the mouse. Both panes carry the shared flyout on <c>ContextFlyout</c> — a MenuFlyout resource, because a
/// ContextMenu control cannot be shared between two trees — so the handler must open THAT. Reading
/// <c>TreeView.ContextMenu</c> finds null, and the key is then swallowed (the handler marks it handled) while doing
/// nothing at all.
/// </summary>
public class KeyboardContextMenuTests
{
    [AvaloniaTest]
    public async Task ShiftF10_OpensTheSelectedNodesContextMenu()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var tree = window.FindControl<TreeView>("InstallationTree")!;
        vm.SelectedInstallationNode = vm.InstallationNodes[0].Children[0];   // a locality
        Dispatcher.UIThread.RunJobs();
        tree.GetVisualDescendants().OfType<TreeViewItem>().First().Focus();   // the tree's KeyDown handler services F10
        Dispatcher.UIThread.RunJobs();

        var flyout = (MenuFlyout)tree.ContextFlyout!;
        Assert.That(flyout.IsOpen, Is.False, "precondition: the flyout starts closed");

        window.KeyPress(Key.F10, RawInputModifiers.Shift, PhysicalKey.F10, null);
        Dispatcher.UIThread.RunJobs();

        Assert.That(flyout.IsOpen, Is.True, "Shift+F10 opens the node context menu the panes actually carry");

        window.Close();
    }
}
