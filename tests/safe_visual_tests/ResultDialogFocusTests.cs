using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using Ihc.Vis;
using ihc_openvisual.Services;
using ihc_openvisual.Views;

namespace safe_visual_tests;

/// <summary>
/// The shared focus seam every editor dialog opens through.
///
/// <para>It used to take a <c>TextBox</c> and always select its text. Widening it to any control is what lets a
/// route land the caret on whichever control holds the value a finding is about — a checkbox, a list, a combo —
/// and the select-all stays keyed on the control's TYPE rather than on a flag, so no caller can ask for a
/// text gesture on something that has no text.</para>
/// </summary>
public class ResultDialogFocusTests : AvaloniaTestBase
{
    /// <summary>A dialog whose focus target is built by the caller and placed in the window.</summary>
    private sealed class Fixed : ResultDialog<string>
    {
        public Fixed(Control target, params Control[] others)
        {
            var panel = new StackPanel();
            panel.Children.Add(target);
            foreach (Control other in others)
            {
                panel.Children.Add(other);
            }
            Content = panel;
            FocusOnOpen(target);
        }

        public static Fixed Open(Control target, params Control[] others)
        {
            Fixed window = new(target, others);
            CurrentTestWindow = window;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }
    }

    /// <summary>
    /// The behaviour the four editor dialogs depend on: the pre-filled text is focused AND fully selected, so the
    /// installer can overtype it without clearing it first.
    /// </summary>
    [AvaloniaTest]
    public void ATextBoxIsFocusedAndItsTextIsSelected()
    {
        TextBox box = new() { Text = "forudfyldt" };
        Fixed window = Fixed.Open(box);

        Assert.Multiple(() =>
        {
            Assert.That(box.IsFocused, Is.True);
            Assert.That(box.SelectionStart, Is.Zero);
            Assert.That(box.SelectionEnd, Is.EqualTo("forudfyldt".Length),
                "the whole value is selected, which is what makes it overtypeable");
        });

        window.Close();
    }

    /// <summary>
    /// A control that is not text is focused and nothing else happens to it. There is no selection to make, and
    /// a seam that tried would either throw or quietly do something the caller never asked for.
    /// </summary>
    [AvaloniaTest]
    public void ANonTextControlIsFocusedWithNoSelectionAttempt()
    {
        CheckBox check = new() { Content = "Gem ved strømsvigt" };
        TextBox untouched = new() { Text = "ikke rørt" };
        Fixed window = Fixed.Open(check, untouched);

        Assert.Multiple(() =>
        {
            Assert.That(check.IsFocused, Is.True, "the control the caller named holds focus");
            Assert.That(untouched.IsFocused, Is.False);
            Assert.That(untouched.SelectionStart, Is.EqualTo(untouched.SelectionEnd),
                "and no OTHER control's text was selected on its behalf");
        });

        window.Close();
    }

    /// <summary>
    /// The four shipped callers still open on their first field. Asserted through a REAL one rather than through
    /// the seam alone: the widening would also look "harmless" if a caller had quietly stopped calling it.
    /// <para>FOCUS only. The select-all is not observable through this path — measured: with the box holding its
    /// pre-filled text and the field focused, the selection reads back empty, and it does so with the
    /// select-then-focus order this seam has always used AND with the reverse. That is a property of showing a
    /// modal headlessly, not of this change; the selection behaviour itself is pinned by the seam test above.</para>
    /// </summary>
    [AvaloniaTest]
    public async Task TheProjectInfoDialogStillOpensOnItsFirstField()
    {
        Window owner = new();
        CurrentTestWindow = owner;
        owner.Show();
        Task<ProjectInfoData?> pending = ProjectInfoWindow.ShowAsync(
            owner, ProjectInfoData.Empty with { Number = "12345" }, ProjectInfoSuggestions.Empty);
        Dispatcher.UIThread.RunJobs();

        ProjectInfoWindow shown = (ProjectInfoWindow)owner.OwnedWindows[0];
        TextBox box = shown.FindControl<TextBox>("ProjNumberBox")!;

        Assert.Multiple(() =>
        {
            Assert.That(box.IsFocused, Is.True, "the dialog opens on its first field");
            Assert.That(box.Text, Is.EqualTo("12345"), "sanity: it was populated before it was focused");
        });

        shown.Close();
        Dispatcher.UIThread.RunJobs();
        await pending;
        owner.Close();
    }
}
