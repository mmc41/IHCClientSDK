using System;

using ihc_openvisual.Services;

using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// That the fault assertion every scenario now carries is reading something REAL.
/// </summary>
/// <remarks>
/// <para>Without this, the assertion is a green light with nothing behind it — and it very nearly was. The
/// headless driver used to build its <c>ProjectAppService</c> with no fault port, its <c>ProjectWorkflow</c>
/// with no sink, its shell with no log, and never pointed the static supervisor anywhere. A count added on top
/// of that would have been incremented by nothing and compared equal on every CI run for ever, which is a worse
/// outcome than not asserting at all: it reports the absence of an observer as the absence of faults.</para>
///
/// <para>So this raises a real fault through a real layer and requires the count to move. It is a test of the
/// DRIVER's wiring rather than a scenario about the product, so it is not counted as one: the end-to-end bar
/// admits scenarios, and this is a control, like <see cref="DialogFocusProbeTests"/>.</para>
/// </remarks>
public class FaultReportingTests
{
    /// <summary>Deliberately NOT an <see cref="E2EScenario"/>: this fixture raises the fault on purpose.</summary>
    private const string FixtureFile = "Project1-SimpelWired.vis";

    [Test]
    public void AFaultInsideTheApplicationMovesTheCountEveryScenarioAssertsOn()
    {
        if (!E2E.Headless)
        {
            // The probe reports through a process-wide static, so it only reaches the application when the two
            // share a process. Against the real executable it would raise the fault in the TEST process and the
            // application would rightly report nothing — a red test about the probe, not about the product.
            Assert.Ignore("The probe and the application must share a process; that is the in-process driver.");
        }

        E2E.Launch(E2E.Fixture(FixtureFile));
        try
        {
            int before = E2E.RunOk("session", "faults").Number("appended");

            TaskSupervisor.Report(
                new InvalidOperationException("fault-wiring probe"), $"{nameof(FaultReportingTests)}.probe");

            E2E.Envelope after = E2E.RunOk("session", "faults");
            Assert.Multiple(() =>
            {
                Assert.That(after.Number("appended"), Is.EqualTo(before + 1),
                    "the driver's fault wiring is dead: a fault raised through the supervisor reached no log, so "
                    + "every scenario's fault assertion is comparing a counter nothing increments");
                Assert.That(after.Text("last"), Is.Not.Empty,
                    "the count moved but named no code, so a red scenario would say THAT something faulted "
                    + "without saying what");
            });
        }
        finally
        {
            // Also detaches the supervisor's port, so the fault raised here cannot be delivered into whatever
            // fixture runs next.
            E2E.KillApp();
        }
    }
}
