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
    /// resolves to the edited values or null when cancelled. A non-null <paramref name="origin"/> adds the
    /// read-only library-provenance group a library function block shows (US-019, uxparity S-19).</summary>
    public static Task<PropertiesResult?> ShowAsync(Window owner, string title, string name, string note,
        LibraryOrigin? origin = null, string affirmative = "OK", string? userGroupCaption = null,
        bool? conditionsOr = null)
    {
        var window = new PropertiesWindow { Title = title };
        window.Populate(name, note, origin, affirmative, userGroupCaption, conditionsOr);
        window.FocusOnOpen(window.NameBox);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog. Separate from <see cref="ShowAsync"/> so the parity tests can exercise the
    /// window's shape without a parent window to show it over.</summary>
    internal void Populate(string name, string note, LibraryOrigin? origin = null, string affirmative = "OK",
        string? userGroupCaption = null, bool? conditionsOr = null)
    {
        PropertiesWindow window = this;
        window.OkButton.Content = affirmative;
        window.NameBox.Text = name;
        window.NoteBox.Text = note;
        // F-24: the vendor captions the editable pair on a FUNCTION BLOCK's dialog ("Bruger egenskaber") and leaves
        // its other properties dialogs' fields uncaptioned, so the caller decides rather than this window.
        if (userGroupCaption is { Length: > 0 })
        {
            window.UserGroupCaption.Text = userGroupCaption;
            window.UserGroupCaption.IsVisible = true;
        }
        if (origin is not null)
        {
            window.OriginNameBox.Text = origin.Name;
            window.OriginNumberBox.Text = origin.Number;
            window.OriginVersionBox.Text = origin.Version;
            window.OriginCreatedBox.Text = origin.Created;
            window.OriginDeveloperBox.Text = origin.Developer;
            window.OriginPanel.IsVisible = true;
        }
        // F-48: a Betingelser group's operator, shown as the reference application's captioned AND/OR field.
        // Its ORDER is the value: index 0 is AND, index 1 OR, matching the original's own combo.
        if (conditionsOr is { } or)
        {
            window.LogicBox.ItemsSource = LogicOptions;
            window.LogicBox.SelectedIndex = or ? 1 : 0;
            window.LogicPanel.IsVisible = true;
        }
    }

    /// <summary>The two operator labels, in the reference application's order (AND first, the default).</summary>
    private static readonly string[] LogicOptions = ["AND", "OR"];

    /// <summary>The value the dialog would commit right now — the parity tests' read of the operator field.</summary>
    internal PropertiesResult ResultForTest() => Read();

    private PropertiesResult Read() =>
        new(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty,
            // Null when the field is absent, so a node type without an operator can never be reported as AND.
            LogicPanel.IsVisible ? LogicBox.SelectedIndex == 1 : null);

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(Read());
}
