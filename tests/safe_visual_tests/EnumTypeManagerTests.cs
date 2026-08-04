using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// W10 / F12 (uxparity2 T030): the enumerator-type manager on the Library menu.
/// <para>
/// Re-measured against the reference application 2026-08-04 (project <c>g10 4-10-2025</c>, via the ihcvisual MCP
/// driver). Its <i>Bibliotek ▸ Rediger Enumerator typer</i> opens "Enumerator typer og værdier": TWO list panes —
/// <i>Enumerator type</i> and <i>Enumerator værdier - &lt;type&gt;</i> — each with <c>Ny</c> / <c>Slet</c> /
/// <c>Omdøb</c>, and a lone <c>OK</c>. OpenVisual had a ONE-pane list with "Ny type…" and "Luk", so five of the six
/// operations had no route at all. These tests pin the six operations and the read-only rule the vendor enforces:
/// selecting a <c>[read only]</c> built-in greys type-Slet, type-Omdøb AND all three value buttons.
/// </para>
/// </summary>
public class EnumTypeManagerTests : AvaloniaTestBase
{
    [Test]
    public async Task TheManager_ListsTheProjectsTypes_WithTheirValues()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddStandaloneEnumTypeAsync("Persienne", new[] { "Oppe", "Nede" });

        IReadOnlyList<EnumTypeView> shown = [];
        harness.Dialogs.EnumTypeManagerScript = input => { shown = input.Types(); return Task.CompletedTask; };
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastEnumTypeManagerInput, Is.Not.Null, "the manager dialog opened");
            Assert.That(harness.Dialogs.LastEnumTypeManagerInput!.Title, Is.EqualTo("Enumerator typer og værdier"),
                "…under the reference application's title");
            Assert.That(shown.Select(t => t.Name), Does.Contain("Persienne"));
            Assert.That(shown.First(t => t.Name == "Persienne").Values, Is.EqualTo(new[] { "Oppe", "Nede" }),
                "…and the right-hand pane's values come with it");
        });
    }

    // "Ny" in the types pane is name-only: the vendor's prompt asks a name and nothing else, and values are added
    // afterwards one at a time in the right-hand pane.
    [Test]
    public async Task NewType_CreatesAnEmptyType()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        int before = harness.Session.Current!.GetEnumeratorTypes().Count;

        await RunManager(harness, vm, new EnumTypeManagerOperation.NewType("Ventilation"));

        IReadOnlyList<EnumTypeView> after = harness.Session.Current!.GetEnumeratorTypeViews();
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(before + 1), "exactly one type was created");
            Assert.That(after.First(t => t.Name == "Ventilation").Values, Is.Empty, "…and it starts with no values");
        });
    }

    [Test]
    public async Task RenameType_RenamesIt()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddStandaloneEnumTypeAsync("GammeltNavn", new[] { "A" });

        await RunManager(harness, vm, new EnumTypeManagerOperation.RenameType("GammeltNavn", "NytNavn"));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Does.Contain("NytNavn"));
            Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Does.Not.Contain("GammeltNavn"));
        });
    }

    [Test]
    public async Task DeleteType_RemovesIt()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddStandaloneEnumTypeAsync("Midlertidig", new[] { "A" });

        await RunManager(harness, vm, new EnumTypeManagerOperation.DeleteType("Midlertidig"));

        Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Does.Not.Contain("Midlertidig"));
    }

    [Test]
    public async Task ValueOperations_Add_Rename_AndDelete()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        await harness.Session.AddStandaloneEnumTypeAsync("Trin", new[] { "Lav", "Høj" });

        await RunManager(harness, vm,
            new EnumTypeManagerOperation.NewValue("Trin", "Mellem"),
            new EnumTypeManagerOperation.RenameValue("Trin", 0, "Laveste"),
            new EnumTypeManagerOperation.DeleteValue("Trin", 1));

        Assert.That(Values(harness, "Trin"), Is.EqualTo(new[] { "Laveste", "Mellem" }),
            "append, relabel by position, then remove by position — the three value buttons");
    }

    // The vendor greys every mutation on a "[read only]" built-in. We must refuse them with a REASON the dialog can
    // show, and leave the type alone — the flag the dialog greys by is the same one the engine refuses by.
    [Test]
    public async Task OperationsOnAReadOnlyType_AreRefusedWithAReason()
    {
        using var harness = ShellHarness.Create();
        var vm = harness.CreateViewModel();
        await vm.InitializeAsync();

        string? refusal = null;
        EnumTypeView? builtIn = null;
        harness.Dialogs.EnumTypeManagerScript = async input =>
        {
            builtIn = input.Types().FirstOrDefault(t => t.IsReadOnly);
            if (builtIn is not null)
            {
                refusal = await input.Apply(new EnumTypeManagerOperation.RenameType(builtIn.Name, "Nej"));
            }
        };
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(builtIn, Is.Not.Null, "the seed project defines at least one built-in [read only] type");
            Assert.That(builtIn!.DisplayName, Does.EndWith(" [read only]"), "…listed with the vendor's marker");
            Assert.That(refusal, Is.Not.Null.And.Contain("read only"), "the refusal reaches the dialog");
            Assert.That(harness.Session.Current!.GetEnumeratorTypes(), Does.Contain(builtIn.Name), "…and nothing changed");
        });
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

    /// <summary>Plays the installer: opens the manager and applies <paramref name="operations"/> in order through the
    /// same <c>Apply</c> callback the real dialog's buttons use.</summary>
    private static async Task RunManager(ShellHarness harness, MainWindowViewModel vm,
        params EnumTypeManagerOperation[] operations)
    {
        harness.Dialogs.EnumTypeManagerScript = async input =>
        {
            foreach (EnumTypeManagerOperation operation in operations)
            {
                Assert.That(await input.Apply(operation), Is.Null, $"{operation} was refused");
            }
        };
        await vm.ManageEnumTypesCommand.ExecuteAsync(null);
    }

    private static IReadOnlyList<string> Values(ShellHarness harness, string typeName) =>
        harness.Session.Current!.GetEnumeratorTypeViews().First(t => t.Name == typeName).Values;
}
