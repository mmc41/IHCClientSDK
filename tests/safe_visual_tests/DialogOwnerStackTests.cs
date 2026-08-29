using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using ihc_openvisual.Services;

namespace safe_visual_tests;

/// <summary>
/// Which window a modal parents on.
///
/// <para>While every dialog was raised from the shell, "the owner" and "the main window" were the same thing.
/// Once a dialog can be opened from inside another they are not: a sub-dialog parented on the shell is not modal
/// to the dialog that raised it, so the installer can reach behind it and edit the very values it was opened to
/// change — and can close the parent out from under it.</para>
/// </summary>
public class DialogOwnerStackTests : AvaloniaTestBase
{
    [AvaloniaTest]
    public void WithOnlyTheShellOpenADialogParentsOnTheShell()
    {
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaDialogService service = new() { Owner = shell };

        Assert.That(Owner(service), Is.SameAs(shell));

        shell.Close();
    }

    [AvaloniaTest]
    public void ADialogOpenedWhileAnotherIsOpenParentsOnTheInnerOne()
    {
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Window inner = new();
        inner.Show(shell);
        Dispatcher.UIThread.RunJobs();
        AvaloniaDialogService service = new() { Owner = shell };

        Assert.That(Owner(service), Is.SameAs(inner),
            "the modal belongs to the dialog that raised it, not to the shell behind it");

        inner.Close();
        shell.Close();
    }

    [AvaloniaTest]
    public void TheChainFollowsEveryLevelAndUnwindsWhenAWindowCloses()
    {
        Window shell = new();
        CurrentTestWindow = shell;
        shell.Show();
        Window middle = new();
        middle.Show(shell);
        Window innermost = new();
        innermost.Show(middle);
        Dispatcher.UIThread.RunJobs();
        AvaloniaDialogService service = new() { Owner = shell };

        Assert.That(Owner(service), Is.SameAs(innermost), "three deep");

        innermost.Close();
        Dispatcher.UIThread.RunJobs();

        Assert.That(Owner(service), Is.SameAs(middle),
            "and it unwinds by itself — nothing had to remember to pop, which is the point of reading the "
            + "window manager's own chain rather than keeping a parallel stack");

        middle.Close();
        shell.Close();
    }

    /// <summary>
    /// The headless/design-time guard still holds: with no owner at all, a modal that goes through the shared
    /// guard is skipped and the call resolves rather than throwing.
    /// </summary>
    /// <remarks>
    /// Asserted on a dialog that USES the guard. The code-built message boxes do not — they build their window
    /// inline and resolve through a TaskCompletionSource — so with no owner they show a modeless window and wait
    /// for it, which is a hang rather than a value. That is pre-existing behaviour and not this task's to change;
    /// what this task did change there is which window they parent on when there IS an owner.
    /// </remarks>
    [AvaloniaTest]
    public async Task WithNoOwnerAtAllAGuardedModalIsSkippedRatherThanThrown()
    {
        AvaloniaDialogService service = new();

        PropertiesResult? edited = await service.EditPropertiesAsync("t", "n", "note");

        Assert.That(edited, Is.Null);
    }

    /// <summary>The resolution the service itself uses — internal, so a caller cannot depend on it.</summary>
    private static Window Owner(AvaloniaDialogService service) =>
        AvaloniaDialogService.Innermost(service.Owner!);
}
