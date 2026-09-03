using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ihc_openvisual.Services;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What the validation monitor reports about a document change, and about a rule that crashes.
///
/// A crashed rule used to leave NOTHING behind: <c>OnFaulted</c> received the exception and dropped it, so the
/// only symptom was a Problemer panel that quietly stopped updating. And the monitor derives WHICH transition
/// it is looking at from two individually-ambiguous facts - <c>Open</c> bumps the version, <c>MarkSaved</c>
/// does not move it - so a generation that fails to increment on a replacement shows up only as the previous
/// file's findings answering questions about the new one.
/// </summary>
[TestFixture]
public class ValidationMonitorTelemetryTests
{
    private static readonly StructuredValidationResult NoFindings = StructuredValidationResult.Empty;

    /// <summary>
    /// REPRODUCE-FIRST for the silent-staleness defect: a faulted run used to leave a LOG LINE and
    /// nothing else, so the panel went on showing rows that describe a document state the run never reached —
    /// and they read as current. The row is the fix, and its sentence is what carries the consequence.
    /// </summary>
    /// <remarks>
    /// Asserted on the sentence and the code TOGETHER. The catch-all would have satisfied a code-only assertion
    /// while telling the reader nothing they can act on; what makes this row worth having is that it says the
    /// list may be out of date.
    /// </remarks>
    [Test]
    public async Task AFaultingRunLeavesADurableRowSayingTheListMayBeStale()
    {
        using ShellHarness harness = ShellHarness.Create();
        List<Ihc.Vis.Problems.InternalError> reported = [];
        bool shouldThrow = false;

        using var monitor = new ValidationMonitor(
            harness.Session,
            _ => shouldThrow ? throw new System.TimeoutException("a rule hung") : NoFindings,
            onFault: reported.Add);

        await harness.Session.NewAsync();
        await harness.SettleValidationAsync(monitor);
        Assert.That(reported, Is.Empty, "a healthy run reports nothing");

        shouldThrow = true;
        ElementId locality = harness.Session.Current!.Groups[0].Id!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(locality);
        await harness.SettleValidationAsync(monitor);

        Assert.Multiple(() =>
        {
            Assert.That(reported, Has.Count.EqualTo(1), "the fault is now durable, not only logged");
            Ihc.Vis.Problems.InternalError row = reported[0];
            Assert.That(row.Code.Value, Is.EqualTo("app.openvisual.validation-faulted"),
                "its OWN code, not the shell's catch-all");
            Assert.That(row.Message, Is.EqualTo(HostProblems.ValidationFaulted().Message),
                "rendered WHOLE from the catalogue, never re-worded here");
            Assert.That(row.Message, Does.Contain("forældet"),
                "and the sentence carries the CONSEQUENCE: the list on screen may be out of date");
            Assert.That(row.Origin, Is.EqualTo(Ihc.Vis.Problems.InternalErrorOrigin.Host),
                "the loop around the engine failed, not a rule inside it");
            Assert.That(row.Detail, Does.Contain("a rule hung"),
                "with the original exception captured for a support case");
        });
    }

    /// <summary>
    /// The gate's assertion. A REAL ILogger, never a mock: the point is that the fault reaches the logging
    /// pipeline that exports it, and a mock would prove only that a method was called.
    /// </summary>
    [Test]
    public async Task AFaultingRun_IsLogged_AndThePreviouslyBoundResultStaysBound()
    {
        using ShellHarness harness = ShellHarness.Create();
        var logging = new CapturingLoggerFactory();
        bool shouldThrow = false;

        using var monitor = new ValidationMonitor(
            harness.Session,
            _ => shouldThrow ? throw new System.TimeoutException("a rule hung") : NoFindings,
            logging);

        await harness.Session.NewAsync();
        await harness.SettleValidationAsync(monitor);
        Assert.That(monitor.Result, Is.Not.Null, "a first result binds before the fault");
        ValidationOutcome bound = monitor.Result!;

        // Now make the next run crash.
        shouldThrow = true;
        ElementId locality = harness.Session.Current!.Groups[0].Id!.Value;
        await harness.Session.AddEmptyFunctionBlockAsync(locality);
        await harness.SettleValidationAsync(monitor);

        Assert.Multiple(() =>
        {
            Assert.That(logging.Messages.Any(m => m.Contains("faulted", System.StringComparison.OrdinalIgnoreCase)),
                Is.True, "the exception used to be dropped here, leaving no record at all");
            Assert.That(monitor.Result, Is.SameAs(bound),
                "a failed run is not evidence the previous findings went away, so they stay bound");
        });
    }

    [Test]
    public async Task TheFirstDocument_ReportsTheFirstBranch()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationMonitor.OnDocumentChanged" }))
        {
            using ShellHarness harness = ShellHarness.Create();
            using var monitor = new ValidationMonitor(harness.Session, _ => NoFindings);
            await harness.Session.NewAsync();
            await harness.SettleValidationAsync(monitor);

            Assert.That(capture.Spans.Select(s => s.GetTagItem("ihc.document.branch")?.ToString()),
                Does.Contain("first"));
        }
    }

    /// <summary>
    /// The one that matters: replacing the document must increment the generation AND say so, because the
    /// failure mode is silent - the previous file's findings answering questions about the new one.
    /// </summary>
    [Test]
    public async Task ReplacingTheDocument_IncrementsTheGeneration_AndReportsTheReplacementBranch()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationMonitor.OnDocumentChanged" }))
        {
            using ShellHarness harness = ShellHarness.Create();
            using var monitor = new ValidationMonitor(harness.Session, _ => NoFindings);

            await harness.Session.NewAsync();
            await harness.SettleValidationAsync(monitor);
            int firstGeneration = monitor.Generation;

            // A second New is a REPLACEMENT: LastChange is null and the version moved.
            await harness.Session.NewAsync();
            await harness.SettleValidationAsync(monitor);

            int[] generations = capture.Spans
                .Select(s => s.GetTagItem("ihc.document.generation"))
                .OfType<int>().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(monitor.Generation, Is.GreaterThan(firstGeneration), "a new document is a new generation");
                Assert.That(capture.Spans.Select(s => s.GetTagItem("ihc.document.branch")?.ToString()),
                    Does.Contain("replacement"));
                Assert.That(generations, Does.Contain(monitor.Generation),
                    "the generation is on the span, so a failure to increment is visible rather than merely wrong");
            });
        }
    }

    [Test]
    public async Task AnEdit_ReportsTheEditBranch_WithoutANewGeneration()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: new[] { "ValidationMonitor.OnDocumentChanged" }))
        {
            using ShellHarness harness = ShellHarness.Create();
            using var monitor = new ValidationMonitor(harness.Session, _ => NoFindings);
            await harness.Session.NewAsync();
            await harness.SettleValidationAsync(monitor);
            int generation = monitor.Generation;

            ElementId locality = harness.Session.Current!.Groups[0].Id!.Value;
            await harness.Session.AddEmptyFunctionBlockAsync(locality);
            await harness.SettleValidationAsync(monitor);

            Assert.Multiple(() =>
            {
                Assert.That(capture.Spans.Select(s => s.GetTagItem("ihc.document.branch")?.ToString()),
                    Does.Contain("edit"));
                Assert.That(monitor.Generation, Is.EqualTo(generation),
                    "an edit is the same document - a new generation here would drop findings needlessly");
            });
        }
    }
}
