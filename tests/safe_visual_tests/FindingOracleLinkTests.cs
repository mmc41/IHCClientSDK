using System.Linq;

using Ihc.Tests.Shared;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The shared oracle harness, as THIS suite depends on it.
///
/// <para><b>Every test here asserts on the reader DIRECTLY, and that is the whole point.</b> The two classes
/// that actually consume the recorded rows — <c>ProblemsPanelE2ETests</c> and
/// <c>ProblemsNavigationE2ETests</c>, both in <c>safe_visual_e2e_tests</c> — build their assertions ON the
/// list, so a reader that silently returned NOTHING would not break them: an empty list makes "the panel shows
/// every recorded code" trivially true. Only a test that asserts about the list itself can tell the two apart,
/// and these are those tests.</para>
///
/// <para>CI does run some of those consumers — the headless leg covers every scenario not marked desktop-only —
/// but the desktop-only scenarios and the whole real-GUI mode are reached deliberately, not on every push. A
/// silent zero would read as a pass in all of them.</para>
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
    /// THE guard: the recorded rows for the end-to-end fixture are actually found, and there are as many of
    /// them as the assertions below name.
    ///
    /// <para><b>Why a count and not merely "not empty".</b> The number is what the E2E assertions compare
    /// against — how many rows the panel must show, and which codes — so a reader that found SOME rows but not
    /// all of them would move those assertions without failing anything here.</para>
    ///
    /// <para><b>Why the case key is the interesting part.</b> The consumers pass <c>Project6-Errors</c> while
    /// the oracle records <c>fixture/Project6-Errors</c>. The folder-prefix stripping that bridges the two is
    /// invisible to every other test, so this is where it is pinned.</para>
    ///
    /// <para><b>The tier split is pinned too, and it is no longer one tier.</b> Beside the warnings the fixture
    /// carries Information rows — the S0 meter's, the wireless family's, the retention budget, three the LED
    /// dimmer brings (what it does after a power failure, the bus it sits on, and its unlinked fault resources)
    /// and four user-built blocks whose `.ifb` files are worth archiving. The E2E panel counts are read PER
    /// TIER, so a test that compared the panel's <i>Advarsel</i> count against this whole population would be
    /// off by exactly those rows; asserting the split here is what keeps that mistake visible in the suite that
    /// runs on every build rather than only in the <c>[Explicit]</c> one that does not.</para>
    ///
    /// <para><b>The Warning count is the load-bearing half.</b> It has moved twice, both times downward and
    /// both in 2026-08: the Tier-1 campaign deleted eleven rules, and the Tier-2 pass that followed deleted
    /// three more and narrowed four. The Information count otherwise grows with each advisory row the catalogue
    /// adds that the fixture happens to witness. A failure showing the WARNING number moved is therefore a
    /// different and more serious thing than one showing only the Info number did.</para>
    /// </summary>
    [Test]
    public void TheRecordedRowsForTheE2EFixtureAreFoundAndSplitAcrossTwoTiers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(FindingOracleRows.Rows(OracleCase), Has.Count.EqualTo(126));
            Assert.That(FindingOracleRows.Codes(OracleCase), Has.Count.EqualTo(126), "and the code projection agrees");
            Assert.That(
                FindingOracleRows.Rows(OracleCase).Count(r => r.Severity == "Warning"), Is.EqualTo(116),
                "the advisory rows the panel's Advarsel count must equal");
            Assert.That(
                FindingOracleRows.Rows(OracleCase).Count(r => r.Severity == "Info"), Is.EqualTo(10),
                "and the Information rows, counted under their own tier and not with the warnings");
            Assert.That(
                FindingOracleRows.Rows(OracleCase).Select(r => r.Severity).Distinct(),
                Is.EquivalentTo(new[] { "Warning", "Info" }),
                "no Error row: nothing this fixture carries blocks a save, which the panel's tier counts assume");
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
            Assert.That(FindingOracleRows.Rows("fixture/Project6-Errors"), Is.Empty, "the prefixed form is not the key");
            Assert.That(FindingOracleRows.Rows("Project6-Errors.vis"), Is.Empty, "and neither is the file name");
            Assert.That(FindingOracleRows.Rows("Project6-Errors"), Is.Not.Empty, "this one is");
        });
    }

    /// <summary>
    /// A row carries the finding's own cells, and an absent locator keeps the non-null shape the four
    /// consuming sites were written against.
    /// </summary>
    [Test]
    public void ARowCarriesTheRecordedCells()
    {
        OracleRow row = FindingOracleRows.Rows(OracleCase)[0];

        Assert.Multiple(() =>
        {
            Assert.That(row.Severity, Is.Not.Empty);
            Assert.That(row.Code, Is.Not.Empty);
            Assert.That(row.Category, Is.Not.Empty);
            Assert.That(row.Message, Is.Not.Empty);
            Assert.That(
                FindingOracleRows.Rows(OracleCase).Select(r => r.Locator), Is.All.Not.Null,
                "never null: these rows keep the shape their consumers expect");
        });
    }
}
