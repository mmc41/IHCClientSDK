using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Pin addressing was the one flow that read a non-committed outcome itself instead of handing it to the
/// view-model's classifier, and it read it as a BOOLEAN: anything that was not <c>Committed</c> became
/// "Datalinie X, klemme Y er ikke en gyldig adresse." So a no-op re-apply, and an engine failure, both told the
/// installer their address was invalid when it was not — and a failure additionally produced no dialog, no log
/// record and no span, because the classifier is what raises those.
/// <para>
/// The flow now goes through the classifier like every other, handing its address sentence in as the REFUSAL
/// OVERRIDE. Only a genuine refusal keeps that sentence; a no-op says nothing and a failure takes the standard
/// coded route. <see cref="OutcomeReasonTests"/> owns the rule for an outcome carrying no override.
/// </para>
/// </summary>
public class PinPropertiesDialogOutcomeTests : AvaloniaTestBase
{
    private const string AddressSentence = "Datalinie 1, klemme 0 er ikke en gyldig adresse.";
    private const string EnglishDiagnostic = "The element (id _0x2132) no longer exists.";
    private const string DanishRefusal = "Klemmenummeret ligger uden for datalinjens område.";

    private static EditOutcome Outcome(EditStatus status, string? reason) =>
        new(status, "Adresser klemme", reason, null);

    /// <summary>
    /// The reproduce-first rule: supplying the address sentence must not make it reachable from a status the
    /// sentence does not describe. A <c>Failed</c> pin commit is an engine defect, not an invalid address.
    /// <para>Asserted on the rule rather than by provoking a failing edit, for the reason
    /// <see cref="OutcomeReasonTests"/> records: <c>UpdatePin</c> guards both of its failure modes into REFUSALS
    /// (a missing terminal, an out-of-range address), so no <c>Failed</c> outcome is reachable through the pin
    /// flow at all. The defect is latent — real, but only provokable by first breaking a guard, which would pin
    /// the broken guard instead of this rule.</para>
    /// </summary>
    [Test]
    public void AnOverriddenRefusal_IsOfferedToRefusalsOnly()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Failed, EnglishDiagnostic), AddressSentence),
                Is.Null,
                "a failed pin commit is an engine defect; claiming the address is invalid names the wrong cause");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.NoChange, null), AddressSentence),
                Is.Null,
                "re-applying the address a pin already has changes nothing — and is not invalid");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Committed, null), AddressSentence),
                Is.Null,
                "a success is reported by its own status sentence");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Refused, DanishRefusal), AddressSentence),
                Does.Contain(AddressSentence),
                "a refusal is the one status the caller's own sentence describes");
        });
    }

    /// <summary>The override replaces the SDK's WORDS, never its IDENTITY: the code still travels, so a refusal
    /// carries the same bracketed id on the status bar as it does in a dialog (R18).</summary>
    [Test]
    public void AnOverriddenRefusal_KeepsTheOutcomesCode()
    {
        string plain = MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Refused, DanishRefusal))!;
        string overridden = MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Refused, DanishRefusal), AddressSentence)!;

        Assert.That(overridden.Replace(AddressSentence, DanishRefusal), Is.EqualTo(plain),
            "only the sentence differs — whatever identity the presenter renders around it is unchanged");
    }

    /// <summary>End to end: a genuine refusal still reads as the address sentence the installer had before.</summary>
    [Test]
    public async Task RefusedAddress_StillReportsTheAddressSentence()
    {
        var (harness, vm, pin) = await ProductWithPinAsync();
        using var _ = harness;

        // Terminal 0 is the "unaddressed" placeholder the dialog pre-fills, and it is not a legal terminal:
        // committing it is what a refusal looks like from this dialog.
        harness.Dialogs.PinPropertiesResult = new PinPropertiesResult(1, 0, "", "", false);
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, pin));

        Assert.That(vm.StatusText, Does.Contain("er ikke en gyldig adresse"),
            "an address the SDK refuses is still reported in the caller's own words");
    }

    /// <summary>End to end, the shipped symptom: re-applying the address a pin already has claimed the address
    /// was invalid.</summary>
    [Test]
    public async Task ReApplyingTheSameAddress_DoesNotClaimItIsInvalid()
    {
        var (harness, vm, pin) = await ProductWithPinAsync();
        using var _ = harness;

        harness.Dialogs.PinPropertiesResult = new PinPropertiesResult(1, 5, "", "", false);
        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, pin));
        string afterFirst = vm.StatusText;

        await vm.PropertiesCommand.ExecuteAsync(TreeNodes.FindById(vm.InstallationNodes, pin));

        Assert.Multiple(() =>
        {
            Assert.That(afterFirst, Does.Contain("blev adresseret"), "the first commit is the success case");
            Assert.That(vm.StatusText, Does.Not.Contain("er ikke en gyldig adresse"),
                "an address that did not change is not an address that was rejected");
        });
    }

    /// <summary>The precondition the test above rests on, pinned rather than assumed: the second commit really is
    /// a NO-OP and not a refusal, so "does not claim it is invalid" is a statement about the fix and not about a
    /// status that never occurred.</summary>
    [Test]
    public async Task ReApplyingTheSameAddress_IsANoOp()
    {
        var (harness, _, pin) = await ProductWithPinAsync();
        using var _h = harness;
        var same = new PinPropertiesResult(1, 5, "", "", false);

        EditOutcome first = await harness.Session.ApplyAsync(
            harness.Session.Commands.UpdatePin(harness.Session.Current!, pin, same));
        EditOutcome second = await harness.Session.ApplyAsync(
            harness.Session.Commands.UpdatePin(harness.Session.Current!, pin, same));

        Assert.Multiple(() =>
        {
            Assert.That(first.Status, Is.EqualTo(EditStatus.Committed));
            Assert.That(second.Status, Is.EqualTo(EditStatus.NoChange),
                "re-addressing a pin to where it already is changes no document byte");
        });
    }

    private static async Task<(ShellHarness harness, MainWindowViewModel vm, ElementId pin)> ProductWithPinAsync()
    {
        var harness = ShellHarness.Create();
        MainWindowViewModel vm = harness.CreateViewModel();
        await vm.InitializeAsync();
        ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
        var product = harness.ProjectService.GetAvailableProducts()
            .First(p => p.Resources.Any(r => r.Tag == "dataline_input"));
        await harness.Session.AddProductAsync(locality, product.ProductIdentifier);
        ElementId pin = harness.Session.Current!.FindById(locality)!
            .Descendants().First(d => d.Tag == "dataline_input").Id!.Value;
        return (harness, vm, pin);
    }
}
