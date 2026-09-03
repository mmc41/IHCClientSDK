using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// Alignment F-50 — an enum variable opens its OWN properties dialog, not its type's editor.
///
/// <para>Measured live 2026-08-11 on an unlocked block holding a <c>Persienne tilstand</c> enum. The reference
/// application's <c>Rediger Persienne tilstand egenskaber</c> is the ordinary variable dialog:</para>
/// <code>
///   Navn (213) · Tekst til funktionsdokumentation (214) · Noter for hjælpetekst (517)
///   Initial værdi (215)  combo: Ukendt, Oppe, Nede, Kører op, Kører ned
///   Ved strømsvigt ▸ Gem aktuel værdi (216)
///   &amp;Rediger (237) → a SEPARATE dialog, "Enumerator typer og værdier"
/// </code>
///
/// <para>OpenVisual opened the TYPE editor straight from the row (verified by double-click and F2 alike), so the
/// variable's name, both documentation fields, its initial state and its power-loss flag were unreachable — and
/// the ordinary gesture on one variable edited data every variable of that type shares.</para>
///
/// <para>The initial state needs its own command: a <c>resource_enum</c>'s <c>inivalue</c> is an <b>IDREF</b> to
/// one of its type's <c>enum_value</c> elements, so the generic value writer would store the state's NAME and
/// break the reference. These tests pin that the reference survives.</para>
/// </summary>
public class EnumVariablePropertiesParityTests
{
    private static readonly string[] ShutterStates = ["Ukendt", "Oppe", "Nede", "Kører op", "Kører ned"];

    /// <summary>
    /// THE ROUTE: the enum row opens the ordinary VARIABLE dialog rather than the type editor, and the state
    /// committed there reaches the document as the chosen value's IDREF.
    ///
    /// <para>The far-end assertion is the one that matters: a NAME stored here would be a broken reference that
    /// still looked right in the dialog. It is also evidence the value came from this dialog -- a fresh enum
    /// starts on its first state, so a stored reference resolving to a different one arrived by this route.</para>
    /// </summary>
    [Test]
    public async Task TheDialogsChosenState_ReachesTheDocumentAsAnIdRef()
    {
        var (harness, vm, variable) = await WithEnumAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Persienne tilstand", string.Empty, ResourceInitialValue.OfChoice("Nede"), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Persienne tilstand")!);

        string? stored = harness.Session.Current!.FindById(variable)!.GetAttribute("inivalue");
        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.EditVariablePropertiesCalls, Is.EqualTo(1),
                "the original opens the ordinary variable dialog for an enum row");
            Assert.That(harness.Dialogs.EditEnumDefinitionCalls, Is.EqualTo(0),
                "…and NOT the type editor, which edits data every variable of the type shares");
            Assert.That(stored, Is.Not.EqualTo("Nede"), "the state's NAME would be a broken reference");
            Assert.That(ElementId.TryParse(stored, out ElementId valueId), Is.True, "it is an element reference");
            Assert.That(harness.Session.Current!.FindById(valueId), Is.Not.Null, "…and it resolves");
            Assert.That(harness.Session.Current!.View(harness.Session.Current!.FindById(valueId)!).Name,
                Is.EqualTo("Nede"), "…to the state that was chosen");
        });
    }

    /// <summary>Its initial-value control offers the TYPE's states, in the type's order, with the current one
    /// selected — the original's <i>Initial værdi</i> combo.</summary>
    [Test]
    public async Task TheDialog_OffersTheTypesStates()
    {
        var (harness, vm, _) = await WithEnumAsync();
        using var _1 = harness;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Persienne tilstand")!);

        VariablePropertiesInput input = harness.Dialogs.LastVariablePropertiesInput!;
        Assert.Multiple(() =>
        {
            Assert.That(input.ChoiceOptions, Is.EqualTo(ShutterStates).AsCollection);
            Assert.That(input.Current.Kind, Is.EqualTo(ResourceValueKind.Choice));
            Assert.That(input.Current.Token, Is.EqualTo("Ukendt"), "a fresh enum starts on its first state");
        });
    }

    /// <summary>The row follows the value — the F-43 lesson, applied to the type this turn adds.</summary>
    [Test]
    public async Task TheTreeRowFollowsTheChosenState()
    {
        var (harness, vm, _) = await WithEnumAsync();
        using var _1 = harness;
        harness.Dialogs.VariablePropertiesResult = new VariablePropertiesResult(
            "Persienne tilstand", string.Empty, ResourceInitialValue.OfChoice("Kører op"), string.Empty);

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindPin(vm.InstallationNodes, "Persienne tilstand")!);

        Assert.That(TreeNodes.FindPin(vm.InstallationNodes, "Persienne tilstand")!.DisplayName,
            Does.Contain("Kører op"));
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId variable)> WithEnumAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = await harness.EnterProgrammingModeOnNewBlockAsync();
        ElementId section = vm.InstallationNodes[0].Children
            .Single(n => n.NodeKind == "section:internalsettings").ElementId!.Value;
        ElementId variable = (await harness.Session.AddEnumVariableAsync(
            section, "Persienne tilstand", "Persienne tilstand", ShutterStates))!.Value;
        return (harness, vm, variable);
    }
}
