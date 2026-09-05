using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Ihc.Tests.Shared;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using ihc_openvisual.Services;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// The one report-viewing failure the shell reports as its own coded outcome: the report could not be PRODUCED.
///
/// <para>The sibling arm - the report was produced but the OS opened no viewer - is covered by
/// <c>FailureChannelTests</c>, which reaches it by refusing the external open. This one needs the generation
/// itself to fail, so it takes over the directory the workflow writes into: the workflow mints that directory
/// once and caches it, so replacing it with a FILE between two views makes the second write fail exactly as a
/// full disk or a revoked permission would, without any fault injection the product would have to carry.</para>
///
/// <para>What the tier buys here is the asymmetry documented in TESTSTRATEGY's <i>errors that do not surface</i>:
/// this failure has a dialog channel AND a span, so both are asserted - and, because a storage condition is one
/// of the things the coded outcome exists to WORD, it must leave NO internal-error row. A fault list that
/// collected every unwritable path is a fault list nobody reads.</para>
/// </summary>
[TestFixture]
public sealed class ReportViewFailureTests
{
    /// <summary>The application's fault port is process-wide static; leave it as this suite found it.</summary>
    [SetUp]
    [TearDown]
    public void DetachTheFaultPort() => TaskSupervisor.ReportTo(null);

    private sealed class Fixture : IDisposable
    {
        internal FakeDialogService Dialogs { get; } = new();
        internal CapturingLoggerFactory Logs { get; } = new();
        internal ProjectReportWorkflow Workflow { get; }

        internal Fixture()
        {
            Project project = Tree.MinimalProject();
            Workflow = new ProjectReportWorkflow(
                new ProjectAppService(TestSetup.Settings), Dialogs, Logs.CreateLogger(nameof(ReportViewFailureTests)),
                () => project);
        }

        internal Task ViewAsync() =>
            Workflow.ViewInBrowserAsync(ReportKind.Functions, ReportMode.Full, ReportFormat.Html);

        /// <summary>
        /// The per-run viewing directory, learned from the path the workflow handed the OS - the workflow keeps
        /// it private, which is the point: a test that reached in for it would be testing its own copy.
        /// </summary>
        internal string ViewDirectory { get; private set; } = string.Empty;

        internal void RememberViewDirectory() =>
            ViewDirectory = Path.GetDirectoryName(Dialogs.LastOpenedUrl!)!;

        /// <summary>
        /// Makes the next generation unwritable by putting a FILE where the cached directory was. A storage
        /// condition arriving as an <see cref="IOException"/> is what the product would see from a full disk.
        /// </summary>
        internal void TakeOverTheViewingDirectory()
        {
            string dir = ViewDirectory;
            Directory.Delete(dir, recursive: true);
            File.WriteAllText(dir, string.Empty);
        }

        public void Dispose()
        {
            Workflow.Dispose();
            Logs.Dispose();
        }
    }

    private static async Task<Fixture> ViewedOnceAsync()
    {
        var fixture = new Fixture();
        await fixture.ViewAsync();
        Assert.That(fixture.Dialogs.LastOpenedUrl, Is.Not.Null,
            "the first view has to succeed, or the failure below is not the one being tested");
        Assert.That(fixture.Dialogs.LastProblem, Is.Null, "and has to succeed cleanly");
        fixture.RememberViewDirectory();
        fixture.Dialogs.Reset();
        fixture.Logs.Clear();
        return fixture;
    }

    [Test]
    public async Task ViewInBrowser_WhenTheReportCannotBeWritten_TellsTheInstallerAndTheSpan()
    {
        using Fixture fixture = await ViewedOnceAsync();
        fixture.TakeOverTheViewingDirectory();

        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: ["ProjectReportWorkflow.ViewInBrowserAsync"]);

        await fixture.ViewAsync();

        Activity span = spans.Span("ProjectReportWorkflow.ViewInBrowserAsync");

        // The four channels are what "reported a failure" means application-wide, so they are asserted through
        // the one statement of it rather than restated here.
        FailureChannelTests.AssertAllChannels("view report", fixture.Dialogs, fixture.Logs, span);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Dialogs.LastProblem!.Code, Is.EqualTo(HostProblemCodes.ReportViewFailed),
                "and told it under the host code for a failed report VIEW, not the save one");
            Assert.That(fixture.Dialogs.LastMessage, Does.Not.Contain("{"),
                "with no slot left spelled as its own placeholder");
            Assert.That(fixture.Dialogs.LastOpenedUrl, Is.Null,
                "and nothing is handed to the OS - there is no report to open");
        });
    }

    /// <summary>
    /// The deliberate asymmetry: an unwritable path is one of the conditions the coded outcome exists to word,
    /// so it is reported to the installer and NOT filed as an internal fault. <i>Intern fejl</i> is for what
    /// nobody anticipated.
    /// </summary>
    [Test]
    public async Task ViewInBrowser_WhenTheReportCannotBeWritten_LeavesNoInternalErrorRow()
    {
        List<InternalError> faults = [];
        TaskSupervisor.ReportTo(faults.Add);

        using Fixture fixture = await ViewedOnceAsync();
        fixture.TakeOverTheViewingDirectory();

        await fixture.ViewAsync();

        Assert.That(faults, Is.Empty,
            "a storage condition the outcome already words must not also be reported as a defect in the tool");
    }

    /// <summary>
    /// Shutdown cleanup is best-effort by design: the viewing directory holds a generated report carrying
    /// project data, so it is removed on the way out - but a viewer still holding the page, or anything else
    /// that makes the removal fail, must not turn shutdown into a crash.
    /// </summary>
    [Test]
    public async Task Dispose_WhenTheViewingDirectoryCannotBeRemoved_LogsItAndCarriesOn()
    {
        Fixture fixture = await ViewedOnceAsync();
        string dir = fixture.ViewDirectory;
        fixture.TakeOverTheViewingDirectory();

        Assert.DoesNotThrow(fixture.Dispose);

        File.Delete(dir);
    }

    /// <summary>
    /// The ordinary path, as the control: a viewed report leaves nothing behind once the app shuts down.
    /// Without it the case above could pass over a Dispose that had quietly stopped cleaning up at all.
    /// </summary>
    [Test]
    public async Task Dispose_AfterViewingAReport_RemovesTheGeneratedPage()
    {
        Fixture fixture = await ViewedOnceAsync();
        string dir = fixture.ViewDirectory;
        Assert.That(Directory.Exists(dir), Is.True, "the report was written somewhere");

        fixture.Dispose();

        Assert.That(Directory.Exists(dir), Is.False, "and that somewhere does not outlive the run");
    }

    /// <summary>
    /// Constructing the collaborator writes nothing - the previewer constructs one per preview, and a directory
    /// minted per construction would litter a temp folder with empty directories nobody removes.
    /// </summary>
    [Test]
    public void Dispose_WithoutEverViewingAReport_HasNothingToRemove()
    {
        using var fixture = new Fixture();

        Assert.DoesNotThrow(fixture.Workflow.Dispose);
    }
}
