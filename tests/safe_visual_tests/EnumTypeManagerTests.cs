using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W10 / F12 (uxparity2 T030): a dedicated enumerator-type manager, reachable from the Library menu.
/// <para>
/// Measured (`tmp/uxparity2/verify/V6/findings.md`): the reference application's `Bibliotek` menu carries
/// <i>Rediger Enumerator typer</i>; OpenVisual's Library had only the insert/import entries, and type creation was
/// reachable only by opening a variable-insert flyout on a value section — a route you would not find if you were
/// looking for the project's types.
/// </para>
/// </summary>
public class EnumTypeManagerTests : AvaloniaTestBase
{
    [Test]
    public async Task TheManager_ListsTheProjectsEnumeratorTypes()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddStandaloneEnumTypeAsync("Persienne", new[] { "Oppe", "Nede" });
        await harness.Session.AddStandaloneEnumTypeAsync("Drift", new[] { "Auto", "Manuel" });

        harness.Dialogs.EnumTypeManagerResult = null;   // the installer just looks, then cancels
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastEnumTypeManagerInput, Is.Not.Null, "the manager dialog opened");
            Assert.That(harness.Dialogs.LastEnumTypeManagerInput!.Types,
                Is.SupersetOf(new[] { "Persienne", "Drift" }),
                "…and lists the project's enumerator types");
        });
    }

    [Test]
    public async Task TheManager_CanCreateAType()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.Current!.GetEnumeratorTypes().Count;

        // "New…" in the manager, then the definition dialog supplies the name and states.
        harness.Dialogs.EnumTypeManagerResult = new EnumTypeManagerResult(SelectedType: null);
        harness.Dialogs.EnumDefinitionResult = new EnumDefinitionResult("Ventilation", new[] { "Lav", "Høj" });
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);

        IReadOnlyList<string> after = harness.Session.Current!.GetEnumeratorTypes();
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(before + 1), "exactly one type was created");
            Assert.That(after, Does.Contain("Ventilation"));
            Assert.That(vm.StatusText, Does.Contain("Ventilation"), "…and the status bar says so");
        });
    }

    // Cancelling the manager creates nothing — the dialog is a way to look at the project's types, not a trap.
    [Test]
    public async Task CancellingTheManager_CreatesNothing()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.Current!.GetEnumeratorTypes().Count;

        harness.Dialogs.EnumTypeManagerResult = null;
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);

        Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Has.Count.EqualTo(before));
    }

    // The route is the LIBRARY menu — that is the finding. A command nobody can reach fixes nothing.
    [Test]
    public void TheManager_IsARegistryRow_OnTheMenuBar()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();

        Assert.That(vm.Registry.Rows.Any(r => r.Id == "library.manageEnumTypes"), Is.True,
            "the manager is a registry row, so its availability is evaluated like every other command");
    }
}
