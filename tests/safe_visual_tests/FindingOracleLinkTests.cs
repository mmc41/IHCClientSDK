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
    /// THE guard: the recorded rows for the E2E fixture are actually found, and there are 160 of them.
    ///
    /// <para><b>Why a count and not merely "not empty".</b> The number is what the E2E assertions compare
    /// against — how many rows the panel must show, and which codes — so a reader that found SOME rows but not
    /// all of them would move those assertions without failing anything here.</para>
    ///
    /// <para><b>Why the case key is the interesting part.</b> The consumers pass <c>Project6-Errors</c> while
    /// the oracle records <c>fixture/Project6-Errors</c>. The folder-prefix stripping that bridges the two is
    /// invisible to every other test, so this is where it is pinned.</para>
    ///
    /// <para><b>The tier split is pinned too, and it is no longer one tier.</b> The fixture carries 150 Warnings
    /// and 10 Information rows — the S0 meter's, the wireless family's, the retention budget, three the LED
    /// dimmer brings (what it does after a power failure, the bus it sits on, and its unlinked fault resources)
    /// and four user-built blocks whose `.ifb` files are worth archiving. The E2E panel counts are read PER TIER, so a test that compared the
    /// panel's <i>Advarsel</i> count against this whole population would be off by exactly those rows; asserting
    /// the split here is what keeps that mistake visible in the suite that runs on every build rather than only
    /// in the <c>[Explicit]</c> one that does not.</para>
    ///
    /// <para><b>The Warning count is the stable half.</b> 150 is what this fixture has carried throughout; the
    /// Information count grows with each advisory row the catalogue adds that the fixture happens to witness. A
    /// failure showing the WARNING number moved is therefore a different and more serious thing than one showing
    /// only the Info number did.</para>
    /// </summary>
    [Test]
    public void TheRecordedRowsForTheE2EFixtureAreFoundAndSplitAcrossTwoTiers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(E2E.OracleRows(OracleCase), Has.Count.EqualTo(162));
            Assert.That(E2E.OracleCodes(OracleCase), Has.Count.EqualTo(162), "and the code projection agrees");
            Assert.That(
                E2E.OracleRows(OracleCase).Count(r => r.Severity == "Warning"), Is.EqualTo(152),
                "the advisory rows the panel's Advarsel count must equal");
            Assert.That(
                E2E.OracleRows(OracleCase).Count(r => r.Severity == "Info"), Is.EqualTo(10),
                "and the Information rows, counted under their own tier and not with the warnings");
            Assert.That(
                E2E.OracleRows(OracleCase).Select(r => r.Severity).Distinct(),
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
