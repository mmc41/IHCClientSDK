using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W5 / F9 / D11 (uxparity2 T021): a VARIABLE's properties dialog offers BOTH documentation fields — the function
/// documentation and the installer help text — matching what the reference application shows (control ids 214 and
/// 517, measured in `tmp/uxparity2/verify/V2/notes.md`). A LOCALITY keeps exactly two fields (Name + Note): D11 is
/// explicit that US-007 already matches there and widening it would introduce a divergence, so that is pinned too.
/// </summary>
public class PinDocumentationFieldsTests : AvaloniaTestBase
{
    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> BlockWithVariableAsync()
    {
        var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId loc = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(loc);
        vm.EnterProgrammingModeCommand.Execute(vm.FunctionNodes[0].Children[0].Children[0]);
        ElementId inputs = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        await harness.Session.AddVariableAsync(inputs, "resource_input", "Doorbell");
        ElementId variable = TreeNodes.FindPin(vm.InstallationNodes, "Doorbell")!
            .ElementId!.Value;
        return (harness, vm, variable);
    }

    // The dialog is HANDED the stored help text, and what the installer types is APPLIED — both directions, because
    // a write-only field would look correct in a screenshot and lose data on the next open.
    [Test]
    public async Task VariableDialog_CarriesBothDocumentationFields_InAndOut()
    {
        var (harness, vm, variable) = await BlockWithVariableAsync();
        using var _ = harness;

        harness.Dialogs.VariablePropertiesResult =
            new VariablePropertiesResult("Doorbell", "function documentation", ResourceInitialValue.None,
                HelpNote: "installer help text");
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, variable));

        ProjectElement saved = harness.Session.Current!.FindById(variable)!;
        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.View(saved).Note, Is.EqualTo("function documentation"));
            Assert.That(harness.Session.Current!.View(saved).HelpNote, Is.EqualTo("installer help text"),
                "the second field is applied, not dropped");
        });

        // Re-open: the dialog must be PRE-FILLED with what was stored, or the field is write-only.
        harness.Dialogs.VariablePropertiesResult = null;   // cancel
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, variable));
        Assert.That(harness.Dialogs.LastVariablePropertiesInput!.HelpNote, Is.EqualTo("installer help text"),
            "re-opening shows the stored help text");
    }

    // D11: the locality dialog stays at exactly two fields. Its contract carries Name and Note and nothing else.
    [Test]
    public void LocalityDialogContract_HasExactlyTwoEditableFields()
    {
        var fields = typeof(PropertiesResult).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();

        Assert.That(fields, Is.EquivalentTo(new[] { "Name", "Note" }),
            "US-007's 'exactly two fields' is unchanged by W5 (D11) — a locality gains no help text");
    }
}
