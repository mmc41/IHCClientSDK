using Avalonia.Controls;
using Ihc.Vis.Validation;

namespace ihc_openvisual.Views;

/// <summary>
/// Applies the SDK's declared field constraints to a <see cref="NumericUpDown"/> — the ONE place a window turns
/// dialog metadata into a control's bounds (T045).
/// <para>
/// It exists because the two hand-written windows used to carry their own copies of the catalog's numbers in their
/// markup: 200–60000, 2000–10000, 0–100, 0–59. Those numbers are declared somewhere already — a catalog preset per
/// dimmer setting, the scene value's own factory — and a second copy in XAML could only ever be right by
/// coincidence. Nothing here decides a bound; it forwards one.
/// </para>
/// <para>
/// An UNCONSTRAINED field is given the control's own full range rather than zero: absence of a declared bound means
/// the catalog states no limit, and clamping such a field to 0 would invent one.
/// </para>
/// </summary>
internal static class NumericFieldBounds
{
    /// <summary>Binds one field's declared bounds onto the control that edits it.</summary>
    /// <param name="box">The control to bound.</param>
    /// <param name="constraint">What the SDK says this field may hold.</param>
    public static void Apply(NumericUpDown box, FieldConstraintMetadata constraint)
    {
        box.Minimum = constraint.Minimum is { } minimum ? (decimal)minimum : decimal.MinValue;
        box.Maximum = constraint.Maximum is { } maximum ? (decimal)maximum : decimal.MaxValue;
    }
}
