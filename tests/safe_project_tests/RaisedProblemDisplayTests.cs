using System;
using System.Linq;
using System.Threading.Tasks;

using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests;

/// <summary>
/// T034: an exception carrying an AGGREGATE is shown as head-plus-items, not as the shell's generic framing.
///
/// <para>T016 widened the contract and gave the dialog surface an aggregate overload, but every catch site still
/// composed a <see cref="ProblemChain"/> unconditionally. A <see cref="ProjectValidationException"/> answers
/// <c>Problems</c> with null — it carries an aggregate, not a chain — so
/// <see cref="HostProblems.Narrate"/> fell through to its uncoded branch and the installer got the shell's own
/// framing with the N findings that explain the refusal nowhere in sight.</para>
///
/// <para><b>There is no live raiser in this application today, and that is measured rather than assumed.</b>
/// <c>Save</c> defaults <c>ValidateBeforeSave</c> to false and the shell never sets it; the shell never calls
/// <c>UploadTo</c> ("this build never contacts a controller", E10); and catalog import parses through
/// <c>CatalogReader</c>, not the definition builders that throw it. So this pins the SHOWING path — the half that
/// can be wrong before a raiser exists — and the day the controller transfer lands it renders correctly rather
/// than needing to be discovered again.</para>
/// </summary>
public class RaisedProblemDisplayTests
{
    private const string Title = "Lagring mislykkedes";

    private static ProjectValidationException RefusedByValidation(params string[] errors) =>
        new(OperationCodes.Save, ProjectValidationResult.FromFindings(
        [
            .. errors.Select((message, i) => new ProjectValidationFinding(
                ValidationSeverity.Error, "attr-required", $"_0x{i:x}", message)),
        ]));

    private static async Task<FakeDialogService> Shown(Exception raised)
    {
        FakeDialogService dialogs = new();
        await RaisedProblemDisplay.ShowAsync(
            dialogs, Title, HostProblems.ProjectSaveFailed(@"C:\p\Project1.vis", raised), raised);
        return dialogs;
    }

    [Test]
    public async Task AnAggregateCarrierIsShownAsHeadPlusItems()
    {
        FakeDialogService dialogs = await Shown(
            RefusedByValidation("Mangler påkrævet attribut", "Ukendt attribut 'bogus' på <group>."));

        Assert.That(dialogs.LastProblemAggregate, Is.Not.Null,
            "a refusal carrying N independent findings is shown as the set it is");
        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastProblemAggregate!.Items, Has.Length.EqualTo(2), "every finding survives");
            Assert.That(dialogs.LastMessage, Does.Contain("Mangler påkrævet attribut"));
            Assert.That(dialogs.LastMessage, Does.Contain("Ukendt attribut 'bogus' på <group>."));
            Assert.That(dialogs.LastMessage, Does.Contain("2"), "and the head says how many block the operation");
        });
    }

    /// <summary>
    /// A CHAIN carrier is unchanged: one sentence, the SDK's cause (D01). The shape decision must not have turned
    /// every refusal into a list.
    /// </summary>
    [Test]
    public async Task AChainCarrierIsStillShownAsItsCauseAlone()
    {
        FakeDialogService dialogs = await Shown(
            new Ihc.Vis.Io.ProjectFormatException(Ihc.Vis.Io.LoadRefusalCodes.Empty, "the stream holds no bytes"));

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastProblemAggregate, Is.Null, "a chain is not an aggregate");
            Assert.That(dialogs.LastProblemChain, Is.Not.Null);
            Assert.That(dialogs.LastMessage, Is.EqualTo("Filen er tom [load-empty]"));
        });
    }

    /// <summary>An uncoded failure still gets the shell's framing — the branch T002 left in place.</summary>
    [Test]
    public async Task AnUncodedFailureStillGetsTheShellsFraming()
    {
        FakeDialogService dialogs = await Shown(new InvalidOperationException("no identity here"));

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastProblemAggregate, Is.Null);
            Assert.That(dialogs.LastMessage, Does.Contain("kunne ikke gemmes"));
        });
    }
}
