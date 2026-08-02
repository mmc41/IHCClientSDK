using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// crudarch T010 (proposal §3.2): the explicit <see cref="ShellContext"/> model — every availability trigger
/// (selection, pane-active, mode, clipboard, document state) funnels through ONE RebuildContext() and is
/// announced by ONE ContextChanged event, projecting ids and VALUE flags only — never a live node or Project.
/// Each trigger produces exactly one rebuilt context with the right values.
/// </summary>
public class ShellContextTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm)> BuildAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        return (harness, vm);
    }

    [Test]
    public async Task SelectionTrigger_RebuildsOnce_WithNodeValues()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        var node = vm.InstallationNodes[0].Children[0];   // a locality row
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        vm.SelectNode(node);

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one selection change -> exactly one rebuilt context");
            Assert.That(vm.Context.Node, Is.Not.Null);
            Assert.That(vm.Context.Node!.Id, Is.EqualTo(node.ElementId));
            Assert.That(vm.Context.Node.Kind, Is.EqualTo(TreeNodeKind.Locality));
            Assert.That(vm.Context.Node.CanCut, Is.True, "a locality is cuttable — the flag is value-copied");
            Assert.That(vm.Context.Node.IsPin, Is.False);
            Assert.That(vm.Context.ProjectOpen, Is.True);
        });
    }

    [Test]
    public async Task PaneTrigger_RebuildsOnce_WithPaneFlag()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        bool before = vm.IsInstallationPaneActive;
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        vm.IsInstallationPaneActive = !before;

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one pane flip -> exactly one rebuilt context");
            Assert.That(vm.Context.InstallationPaneActive, Is.EqualTo(!before));
        });
    }

    [Test]
    public async Task ModeTrigger_RebuildsOnce_WithModeFlag()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        vm.IsProgrammingMode = true;

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one mode flip -> exactly one rebuilt context");
            Assert.That(vm.Context.IsProgrammingMode, Is.True);
        });
    }

    [Test]
    public async Task ClipboardTrigger_RebuildsOnce_WithClipboardValues()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        var node = vm.InstallationNodes[0].Children[0];
        vm.SelectNode(node);   // T012: a command parameter selects first — pre-select so Execute is purely the clipboard trigger
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        vm.CopyCommand.Execute(node);

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one clipboard change -> exactly one rebuilt context");
            Assert.That(vm.Context.Clipboard,
                Is.EqualTo(new ClipboardContext(node.ElementId!.Value, IsCut: false)),
                "the clipboard context is the (source id, is-cut) value pair");
        });
    }

    // review F03: the ONE-rebuild rule must hold on the FULL-REBUILD fallback path too, not only on the
    // reconcile path the other document test exercises. Leaving programming mode takes that path AND re-enters
    // two more triggers from inside the refresh — the mode assignment and the selection restore — so it used to
    // sweep the whole registry three times back to back for one transition.
    [Test]
    public async Task ModeExitTrigger_RebuildsOnce_DespiteTheFullRebuildFallback()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        var loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        vm.LeaveProgrammingModeCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one mode exit -> exactly one rebuilt context");
            Assert.That(vm.Context.IsProgrammingMode, Is.False, "…carrying the settled post-transition values");
            Assert.That(vm.Context.ProjectOpen, Is.True);
        });
    }

    [Test]
    public async Task DocumentTrigger_RebuildsOnce_WithHistoryValues()
    {
        var (harness, vm) = await BuildAsync();
        using var _ = harness;
        int rebuilds = 0;
        vm.ContextChanged += (_, _) => rebuilds++;

        await harness.Session.AddLocalityAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rebuilds, Is.EqualTo(1), "one document transition -> exactly one rebuilt context");
            Assert.That(vm.Context.CanUndo, Is.True, "the context carries the flags the undo/redo gates read");
            // Dirty state and the action-named labels are NOT snapshot (review F10): no gate reads them — the
            // title and the Edit-menu headers read them off the workflow, which is where they are asserted.
            Assert.That(harness.Session.IsDirty, Is.True);
            Assert.That(harness.Session.UndoLabel, Is.Not.Null, "the workflow names the action to undo");
        });
    }
}
