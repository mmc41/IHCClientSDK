using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The one-field name prompt. The reference application raises exactly this shape — a "Navn" group box holding one
/// text box, plus OK / Annuller — for all four of its enumerator Ny/Omdøb buttons, so one window serves all four and
/// only the <see cref="NamePromptInput.Title"/> differs.
/// <para>
/// The initial text is SELECTED on open (<see cref="ResultDialog{TResult}.FocusOnOpen"/>), matching the vendor:
/// "Ny" starts on the placeholder "Navn" and "Omdøb" on the current name, and in both cases typing replaces it.
/// </para>
/// </summary>
public partial class NamePromptWindow : ResultDialog<string>
{
    public NamePromptWindow()
    {
        InitializeComponent();
    }

    public static Task<string?> ShowAsync(Window owner, NamePromptInput input)
    {
        var window = new NamePromptWindow { Title = input.Title };
        window.NameBox.Text = input.InitialName;
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    // An all-whitespace name is not a name: OK does nothing rather than committing one the engine would have to
    // second-guess. Cancel remains the way out.
    private void OnOk(object? sender, RoutedEventArgs e)
    {
        string name = NameBox.Text?.Trim() ?? string.Empty;
        if (name.Length > 0)
        {
            Accept(name);
        }
    }
}
