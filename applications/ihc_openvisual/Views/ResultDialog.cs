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

    /// <summary>Shows the dialog modally over <paramref name="owner"/> and resolves to the recorded result, or
    /// <c>null</c> when the user cancelled.</summary>
    protected async Task<TResult?> ShowDialogForResult(Window owner)
    {
        await ShowDialog(owner);
        return _result;
    }

    /// <summary>The shared Cancel handler: leaves the result null and closes.</summary>
    protected void OnCancel(object? sender, RoutedEventArgs e) => Close();

    /// <summary>Selects and focuses <paramref name="textBox"/> once the window has opened — the shared "the pre-filled
    /// name is selected and ready to overtype" behaviour of the editor dialogs (US-007/011/013). Call from
    /// <c>ShowAsync</c> before showing.</summary>
    protected void FocusOnOpen(TextBox textBox) =>
        Opened += (_, _) =>
        {
            textBox.SelectAll();
            textBox.Focus();
        };
}
