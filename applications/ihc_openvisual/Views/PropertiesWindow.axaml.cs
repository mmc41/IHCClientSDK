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
        bool? conditionsOr = null, ElementDialogField? focus = null)
    {
        var window = new PropertiesWindow { Title = title };
        window.Populate(name, note, origin, affirmative, userGroupCaption, conditionsOr, focus);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>Fills the dialog. Separate from <see cref="ShowAsync"/> so the parity tests can exercise the
    /// window's shape without a parent window to show it over.</summary>
    internal void Populate(string name, string note, LibraryOrigin? origin = null, string affirmative = "OK",
        string? userGroupCaption = null, bool? conditionsOr = null, ElementDialogField? focus = null)
    {
        OkButton.Content = affirmative;
        NameBox.Text = name;
        NoteBox.Text = note;
        // F-24: the vendor captions the editable pair on a FUNCTION BLOCK's dialog ("Bruger egenskaber") and leaves
        // its other properties dialogs' fields uncaptioned, so the caller decides rather than this window.
        if (userGroupCaption is { Length: > 0 })
        {
            UserGroupCaption.Text = userGroupCaption;
            UserGroupCaption.IsVisible = true;
        }
        if (origin is not null)
        {
            OriginNameBox.Text = origin.Name;
            OriginNumberBox.Text = origin.Number;
            OriginVersionBox.Text = origin.Version;
            OriginCreatedBox.Text = origin.Created;
            OriginDeveloperBox.Text = origin.Developer;
            OriginPanel.IsVisible = true;
        }
        // F-48: a Betingelser group's operator, shown as the reference application's captioned AND/OR field.
        // Its ORDER is the value: index 0 is AND, index 1 OR, matching the original's own combo.
        if (conditionsOr is { } or)
        {
            LogicBox.ItemsSource = LogicOptions;
            LogicBox.SelectedIndex = or ? 1 : 0;
            LogicPanel.IsVisible = true;
        }
        // The route's field when it asked for one, the NAME otherwise — which is where this dialog has always
        // opened. Registered here rather than in ShowAsync so a headless test drives the same wiring the
        // application does, and AFTER the panels above, because whether the logic field is showing decides
        // whether it can be focused at all.
        FocusOnOpen(FocusTarget(focus) ?? NameBox);
    }

    /// <summary>
    /// The window's own map from a route's field key to the control holding that value — by compiled
    /// <c>x:Name</c>, so a rename is a compile error rather than a focus that silently lands nowhere.
    /// <para>The logic key answers null unless its PANEL is showing — which is only for a <c>Betingelser</c>
    /// group. The test is on the panel rather than on the box, because a control inside a collapsed parent still
    /// reports itself visible; asking the box would have focused a field that is not on screen.</para>
    /// </summary>
    internal Control? FocusTarget(ElementDialogField? field) =>
        field is { } wanted && ControlFor(wanted) is { IsEnabled: true } target ? target : null;

    private Control? ControlFor(ElementDialogField field) => field switch
    {
        ElementDialogField.Name => NameBox,
        ElementDialogField.Note => NoteBox,
        ElementDialogField.Logic => LogicPanel.IsVisible ? LogicBox : null,
        _ => null,
    };

    /// <summary>The two operator labels, in the reference application's order (AND first, the default).</summary>
    private static readonly string[] LogicOptions = ["AND", "OR"];

    /// <summary>The value the dialog would commit right now — also the parity tests' read of the operator field,
    /// which is otherwise only observable by driving a modal to OK.</summary>
    internal PropertiesResult Read() =>
        new(NameBox.Text ?? string.Empty, NoteBox.Text ?? string.Empty,
            // Null when the field is absent, so a node type without an operator can never be reported as AND.
            LogicPanel.IsVisible ? LogicBox.SelectedIndex == 1 : null);

    private void OnOk(object? sender, RoutedEventArgs e) => Accept(Read());
}
