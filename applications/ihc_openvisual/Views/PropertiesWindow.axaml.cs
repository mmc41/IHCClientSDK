using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal element Properties dialog (US-007): a single-line <i>Name</i> field pre-filled and selected, a
/// multi-line <i>Note</i> field, and OK/Cancel. Title follows the pattern <c>Edit &lt;name&gt; properties</c>.
/// Returns the edited <see cref="PropertiesResult"/>, or null on Cancel.
/// </summary>
public partial class PropertiesWindow : ResultDialog<PropertiesResult>
{
    public PropertiesWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>, pre-filling the name (selected) and note;
    /// resolves to the edited values or null when cancelled.</summary>
    public static Task<PropertiesResult?> ShowAsync(Window owner, string title, string name, string note)
    {
        var window = new PropertiesWindow { Title = title };
        window.NameBox.Text = name;
        window.NoteBox.Text = note;
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new PropertiesResult(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty));
}
