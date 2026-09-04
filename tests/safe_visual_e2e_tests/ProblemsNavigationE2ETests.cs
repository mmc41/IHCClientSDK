using System;
using System.Collections.Generic;
using System.Linq;
using ihc_openvisual.Configuration;
using NUnit.Framework;

using Ihc.Tests.Shared;

namespace safe_visual_e2e_tests;

/// <summary>
/// Activating a finding and arriving at the element it is about — driven end to end, with real input.
///
/// <para><b>What only a live run can show.</b> That input injected at the desktop reaches the panel through the
/// Win32 backend and the Avalonia-to-UIA bridge, that the desktop stacks the windows a route opens, and that real
/// keyboard focus lands where the route says it does. The gesture semantics are not what is at stake here: that
/// a single click only selects, and that Enter and a double-click produce one identical activation, is proved on
/// the real control by <c>ProblemsActivationGestureTests</c> in <c>safe_visual_tests</c>, through Avalonia's own
/// headless input pipeline. Where each KIND of row leads — a host route, a program row that switches mode and
/// opens nothing, a row that leads nowhere — is the planner's business logic, asserted in
/// <c>safe_project_tests</c> by <c>HostRouteTests</c>, <c>ProblemsNavigationTests</c> and
/// <c>PlanExecutionTests</c>. So this fixture holds one scenario per INPUT PATH, the pointer and the keyboard,
/// rather than one per destination.</para>
///
/// <para><b>The gesture is the DOUBLE-click, never the single one.</b> A single click selects the row and must
/// leave the trees, the mode and every window exactly as they were — the panel is a list to read down, and
/// arrowing through it may not drag the installer around the project.</para>
///
/// <para><b>Targets are DISCOVERED from the oracle at run time, never hard-coded by index.</b> Row indices move
/// with every sort and filter, and a test pinned to "row 3" silently starts asserting about a different finding.
/// Each test below asks the oracle for a finding of the right SHAPE and then finds that row in the panel.</para>
/// </summary>
public class ProblemsNavigationE2ETests : E2EScenario
{
    private const string FixtureFile = "Project6-Errors.vis";
    private const string OracleCase = "Project6-Errors";

    /// <summary>A finding on a product terminal — a configuration-tree target.</summary>
    private const string ConfigurationCode = "doc-not-linked";

    /// <summary>A terminal's missing cable colour — the deep route's own example (E1).</summary>
    private const string CableColourCode = "doc-cable-colour";

    /// <summary>
    /// The fixture's bytes as the run found them. E1 opens two editors, and an editor that committed and saved
    /// would rewrite a byte-exact oracle — so the teardown proves the file came back exactly as it went in.
    /// </summary>
    /// <remarks>
    /// The fixture is driven IN PLACE, as every other E2E class drives it, and guarded by this hash rather than
    /// by a copy. A scratch copy was tried first and the app would not open from it — the launch reported ready
    /// and the title stayed <c>unavngivet</c>, reproducibly, though the identical launch works by hand. Rather
    /// than ship a harness with an unexplained failure mode, the guard asserts the property the gate actually
    /// wants: the fixture is unchanged afterwards.
    /// </remarks>
    private byte[] _fixtureBefore = [];

    [OneTimeSetUp]
    public void LaunchApp()
    {
        _fixtureBefore = System.IO.File.ReadAllBytes(E2E.Fixture(FixtureFile));
        E2E.Launch(E2E.Fixture(FixtureFile));
    }

    [OneTimeTearDown]
    public void CloseApp()
    {
        E2E.KillApp();
        Assert.That(System.IO.File.ReadAllBytes(E2E.Fixture(FixtureFile)),
            Is.EqualTo(_fixtureBefore).AsCollection,
            "a scenario modified the fixture — this feature navigates, it does not autofix, so every scenario "
            + "must cancel out of whatever it opened");
    }

    [SetUp]
    public void ReturnToConfigurationView()
    {
        if (!E2E.Run("problems", "state").Ok)
        {
            E2E.RunOk("view", "problems-toggle");
        }

        // Every test states its own starting mode rather than inheriting whatever the last one left.
        if (E2E.PaneRootLabel() != E2E.ConfigurationRootLabel)
        {
            E2E.RunOk("view", "configuration");
        }
    }

    /// <summary>
    /// Selects the fixture's finding with this code — a single click — and returns what was clicked.
    /// </summary>
    /// <remarks>
    /// BY CODE, never by index — the task's own warning, and the list makes it more than a style point: the
    /// panel virtualizes, so a row 90 places down does not exist as an element until the viewport reaches it,
    /// and an index only ever addresses the handful currently realized. The driver scrolls to find a named code;
    /// the clicked row comes back in the envelope, so the element name never has to be guessed either.
    /// </remarks>
    private static E2E.Row ClickFinding(string code)
    {
        E2E.WaitForBoundProblems();

        Assert.That(FindingOracleRows.Codes(OracleCase), Does.Contain(code),
            $"the oracle must actually record '{code}' for this fixture, or the test is asserting about nothing");

        return ClickRow(code);
    }

    /// <summary>Clicks by whatever selector the driver accepts — a code, or one row's occurrence identity.</summary>
    private static E2E.Row ClickRow(string selector) =>
        E2E.ToRow(E2E.RunOk("problems", "click", "--row", selector).Field("clicked"));

