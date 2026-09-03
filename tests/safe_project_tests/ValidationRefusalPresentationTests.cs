using System;
using System.Collections.Generic;
using System.Linq;

using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;

namespace Ihc.Vis.Tests;

/// <summary>
/// RF-7 / D03 / D06: a refused validation is caught by the SAME one catch shape as every other coded refusal,
/// and renders as head-plus-items.
///
/// <para>The problem contract's central claim is that <c>catch (… is IProblemCarrier)</c> covers every coded
/// refusal. <see cref="ProjectValidationException"/> was the exception to that claim: it carried a perfectly good
/// <see cref="ProblemAggregate"/> and implemented nothing, so the shell's coded path did not recognise it and the
/// installer got the shell's generic framing — with the N findings that explain WHY the operation stopped
/// nowhere in sight.</para>
///
/// <para>Per D06 the interface was WIDENED with a defaulted second member rather than split into a sibling. A
/// sibling would have required two type tests at every catch site, and forgetting the second reproduces exactly
/// this bug. The five existing sealed implementers are unchanged — asserted below, because "non-breaking" is the
/// property that made the widening the right call.</para>
/// </summary>
public class ValidationRefusalPresentationTests
{
    private static ProjectValidationException Refused(params string[] errors) =>
        new(OperationCodes.Save, ProjectValidationResult.FromFindings(
        [
            .. errors.Select((message, i) => new ProjectValidationFinding(
                ValidationSeverity.Error, "attr-required", $"_0x{i:x}", message)),
        ]));

    /// <summary>
    /// THE GATE: one catch shape, and what it yields renders as head-plus-items. The catch is written exactly as
    /// a shell site writes it — no test for the concrete exception type anywhere.
    /// </summary>
    [Test]
    public void OneCatchShapeSeesAValidationRefusalAndRendersItAsHeadPlusItems()
    {
        string[] entries = [];

        try
        {
            throw Refused("Mangler påkrævet attribut", "Ukendt attribut 'bogus' på <group>.");
        }
        catch (Exception ex) when (ex is IProblemCarrier)
        {
            IProblemCarrier carrier = (IProblemCarrier)ex;
            Assert.That(carrier.Aggregate, Is.Not.Null, "a validation refusal answers with an aggregate");
            entries = [.. ProblemPresenter.Entries(carrier.Aggregate!)];
        }

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Length.EqualTo(3), "the head and BOTH items — nothing collapsed into the head");
            Assert.That(entries[0], Does.StartWith("Projektet kunne ikke gemmes"), "the head frames the failure");
            Assert.That(entries[0], Does.Contain("2"), "and says how many errors block it");
            Assert.That(entries[1], Is.EqualTo("Mangler påkrævet attribut [attr-required]"));
            Assert.That(entries[2], Is.EqualTo("Ukendt attribut 'bogus' på <group>. [attr-required]"));
        });
    }

    /// <summary>
    /// A carrier answers ONE of the two shapes. The validation refusal has no chain, so a site that reads
    /// <c>Problems</c> alone gets null rather than a chain it would then render by the wrong rule.
    /// </summary>
    [Test]
    public void AValidationRefusalCarriesAnAggregateAndNoChain()
    {
        IProblemCarrier carrier = Refused("Mangler påkrævet attribut");

        Assert.Multiple(() =>
        {
            Assert.That(carrier.Problems, Is.Null, "an aggregate is not a chain");
            Assert.That(carrier.Aggregate, Is.Not.Null);
            Assert.That(carrier.Aggregate!.Items, Has.Length.EqualTo(1));
        });
    }

    /// <summary>
    /// The widening is NON-BREAKING, which is the whole reason D06 chose it. Every chain-carrying refusal still
    /// answers <c>Problems</c> and now answers <c>Aggregate</c> with null, without one of them being edited.
    /// </summary>
    [Test]
    public void TheChainCarryingRefusalsAreUnchangedAndDefaultTheNewMember()
    {
        IProblemCarrier[] carriers =
        [
            new Ihc.Vis.Io.ProjectFormatException(Ihc.Vis.Io.LoadRefusalCodes.Empty, "empty"),
            new RefusedOperationException(Ihc.Vis.Io.SaveRefusalCodes.RoundTripMismatch, "mismatch"),
            new RefusedWriteException(Ihc.Vis.Io.SaveRefusalCodes.TargetUnwritable, "unwritable"),
            new RefusedImportException(Ihc.Vis.Catalog.ImportRefusalCodes.CatalogUnparsable, "bad definition"),
        ];

        Assert.Multiple(() =>
        {
            foreach (IProblemCarrier carrier in carriers)
            {
                Assert.That(carrier.Problems, Is.Not.Null, carrier.GetType().Name + " still answers with its chain");
                Assert.That(carrier.Aggregate, Is.Null,
                    carrier.GetType().Name + " defaults the widened member — it was not edited to add it");
            }
        });
    }

    /// <summary>
    /// The shell's dialog surface can show the shape. Without an overload for it a site would have to flatten the
    /// items into one string itself, which is a second presentation path in the shell.
    /// </summary>
    [Test]
    public void TheDialogSurfaceShowsAnAggregateThroughTheOnePresentationPath()
    {
        FakeDialogService dialogs = new();
        ProblemAggregate aggregate = ((IProblemCarrier)Refused("Mangler påkrævet attribut")).Aggregate!;

        dialogs.ShowProblemAsync("Lagring mislykkedes", aggregate).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastProblemAggregate, Is.SameAs(aggregate));
            Assert.That(dialogs.LastMessage, Is.EqualTo(ProblemPresenter.Text(aggregate)),
                "rendered by the shell's one presentation path, not by the dialog");
            Assert.That(dialogs.LastMessage, Does.Contain("Mangler påkrævet attribut"),
                "and the item is in the box, not summarised away");
        });
    }
}
