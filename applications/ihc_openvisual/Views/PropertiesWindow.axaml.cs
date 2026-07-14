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
public partial class PropertiesWindow : Window
{
    private PropertiesResult? _result;

    public PropertiesWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>, pre-filling the name (selected) and note;
    /// resolves to the edited values or null when cancelled.</summary>
    public static async Task<PropertiesResult?> ShowAsync(Window owner, string title, string name, string note)
    {
        var window = new PropertiesWindow { Title = title };
        window.NameBox.Text = name;
        window.NoteBox.Text = note;
        window.Opened += (_, _) =>
        {
            window.NameBox.SelectAll();
            window.NameBox.Focus();
        };
        await window.ShowDialog(owner);
        return window._result;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        _result = new PropertiesResult(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty);
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        _result = null;
        Close();
    }
}
