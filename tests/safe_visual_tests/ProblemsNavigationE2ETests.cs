using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Clicking a finding and arriving at the element it is about — driven end to end, with real pointer input.
///
/// <para><b>This is the one behaviour that cannot be proved headlessly.</b> The headless tests assert that the
/// owning pane's selected property moves; only a live run proves that a POINTER CLICK on a row produces that,
/// through the app's real hit-testing, selection and mode machinery. The click is deliberately not a
/// selection call: setting the selection directly would reach the outcome by a route no user can take, and the
/// route is the thing under test.</para>
///
/// <para><b>Targets are DISCOVERED from the oracle at run time, never hard-coded by index.</b> Row indices move
/// with every sort and filter, and a test pinned to "row 3" silently starts asserting about a different finding.
/// Each test below asks the oracle for a finding of the right SHAPE and then finds that row in the panel.</para>
/// </summary>
[Explicit("Launches the real desktop app; run deliberately with --filter \"TestCategory=E2E\".")]
[Category(E2E.Category)]
public class ProblemsNavigationE2ETests
{
    private const string FixtureFile = "Project6-Errors.vis";
    private const string OracleCase = "Project6-Errors";

    /// <summary>The fixture's whole-project finding: its first oracle row, and the panel's non-navigable one.</summary>
    private const string WholeProjectCode = "doc-project-info-blank";

    /// <summary>A finding on a product terminal — a configuration-tree target.</summary>
    private const string ConfigurationCode = "doc-not-linked";

    /// <summary>A finding on a variable inside a function block's program — reachable only in programming view.</summary>
    private const string ProgrammingCode = "logic-variable-unused";

    [OneTimeSetUp]
    public void LaunchApp() => E2E.Launch(E2E.Fixture(FixtureFile));

    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

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
    /// Clicks the fixture's finding with this code and returns what was clicked.
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

        Assert.That(E2E.OracleCodes(OracleCase), Does.Contain(code),
            $"the oracle must actually record '{code}' for this fixture, or the test is asserting about nothing");

        E2E.Envelope click = E2E.RunOk("problems", "click", "--row", code);
        System.Text.Json.JsonElement clicked = click.Field("clicked");
        return new E2E.Row(
            clicked.GetProperty("index").GetInt32(),
            clicked.GetProperty("code").GetString() ?? string.Empty,
            clicked.GetProperty("severity").GetString() ?? string.Empty,
            clicked.GetProperty("message").GetString() ?? string.Empty,
            clicked.GetProperty("element").GetString() ?? string.Empty);
    }

    [Test]
    public void ClickingAnElementAnchoredRowSelectsThatElementInTheOwningTreePane()
    {
        E2E.Row target = ClickFinding(ConfigurationCode);
        Assert.That(target.Element, Is.Not.Empty, "precondition: the row names an element to navigate to");

        IReadOnlyList<E2E.Selection> selections = E2E.Selections();

        Assert.Multiple(() =>
        {
            Assert.That(selections, Is.Not.Empty,
                "a click on an element-anchored row must leave a pane with something selected");
            Assert.That(selections.Select(s => s.Name), Has.Some.Contains(target.Element),
                $"the selected row names the finding's element ('{target.Element}'); selections were: "
                + string.Join(", ", selections.Select(s => $"{s.Tree}={s.Name}")));
            Assert.That(selections.Select(s => s.Tree), Has.Some.EqualTo("InstallationTree"),
                "and it is the OWNING pane that moved — a configuration target belongs to the installation tree");
        });
    }

    [Test]
    public void ClickingARowInsideABlocksProgramSwitchesToProgramViewFirst()
    {
        Assert.That(E2E.PaneRootLabel(), Is.EqualTo(E2E.ConfigurationRootLabel), "precondition: configuration view");
        ClickFinding(ProgrammingCode);

        Assert.Multiple(() =>
        {
            Assert.That(E2E.PaneRootLabel(), Is.Not.EqualTo(E2E.ConfigurationRootLabel),
                "the target has no row in the configuration tree at all, so the mode has to change before it "
                + "exists — switching is not a courtesy here, it is what makes the element reachable");
            Assert.That(E2E.Selections(), Is.Not.Empty, "and something is selected once it is");
        });
    }

    [Test]
    public void ClickingTheWholeProjectRowIsNonNavigableAndLeavesTheSelectionAlone()
    {
        // Give the panel a selection to disturb, so "unchanged" is a real observation rather than the
        // vacuous truth of nothing having been selected in the first place.
        ClickFinding(ConfigurationCode);
        IReadOnlyList<E2E.Selection> before = E2E.Selections();
        Assert.That(before, Is.Not.Empty, "precondition: something is selected before the non-navigable click");

        E2E.Row wholeProject = ClickFinding(WholeProjectCode);
        Assert.That(wholeProject.Element, Is.EqualTo("utcs_project"),
            "its primary location has no parsed element, so the Element cell shows the raw locator — which is "
            + "also why the row leads nowhere");

        Assert.That(E2E.Selections().Select(s => $"{s.Tree}={s.Name}"),
            Is.EqualTo(before.Select(s => $"{s.Tree}={s.Name}")),
            "a finding about the project as a whole names no single site, so there is nowhere to go and the "
            + "previous selection stays exactly where it was");
    }
}
