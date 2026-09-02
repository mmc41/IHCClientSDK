using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

using Ihc.Tests.Shared;

namespace safe_visual_e2e_tests;

/// <summary>
/// The Problemer panel driven end to end: the real application, on a real fixture, read and operated through the
/// <c>aui</c> UI-Automation driver exactly as a person would.
///
/// <para>What these tests buy that the headless ones cannot: they exercise the panel through the ACCESSIBILITY
/// SURFACE. A headless test reads a view-model property; this reads what UI Automation actually publishes, which
/// is what a screen reader, a driver, and any other assistive client see. A panel that binds correctly but
/// publishes nothing readable passes every headless test in this suite and is unusable.</para>
///
/// <para><b>Fixture:</b> <c>Project6-Errors.vis</c>, which carries Warnings and Information rows and no Errors.
/// Every expected value is read from the committed oracle at run time rather than typed here, so this file
/// cannot drift from the corpus it asserts about. It states no counts on purpose: the prose that used to carry
/// them went stale by dozens of rows across two rule-deletion campaigns, unnoticed for as long as this file was
/// <c>[Explicit]</c> and nothing read it. The numbers live in <c>FindingOracleLinkTests</c>, where they are
/// assertions.</para>
///
/// <para><b>Read the oracle PER TIER, never as one population.</b> The panel counts each tier separately, so a
/// comparison against every recorded row is right only while the fixture carries exactly one tier — which it no
/// longer does. Nothing here runs in CI, so that mistake would fail nothing on a gated build;
/// <c>FindingOracleLinkTests</c>, which does run there, pins the split for that reason.</para>
/// </summary>
public class ProblemsPanelE2ETests
{
    /// <summary>The file the app opens.</summary>
    private const string FixtureFile = "Project6-Errors.vis";

    /// <summary>The same fixture's CASE name in the characterization oracle — no suffix, no folder prefix.</summary>
    private const string OracleCase = "Project6-Errors";

    /// <summary>
    /// The Danish word the Alvor column shows for a Warning — how a row's tier is read back through automation,
    /// since the driver splits it out of the accessible name the app composes.
    /// </summary>
    private const string WarningLabel = "Advarsel";

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

    [Test]
    public void ThePanelIsVisibleAndReportsTheFixturesFindings()
    {
        E2E.Envelope state = E2E.WaitForBoundProblems();

        // PER TIER, because the panel counts per tier. Taking the whole oracle as the warning count was right
        // only while every recorded row was a Warning, and the fixture's S0 meter ended that.
        int expectedWarnings = TierRows("Warning").Count;
        int expectedInfos = TierRows("Info").Count;

        Assert.Multiple(() =>
        {
            Assert.That(state.Flag("visible"), Is.True, "the panel is shown by default — no gesture needed");
            Assert.That(state.Text("state"), Is.EqualTo("findings"));
            Assert.That(expectedWarnings, Is.GreaterThan(0), "sanity: the oracle must actually describe this fixture");
            Assert.That(state.Number("warnings"), Is.EqualTo(expectedWarnings),
                "the Advarsel count equals the fixture's WARNING count in its own findings oracle, read at run "
                + "time rather than typed here twice");
            Assert.That(state.Number("errors"), Is.Zero, "this fixture carries no Error rows");
            Assert.That(state.Number("infos"), Is.EqualTo(expectedInfos),
                "and the Information count equals its Info rows — the S0 meter's datasheet row");
        });
    }

    /// <summary>
    /// The rows the panel shows, against the oracle's rows RE-SORTED the way the panel orders them.
    ///
    /// <para>The transform is stated explicitly rather than left implicit. On this fixture it is ALMOST the
    /// identity — the Warnings far outnumber the Information rows, so severity-first-then-scan-order equals scan
    /// order for everything except that tail, which sorts to the end. Comparing against raw oracle order would
    /// pass anyway at the panel's default height, since no Info row realizes that far down, so the transform is
    /// applied rather than assumed and the assertion stays right for the right reason. The severity sort itself
    /// is proved on mixed severities in <c>ProblemsListTests</c>.</para>
    /// </summary>
    [Test]
    public void TheFirstRowsMatchTheOracleInThePanelsOwnOrder()
    {
        E2E.WaitForBoundProblems();

        // Severity-first, then engine order: Warnings keep their scan order, the Information row follows them.
        IReadOnlyList<string> expected =
        [
            .. TierRows("Warning").Select(r => r.Code),
            .. TierRows("Info").Select(r => r.Code),
        ];
        IReadOnlyList<E2E.Row> rows = E2E.Rows();

        Assert.That(rows, Is.Not.Empty, "some rows realize at the panel's default height");
        Assert.That(rows.Select(r => r.Code), Is.EqualTo(expected.Take(rows.Count)),
            "the realized rows are the oracle's first rows, in the panel's order");
        Assert.That(rows.Select(r => r.Severity), Has.All.EqualTo("Advarsel"),
            "and each says its tier in Danish, not by colour alone — the realized rows are all Warnings because "
            + "the single Information row sorts below the panel's default height");
    }

