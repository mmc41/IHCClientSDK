using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Model;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Vis.Tests;

/// <summary>
/// What the findings projection costs, and by how much.
///
/// Binding a validation result walks the snapshot to index it by id and then builds one row per finding, ON
/// THE UI THREAD. So the cost is a function of the finding count and is paid on every validation run - and a
/// project that produces a great many findings pays it repeatedly. Without the count, a slow bind is just a
/// slow bind; with it, the duration is explicable.
///
/// <para><b>Both tests scope their spans through a <see cref="TraceProbe"/>.</b> The bind runs inside the
/// debounced validation run, which links back to the edit that armed it rather than parenting to it, so the
/// probe reaches the bind through that link — see <see cref="TraceProbe"/> for why picking the span by name or
/// by start time cannot work over a process-wide capture.</para>
/// </summary>
[TestFixture]
public class ProblemsPanelBindTelemetryTests
{
    /// <summary>The bind span, and the run it is posted from — the run carries the link back to the probe.</summary>
    private static readonly string[] BindAndItsRun =
        ["ProblemsPanelViewModel.Bind", "ValidationWorker.Run"];

    /// <summary>
    /// The gate's assertion: the number on the span is the number the panel shows. Asserting them EQUAL is
    /// what makes the attribute trustworthy - a count derived separately could drift from the rows.
    /// </summary>
    [Test]
    public async Task TheBindSpanReportsTheSameFindingCountThePanelDisplays()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: BindAndItsRun))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();
            await vm.InitializeAsync();

            // A duplicate address is the fixture-free way to produce a known, non-zero set of findings.
            ElementId locality = vm.InstallationNodes[0].Children[0].ElementId!.Value;

            // The probe covers the edit that arms the run whose bind this test is about. The earlier
            // notification from InitializeAsync coalesces into the same pending request, and the LAST notify's
            // context is the one the run links back to - so the probe has to be open across this edit.
            using TraceProbe probe = TraceProbe.Start();
            await harness.Session.AddEmptyFunctionBlockAsync(locality);
            await harness.SettleValidationAsync();

            Activity bind = probe.Span(capture, "ProblemsPanelViewModel.Bind");
            int reported = (int)bind.GetTagItem("ihc.validation.finding_count")!;

            Assert.Multiple(() =>
            {
                Assert.That(harness.Session.Validation.Result, Is.Not.Null, "a result must have bound");
                Assert.That(reported, Is.EqualTo(harness.Session.Validation.Result!.Findings.Count),
                    "the span's count IS the bound result's count, not a separately derived number");
            });
        }
    }

    [Test]
    public async Task ABindWithNoFindings_ReportsZeroRatherThanOmittingTheCount()
    {
        using (TelemetryCapture capture = TelemetryCapture.Listen(ihc_openvisual.Configuration.Telemetry.ActivitySourceName,
            spanNames: BindAndItsRun))
        {
            using ShellHarness harness = ShellHarness.Create();
            MainWindowViewModel vm = harness.CreateViewModel();

            using TraceProbe probe = TraceProbe.Start();
            await vm.InitializeAsync();
            await harness.SettleValidationAsync();

            var binds = probe.SpansNamed(capture, "ProblemsPanelViewModel.Bind");
            Assert.That(binds, Is.Not.Empty, "the panel binds the first result too");
            Assert.That(binds.All(s => s.GetTagItem("ihc.validation.finding_count") is not null), Is.True,
                "an absent count and a count of zero must not look the same");
        }
    }
}
