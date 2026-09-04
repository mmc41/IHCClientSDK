using NUnit.Framework;

namespace safe_visual_e2e_tests;

/// <summary>
/// The Problemer panel driven end to end: the real application, on a real fixture, read and operated through the
/// suite's own UI-Automation driver exactly as a person would.
///
/// <para>What these tests buy that the headless ones cannot: they exercise the panel through the ACCESSIBILITY
/// SURFACE. A headless test reads a view-model property; this reads what UI Automation actually publishes, which
/// is what a screen reader, a driver, and any other assistive client see. A panel that binds correctly but
/// publishes nothing readable passes every headless test in this suite and is unusable.</para>
///
/// <para>That is also the whole admission test for this file: a scenario stays only if it can fail for a reason
/// that exists SOLELY in the real desktop. What the panel orders, filters, sorts and counts is business logic,
/// it is cheaper one level down, and it is asserted there — the per-tier tallies for this very fixture in
/// <c>FindingOracleLinkTests</c> and <c>ProblemsPanelViewModelTests</c> — so no count is compared here, only
/// that one arrived.</para>
///
/// <para><b>Fixture:</b> <c>Project6-Errors.vis</c>, which carries Warnings and Information rows and no Errors.
/// Nothing here says how many of either: the prose that used to carry the numbers went stale by dozens of rows
/// across two rule-deletion campaigns, unnoticed for as long as this file was <c>[Explicit]</c> and nothing read
/// it.</para>
/// </summary>
public class ProblemsPanelE2ETests : E2EScenario
{
    /// <summary>The file the app opens.</summary>
    private const string FixtureFile = "Project6-Errors.vis";

    [OneTimeSetUp]
    public void LaunchApp() => E2E.Launch(E2E.Fixture(FixtureFile));

    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

    [SetUp]
    public void EnsurePanelShown()
    {
        // Each test starts from a shown panel and all four tiers on. A previous test that hid something would
        // otherwise make the next one fail for a reason that has nothing to do with what it asserts.
        if (!E2E.Run("problems", "state").Ok)
        {
            E2E.RunOk("view", "problems-toggle");
        }
    }

    /// <summary>
    /// Process start-up and document binding, read back through the automation surface: the application opened
    /// the fixture, validated it, and published a bound result a client can read.
    /// </summary>
    [Test]
    public void ThePanelIsShownAndBindsTheFixturesFindings()
    {
        E2E.Envelope state = E2E.WaitForBoundProblems();

        Assert.Multiple(() =>
        {
            Assert.That(state.Flag("visible"), Is.True, "the panel is shown by default — no gesture needed");
            Assert.That(state.Text("state"), Is.EqualTo(ProblemsStates.Findings), "and bound to a result with rows");
            Assert.That(state.Number("warnings"), Is.GreaterThan(0),
                "and a tally reached the surface. HOW MANY is asserted one level down, against the oracle; what "
                + "a live run adds is that the number is readable at all");
        });
    }

    /// <summary>
    /// The Vis row, reached the way a person reaches it: the bar menu opened and its leaf invoked through the
    /// bridge. That the command toggles the panel is <c>ProblemsPanelSkeletonTests</c>' claim, one level down;
    /// this one is that the menu route reaches it, and that a hidden panel really leaves the automation tree
    /// rather than merely going transparent.
    /// </summary>
    [Test]
    public void TheVisMenuRowHidesAndReshowsThePanel()
    {
        E2E.WaitForBoundProblems();

        E2E.RunOk("view", "problems-toggle");
        E2E.Envelope hidden = E2E.Run("problems", "state");
        Assert.That(hidden.Code, Is.EqualTo("ControlNotFound"),
            $"with the panel hidden there must be nothing to read; the driver answered: {hidden.Raw}");

        E2E.RunOk("view", "problems-toggle");
        Assert.That(E2E.RunOk("problems", "state").Flag("visible"), Is.True, "and it comes back");
    }
}
