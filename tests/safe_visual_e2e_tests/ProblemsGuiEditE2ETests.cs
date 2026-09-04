using System;
using System.Linq;
using ihc_openvisual.Configuration;
using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The one thing about Error findings that only a live run can show: that an edit made THROUGH THE GUI produces
/// one, inside a single debounce-and-run cycle.
///
/// <para>It proves the loop end to end through the user's own route: a properties dialog's controls are operated
/// over the bridge and commit an address, the workflow raises its change, the background worker debounces and
/// revalidates, and a new Error row reaches the panel with no gesture but the edit itself. It is the only
/// scenario in the suite that OPERATES a dialog rather than merely opening one, which is what earns it its
/// level. What happens after the row arrives is not desktop business: that undo takes it away again is
/// <c>ProblemsPanelViewModelTests</c>' claim and the send gate's reopening is <c>ProblemsSendGateTests</c>',
/// both one level down.</para>
///
/// <para>The send GATE itself is not asserted here, and deliberately so: no controller is attached in an E2E
/// run and the connection refusal is checked first, so <c>Send projekt</c> is withheld whether or not the
/// project validates — a scenario about it could not fail for the reason it appeared to be about.
/// <c>ProblemsSendGateTests</c> drives the two gates independently, including the case where the visible reason
/// stays the connection one.</para>
/// </summary>
public class ProblemsGuiEditE2ETests : E2EScenario
{
    /// <summary>Findings, but no Errors and none about a duplicate address — a clean slate for Errors.</summary>
    private static string CleanFixture() => E2E.Fixture("Project1-SimpelWired.vis");

    /// <summary>
    /// This fixture launches inside its test body rather than in a setup, so the kill is the fixture's rather
    /// than the test's. It has to run AFTER the base class's fault assertion, and NUnit runs a derived
    /// <c>[TearDown]</c> BEFORE the base one — which would have killed the application before anything could
    /// ask it what faulted. A second scenario added here would still start clean: <see cref="E2E.Launch"/>
    /// kills a survivor before it launches.
    /// </summary>
    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

    /// <summary>
    /// AC2, driven through the GUI: author a duplicate address and watch an Error row appear.
    /// </summary>
    /// <remarks>
    /// <para>The duplicate is authored the way a user would — the pin's own properties dialog, whose terminal
    /// list offers the in-use value explicitly as <c>"1 (i brug)"</c>. OpenVisual allows it (its
    /// <c>UpdatePin</c> refuses only out-of-range addresses) where the vendor tool disables OK on it; that
    /// asymmetry is exactly why the catalogue treats a duplicate address as a finding rather than a refusal, and
    /// it is what makes this scenario reachable at all.</para>
    ///
    /// <para>The document is left dirty on purpose: the fixture ends in a kill, nothing saves, and undoing the
    /// edit here would only re-assert what the view-model tests already own.</para>
    /// </remarks>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void AnAddressDuplicateAuthoredInTheGuiAppearsAsAnError()
    {
        E2E.Launch(CleanFixture());
        E2E.Envelope before = E2E.WaitForBoundProblems();
        Assert.That(before.Number("errors"), Is.Zero, "precondition: the clean fixture carries no Errors");

        const string Pin = "Lokaliteter/Stue/LK FUGA Tryk 2 tast (Ved dør) /Tryk (højre)";
        E2E.RunOk("node", "select", "--path", Pin);
        E2E.RunOk("node", "get-properties");
        // Its sibling already holds terminal 1 on data line 1; the list marks it in use rather than hiding it.
        E2E.RunOk("dialog", "select-item", "--control", AutomationIds.TerminalList, "--item", "1 (i brug)");
        // By its automation id, not by the Danish word on it: the driver accepts either, but only the id is a
        // declared contract that AutomationIdConstantsTests holds the dialog to. Addressed by label, a relabelled
        // button breaks this scenario on the desktop while every gate that could have caught it stays green.
        E2E.Run("dialog", "click", "--button", AutomationIds.OkButton);

        E2E.Envelope after = E2E.WaitForBoundProblems();
        Assert.That(after.Number("errors"), Is.EqualTo(1),
            "one duplicate group, one Error — reaching the panel with no gesture but the edit itself");
    }
}
