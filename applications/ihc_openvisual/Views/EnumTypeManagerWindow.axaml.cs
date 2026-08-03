using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The enumerator-type manager (US-030, uxparity2 W10/F12): lists the enumerator types the project defines and offers
/// to create another. It is deliberately a LIST plus <i>Ny type…</i> rather than a second editor — the definition
/// dialog (<see cref="EnumDefinitionWindow"/>) already owns naming a type and its states, and duplicating it here
/// would be two editors for one thing.
/// <para>
/// Reached from the Library menu, which is the finding: type creation existed before but only inside a
/// variable-insert flyout on a value section — a route nobody looking for "the project's types" would find.
/// </para>
/// </summary>
public partial class EnumTypeManagerWindow : ResultDialog<EnumTypeManagerResult>
{
    public EnumTypeManagerWindow()
    {
        InitializeComponent();
    }

    public static Task<EnumTypeManagerResult?> ShowAsync(Window owner, EnumTypeManagerInput input)
    {
        var window = new EnumTypeManagerWindow { Title = input.Title };
        window.TypesList.ItemsSource = input.Types;
        // An empty project should say so rather than showing an unexplained empty box.
        window.EmptyHint.IsVisible = input.Types.Count == 0;
        return window.ShowDialogForResult(owner);
    }

    // "Ny type…" — the manager reports the INTENT; the view-model then opens the definition dialog, so the naming
    // rules live in exactly one place.
    private void OnNew(object? sender, RoutedEventArgs e) => Accept(new EnumTypeManagerResult(SelectedType: null));

    private void OnClose(object? sender, RoutedEventArgs e) => Close(null);
}
