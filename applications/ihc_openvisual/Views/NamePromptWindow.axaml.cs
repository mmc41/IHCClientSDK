using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;
using Ihc.Vis.Problems;

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

    /// <summary>
    /// The blank decision, SUPPLIED. A View may not name <c>ProjectAppService</c>, so it cannot mint this and it
    /// cannot fall back to one of its own — an emptiness test written here is exactly the shell-side duplicate
    /// this window stopped carrying. Null only on the parameterless construction path, which is the XAML previewer
    /// and the tests that never press OK; every production route builds a <see cref="NamePromptInput"/>, whose
    /// <see cref="NamePromptInput.Blank"/> the compiler requires.
    /// </summary>
    private Func<string?, Problem?>? _blank;

    public static Task<string?> ShowAsync(Window owner, NamePromptInput input)
    {
        NamePromptWindow window = Create(input);
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>The window as the prompt configures it, so a test drives the SAME wiring the application does
    /// rather than a window whose validator was never attached.</summary>
    internal static NamePromptWindow Create(NamePromptInput input)
    {
        var window = new NamePromptWindow { Title = input.Title };
        window._blank = input.Blank;
        window.NameBox.Text = input.InitialName;
        return window;
    }

    // An all-whitespace name is not a name, so OK refuses it rather than committing one the engine would have to
    // second-guess — but it SAYS SO. Refusing silently is the worse half of that behaviour: the button appears
    // broken, and a keyboard or screen-reader user gets no signal at all. Cancel remains the way out.
    //
    // T045: the DECISION is the SDK's required-field constraint and the SENTENCE is its coded problem's — this
    // window's own emptiness test and its markup's fixed line are gone. What stays here is the interaction the
    // task keeps in the windows: the inline feedback, the focus return, and the dialog staying open.
    //
    // The decision now arrives on the input rather than through a shell-side helper: the caller asks the SDK
    // facade and hands the answer in, because a View may not drive ProjectAppService.
    private void OnOk(object? sender, RoutedEventArgs e)
    {
        // NO VALIDATOR MEANS NO DECISION, so OK commits nothing. It is not a fallback emptiness test: writing one
        // here would be exactly the shell-side duplicate this window stopped carrying, and it would be a SECOND
        // answer to a question the SDK already answers. Refusing is the honest outcome and matches what a blank
        // name produces — the dialog stays open. Invoked with `?.` before, OK skipped the check and committed an
        // unchecked name on this path.
        if (_blank is null)
        {
            NameBox.Focus();
            return;
        }

        if (_blank(NameBox.Text) is { } blank)
        {
            // Rendered, not raw: the inline error carries the refusal's identity exactly as a dialog does (R18).
            NameError.Text = ihc_openvisual.ViewModels.ProblemPresenter.Text(blank);
            NameError.IsVisible = true;
            NameBox.Focus();   // put the caret where the fix has to happen
            return;
        }
        // No `!`: the box's text is null until it is touched, and the bang had nothing behind it — the validator
        // above is what rules a null out, and only once there IS one.
        Accept((NameBox.Text ?? string.Empty).Trim());
    }
}
