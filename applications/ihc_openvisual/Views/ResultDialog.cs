using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ihc_openvisual.Views;

/// <summary>
/// Base for the modal editor dialogs that resolve to an optional <typeparamref name="TResult"/>. It owns the pending
/// result, the shared Cancel handler (leaves the result null and closes), and the show-and-return helper. A concrete
/// dialog records its value in the OK handler via <see cref="Accept"/> and exposes a static <c>ShowAsync</c> that
/// configures the window and awaits <see cref="ShowDialogForResult"/>.
/// </summary>
/// <typeparam name="TResult">The value the dialog produces; <c>null</c> means the user cancelled.</typeparam>
public abstract class ResultDialog<TResult> : Window where TResult : class
{
    private TResult? _result;

    /// <summary>Records the dialog's result and closes it — call from the OK handler.</summary>
    protected void Accept(TResult result)
    {
        _result = result;
        Close();
    }

    /// <summary>What the dialog would return — the seam the headless view tests read, since
    /// <see cref="ShowDialogForResult"/> needs a modal loop they do not run. Null while nothing has been
    /// accepted, which is also what a cancellation leaves behind.</summary>
    internal TResult? AcceptedResult => _result;

    /// <summary>Shows the dialog modally over <paramref name="owner"/> and resolves to the recorded result, or
    /// <c>null</c> when the user cancelled.</summary>
    protected async Task<TResult?> ShowDialogForResult(Window owner)
    {
        await ShowDialog(owner);
        return _result;
    }

    /// <summary>The shared Cancel handler: leaves the result null and closes.</summary>
    protected void OnCancel(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Focuses <paramref name="control"/> once the window has opened. Call from <c>ShowAsync</c> before showing.
    /// </summary>
    /// <remarks>
    /// <para>A <see cref="TextBox"/> is additionally SELECTED — the shared "the pre-filled name is selected and
    /// ready to overtype" behaviour of the editor dialogs (US-007/011/013). Every other control kind is only
    /// focused: select-all is a text gesture, and there is nothing for a checkbox or a list to select.</para>
    /// <para>Widened from <c>TextBox</c> so a route can land the caret on whichever control actually holds the
    /// value a finding is about. The text behaviour is keyed on the control's TYPE rather than on a flag the
    /// caller passes, so no caller can ask for a select-all that means nothing.</para>
    /// </remarks>
    protected void FocusOnOpen(Control control) =>
        Opened += (_, _) =>
        {
            if (control is TextBox textBox)
            {
                textBox.SelectAll();
            }
            control.Focus();
        };
}
