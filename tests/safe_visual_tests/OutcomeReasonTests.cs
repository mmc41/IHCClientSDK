using ihc_openvisual.ViewModels;
using Ihc.Vis.Session;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Which of an <see cref="EditOutcome"/>'s reasons may reach the installer (D01).
/// <para>
/// <c>Refused</c> and <c>Failed</c> both carry a non-null <c>Reason</c>, and the two are written in different
/// languages for different readers: a refusal is a rule the installer can act on and the SDK writes it in Danish
/// (FR-2.6 / D13), while a failure's reason is the engine's own exception message — an English developer
/// diagnostic naming element tags, attribute names and <c>_0x</c> ids. So the two must be told apart by STATUS,
/// never by "is there a reason to show" — the shape of the bug this pins, where a dialog fell back to
/// <c>outcome.Reason</c> for every non-committed status and would have shown the English diagnostic verbatim.
/// </para>
/// <para>
/// Asserted on the rule rather than by provoking a failing edit: the enum-manager operations that reach this path
/// are guarded well enough that no <c>Failed</c> outcome is reachable through them at all (a structural container
/// cannot be deleted, an absent or read-only type is refused before Execute), so the defect is LATENT — real, but
/// unreachable from the current fixtures. A test that could only be written by first breaking a guard would pin
/// the broken guard, not this rule.
/// </para>
/// <para>
/// By subject this is view-model logic with no Avalonia in it, which the repo would normally put in
/// <c>safe_unit_tests</c>. It lives here because the rule it pins is <c>internal</c> to the app, and
/// <c>ihc_openvisual</c> opens its internals to this suite alone — the openvisual tests over in
/// <c>safe_unit_tests</c> all work through the public surface. Widening that boundary to relocate one test would
/// cost more than the tidier address is worth.
/// </para>
/// </summary>
public class OutcomeReasonTests
{
    private const string EnglishDiagnostic = "The element (id _0x2132) no longer exists.";
    private const string DanishRefusal = "Elementet findes ikke længere.";

    private static EditOutcome Outcome(EditStatus status, string? reason) =>
        new(status, "Omdøb", reason, null);

    [Test]
    public void ARefusalsReason_IsUserFacing_AndEveryOtherOutcomesReasonIsNot()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Refused, DanishRefusal)),
                Is.EqualTo(DanishRefusal),
                "a refusal IS the sentence written for the installer — shown verbatim, never re-authored here");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Failed, EnglishDiagnostic)),
                Is.Null,
                "a failure's reason is an English engine diagnostic; it belongs in the log, not on screen");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.NoChange, null)),
                Is.Null,
                "a no-op has nothing to say");
            Assert.That(MainWindowViewModel.UserFacingRefusal(Outcome(EditStatus.Committed, null)),
                Is.Null,
                "a success is reported by its own status sentence");
        });
    }

    /// <summary>
    /// The generic that stands in for a reason a dialog may not show must not be the failure DIALOG's sentence: by
    /// the time a dialog falls back to it, <c>ReportOutcomeAsync</c> has already told the installer that the edit
    /// failed, so repeating that sentence inside the still-open dialog would report one event twice.
    /// <para>Only the distinctness is asserted. Restating the sentence itself as a literal would pin nothing — it
    /// can only fail when someone edits the constant deliberately, and then the fix is to edit the test.</para>
    /// </summary>
    [Test]
    public void TheFallbackSentence_IsDistinctFromTheFailureDialogs() =>
        Assert.That(MainWindowViewModel.EditRejectedMessage, Is.Not.EqualTo(MainWindowViewModel.EditFailedMessage));
}
