using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The Problemer panel driven end to end: the real application, on a real fixture, read and operated through the
/// <c>aui</c> UI-Automation driver exactly as a person would.
///
/// <para>What these tests buy that the headless ones cannot: they exercise the panel through the ACCESSIBILITY
/// SURFACE. A headless test reads a view-model property; this reads what UI Automation actually publishes, which
/// is what a screen reader, a driver, and any other assistive client see. A panel that binds correctly but
/// publishes nothing readable passes every headless test in this suite and is unusable.</para>
///
/// <para><b>Fixture:</b> <c>Project6-Errors.vis</c> — 150 findings, every one a Warning. Its expected values are
/// read from the committed oracle at run time rather than typed here, so this file cannot drift from the corpus
/// it is asserting about.</para>
/// </summary>
[Explicit("Launches the real desktop app; run deliberately with --filter \"TestCategory=E2E\".")]
[Category(E2E.Category)]
public class ProblemsPanelE2ETests
{
    /// <summary>The file the app opens.</summary>
    private const string FixtureFile = "Project6-Errors.vis";

    /// <summary>The same fixture's CASE name in the characterization oracle — no suffix, no folder prefix.</summary>
    private const string OracleCase = "Project6-Errors";

    [OneTimeSetUp]
    public void LaunchApp() => E2E.Launch(E2E.Fixture(FixtureFile));

    [OneTimeTearDown]
    public void CloseApp() => E2E.KillApp();

    [SetUp]
    public void EnsurePanelShown()
    {
        // Each test starts from a shown panel and all three tiers on. A previous test that hid something would
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
        int expectedWarnings = E2E.OracleCodes(OracleCase).Count;

        Assert.Multiple(() =>
        {
            Assert.That(state.Flag("visible"), Is.True, "the panel is shown by default — no gesture needed");
            Assert.That(state.Text("state"), Is.EqualTo("findings"));
            Assert.That(expectedWarnings, Is.GreaterThan(0), "sanity: the oracle must actually describe this fixture");
            Assert.That(state.Int("warnings"), Is.EqualTo(expectedWarnings),
                "the Advarsel count equals the fixture's finding count in its own findings oracle, read at run "
                + "time rather than typed here twice");
            Assert.That(state.Int("errors"), Is.Zero, "this fixture carries no Error rows");
            Assert.That(state.Int("infos"), Is.Zero, "and no rule emits Info yet");
        });
    }

    /// <summary>
    /// The rows the panel shows, against the oracle's rows RE-SORTED the way the panel orders them.
    ///
    /// <para>The transform is stated explicitly rather than left implicit, because on this fixture it is the
    /// identity: every finding is a Warning, so severity-first-then-scan-order and plain scan order coincide.
    /// Comparing against raw oracle order would therefore pass whether or not the panel sorts at all — the
    /// assertion would be right for the wrong reason. Saying which sequence is expected keeps the test honest
    /// about what it can and cannot prove; the severity sort itself is proved on mixed severities in
    /// <c>ProblemsListTests</c>.</para>
    /// </summary>
    [Test]
    public void TheFirstRowsMatchTheOracleInThePanelsOwnOrder()
    {
        E2E.WaitForBoundProblems();

        // Severity-first, then engine order. Every row here is a Warning, so this reduces to engine order —
        // stated as a transform so the coincidence is visible rather than assumed.
        IReadOnlyList<string> expected = E2E.OracleCodes(OracleCase);
        IReadOnlyList<E2E.Row> rows = E2E.Rows();

        Assert.That(rows, Is.Not.Empty, "some rows realize at the panel's default height");
        Assert.That(rows.Select(r => r.Code), Is.EqualTo(expected.Take(rows.Count)),
            "the realized rows are the oracle's first rows, in the panel's order");
        Assert.That(rows.Select(r => r.Severity), Has.All.EqualTo("Advarsel"),
            "and each says its tier in Danish, not by colour alone");
    }

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

    [Test]
    public void HidingTheWarningTierEmptiesTheListWithoutChangingItsCount()
    {
        E2E.Envelope before = E2E.WaitForBoundProblems();
        int warnings = before.Int("warnings");
        Assert.That(before.Int("visibleRows"), Is.GreaterThan(0), "precondition: rows are showing");

        try
        {
            E2E.Envelope off = E2E.RunOk("problems", "toggle", "--tier", "warning");
            E2E.Envelope hidden = E2E.RunOk("problems", "state");

            Assert.Multiple(() =>
            {
                Assert.That(off.Int("visibleRows"), Is.Zero, "every row on this fixture is a Warning");
                Assert.That(hidden.Int("warnings"), Is.EqualTo(warnings),
                    "the COUNT is unmoved: hiding a tier is not fixing its findings, and a count that fell would "
                    + "say it was");
            });
        }
        finally
        {
            E2E.RunOk("problems", "toggle", "--tier", "warning");
        }

        E2E.Envelope restored = E2E.RunOk("problems", "state");
        Assert.That(restored.Int("visibleRows"), Is.GreaterThan(0), "toggling back restores the rows");
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
            .. E2E.OracleCodes(OracleCase).OrderBy(c => c, StringComparer.Ordinal).Take(rows.Count),
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
