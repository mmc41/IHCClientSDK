using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The shell's two boundaries: start-up, and the catch every command funnels through.
///
/// Start-up used to produce NO span at all - the work between launch and a usable window hung off nothing, so
/// it could not be seen as one operation and its failures were reported unlike every other command's. The
/// error boundary itself swallows on purpose (the installer gets one fixed Danish sentence rather than an
/// English diagnostic naming element tags); that contract is unchanged, and the point is that swallowing is
/// no longer the same as vanishing.
/// </summary>
[TestFixture]
public class ShellErrorBoundaryTelemetryTests
{
    [Test]
    public async Task Startup_EmitsANamedSpan()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "MainWindowViewModel."))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();

            using TraceProbe probe = TraceProbe.Start();
            await vm.InitializeAsync();

            Activity startup = probe.Span(capture, "MainWindowViewModel.InitializeAsync");
            Assert.Multiple(() =>
            {
                Assert.That(startup.Status, Is.EqualTo(ActivityStatusCode.Unset), "a clean start-up is not an error");
                Assert.That(startup.GetTagItem("ihc.edit.status"), Is.EqualTo("ok"));
            });
        }
    }

    /// <summary>
    /// A start-up that fails must report like any other command failure - and still reach the Danish dialog.
    /// </summary>
    [Test]
    public async Task AFailingCommand_ReachesTheDanishDialog_AndCarriesTheNormalizedErrorType()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanPrefix: "MainWindowViewModel."))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();

            // A faulting StateChanged subscriber makes the next command throw inside the boundary.
            harness.Session.StateChanged += (_, _) => throw new System.TimeoutException("a faulting refresh");

            using TraceProbe probe = TraceProbe.Start();
            await vm.NewCommand.ExecuteAsync(null);

            // The failing span is THIS command's, found by the probe rather than by taking the last error in a
            // process-wide capture — which would be whichever fixture errored most recently.
            Activity failed = probe.Spans(capture).Single(s => s.Status == ActivityStatusCode.Error);
            Assert.Multiple(() =>
            {
                Assert.That(failed.GetTagItem("ihc.edit.status"), Is.EqualTo("failed"));
                Assert.That(failed.GetTagItem("error.type"), Is.EqualTo("System.TimeoutException"),
                    "the normalized type, not the English diagnostic the installer never sees");
                Assert.That(harness.Dialogs.LastProblem, Is.Not.Null,
                    "the Danish problem dialog still appears - the boundary's contract is unchanged");
            });
        }
    }
}
