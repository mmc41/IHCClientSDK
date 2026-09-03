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
/// <para>That is also the whole admission test for this file: a scenario stays only if it can fail for a reason
/// that exists SOLELY in the real desktop. What the panel orders, filters, sorts and counts is business logic,
/// it is cheaper one level down, and it is asserted there -- so it is not restated here.</para>
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

    /// <summary>The fixture's recorded rows of one tier, in the oracle's own production order.</summary>
    /// <param name="severity">The oracle's severity word — <c>Warning</c>, <c>Info</c> or <c>Error</c>.</param>
    private static IReadOnlyList<OracleRow> TierRows(string severity) =>
        [.. FindingOracleRows.Rows(OracleCase).Where(r => r.Severity == severity)];

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
