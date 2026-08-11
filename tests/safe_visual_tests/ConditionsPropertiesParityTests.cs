using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

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
/// </summary>
public class ConditionsPropertiesParityTests
{
    [Test]
    public async Task Properties_OnAConditionsGroup_OpensADialog()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Assert.That(harness.Dialogs.EditPropertiesCalls, Is.EqualTo(1),
            "the reference application opens 'Rediger Betingelser egenskaber' here");
    }

    /// <summary>Its title follows the same <c>Rediger &lt;navn&gt; egenskaber</c> form the rest of the application
    /// uses, and it is pre-filled with the group's own name.</summary>
    [Test]
    public async Task TheDialog_IsTitledAndFilledFromTheGroup()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastPropertiesTitle, Is.EqualTo("Rediger Betingelser egenskaber"));
            Assert.That(harness.Dialogs.LastPropertiesName, Is.EqualTo("Betingelser"));
        });
    }

    /// <summary>The dialog carries the OPERATOR, which is the field that makes this dialog worth having: the
    /// original shows it as a captioned <i>Logisk betingelse</i> combo of AND/OR. A fresh conditions group is
    /// AND.</summary>
    [Test]
    public async Task TheDialog_OffersTheLogicOperator()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Assert.That(harness.Dialogs.LastPropertiesConditionsOr, Is.False,
            "the operator travels to the dialog, and a fresh group combines with AND");
    }

    /// <summary>Committing OR applies it — the dialog is a real second route to the operator, not a display of
    /// it.</summary>
    [Test]
    public async Task CommittingOr_CombinesTheGroupWithOr()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;
        harness.Dialogs.PropertiesResult = new PropertiesResult("Betingelser", string.Empty, ConditionsOr: true);

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Assert.That(Conditions(vm).IsOrGroup, Is.True, "the group now combines with OR");
    }

    /// <summary>And Navn/Note commit too — the two fields the missing dialog had made unreachable.</summary>
    [Test]
    public async Task CommittingNameAndNote_AppliesThem()
    {
        var (harness, vm, conditions) = await WithConditionsGroupAsync();
        using var _ = harness;
        harness.Dialogs.PropertiesResult = new PropertiesResult("Alle tændt", "Begge tryk inde", ConditionsOr: false);

        await vm.PropertiesCommand.ExecuteAsync(conditions);

        Ihc.Vis.Model.ProjectElement written = harness.Session.Current!.FindById(conditions.ElementId!.Value)!;
        Assert.Multiple(() =>
        {
            Assert.That(written.GetAttribute("name"), Is.EqualTo("Alle tændt"));
            Assert.That(written.GetAttribute("note"), Is.EqualTo("Begge tryk inde"));
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
