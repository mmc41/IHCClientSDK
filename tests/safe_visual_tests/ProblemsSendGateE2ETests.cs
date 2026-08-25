using System;
using System.Linq;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// The two things about Error findings that only a live run can show: that a project carrying them reaches the
/// panel with the transfer withheld, and that an edit made THROUGH THE GUI produces one — and undoing it takes
/// it away again — inside a single debounce-and-run cycle.
///
/// <para><b>What the send-gate test can and cannot prove here, stated rather than implied.</b> No controller is
/// attached in an E2E run, and the connection refusal is checked first, so <c>Send projekt</c> is withheld for
/// that reason whether or not the project validates. This test therefore asserts what is OBSERVABLE — errors
/// present, transfer withheld — and does not claim to prove gate composition. That is
/// <c>ProblemsSendGateTests</c>' job, which drives the two gates independently with a connected controller.</para>
///
/// <para><b>The live edit is the real prize.</b> It proves the loop end to end through the user's own route: a
/// properties dialog commits an address, the workflow raises its change, the background worker debounces and
/// revalidates, and a new Error row reaches the panel — none of which a headless test exercises together.</para>
/// </summary>
[Explicit("Launches the real desktop app; run deliberately with --filter \"TestCategory=E2E\".")]
[Category(E2E.Category)]
public class ProblemsSendGateE2ETests
{
    /// <summary>Three duplicate data-line address groups — the corpus' Error fixture (pinned by T003).</summary>
    private static string ErrorFixture() => E2E.Fixture("Synthetic", "DuplicatedAdressErrors.vis");

    /// <summary>30 findings, every one a Warning and none about a duplicate address — a clean slate for Errors.</summary>
    private static string CleanFixture() => E2E.Fixture("Project1-SimpelWired.vis");

    private const string DuplicateAddressCode = "dataline-address-duplicate";

    [TearDown]
    public void CloseApp() => E2E.KillApp();

    [Test]
    public void AProjectCarryingErrorsReachesThePanelWithTheTransferWithheld()
    {
        E2E.Launch(ErrorFixture());
        E2E.Envelope state = E2E.WaitForBoundProblems();

        Assert.Multiple(() =>
        {
            Assert.That(state.Int("errors"), Is.GreaterThanOrEqualTo(1),
                "the fixture's duplicate data-line addresses are an ACTIVE Error row");
            Assert.That(E2E.SendProjectEnabled(), Is.False, "and the transfer is withheld");
        });

        // Named for what it is: with no controller attached the connection gate alone would withhold it, so this
        // is a smoke of the observable state, not proof that the validation gate did the withholding.
        TestContext.Out.WriteLine(
            "note: no controller is attached, so the connection gate would also withhold Send projekt; "
            + "composition is proved headlessly in ProblemsSendGateTests.");
    }

    [Test]
    public void TheErrorRowsAreTheDuplicateAddressFindings()
    {
        E2E.Launch(ErrorFixture());
        E2E.WaitForBoundProblems();

        E2E.Envelope click = E2E.RunOk("problems", "click", "--row", DuplicateAddressCode);

        Assert.That(click.Field("clicked").GetProperty("code").GetString(), Is.EqualTo(DuplicateAddressCode),
            "the Error the panel lists is the one the catalogue says it should be");
    }

    /// <summary>
    /// AC2, driven through the GUI: author a duplicate address, watch an Error row appear, undo, watch it go.
    /// </summary>
    /// <remarks>
    /// The duplicate is authored the way a user would — the pin's own properties dialog, whose terminal list
    /// offers the in-use value explicitly as <c>"1 (i brug)"</c>. OpenVisual allows it (its <c>UpdatePin</c>
    /// refuses only out-of-range addresses) where the vendor tool disables OK on it; that asymmetry is exactly
    /// why the catalogue treats a duplicate address as a finding rather than a refusal, and it is what makes
    /// this scenario reachable at all.
    /// </remarks>
    [Test]
    public void AnAddressDuplicateAuthoredInTheGuiAppearsAsAnErrorAndUndoRemovesIt()
    {
        E2E.Launch(CleanFixture());
        E2E.Envelope before = E2E.WaitForBoundProblems();
        Assert.That(before.Int("errors"), Is.Zero, "precondition: the clean fixture carries no Errors");
        int warnings = before.Int("warnings");

        const string Pin = "Lokaliteter/Stue/LK FUGA Tryk 2 tast (Ved dør) /Tryk (højre)";
        E2E.RunOk("node", "select", "--path", Pin);
        E2E.RunOk("node", "get-properties");
        // Its sibling already holds terminal 1 on data line 1; the list marks it in use rather than hiding it.
        E2E.RunOk("dialog", "select-item", "--control", "TerminalList", "--item", "1 (i brug)");
        E2E.Run("dialog", "click", "--button", "OK");

        E2E.Envelope after = E2E.WaitForBoundProblems();
        Assert.Multiple(() =>
        {
            Assert.That(after.Int("errors"), Is.EqualTo(1),
                "one duplicate group, one Error — reaching the panel with no gesture but the edit itself");
            Assert.That(after.Int("warnings"), Is.LessThanOrEqualTo(warnings),
                "and the warning tally moves with the project rather than growing alongside a stale copy");
        });

        E2E.RunOk("edit", "undo");
        E2E.Envelope undone = E2E.WaitForBoundProblems();

        Assert.Multiple(() =>
        {
            Assert.That(undone.Int("errors"), Is.Zero, "undo takes the Error away again");
            Assert.That(undone.Int("warnings"), Is.EqualTo(warnings), "and the counts return to where they were");
        });
    }

    /// <summary>
    /// AC7: opening a different document must clear the previous one's findings rather than carry them over.
    /// </summary>
    /// <remarks>
    /// Added here because this task's session already has two documents in hand, which made it cheap — the
    /// backlog flagged AC7 as otherwise having no end-to-end coverage at all, pinned only by unit tests. It is
    /// the defect the generation counter exists to prevent: rows about a file the user has closed, displayed
    /// over the one they just opened.
    /// </remarks>
    [Test]
    public void ReplacingTheDocumentClearsThePreviousProjectsFindings()
    {
        E2E.Launch(ErrorFixture());
        E2E.Envelope errors = E2E.WaitForBoundProblems();
        Assert.That(errors.Int("errors"), Is.GreaterThanOrEqualTo(1), "precondition: the old document has Errors");

        E2E.RunOk("project", "new");
        E2E.Envelope fresh = E2E.WaitForBoundProblems();

        Assert.Multiple(() =>
        {
            Assert.That(fresh.Int("errors"), Is.Zero,
                "a fresh project has no Errors, and the closed file's must not survive into it");
            Assert.That(fresh.Int("warnings"), Is.Not.EqualTo(errors.Int("warnings")),
                "the whole result is the new document's, not the old one's still on screen");
        });
    }
}
