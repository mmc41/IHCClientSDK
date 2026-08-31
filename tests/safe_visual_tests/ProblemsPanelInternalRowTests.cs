using System;
using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.Services;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Problems;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The panel lists faults beside findings, and the state it reports has to account for both.
///
/// <para>The defect this fixture is written against is the one a start-up fault produces: it arrives before any
/// validation result is bound, so the panel would report <c>Validating</c> — "Validerer projektet..." — and paint
/// that sentence across the single row the user most needed to read.</para>
/// </summary>
[TestFixture]
public class ProblemsPanelInternalRowTests : AvaloniaTestBase
{
    private static InternalError Fault(string code = "app.openvisual.unexpected") =>
        ProblemsTestData.Fault(
            code, "Uventet fejl under 'Start'.", "boom", InternalErrorOrigin.Host, "at Startup()");

    /// <param name="showingAFault">Appends one fault before the panel is returned.</param>
    private static (ProblemsShellRig Rig, InternalErrorLog Log, ProblemsPanelViewModel Panel) PanelWithSink(
        bool showingAFault = false)
    {
        ProblemsShellRig rig = new();
        InternalErrorLog log = new();
        ProblemsPanelViewModel panel = new(rig.Harness.Session, rig.Harness.Session.Validation,
            internalErrors: log);
        if (showingAFault)
        {
            log.Append(Fault());
        }
        return (rig, log, panel);
    }

    /// <summary>
    /// Reproduce-first: a fault raised BEFORE the first validation result must not read as "validating".
    /// </summary>
    [Test]
    public void AFaultRaisedBeforeAnyResult_IsListed_AndDoesNotReadAsValidating()
    {
        var (rig, log, panel) = PanelWithSink();
        using var _ = rig;
        using var _p = panel;

        log.Append(Fault());

        Assert.Multiple(() =>
        {
            Assert.That(panel.Rows, Has.Count.EqualTo(1), "the fault is listed");
            Assert.That(panel.State, Is.Not.EqualTo(ProblemsState.Validating),
                "a panel with a row to show is not validating, whatever the run is doing — and the sentence "
                + "would otherwise be painted across that very row");
            Assert.That(panel.State, Is.EqualTo(ProblemsState.Findings),
                "resolving to Findings rather than a state of its own: Findings already means there are rows");
        });
    }

    /// <summary>A fault the sink already holds is picked up when the panel is built — the start-up case, where
    /// the fault exists before anything is on screen to show it.</summary>
    [Test]
    public void AFaultAlreadyInTheSink_IsListedAsSoonAsThePanelExists()
    {
        ProblemsShellRig rig = new();
        using var _ = rig;
        InternalErrorLog log = new();
        log.Append(Fault());

        using ProblemsPanelViewModel panel =
            new(rig.Harness.Session, rig.Harness.Session.Validation, internalErrors: log);

        Assert.That(panel.Rows, Has.Count.EqualTo(1));
    }

    [Test]
    public void AFaultRowSortsAboveEveryFinding()
    {
        var (rig, _log, panel) = PanelWithSink(showingAFault: true);
        using var _ = rig;
        using var _p = panel;

        Assert.That(panel.Rows.First(), Is.TypeOf<InternalErrorRowViewModel>(),
            "Internal is the worst tier, and the default sort is by tier — the tool failing outranks anything "
            + "the tool reports");
    }

    [Test]
    public void ClearingTheSink_RemovesItsRows()
    {
        var (rig, log, panel) = PanelWithSink();
        using var _ = rig;
        using var _p = panel;
        log.FollowGeneration(1);
        log.Append(Fault());

        log.FollowGeneration(2);

        Assert.That(panel.Rows, Is.Empty, "the sink owns the lifetime, and the panel follows it");
    }

    /// <summary>The counts are per tier, so a fault increments the Internal chip and nothing else.</summary>
    [Test]
    public void AFaultCountsOnTheInternalChipAlone()
    {
        var (rig, _log, panel) = PanelWithSink(showingAFault: true);
        using var _ = rig;
        using var _p = panel;

        Assert.Multiple(() =>
        {
            Assert.That(panel.Internals.Count, Is.EqualTo(1));
            Assert.That(panel.Tiers.Where(t => t.Tier != ProblemsTier.Internal).Select(t => t.Count),
                Is.All.Zero);
        });
    }

    /// <summary>Hiding the Internal tier hides its rows, exactly as every other tier toggle does. A tier is a
    /// filtering key, and that is all it is.</summary>
    [Test]
    public void HidingTheInternalTier_HidesItsRows()
    {
        var (rig, _log, panel) = PanelWithSink(showingAFault: true);
        using var _ = rig;
        using var _p = panel;

        panel.Internals.IsShown = false;

        Assert.Multiple(() =>
        {
            Assert.That(panel.Rows, Is.Empty);
            Assert.That(panel.Internals.Count, Is.EqualTo(1),
                "the COUNT is of the whole result: switching a tier off must never look like its faults were fixed");
        });
    }
}
