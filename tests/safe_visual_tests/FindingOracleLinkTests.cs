using System.Linq;

using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The shared oracle harness, as THIS suite depends on it.
///
/// <para><b>Every test here is deliberately non-<c>[Explicit]</c>, and that is the whole point.</b> The two
/// classes that actually consume the recorded rows — <c>ProblemsPanelE2ETests</c> and
/// <c>ProblemsNavigationE2ETests</c> — are <c>[Explicit]</c>, because they drive the real GUI through UI
/// Automation on Windows. So nothing in a default run exercises the reader they depend on, and a reader that
/// silently returned NOTHING would break neither: their assertions are built on the list, so an empty list
/// makes "the panel shows every recorded code" trivially true. These tests are the only thing standing
/// between that reader and a silent zero.</para>
/// </summary>
public class FindingOracleLinkTests
{
    /// <summary>The case both E2E classes name — unprefixed, exactly as they spell it.</summary>
    private const string OracleCase = "Project6-Errors";

    [Test]
    public void TheSharedFindingOracleHarnessIsAvailableHere()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                FindingOracleHarness.FileNameFor("fixture/Project6-Errors"),
                Is.EqualTo("fixture-Project6-Errors.xml"));
            Assert.That(
                FindingOracleHarness.Clock().GetLocalNow(), Is.EqualTo(ReportOracleHarness.PinnedInstant),
                "and on the one pinned instant both oracle families share");
        });
    }

    /// <summary>
    /// THE guard: the recorded rows for the E2E fixture are actually found, and there are 150 of them.
    ///
    /// <para><b>Why a count and not merely "not empty".</b> The number is what the E2E assertions compare
    /// against — how many rows the panel must show, and which codes — so a reader that found SOME rows but not
    /// all of them would move those assertions without failing anything here.</para>
    ///
    /// <para><b>Why the case key is the interesting part.</b> The consumers pass <c>Project6-Errors</c> while
    /// the oracle records <c>fixture/Project6-Errors</c>. The folder-prefix stripping that bridges the two is
    /// invisible to every other test, so this is where it is pinned.</para>
    /// </summary>
    [Test]
    public void TheRecordedRowsForTheE2EFixtureAreFoundAndCountOneHundredAndFifty()
    {
        Assert.Multiple(() =>
        {
            Assert.That(E2E.OracleRows(OracleCase), Has.Count.EqualTo(150));
            Assert.That(E2E.OracleCodes(OracleCase), Has.Count.EqualTo(150), "and the code projection agrees");
            Assert.That(
                E2E.OracleRows(OracleCase).Select(r => r.Severity).Distinct(), Is.EqualTo(new[] { "Warning" }),
                "the fixture's findings are all advisory, which the panel's tier counts depend on");
        });
    }

    /// <summary>
    /// The prefixed name does NOT match, and neither does a file name. Both are ways a caller could reasonably
    /// spell the case and get an empty list; asserting they stay wrong is what makes the one right spelling
    /// meaningful rather than accidental.
    /// </summary>
    [Test]
    public void OnlyTheUnprefixedCaseNameMatches()
    {
        Assert.Multiple(() =>
        {
            Assert.That(E2E.OracleRows("fixture/Project6-Errors"), Is.Empty, "the prefixed form is not the key");
            Assert.That(E2E.OracleRows("Project6-Errors.vis"), Is.Empty, "and neither is the file name");
            Assert.That(E2E.OracleRows("Project6-Errors"), Is.Not.Empty, "this one is");
        });
    }

    /// <summary>
    /// A row carries the finding's own cells, and an absent locator keeps the non-null shape the four
    /// consuming sites were written against.
    /// </summary>
    [Test]
    public void ARowCarriesTheRecordedCells()
    {
        E2E.OracleRow row = E2E.OracleRows(OracleCase)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Severity, Is.Not.Empty);
            Assert.That(row.Code, Is.Not.Empty);
            Assert.That(row.Category, Is.Not.Empty);
            Assert.That(row.Message, Is.Not.Empty);
            Assert.That(
                E2E.OracleRows(OracleCase).Select(r => r.Locator), Is.All.Not.Null,
                "never null: these rows keep the shape their consumers expect");
        });
    }
}
