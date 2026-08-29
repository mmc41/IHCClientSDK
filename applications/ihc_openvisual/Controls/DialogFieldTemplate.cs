using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ihc_openvisual.ViewModels;
using Ihc.Vis.Products;

namespace ihc_openvisual.Controls;

/// <summary>
/// Picks the editor a product-dialog field is realized with, from the field's <see cref="DialogControlKind"/>.
/// <para>Avalonia's stock <c>DataTemplate</c> matches on the data's CLR TYPE, and every field of the generic
/// product dialog is the same type — they differ by a property. So the choice has to be made here. It is still
/// made out of <c>DataTemplate</c>s (one per kind, authored in the window's XAML and assigned to the properties
/// below); this class only decides which one applies.</para>
/// <para>Why a selector rather than four controls with <c>IsVisible</c> bindings: hiding is not the same as not
/// building. Four realized editors per field means four elements sharing one <c>AutomationId</c>, of which three
/// are invisible but focusable — a driver asking for that id gets four elements and cannot proceed, and a screen
/// reader walks three phantom boxes. Measured, before this existed: every field of every family realized ×4.</para>
/// </summary>
public sealed class DialogFieldTemplate : IDataTemplate
{
    /// <summary>
    /// Thrown when a field carries a kind no template covers. FAIL LOUD, deliberately: returning null here renders
    /// the field as a caption with no editor — a dialog silently missing a value the installer needs, invisible to
    /// every assertion that does not already know the field should be there.
    /// </summary>
    public sealed class UnknownControlKindException(DialogControlKind kind, string automationId)
        : InvalidOperationException(
            $"The product dialog has no template for control kind '{kind}' (field '{automationId}'). "
            + $"Add one to {nameof(DialogFieldTemplate)} in ProductDialogWindow.axaml rather than letting the "
            + "field render as nothing.");

    /// <summary>Single-line free text.</summary>
    public IDataTemplate? Text { get; set; }

    /// <summary>Multi-line free text (the Note field).</summary>
    public IDataTemplate? TextMultiline { get; set; }

    /// <summary>Free text with a suggestion list — always editable, never a closed list (D07).</summary>
    public IDataTemplate? ComboSuggest { get; set; }

    /// <summary>A bounded integer.</summary>
    public IDataTemplate? Number { get; set; }

    /// <summary>A yes/no tick box, labelled by its own caption rather than by the shared caption block.</summary>
    public IDataTemplate? Checkbox { get; set; }

    /// <summary>A CLOSED combo over an enumerated attribute — the list is the declaration's, not a typing aid.</summary>
    public IDataTemplate? ComboFixed { get; set; }

    /// <summary>The template for a kind, or null when none is assigned. Public so a test can assert coverage of the
    /// whole enum — the check that keeps <see cref="Build"/>'s throw unreachable for every REAL kind.</summary>
    public IDataTemplate? ForKind(DialogControlKind kind) => kind switch
    {
        DialogControlKind.Text => Text,
        DialogControlKind.TextMultiline => TextMultiline,
        DialogControlKind.ComboSuggest => ComboSuggest,
        DialogControlKind.Number => Number,
        DialogControlKind.Checkbox => Checkbox,
        DialogControlKind.ComboFixed => ComboFixed,
        _ => null,
    };

    public bool Match(object? data) => data is ProductDialogFieldViewModel;

    public Control? Build(object? param)
    {
        var field = (ProductDialogFieldViewModel)param!;
        IDataTemplate template = ForKind(field.Control)
            ?? throw new UnknownControlKindException(field.Control, field.AutomationId);
        return template.Build(field);
    }
}
