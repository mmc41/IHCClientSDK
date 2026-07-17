using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The modal scene-container (<c>Scenarier</c>) dialog (US-024) — what IHC Visual opens when a product's scene
/// container is activated. The container's <i>Name</i> is read-only (it comes from the product's catalog
/// definition), the <i>Note</i> is editable, and the table lists the product's scene memberships: the scenario, the
/// function block driving it, that block's locality, and the value/ramp time this product takes in it.
/// Returns the edited <see cref="SceneContainerResult"/>, or null when dismissed.
/// </summary>
public partial class SceneContainerWindow : ResultDialog<SceneContainerResult>
{
    public SceneContainerWindow()
    {
        InitializeComponent();
    }

    /// <summary>Shows the dialog modally over <paramref name="owner"/>; resolves to the edited note or null.</summary>
    public static Task<SceneContainerResult?> ShowAsync(Window owner, SceneContainerInput input)
    {
        var window = new SceneContainerWindow { Title = input.Name };
        window.NameBox.Text = input.Name;
        window.NoteBox.Text = input.Note;
        window.ScenesList.ItemsSource = input.Rows;
        window.Opened += (_, _) => window.NoteBox.Focus();
        return window.ShowDialogForResult(owner);
    }

    private void OnOk(object? sender, RoutedEventArgs e) =>
        Accept(new SceneContainerResult(NoteBox.Text ?? string.Empty));
}
