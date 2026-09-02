using System.Linq;
using System.Threading.Tasks;
using ihc_openvisual.ViewModels;
using NUnit.Framework;

namespace safe_visual_tests;

/// <summary>
/// Cancelling a product insert rolls the insert back, and the roll-back has an ANSWER. The cancel arm discarded
/// it and announced "Indsætning af 'X' annulleret." unconditionally — so the one sentence the installer reads to
/// learn what happened was written before anything had been checked.
/// <para>
/// The stronger reading — that the announcement regularly outlives a product still sitting in the tree — is
/// deliberately NOT claimed here. A cancelled composed dialog returns with its step edits still pending rather
/// than committed, so <c>Rollback</c> pops the insert as intended on the ordinary path. What is pinned is the
/// narrow, real thing: the answer is read before the sentence is written.
/// </para>
/// <para>
/// The CONTROL — that an ordinary cancel still announces itself — is
/// <see cref="InsertStatusHonestyTests.CancellingTheDialog_SaysSo_AndInsertsNothing"/>, which already asserts
/// exactly that. Repeating it here would only make the fix look better tested than it is.
/// </para>
/// </summary>
public class ProductInsertRollbackTests
{
    /// <summary>
    /// Reproduce-first: with the roll-back point already taken, the roll-back does NOT happen — and the cancel arm
    /// said "annulleret" anyway, because it never looked. The answer now decides the sentence, and the failure
    /// reaches the log as well as the status line.
    /// </summary>
    [Test]
    public async Task ACancelledInsert_WhoseRollbackDidNotHappen_IsReportedInsteadOfAnnounced()
    {
        using ShellHarness harness = ShellHarness.Create();
        var logs = new CapturingLoggerFactory();
        MainWindowViewModel vm = harness.CreateViewModel(logs);
        await vm.InitializeAsync();
        // Something else takes the history entry while the dialog is open, so the cancel arm's own Rollback finds
        // nothing to pop. RollbackAsync completes synchronously, so this cannot deadlock the dialog.
        harness.Dialogs.ProductDialogResponder = _ =>
        {
            harness.Session.RollbackAsync().GetAwaiter().GetResult();
            return null;   // Annuller
        };

        await InsertGesture.InsertLampeudtagAsync(vm);

        Assert.Multiple(() =>
        {
            Assert.That(vm.StatusText, Does.Not.Contain("annulleret"),
                "a roll-back that did not happen may not be announced as a completed cancellation");
            Assert.That(logs.Messages.Any(m => m.StartsWith("Error:", StringComparison.Ordinal)), Is.True,
                "and it reaches the log, where a fault the installer cannot act on belongs");
        });
    }
}
