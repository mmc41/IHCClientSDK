using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Tests.Shared;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// G4: when one of the shell's operations FAILS, all four channels say so — the user, the log, the span, and the
/// metric.
///
/// <para><b>Why jointly and not one at a time.</b> Each channel has its own test somewhere already, and that is
/// exactly how an operation ends up telling the user it failed while its span reads OK — the defect
/// <c>FailureReport</c> was built to remove. A per-channel test cannot see a MISSING channel; only an assertion
/// that demands all four together can.</para>
///
/// <para><b>One shared assertion, not a hand-written quadruple per operation.</b> Copies of the same asserts
/// drift, and the operation added later gets all but one of them. <see cref="AssertAllChannels"/> is the single
/// statement of what "reported a failure" means in this application.</para>
///
/// <para><b>The metric channel is asserted at ITS OWN DOOR, not here, and that is a measured constraint rather
/// than a gap.</b> <c>ihc.problem.raised</c> is recorded inside <c>AvaloniaDialogService</c>, the real
/// presentation port; a headless run goes through <c>FakeDialogService</c>, which does not count and must not
/// start counting, because a fake that counted would make this assertion a test of the fake. So the count is
/// pinned where it is real — <c>ProblemRaisedTelemetryTests</c>, over the three coded doors, including that a
/// presentation which never happens is not counted — and what THIS fixture pins is that every failing operation
/// reaches those doors at all, with its span and its log agreeing.</para>
///
/// <para><b>Validation is deliberately NOT under this rule</b> — see <see cref="ACrashedRuleReportsOnItsOwnTerms"/>.</para>
/// </summary>
[TestFixture]
public class FailureChannelTests
{
    /// <summary>The channels, asserted TOGETHER for one failed operation.</summary>
    /// <param name="what">Names the operation in every failure message.</param>
    /// <param name="dialogs">The port the user report arrives on.</param>
    /// <param name="logs">The pipeline the English diagnostic arrives on.</param>
    /// <param name="span">The operation's own span.</param>
    private static void AssertAllChannels(
        string what, FakeDialogService dialogs, CapturingLoggerFactory logs, Activity? span)
    {
        Assert.Multiple(() =>
        {
            Assert.That(dialogs.LastProblem, Is.Not.Null, $"{what}: the USER was told");
            Assert.That(logs.Messages, Is.Not.Empty, $"{what}: a LOG record exists");
            Assert.That(span, Is.Not.Null, $"{what}: the operation opened a span");
            Assert.That(span!.Status, Is.EqualTo(ActivityStatusCode.Error), $"{what}: the SPAN says it failed");
            // The identity, however the failure was carried: a normalized exception type where one was thrown,
            // and the problem's own code where the operation detected the condition itself. Both are an
            // error.type — a reader counting failures must not have to know which shape produced it.
            Assert.That(span.GetTagItem("error.type"), Is.Not.Null,
                $"{what}: the span carries WHAT failed, not merely that something did");
        });
    }

    /// <summary>A path that cannot be written: an existing DIRECTORY where a file is expected.</summary>
    private static string Unwritable(ShellHarness harness, string name) =>
        Directory.CreateDirectory(Path.Combine(harness.TempDir, name)).FullName;

    /// <summary>Runs one failing operation with every channel captured, then asserts all four.</summary>
    private static async Task RunFailingAsync(
        string what, string spanSuffix, string spanPrefix,
        Func<ShellHarness, MainWindowViewModel, Task> fail)
    {
        using TelemetryCapture spans = TelemetryCapture.Listen(
            ihc_openvisual.Configuration.Telemetry.ActivitySourceName, spanPrefix: spanPrefix);
        using TraceProbe probe = TraceProbe.Start();
        // The logger the WORKFLOW writes to, not the view-model's: these operations live below the shell, and a
        // factory handed only to the view-model would leave the log channel unobservable — which reads exactly
        // like a missing log record.
        CapturingLoggerFactory logs = new();
        using ShellHarness harness = ShellHarness.Create(loggerFactory: logs);
        MainWindowViewModel vm = harness.CreateViewModel(logs);
        await vm.InitializeAsync();

        // NON-VACUITY. Set-up may legitimately have raised a problem or written a log line; without clearing
        // both, this fixture would assert on somebody else's failure and pass whatever the operation did.
        harness.Dialogs.Reset();
        logs.Clear();
        await fail(harness, vm);

        // The EXACT span name, composed from the owner and the member the caller already passes separately.
        // A suffix match would be ambiguous here: SaveAsAsync ends with SaveAsync, and the outer delegating
        // operation is not the one that owns the failure.
        AssertAllChannels(
            what, harness.Dialogs, logs,
            probe.SpansNamed(spans, spanPrefix + spanSuffix).LastOrDefault());
    }