    /// <summary>The fixture's recorded rows of one tier, in the oracle's own production order.</summary>
    /// <param name="severity">The oracle's severity word — <c>Warning</c>, <c>Info</c> or <c>Error</c>.</param>
    private static IReadOnlyList<OracleRow> TierRows(string severity) =>
        [.. FindingOracleRows.Rows(OracleCase).Where(r => r.Severity == severity)];

    [Test]
    public void TheFirstRowIsTheWholeProjectFindingShowingItsRawLocator()
    {
        E2E.WaitForBoundProblems();
        E2E.Row first = E2E.Rows()[0];

        Assert.Multiple(() =>
        {
            Assert.That(first.Code, Is.EqualTo("doc-project-info-blank"));
            Assert.That(first.Element, Is.EqualTo("utcs_project"),
                "a finding whose primary location has no parsed element falls back to the raw locator — a blank "
                + "cell would tell the reader nothing about where the engine looked");
        });
    }

    /// <summary>
    /// Hiding a tier hides ITS rows and moves no count. What it does not do is empty the list: this fixture
    /// carries Information findings beside its Warnings, so the rows that survive the toggle are the other
    /// tiers' — which is the point of a per-tier filter rather than a global one.
    /// </summary>
    [Test]
    public void HidingTheWarningTierHidesItsOwnRowsWithoutChangingItsCount()
    {
        E2E.Envelope before = E2E.WaitForBoundProblems();
        int warnings = before.Number("warnings");
        Assert.That(E2E.Rows().Select(r => r.Severity), Has.Some.EqualTo(WarningLabel),
            "precondition: warning rows are showing, or hiding them proves nothing");

        try
        {
            E2E.RunOk("problems", "toggle", "--tier", "warning");
            E2E.Envelope hidden = E2E.RunOk("problems", "state");

            Assert.Multiple(() =>
            {
                Assert.That(E2E.Rows().Select(r => r.Severity), Has.None.EqualTo(WarningLabel),
                    "not one row of the hidden tier is left on screen");
                Assert.That(hidden.Number("warnings"), Is.EqualTo(warnings),
                    "the COUNT is unmoved: hiding a tier is not fixing its findings, and a count that fell would "
                    + "say it was");
            });
        }
        finally
        {
            E2E.RunOk("problems", "toggle", "--tier", "warning");
        }

        Assert.That(E2E.Rows().Select(r => r.Severity), Has.Some.EqualTo(WarningLabel),
            "toggling back restores them");
    }

    [Test]
    public void SortingByCodeReordersTheListAsTheOraclePredicts()
    {
        E2E.WaitForBoundProblems();

        E2E.Envelope sorted = E2E.RunOk("problems", "sort", "--column", "code");
        IReadOnlyList<E2E.Row> rows = E2E.Rows();

        // Ascending by code: the realized rows must be the alphabetically first codes the fixture produces.
        IReadOnlyList<string> expected =
        [
            .. FindingOracleRows.Codes(OracleCase).OrderBy(c => c, StringComparer.Ordinal).Take(rows.Count),
        ];

        Assert.Multiple(() =>
        {
            Assert.That(sorted.Flag("reordered"), Is.True, "the fixture's default order is not its code order");
            Assert.That(rows.Select(r => r.Code), Is.EqualTo(expected),
                "sorted ascending by the Kode column, which the oracle predicts because it lists every code the "
                + "fixture produces");
        });

        // Leave the panel in its default order for whatever runs next.
        E2E.RunOk("problems", "sort", "--column", "severity");
    }

    [Test]
    public void TheVisMenuRowHidesAndReshowsThePanel()
    {
        E2E.WaitForBoundProblems();

        E2E.RunOk("view", "problems-toggle");
        E2E.Envelope hidden = E2E.Run("problems", "state");

        Assert.Multiple(() =>
        {
            Assert.That(hidden.Ok, Is.False, "with the panel hidden there is nothing to read");
            Assert.That(hidden.Code, Is.EqualTo("ControlNotFound"));
            Assert.That(hidden.Message, Does.Contain("view.problems.toggle"),
                "and the refusal names the verb that brings it back, rather than leaving a driver stuck");
        });

        E2E.RunOk("view", "problems-toggle");
        Assert.That(E2E.RunOk("problems", "state").Flag("visible"), Is.True, "and it comes back");
    }

    /// <summary>Evidence for the run: a window capture with the panel populated.</summary>
    [Test]
    public void CaptureThePanelAsEvidence()
    {
        E2E.WaitForBoundProblems();
        E2E.Envelope capture = E2E.RunOk("capture", "window");

        Assert.That(capture.Message, Does.Contain(".png"));
        TestContext.Out.WriteLine(capture.Message);
    }
}
