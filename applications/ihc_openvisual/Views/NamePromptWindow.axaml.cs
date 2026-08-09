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
        // The refusal is about what the box holds NOW, so typing retracts it — an error line that outlives the
        // thing it complained about is worse than none. Watched through the property system rather than the
        // TextChanged event: the property change is raised for every route into the text, typing included.
        NameBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
                NameError.IsVisible = false;
        };
    }

    public static Task<string?> ShowAsync(Window owner, NamePromptInput input)
    {
        var window = new NamePromptWindow { Title = input.Title };
        window.NameBox.Text = input.InitialName;
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    // An all-whitespace name is not a name, so OK refuses it rather than committing one the engine would have to
    // second-guess — but it SAYS SO. Refusing silently is the worse half of that behaviour: the button appears
    // broken, and a keyboard or screen-reader user gets no signal at all. Cancel remains the way out.
    private void OnOk(object? sender, RoutedEventArgs e)
    {
        string name = NameBox.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            NameError.IsVisible = true;
            NameBox.Focus();   // put the caret where the fix has to happen
            return;
        }
        Accept(name);
    }
}
