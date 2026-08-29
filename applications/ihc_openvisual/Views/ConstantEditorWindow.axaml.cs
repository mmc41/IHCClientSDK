using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ihc_openvisual.Services;

namespace ihc_openvisual.Views;

/// <summary>
/// The <i>Rediger konstant</i> editor — one row of the sensors' <i>Indstillinger</i> grid, opened by
/// double-clicking it or through its <i>Egenskaber</i> menu item (T040).
///
/// <para>Measured on build 3.4.72.3: the vendor's window holds twelve controls of which two are visible, one
/// enabled edit box carrying the value and <b>OK with no Annuller</b>. Both halves are reproduced, the missing
/// Annuller included — a cancel button here would be an invention, and the window's own close is the way out the
/// vendor offers.</para>
///
/// <para>It does not interpret what it holds. The text goes back as text and the writing command turns it into a
/// stored value, because the format is the SDK's: a window that parsed <c>0,5</c> here would be a second reading
/// of it, free to drift from the one that writes.</para>
/// </summary>
public partial class ConstantEditorWindow : ResultDialog<string>
{
    public ConstantEditorWindow() => InitializeComponent();

    public static Task<string?> ShowAsync(Window owner, ConstantEditorInput input)
    {
        ConstantEditorWindow window = Create(input);
        window.FocusOnOpen(window.ValueBox);
        return window.ShowDialogForResult(owner);
    }

    /// <summary>The window as the editor configures it, so a test drives the SAME wiring the application does.</summary>
    internal static ConstantEditorWindow Create(ConstantEditorInput input)
    {
        var window = new ConstantEditorWindow();
        window.ValueBox.Text = input.Value;
        // The setting's name, over the markup's generic "Værdi": the vendor shows no visible label, so this is
        // the only thing that tells a screen-reader user WHICH constant they are editing.
        AutomationProperties.SetName(window.ValueBox, input.Name);
        return window;
    }

    // Whatever the box holds, unchanged — including an empty box, which is a value the command has to rule on
    // (returning to the default removes the attribute) rather than a shape this window can judge.
    private void OnOk(object? sender, RoutedEventArgs e) => Accept(ValueBox.Text ?? string.Empty);
}
