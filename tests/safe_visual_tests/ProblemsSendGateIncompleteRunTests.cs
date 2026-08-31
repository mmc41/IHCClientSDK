using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The upload gate's SECOND question: <c>controller.send</c> also refuses while the latest completed validation
/// run did not FINISH.
///
/// <para><b>Why it is not the errors refusal.</b> A crashed rule leaves the panel showing a list that reached no
/// verdict — short by exactly what the rule would have added, which nothing can measure. The errors sentence
/// tells the reader to repair what the panel lists, so saying it here points at the one list that cannot be
/// trusted, and on a faulted run that found nothing it asks for zero repairs. The two conditions ask for
/// different actions and get different words.</para>
///
/// <para><b>Why it is not blocking either.</b> <see cref="ValidationMonitor.HasBlockingFindings"/> stays false on
/// a fault, and must: blocking is a statement about the PROJECT, and folding a fault into it would refuse a
/// transfer on the strength of our own bug while wording it as the user's mistake.</para>
///
/// <para><b>Latent in this build.</b> E10 leaves <c>IsControllerConnected</c> permanently false, so the
/// connection refusal answers first and a user never reaches this one today. It is gated all the same — the line
/// was reading correct while being wrong, and a build that connects would have shipped the hole.</para>
/// </summary>
public class ProblemsSendGateIncompleteRunTests
{
    private static ProblemsRig FaultedRig() => new(_ => new StructuredValidationResult(
        EquatableArray<ValidationFinding>.Empty, ImmutableArray.Create(ProblemsTestData.RuleFailed())));

    /// <summary>The gate's answer for a context, taken off the REGISTERED row rather than a copy of its rule.</summary>
    private static Availability SendAvailability(MainWindowViewModel shell, ShellContext context) =>
        CommandRegistry.For(shell.Registry.Rows.Single(row => row.Id == "controller.send"),
            context, Surface.MenuBar);

