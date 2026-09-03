using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Alignment F-48 — a <b>Betingelser</b> group's properties dialog.
///
/// <para>Measured live 2026-08-11 on an UNLOCKED <c>4.1.01. AND ("Og"- blok)</c> in programming mode (the lock
/// state matters: a locked block's conditions flyout carries <i>Egenskaber…</i> and nothing else). The reference
/// application's <c>Rediger Betingelser egenskaber</c> holds three things:</para>
/// <code>
///   Navn                 (Edit 213)  "Betingelser"
///   Note                 (Edit 214)
///   Logisk betingelse    (ComboBox 251, items ["AND","OR"], selectedIndex 0)   ← a captioned group
/// </code>
///
/// <para>OpenVisual opened <b>no dialog at all</b> — verified by three routes, the flyout item, a double-click and
/// F2, all of which returned without raising one — while still listing <i>Egenskaber…</i> on the flyout. A menu
/// item that answers nothing reads as a command that failed, and it left the group's Navn and Note unreachable.</para>
///
/// <para>The operator itself was never missing: OpenVisual offers it on the flyout as a <i>Logisk betingelse</i>
/// submenu (AND/OR), which is the reference application's own caption for this very field. What was missing is the
/// dialog the caption comes from — so this fixes the surface, not the capability (that distinction is what
/// [F-23] got wrong).</para>
///
/// <para><b>What this fixture asserts, and why only that.</b> A parity fixture exists to prove the ROUTE
/// -- that the dialog's result reaches the command and the command reaches the document -- so it asserts the
/// route and keeps ONE observable effect at the far end as the evidence that the value which arrived came
/// from the dialog. That Navn/Note commit is owned elsewhere, by the generic rename this dialog shares with
/// every other element; re-asserting it here would be the same fact told twice.
/// <br/>The measured CAPTION and the measured PRE-FILL are not owned elsewhere -- no other fixture names
/// <c>Rediger Betingelser egenskaber</c>, and the operator the dialog opens on is this dialog's own field --
/// so they are asserted here, on the dialog call the route already made. They cost no second arrangement.</para>
/// </summary>
public class ConditionsPropertiesParityTests
{
    /// <summary>
    /// THE ROUTE: the group's properties dialog opens, and the operator it returns reaches the document.
    ///
    /// <para>One observable effect at the far end is the whole point, and it has to be one the DIALOG can be
    /// shown to have caused. A fresh conditions group combines with AND, so OR in the document can only have
    /// arrived through this dialog -- which is what an engine test on the command cannot say, because the
    /// command is handed its value either way.</para>
    /// </summary>
    [Test]
    public async Task TheDialogsOperator_ReachesTheGroup()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;
        harness.Dialogs.PropertiesResult = new PropertiesResult("Betingelser", string.Empty, ConditionsOr: true);

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(1),
                "the reference application opens 'Rediger Betingelser egenskaber' here, and before F-48 "
                + "OpenVisual opened nothing at all");
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Rediger Betingelser egenskaber"),
                "titled the way the measurement records, and the way every other element's dialog is");
            Assert.That(harness.Dialogs.LastPropertiesConditionsOr, Is.False,
                "opened on the operator the group actually combines with — a fresh group is AND, so a dialog "
                + "pre-filled with OR would be offering to keep a state the group is not in");
            Assert.That(Conditions(vm).IsOrGroup, Is.True,
                "and the operator the dialog returned reached the document");
        });
    }

    private static TreeNodeViewModel Conditions(MainWindowViewModel vm) =>
        TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsConditionsContainer)!;

    /// <summary>An unlocked block in programming mode with a sub-program, which is what brings a
    /// <c>Betingelser</c> group into being — the same recipe the program-authoring tests use.</summary>
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, TreeNodeViewModel conditions)>
        WithConditionsGroupAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddEmptyFunctionBlockAsync(vm.InstallationNodes[0].Children[0].ElementId!.Value);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        await vm.AddSubProgramCommand.ExecuteAsync(
            TreeNodes.FindFirst(vm.FunctionNodes, n => n.IsCommandsContainer)!);

        TreeNodeViewModel conditions = Conditions(vm);
        vm.SelectNode(conditions);
        return (harness, vm, conditions);
    }
}
