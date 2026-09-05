using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using Ihc.Vis;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// A workflow's broad <c>catch (Exception)</c> turns whatever it caught into a coded OPERATION OUTCOME — the
/// project could not be opened, the report could not be written. That is the right sentence for a condition in
/// the world: a missing file, a full disk, a corrupt <c>.vis</c>. It is the wrong sentence, alone, for the tool
/// breaking, and until now the two were indistinguishable downstream.
///
/// <para><b>The defect these pin.</b> <c>FailureReport.FailedAsync</c> wrote to the span, the log and the dialog
/// and to nothing else, so an UNANTICIPATED exception behind one of those outcomes left no <i>Intern fejl</i>
/// row anywhere. Measured on the save path: a complete file on disk, a dialog saying it could not be saved, and
/// a Problemer panel with nothing in it at all. Eight call sites across four workflows have that shape.</para>
///
/// <para><b>The split, and why it is by exception TYPE.</b> The SDK already separates the two: malformed content
/// arrives as a <see cref="FormatException"/> (<c>ProjectFormatException</c>, <c>CatalogFormatException</c>), a
/// storage condition as an <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>, and a
/// deliberate refusal carries <c>IProblemCarrier</c>. Those are the conditions the coded outcome exists to word,
/// and routing them to the fault tier would fill it with ordinary operating — a corrupt vendor file is not a bug
/// in this application. Everything else is the tool, and gets a row.</para>
/// </summary>
[TestFixture]
public class OperationOutcomeInternalFaultTests
{
    /// <summary>
    /// A CONTRADICTORY export request — an error tier included while no severities are named. The writer refuses
    /// it with an <see cref="ArgumentException"/>, which is a caller defect and not a condition in the world, so
    /// the coded outcome is not the whole story and a fault row must exist beside it.
    /// </summary>
    /// <remarks>
    /// The vehicle matters. An earlier draft used a throwing <c>StateChanged</c> subscriber during an open, which
    /// stopped working the moment the open path's guard was narrowed — that fault is now caught as bookkeeping
    /// and never reaches an operation outcome at all. This one exercises what the rule is actually about: an
    /// exception type nobody anticipated arriving at a broad workflow catch.
    /// </remarks>
    private static FindingsExportRequest ContradictoryRequest() => new(
        Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationFinding>.Empty,
        "Alvor", Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationSeverity>.Empty,
        new ErrorTierFilter(true, true));

    [Test]
    public async Task AnUnanticipatedFaultBehindAnOperationOutcomeIsRecordedAsAnInternalFault()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.NewAsync();

        using CapturedFaults captured = new();
        List<InternalError> reported = captured.Rows;
        harness.Dialogs.SaveReportPath = harness.TempPath("findings.xml");
        harness.Dialogs.Reset();
        await harness.Session.ExportFindingsAsync(ContradictoryRequest());

