using System;
using System.IO;

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
///
/// <para><b>Both channels, because a sweep now reads both.</b> <see cref="ProjectCorpusLoadE2ETests"/> asserts
/// the cumulative tally AND the Intern fejl count, and the second is a separate path — the log announces
/// through the host's marshal and the panel projects the rows. Left unchecked it is a second counter that could
/// report zero for ever while faults piled up behind it, which is the same false green in a new place.</para>
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

            // AFTER the fault verb, which runs the dispatcher's pending jobs: the log announces its change
            // through the host's marshal at background priority, so the panel has not heard about the fault
            // until something delivers that — and in a headless session a verb dispatched at normal priority
            // runs first.
            E2E.Envelope after = E2E.RunOk("session", "faults");
            E2E.Envelope panel = E2E.WaitForBoundProblems();
            Assert.Multiple(() =>
            {
                Assert.That(after.Number("appended"), Is.EqualTo(before + 1),
                    "the driver's fault wiring is dead: a fault raised through the supervisor reached no log, so "
                    + "every scenario's fault assertion is comparing a counter nothing increments");
                Assert.That(after.Text("last"), Is.Not.Empty,
                    "the count moved but named no code, so a red scenario would say THAT something faulted "
                    + "without saying what");
                Assert.That(E2E.TierCount(panel, "internals"), Is.GreaterThan(0),
                    "the fault was recorded but reached no Intern fejl row, so the panel's internal count is a "
                    + "number nothing increments — and every sweep asserting it is zero would pass over a tool "
                    + "that had failed in front of the user");
            });
        }
        finally
        {
            // Also detaches the supervisor's port, so the fault raised here cannot be delivered into whatever
            // fixture runs next.
            E2E.KillApp();
        }
    }

    /// <summary>
    /// The other half of the same control: <c>project open</c> RETURNS when the application cannot read the file,
    /// and says what happened through the document it is still showing and the dialog standing in front of it.
    /// </summary>
    /// <remarks>
    /// <para>Here rather than in <see cref="ProjectCorpusLoadE2ETests"/> for the reason that sweep is written the
    /// way it is: every authentic oracle opens, so the sweep never reaches this path and could not tell a verb
    /// that reports a failed open from one that cannot come back from it at all. The input is authored here — a
    /// file that exists and is not a project — rather than borrowed from an oracle, so what it tests is the
    /// verb's contract and not some fixture's incidental unreadability.</para>
    ///
    /// <para><b>What it would have caught.</b> The in-process driver awaited the shell's open command. A failed
    /// open raises a modal, and in-process <c>ShowDialog</c> does not return until something closes it — so the
    /// verb never answered, the harness blocked on it, and no later verb could dismiss the dialog. The whole run
    /// hung, with the sweep's own "left a dialog standing" assertion never reached. Re-introducing that await
    /// presents HERE as a hang rather than as a red assertion, which is the same failure mode the driver's own
    /// dispatch wrapper and its one-window rule are documented against: the two lines below are only reached by
    /// a verb that came back.</para>
    ///
    /// <para>Not an <see cref="E2EScenario"/>, like its sibling above: this one fails an open on purpose. The
    /// failure is an anticipated one — malformed content — so it words itself as a coded outcome and leaves no
    /// <i>Intern fejl</i> row, which is why nothing here asserts about the tally.</para>
    /// </remarks>
    [Test]
    public void OpeningAnUnreadableFileAnswersTheVerbRatherThanBlockingOnItsOwnDialog()
    {
        string unreadable = Path.Combine(
            Path.GetTempPath(), $"ihc-e2e-not-a-project-{Guid.NewGuid():N}.vis");
        File.WriteAllText(unreadable, "this is not a project");
        E2E.Launch(E2E.Fixture(FixtureFile));
        try
        {
            E2E.Envelope opened = E2E.RunOk("project", "open", "--path", unreadable);

            Assert.Multiple(() =>
            {
                Assert.That(E2E.OpenModalIds(), Is.Not.Empty,
                    "the application reports a file it cannot read through a modal, so a verb that came back "
                    + "with none of them standing did not reach the failure it was supposed to provoke");
                Assert.That(E2E.DocumentOf(opened), Is.EqualTo(FixtureFile),
                    "a failed open leaves the previous document bound, and the verb has to publish THAT — a "
                    + "sweep asserting the document changed is the only thing standing between a broken oracle "
                    + "and a green run");
            });
        }
        finally
        {
            // Before the kill: the open's own activation is still waiting on this dialog, and closing it is what
            // lets that finish rather than being torn down mid-flight.
            E2E.CloseAllModals();
            E2E.KillApp();
            File.Delete(unreadable);
        }
    }
}