    /// <summary>
    /// A settled shell, and the send gate's answer for the context <paramref name="context"/> asks for. The
    /// verdict is a value, so reading it after the rig is disposed is reading what the gate said.
    /// </summary>
    private static async Task<Availability> SendVerdictAsync(Func<ShellContext, ShellContext> context)
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        await rig.SettleAsync();
        return SendAvailability(rig.Shell, context(rig.Shell.Context));
    }

    // ── The monitor answers the question at all ─────────────────────────────────────────────────────────────

    /// <summary>
    /// A bound outcome carrying a fault reads as INCOMPLETE while staying non-blocking. Both halves matter: the
    /// second is what keeps the existing refusal honest, and asserting only the first would pass on a monitor
    /// that had folded faults into blocking.
    /// </summary>
    [Test]
    public async Task ABoundOutcomeCarryingAFaultReadsAsIncompleteWithoutBlocking()
    {
        using ProblemsRig rig = FaultedRig();

        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.HasIncompleteRun, Is.True);
            Assert.That(rig.Validation.HasBlockingFindings, Is.False,
                "a crashed rule says nothing about the project, so it never becomes a blocking finding");
        });
    }

    /// <summary>
    /// The transition RAISES <see cref="ValidationMonitor.BlockingChanged"/>. That event is what makes a gate
    /// rebuild its context, so an incompleteness nobody is told about is an incompleteness no gate ever reads.
    /// </summary>
    [Test]
    public async Task BecomingIncompleteRaisesTheEventAGateRebuildsOn()
    {
        using ProblemsRig rig = FaultedRig();
        int raised = 0;
        rig.Validation.BlockingChanged += (_, _) => raised++;

        await rig.WithNewProjectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.HasIncompleteRun, Is.True, "precondition: the run bound a fault");
            Assert.That(raised, Is.GreaterThan(0),
                "the gate-facing answer moved, so the gate-facing event has to fire");
        });
    }

    // ── The gate acts on it ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AnIncompleteRunRefusesTheSendGateWithItsOwnCodedDanishReason()
    {
        Availability send = await SendVerdictAsync(ctx => ctx with
        {
            ProjectOpen = true,
            ControllerConnected = true,
            ProjectValidationIncomplete = true,
        });

        Assert.Multiple(() =>
        {
            Assert.That(send.Enabled, Is.False,
                "a run that never finished cannot clear a transfer — the checklist reached no verdict");
            Assert.That(send.Reason,
                Is.EqualTo(HostProblemCatalog.ValidationIncompleteBlocksSend.MessageTemplate),
                "the catalogue entry's Danish sentence, verbatim — the gate does not word its own");
            Assert.That(send.Reason,
                Is.Not.EqualTo(HostProblemCatalog.ValidationErrorsBlockSend.MessageTemplate),
                "and it is NOT the errors sentence, which would send the reader to repair findings the run "
                + "never produced");
        });
    }

    /// <summary>The existing route is untouched: a fault-free run carrying Errors still refuses as it always did.</summary>
    [Test]
    public async Task AFaultFreeBlockingRunStillRefusesWithTheErrorsCode()
    {
        Availability send = await SendVerdictAsync(ctx => ctx with
        {
            ProjectOpen = true,
            ControllerConnected = true,
            ProjectHasValidationErrors = true,
            ProjectValidationIncomplete = false,
        });

        Assert.Multiple(() =>
        {
            Assert.That(send.Enabled, Is.False);
            Assert.That(send.Reason, Is.EqualTo(HostProblemCatalog.ValidationErrorsBlockSend.MessageTemplate));
        });
    }

    /// <summary>
    /// The connection refusal keeps its place at the front, for the reason it always had: a user with nothing
    /// attached is not helped by being told about a check that did not finish for a transfer that could not
    /// happen anyway.
    /// </summary>
    [Test]
    public async Task WithoutAControllerTheConnectionReasonOutranksTheIncompleteOne()
    {
        Availability send = await SendVerdictAsync(ctx => ctx with
        {
            ProjectOpen = true,
            ControllerConnected = false,
            ProjectValidationIncomplete = true,
        });

        Assert.That(send.Reason, Is.EqualTo("Ingen controller er forbundet."));
    }

    /// <summary>A complete, clean run over a connected controller still sends.</summary>
    [Test]
    public async Task ACompleteCleanRunStillAllowsTheTransfer()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        rig.Shell.IsControllerConnected = true;
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.HasIncompleteRun, Is.False, "precondition: nothing broke");
            Assert.That(rig.Shell.Registry.Bar["controller.send"].Enabled, Is.True);
        });
    }

    // ── The wiring between them ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The shell's context READS the monitor rather than carrying a default. Asserted against the monitor's own
    /// answer, so a context that hardcoded either value would disagree with it.
    /// </summary>
    [Test]
    public async Task TheShellContextCarriesTheMonitorsIncompletenessAnswer()
    {
        using ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        await rig.SettleAsync();

        Assert.That(rig.Shell.Context.ProjectValidationIncomplete,
            Is.EqualTo(rig.Validation.HasIncompleteRun));
    }

    // ── The catalogue entry ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void TheIncompleteRefusalIsADeclaredHostCodeRatherThanALooseString()
    {
        ProblemCatalogEntry entry = HostProblemCatalog.ValidationIncompleteBlocksSend;

        Assert.Multiple(() =>
        {
            Assert.That(entry.Code.Value, Is.EqualTo("app.openvisual.validation-incomplete-blocks-send"));
            Assert.That(entry.Code.IsHostOwned, Is.True, "gating a transfer is the host's policy, not the SDK's");
            Assert.That(HostProblemCatalog.Current.Entries.Select(e => e.Code), Does.Contain(entry.Code));
            Assert.That(entry.MessageTemplate, Is.Not.Empty);
            Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty);
            Assert.That(entry.MessageTemplate,
                Is.Not.EqualTo(HostProblemCatalog.ValidationErrorsBlockSend.MessageTemplate),
                "two conditions, two sentences — a shared one would make the distinction unobservable");
        });
    }
}
