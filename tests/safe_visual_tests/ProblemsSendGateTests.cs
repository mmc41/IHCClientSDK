using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Validation;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The upload gate: <c>controller.send</c> refuses while the project carries Error findings.
///
/// <para><b>An unbound result never refuses.</b> "Not yet validated", "stale" and "bound and clean" all ALLOW.
/// That is a correctness requirement rather than a preference: the panel validates in the background, so on any
/// cold start there is a window with no result at all, and a gate that refused there would grey the transfer for
/// a project that may be perfectly valid — and would tell the user the opposite of what the panel says. Only a
/// COMPLETED, BOUND result carrying at least one Error closes the gate.</para>
///
/// <para><b>Warning and Info never refuse either.</b> The tiers exist precisely because they do not block; a gate
/// that stopped on a Warning would make the distinction meaningless.</para>
///
/// <para><b>The connection refusal keeps its place at the front.</b> Without a controller the visible reason must
/// stay the connection one — a user with no controller attached is not helped by being told about validation
/// findings for a transfer that could not happen anyway.</para>
/// </summary>
public class ProblemsSendGateTests
{
    /// <summary>The corpus fixture T003 pinned as Error material: three duplicate data-line address groups.</summary>
    private static string ErrorFixture() => System.IO.Path.Combine(
        TestContext.CurrentContext.TestDirectory, "testdata", "projects", "Synthetic", "DuplicatedAdressErrors.vis");

    /// <summary>
    /// The advisory counterpart: the vendor-authored defect fixture carries Warnings and Information rows and
    /// not one Error, which is exactly the population the gate must let through.
    /// </summary>
    private static string AdvisoryFixture() => ProblemsTestData.FixturePath("Project6-Errors.vis");

    /// <summary>
    /// A shell on a fresh project with a controller connected. A REAL validation runs — no injected findings and
    /// no test-only setter — so what the gate reads is what the panel would actually bind.
    /// </summary>
    private static async Task<ProblemsShellRig> ConnectedShell()
    {
        ProblemsShellRig rig = new();
        await rig.Shell.InitializeAsync();
        rig.Shell.IsControllerConnected = true;
        return rig;
    }

    /// <summary>The same shell, with the Error-carrying fixture open and its findings bound.</summary>
    private static async Task<ProblemsShellRig> ConnectedShellWithErrors()
    {
        ProblemsShellRig rig = await ConnectedShell();
        await rig.Harness.Session.OpenAsync(ErrorFixture());
        await rig.SettleAsync();
        Assert.That(rig.Validation.HasBlockingFindings, Is.True,
            "precondition: the fixture binds at least one Error (pinned by ErrorSeverityFixtureTests)");
        return rig;
    }

    private static Availability SendAvailability(MainWindowViewModel shell) =>
        shell.Registry.Bar["controller.send"];

    // ── D17: an unbound result allows ───────────────────────────────────────────────────────────────────────

    [Test]
    public async Task AnUnvalidatedProjectStillAllowsTheTransfer()
    {
        using ProblemsShellRig rig = await ConnectedShell();

        Assert.That(SendAvailability(rig.Shell).Enabled, Is.True,
            "on a cold start nothing is bound yet — 'no result' is not evidence of a fault, and refusing there "
            + "would grey the transfer for a project that may be perfectly valid");
    }

    [Test]
    public async Task ACleanBoundResultAllowsTheTransfer()
    {
        using ProblemsShellRig rig = await ConnectedShell();
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.HasBlockingFindings, Is.False, "precondition: bound and clean");
            Assert.That(SendAvailability(rig.Shell).Enabled, Is.True);
        });
    }

    // ── The refusal ─────────────────────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task ABoundResultCarryingAnErrorRefusesWithItsOwnCodedDanishReason()
    {
        using ProblemsShellRig rig = await ConnectedShellWithErrors();

        Availability send = SendAvailability(rig.Shell);

        Assert.Multiple(() =>
        {
            Assert.That(send.Enabled, Is.False);
            Assert.That(send.Reason, Is.EqualTo(HostProblemCatalog.ValidationErrorsBlockSend.MessageTemplate),
                "the reason is the catalogue entry's Danish sentence, verbatim — the row does not word its own");
            Assert.That(send.Reason, Is.Not.Empty);
        });
    }

    [Test]
    public async Task WarningAndInfoFindingsNeverRefuse()
    {
        using ProblemsShellRig rig = await ConnectedShell();
        await rig.Harness.Session.OpenAsync(AdvisoryFixture());
        await rig.SettleAsync();
        Assert.That(rig.Shell.Problems.Warnings.Count, Is.GreaterThan(0),
            "precondition: the fixture binds Warnings and no Errors");
        Assert.That(rig.Shell.Problems.Errors.Count, Is.Zero);

        Assert.That(SendAvailability(rig.Shell).Enabled, Is.True,
            "the advisory tiers exist because they do NOT block; a gate that stopped on one would erase the "
            + "distinction between them and an Error");
    }

    // ── Gate composition and order ──────────────────────────────────────────────────────────────────────────

    [Test]
    public async Task WithoutAControllerTheVisibleReasonStaysTheConnectionOne()
    {
        using ProblemsShellRig rig = await ConnectedShellWithErrors();
        rig.Shell.IsControllerConnected = false;

        Availability send = SendAvailability(rig.Shell);

        Assert.Multiple(() =>
        {
            Assert.That(send.Enabled, Is.False);
            Assert.That(send.Reason, Is.EqualTo("Ingen controller er forbundet."),
                "the connection refusal is checked FIRST, so a disconnected controller's reason does not change "
                + "just because the panel found something");
        });
    }

    [Test]
    public async Task TheGateFollowsTheLatestBoundResultSoClearingTheErrorsReopensIt()
    {
        using ProblemsShellRig rig = await ConnectedShellWithErrors();
        Assert.That(SendAvailability(rig.Shell).Enabled, Is.False, "precondition: refused");

        // A document with no Errors, and one completed run over it. No other gesture is involved: the gate has to
        // follow the result, not a command the user issues to un-refuse it.
        await rig.Harness.Session.NewAsync();
        await rig.SettleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(rig.Validation.HasBlockingFindings, Is.False);
            Assert.That(SendAvailability(rig.Shell).Enabled, Is.True, "the gate reopens without any other gesture");
        });
    }

    // ── The catalogue entry ─────────────────────────────────────────────────────────────────────────────────

    [Test]
    public void TheRefusalIsADeclaredHostCodeRatherThanALooseString()
    {
        ProblemCatalogEntry entry = HostProblemCatalog.ValidationErrorsBlockSend;

        Assert.Multiple(() =>
        {
            Assert.That(entry.Code.Value, Is.EqualTo("app.openvisual.validation-errors-block-send"));
            Assert.That(entry.Code.IsHostOwned, Is.True, "an app.* code is the host's, not the SDK's");
            Assert.That(HostProblemCatalog.Current.Entries.Select(e => e.Code), Does.Contain(entry.Code),
                "declared in the catalogue, or the completeness gate fails it");
            Assert.That(entry.MessageTemplate, Is.Not.Empty);
            Assert.That(entry.Diagnostic, Is.Not.Null.And.Not.Empty, "English diagnostic beside the Danish sentence");
        });
    }
}
