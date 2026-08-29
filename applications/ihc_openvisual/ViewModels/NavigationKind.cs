namespace ihc_openvisual.ViewModels;

/// <summary>
/// What a <i>Problemer</i> row can take the installer to — decided once when the result binds, and the same value
/// the row's tooltip promises and its activation delivers.
/// <para><see cref="Ancestor"/> is a member of its own rather than a shade of <see cref="Tree"/>. A finding on an
/// element the tree does not draw — a setting inside a <c>*_settings</c> container, a calibration row — lands on
/// the product instead, and a row that said only "the tree" would be promising to select the element itself.
/// Naming the two apart is what lets the row be honest before the click rather than after it.</para>
/// </summary>
public enum NavigationKind
{
    /// <summary>Nowhere: the finding names no single element, or the element it named is gone.</summary>
    None,

    /// <summary>The element's own row in the tree.</summary>
    Tree,

    /// <summary>The nearest ancestor that HAS a row, because the element itself is not drawn.</summary>
    Ancestor,

    /// <summary>
    /// The owner's properties dialog, opened without landing on a particular control.
    /// <para>This is the HONEST answer whenever a field cannot be promised: the attribute is not rendered as a
    /// field, it is read-only, or the row declares none at all. A route that said <see cref="Field"/> and then
    /// opened a dialog with nothing focused is the dishonesty the kind exists to prevent.</para>
    /// </summary>
    Dialog,

    /// <summary>The exact field, focused — claimed only where the dialog really offers it and it is editable.</summary>
    Field,
}