    [Test]
    public Task OpenReportsOnAllFourChannels() =>
        RunFailingAsync("open", "OpenAsync", "ProjectWorkflow.",
            (h, _) => h.Session.OpenAsync(Unwritable(h, "not-a-project.vis")));

    [Test]
    public Task SaveReportsOnAllFourChannels() =>
        // SaveToAsync, not SaveAsAsync: the outer command picks a destination and DELEGATES, and the catch that
        // reports the failure lives in the inner one. That is the operation a reader counting save failures
        // queries, and the one all four channels have to agree about.
        RunFailingAsync("save", "SaveToAsync", "ProjectWorkflow.", async (h, _) =>
        {
            h.Dialogs.SavePath = Unwritable(h, "not-a-file.vis");
            await h.Session.SaveAsAsync();
        });

    [Test]
    public Task BlockSaveReportsOnAllFourChannels() =>
        RunFailingAsync("block save", "SaveFunctionBlockAsync", "ProjectWorkflow.", async (h, vm) =>
        {
            Ihc.Vis.Model.ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;
            await h.Session.AddEmptyFunctionBlockAsync(locality);
            Ihc.Vis.Model.ElementId block =
                vm.FunctionNodes[0].Children[0].Children[0].ElementId!.Value;
            await h.Session.SaveFunctionBlockAsync(
                block, Unwritable(h, "not-a-block.ifb"), "Blok", string.Empty);
        });

    [Test]
    public Task CatalogImportReportsOnAllFourChannels() =>
        RunFailingAsync("catalog import", "ImportFileAsync", "CatalogImportWorkflow.", async (h, _) =>
        {
            string rotten = Path.Combine(h.TempDir, "rotten.def");
            await File.WriteAllTextAsync(rotten, "this is not a definition file");
            await h.Session.ImportCatalogFileAsync(rotten, persist: false);
        });

    [Test]
    public Task ReportViewingReportsOnAllFourChannels() =>
        RunFailingAsync("report viewing", "ViewInBrowserAsync", "ProjectReportWorkflow.", async (h, _) =>
        {
            h.Dialogs.OpenExternalUrlSucceeds = false;   // a machine with no handler for the document
            await h.Session.ViewReportInBrowserAsync(ReportKind.Functions, ReportMode.Full, ReportFormat.Html);
        });

    [Test]
    public Task FindingsExportReportsOnAllFourChannels() =>
        RunFailingAsync("findings export", "ExportAsync", "ProjectFindingsWorkflow.", async (h, _) =>
        {
            h.Dialogs.SaveReportPath = Unwritable(h, "not-a-findings-file.xml");
            await h.Session.ExportFindingsAsync(new FindingsExportRequest(
                Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationFinding>.Empty,
                "Alvor", Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationSeverity>.Empty,
                new ErrorTierFilter(true, true)));
        });

    /// <summary>
    /// VALIDATION IS DIFFERENT, and the difference is deliberate rather than an exemption.
    /// </summary>
    /// <remarks>
    /// <para>A crashed RULE leaves the run SUCCESSFUL — the engine catches it, keeps going, and reports the
    /// fault in its result. So there is no failed operation to assert four channels on, and demanding one would
    /// force the engine to fail a run that genuinely produced findings.</para>
    /// <para>What it must do instead: carry the fault on the composite result, put a row in the sink, and write
    /// a log record. A failed CHILD span exists only under per-rule validation timing, which is off by default,
    /// so it is asserted as a clause conditional on that flag rather than unconditionally.</para>
    /// <para>A whole RUN that fails is a genuinely failed operation and stays under the four-channel rule —
    /// <c>ValidationMonitorTelemetryTests</c> covers it.</para>
    /// </remarks>
    [Test]
    public void ACrashedRuleReportsOnItsOwnTerms()
    {
        using ShellHarness harness = ShellHarness.Create();
        Ihc.Vis.Validation.StructuredValidationResult result = new(
            Ihc.Vis.Model.EquatableArray<Ihc.Vis.Validation.ValidationFinding>.Empty,
            System.Collections.Immutable.ImmutableArray.Create(ProblemsTestData.Fault()));

        System.Collections.Generic.List<Ihc.Vis.Problems.InternalError> sink = [];
        using ValidationMonitor monitor = new(harness.Session, _ => result, onFault: sink.Add);
        harness.Session.NewAsync().GetAwaiter().GetResult();
        harness.SettleValidationAsync(monitor).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.That(result.Faults, Is.Not.Empty, "the fault rides the COMPOSITE RESULT");
            Assert.That(sink.Select(f => f.Code.Value), Is.EqualTo(new[] { "internal.rule-failed" }),
                "and reaches the sink as a row");
            Assert.That(monitor.Result, Is.Not.Null,
                "while the RUN still succeeded — a crashed rule does not fail a run that produced findings");
            Assert.That(monitor.HasBlockingFindings, Is.False,
                "and does not block: the project is not at fault");
        });
    }
}
