using System;
using System.Linq;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The one thing about Error findings that only a live run can show: that an edit made THROUGH THE GUI produces
/// one — and undoing it takes it away again — inside a single debounce-and-run cycle.
///
/// <para>It proves the loop end to end through the user's own route: a properties dialog commits an address,
/// the workflow raises its change, the background worker debounces and revalidates, and a new Error row reaches
/// the panel with no gesture but the edit itself. No headless test exercises those together, because the first
/// step is a real modal committing a real value.</para>
///
/// <para>The send GATE itself is not asserted here, and deliberately so: no controller is attached in an E2E
/// run and the connection refusal is checked first, so <c>Send projekt</c> is withheld whether or not the
/// project validates — a scenario about it could not fail for the reason it appeared to be about.
/// <c>ProblemsSendGateTests</c> drives the two gates independently, including the case where the visible reason
/// stays the connection one.</para>
/// </summary>
public class ProblemsSendGateE2ETests
{
    /// <summary>Findings, but no Errors and none about a duplicate address — a clean slate for Errors.</summary>
    private static string CleanFixture() => E2E.Fixture("Project1-SimpelWired.vis");

    [TearDown]
    public void CloseApp() => E2E.KillApp();

    /// <summary>
    /// AC2, driven through the GUI: author a duplicate address, watch an Error row appear, undo, watch it go.
    /// </summary>
    /// <remarks>
    /// The duplicate is authored the way a user would — the pin's own properties dialog, whose terminal list
    /// offers the in-use value explicitly as <c>"1 (i brug)"</c>. OpenVisual allows it (its <c>UpdatePin</c>
    /// refuses only out-of-range addresses) where the vendor tool disables OK on it; that asymmetry is exactly
    /// why the catalogue treats a duplicate address as a finding rather than a refusal, and it is what makes
    /// this scenario reachable at all.
    /// </remarks>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void AnAddressDuplicateAuthoredInTheGuiAppearsAsAnErrorAndUndoRemovesIt()
    {
        E2E.Launch(CleanFixture());
        E2E.Envelope before = E2E.WaitForBoundProblems();
        Assert.That(before.Number("errors"), Is.Zero, "precondition: the clean fixture carries no Errors");
        int warnings = before.Number("warnings");

        const string Pin = "Lokaliteter/Stue/LK FUGA Tryk 2 tast (Ved dør) /Tryk (højre)";
        E2E.RunOk("node", "select", "--path", Pin);
        E2E.RunOk("node", "get-properties");
        // Its sibling already holds terminal 1 on data line 1; the list marks it in use rather than hiding it.
        E2E.RunOk("dialog", "select-item", "--control", "TerminalList", "--item", "1 (i brug)");
        E2E.Run("dialog", "click", "--button", "OK");

        E2E.Envelope after = E2E.WaitForBoundProblems();
        Assert.Multiple(() =>
        {
            Assert.That(after.Number("errors"), Is.EqualTo(1),
                "one duplicate group, one Error — reaching the panel with no gesture but the edit itself");
            Assert.That(after.Number("warnings"), Is.LessThanOrEqualTo(warnings),
                "and the warning tally moves with the project rather than growing alongside a stale copy");
        });

        E2E.RunOk("edit", "undo");
        E2E.Envelope undone = E2E.WaitForBoundProblems();

        Assert.Multiple(() =>
        {
            Assert.That(undone.Number("errors"), Is.Zero, "undo takes the Error away again");
            Assert.That(undone.Number("warnings"), Is.EqualTo(warnings), "and the counts return to where they were");
        });
    }

}