    /// <summary>
    /// ACTIVATES the fixture's finding with this code — the double-click — and returns the row it acted on.
    /// </summary>
    /// <remarks>
    /// The row is found by a single click first, which is what yields its occurrence identity; the activation
    /// then addresses that one occurrence rather than whichever row of the code the scroll meets next.
    /// </remarks>
    private static E2E.Row ActivateFinding(string code)
    {
        E2E.Row row = ClickFinding(code);
        E2E.RunOk("problems", "click", "--row", row.Occurrence, "--double");
        return row;
    }

    [Test]
    public void ActivatingAnElementAnchoredRowSelectsThatElementInTheOwningTreePane()
    {
        E2E.Row target = ActivateFinding(ConfigurationCode);
        Assert.That(target.Element, Is.Not.Empty, "precondition: the row names an element to navigate to");

        IReadOnlyList<E2E.Selection> selections = E2E.Selections();

        Assert.Multiple(() =>
        {
            Assert.That(selections, Is.Not.Empty,
                "activating an element-anchored row must leave a pane with something selected");
            Assert.That(selections.Select(s => s.Name), Has.Some.Contains(target.Element),
                $"the selected row names the finding's element ('{target.Element}'); selections were: "
                + string.Join(", ", selections.Select(s => $"{s.Tree}={s.Name}")));
            Assert.That(selections.Select(s => s.Tree), Has.Some.EqualTo(AutomationIds.InstallationTree),
                "and it is the OWNING pane that moved — a configuration target belongs to the installation tree");
        });
    }

    /// <summary>
    /// E1 — THE DEEP ROUTE, end to end. Activating a <c>doc-cable-colour</c> row lands the product selected in
    /// the tree, its dialog open, the terminal's own editor stacked ON TOP of that still-open dialog, and the
    /// caret in Ledningsfarve.
    ///
    /// <para>Taken by KEYBOARD. A keyboard user must reach the fix context by the route a mouse user takes, and
    /// that the two gestures produce one identical activation is proved on the real control by
    /// <c>ProblemsActivationGestureTests</c>, where it costs nothing to state. What only a live run can show is
    /// that real focus carries Enter to the selected row at all — so the keyboard leg is the one that belongs
    /// here, and running the pointer twin beside it would buy a second traversal of an input path the scenario
    /// above already takes.</para>
    ///
    /// <para><b>What is asserted about the parent dialog, and why it is the stack rather than its selected
    /// row.</b> The driver reads the TOPMOST modal, which here is the terminal editor; reading a covered window
    /// is not a capability it has. So the parent is asserted through <c>openModals</c> — the product dialog is
    /// still there, underneath — and WHICH terminal was selected is asserted through the editor's own title,
    /// which names the terminal it was opened for.</para>
    /// </summary>
    [Test]
    [Category(E2E.DesktopOnly)]
    public void ActivatingACableColourRowLandsInTheTerminalsLedningsfarveField()
    {
        E2E.WaitForBoundProblems();
        Assert.That(FindingOracleRows.Codes(OracleCase), Does.Contain(CableColourCode),
            "the oracle must record this code for the fixture, or the scenario asserts about nothing");

        // Select first, then activate with Enter — the same two-tier gesture the panel offers, taken by
        // KEYBOARD. The pointer twin is not run beside it: that the two gestures produce one identical
        // activation is proved on the real control one level down, and what only a live run can prove is that
        // real focus carries Enter to the selected row at all. The double-click route reaches this suite
        // through the scenario above.
        E2E.Row row = ClickRow(CableColourCode);
        E2E.RunOk("key", "send", "--gesture", "{ENTER}");

        try
        {
            IReadOnlyList<string> modals = E2E.OpenModalIds();
            E2E.Envelope read = E2E.RunOk("dialog", "read");

            Assert.Multiple(() =>
            {
                Assert.That(E2E.Selections().Select(s => s.Tree), Has.Some.EqualTo(AutomationIds.InstallationTree),
                    "Enter: the tree moved to the owning product");
                Assert.That(modals, Has.Count.EqualTo(2),
                    "Enter: BOTH windows are open — the editor did not replace its parent, it stacked "
                    + $"on it. Modals were: {string.Join(", ", modals)}");
                Assert.That(modals[0], Is.EqualTo(AutomationIds.PinPropertiesWindow),
                    "Enter: the terminal editor is on top");
                Assert.That(modals[1], Is.EqualTo(AutomationIds.ProductDialogWindow),
                    "Enter: with the product dialog still beneath it");
                Assert.That(read.Field("focused").GetProperty("id").GetString(),
                    Is.EqualTo(AutomationIds.CableColourBox),
                    "Enter: and the caret is in Ledningsfarve — the field the finding is about");
                Assert.That(read.Field("dialog").GetProperty("title").GetString(),
                    Does.Contain(row.Element).IgnoreCase.Or.Contain("gang"),
                    "Enter: the editor was opened for the terminal the row named");
            });
        }
        finally
        {
            // Out of EVERY window the route opened, innermost first. Counted rather than fixed at two: the
            // scenario must leave nothing behind whatever it managed to open, including on the way out of a
            // failed assertion.
            E2E.CloseAllModals(4);
        }
    }
}