        Assert.Multiple(() =>
        {
            Assert.That(reported, Is.Not.Empty,
                "an exception the operation never anticipated is the tool breaking, and leaves a row");
            Assert.That(reported[0].Origin, Is.EqualTo(InternalErrorOrigin.Host));
            Assert.That(reported[0].Detail, Does.Contain("ArgumentException"),
                "carrying the real cause, not merely the outcome's code");
        });
    }

    /// <summary>
    /// The installer still gets the operation's own sentence. The row is ADDITIVE — it must not replace the
    /// report the person in front of the screen is reading.
    /// </summary>
    [Test]
    public async Task TheUserStillGetsTheOperationsOwnReport()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.NewAsync();

        using CapturedFaults captured = new();
        harness.Dialogs.SaveReportPath = harness.TempPath("findings-reported.xml");
        harness.Dialogs.Reset();
        await harness.Session.ExportFindingsAsync(ContradictoryRequest());

        Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "the dialog channel is unchanged");
    }

    /// <summary>
    /// A CORRUPT FILE is not a bug in this application. It arrives as a <c>ProjectFormatException</c>, which is
    /// exactly what <c>app.openvisual.project-open-failed</c> exists to word, and it must not reach the fault
    /// tier — a tier that collected these would report the tool as broken every time someone opened the wrong
    /// file, which is the noise that makes a fault list worth ignoring.
    /// </summary>
    [Test]
    public async Task AMalformedProjectFileIsAnOutcomeAndNotAFault()
    {
        using ShellHarness harness = ShellHarness.Create();
        string rotten = harness.TempPath("rotten.vis");
        await File.WriteAllTextAsync(rotten, "this is not a project file");

        using CapturedFaults captured = new();
        List<InternalError> reported = captured.Rows;
        harness.Dialogs.Reset();
        bool opened = (await harness.Session.OpenAsync(rotten)).IsOk;

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.False, "non-vacuity: the open really did fail");
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "and the installer was told");
            Assert.That(reported, Is.Empty, "but the TOOL did not break, so the fault tier stays clean");
        });
    }

    /// <summary>
    /// ONE fault, ONE row — even though the SDK and the shell each have a reason to report it.
    ///
    /// <para><b>What this catches.</b> An exception escaping a traced app-service operation is reported by
    /// <c>AppServiceBase</c>'s own port before it is rethrown, and the composition root points that port at the
    /// SAME sink the shell writes to. The workflow's catch one level up then reported it again through
    /// <see cref="FailureReport"/>. Two rows, with different codes and different details, so neither the sink's
    /// code+detail de-duplication nor anything downstream could fold them: an installer saw the tool report
    /// itself broken twice for one event.</para>
    ///
    /// <para><b>Why the ordinary harness cannot see it.</b> <see cref="ShellHarness"/> builds its service
    /// deliberately WITHOUT a fault port, so in every other fixture only the shell's half fires. This one wires
    /// production's shape — a ported service, the same sink on both routes — which is the only arrangement in
    /// which the duplicate exists.</para>
    /// </summary>
    [Test]
    public async Task AFaultEscapingAnSdkOperationIsReportedOnce()
    {
        using CapturedFaults captured = new();
        List<InternalError> reported = captured.Rows;
        using ScratchDir dir = new("ihc_ov_dup_");
        FakeDialogService dialogs = new() { SaveReportPath = dir.File("findings.xml") };
        // The PORTED service, as the composition root builds it: its faults go to the same sink the shell uses.
        ProjectAppService service = new(new Ihc.IhcSettings(), reported.Add);
        using ProjectWorkflow session = new(
            service, new RecentProjectsStore(dir.File("recent.json")), dialogs,
            catalogDir: dir.File("catalog"), post: action => action(),
            timeProvider: new Microsoft.Extensions.Time.Testing.FakeTimeProvider(), faultSink: reported.Add);
        await session.NewAsync();
        reported.Clear();

        // A contradictory request: the writer refuses it with an ArgumentException from INSIDE a traced
        // operation, so the SDK's port fires and the workflow's catch then sees the same instance.
        await session.ExportFindingsAsync(ContradictoryRequest());

        Assert.Multiple(() =>
        {
            Assert.That(reported, Is.Not.Empty, "non-vacuity: the fault was reported at all");
            Assert.That(reported, Has.Count.EqualTo(1), "and exactly once, by whichever layer saw it first");
            Assert.That(reported[0].Origin, Is.EqualTo(InternalErrorOrigin.Sdk),
                "the SDK's row wins: it names the operation that broke, which the shell's cannot");
        });
    }

    /// <summary>
    /// A storage condition is likewise an outcome. An unwritable destination is the disk answering, not the
    /// application misbehaving.
    /// </summary>
    [Test]
    public async Task AnUnwritableDestinationIsAnOutcomeAndNotAFault()
    {
        using ShellHarness harness = ShellHarness.Create();
        await harness.Session.NewAsync();

        using CapturedFaults captured = new();
        List<InternalError> reported = captured.Rows;
        harness.Dialogs.SaveReportPath =
            Directory.CreateDirectory(harness.TempPath("not-a-findings-file.xml")).FullName;
        harness.Dialogs.Reset();
        // A CONSISTENT request: no severities and neither error tier. Including an error tier while naming no
        // severities is refused by the writer as a contradiction, and that refusal is an ArgumentException —
        // a caller defect, which would reach the fault tier correctly and prove nothing about I/O.
        await harness.Session.ExportFindingsAsync(new FindingsExportRequest(
            Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationFinding>.Empty,
            "Alvor", Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationSeverity>.Empty,
            new ErrorTierFilter(false, false)));

        Assert.Multiple(() =>
        {
            Assert.That(harness.Dialogs.LastProblem, Is.Not.Null, "non-vacuity: the export failed and said so");
            Assert.That(reported, Is.Empty, "an I/O condition is not a fault in the tool");
        });
    }
}
