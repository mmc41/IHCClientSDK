using System.Collections.Immutable;
using Ihc.Vis.Model;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// A product's dialog as it applies to ONE placed element: the preset, totalized.
    /// <para>Where a <see cref="ProductDialogModel"/> describes a family in the abstract — "a field per
    /// <c>sms_modem_phonenumber</c> descendant" — a descriptor describes this modem: thirty fields, each already
    /// resolved to the <see cref="ElementId"/> it writes and carrying the value it currently holds. Nothing is left
    /// for a renderer or a write-back to work out, which is what lets both be family-agnostic.</para>
    /// <para>Plain and immutable: no project reference, no element reference, no lazy evaluation. A descriptor is a
    /// snapshot, so holding one cannot keep a project alive or observe an edit made after it was composed.</para>
    /// </summary>
    /// <param name="Title">The dialog's window title, in the per-family form the original uses.</param>
    /// <param name="Groups">The composed groups, in presentation order.</param>
    public sealed record ProductDialogDescriptor(string Title, EquatableArray<DialogDescriptorGroup> Groups)
    {
        // Equality is the compiler's: EquatableArray<T> compares its elements structurally, so every member
        // declared here — and every member added later — is covered without a handwritten list.

        /// <summary>Every field of every group, flattened — the shape a write-back iterates.</summary>
        /// <remarks>Computed, not stored, so it holds no equality significance of its own: two descriptors are
        /// equal exactly when their groups are.</remarks>
        public ImmutableArray<DialogDescriptorField> AllFields =>
            [.. System.Linq.Enumerable.SelectMany(Groups, g => g.Fields)];
    }

    /// <summary>One composed group: the preset's group with its parts resolved against the placed element.</summary>
    /// <param name="Id">The preset group's id — the stem of every automation id inside it.</param>
    /// <param name="Caption">The group box's title, or null for an uncaptioned block.</param>
    /// <param name="Columns">How many columns the group's fields flow into.</param>
    /// <param name="Fields">The resolved fields, in presentation order.</param>
    /// <param name="Widgets">Hand-written composite widgets that apply to this element, in order.</param>
    public sealed record DialogDescriptorGroup(
        string Id,
        string? Caption,
        int Columns,
        EquatableArray<DialogDescriptorField> Fields,
        EquatableArray<DialogWidgetKind> Widgets)
    {
        /// <summary>Whether the columns read DOWN rather than across — the preset's
        /// <see cref="DialogGroupModel.ColumnMajor"/>, carried through so the renderer can honour it.
        /// A layout hint only: <see cref="Fields"/> stays in DECLARED order either way.</summary>
        public bool ColumnMajor { get; init; }

        /// <summary>Whether the group is drawn collapsed, with an expand/collapse affordance — the preset's
        /// <see cref="DialogGroupModel.Collapsible"/>, carried through. A display hint only: the fields are
        /// composed, validated and committed whether the group is open or shut.</summary>
        public bool Collapsible { get; init; }
    }

    /// <summary>
    /// One resolved field: what to show, what it holds now, and exactly where a commit writes it.
    /// </summary>
    /// <param name="AutomationId">Stable accessibility/automation id, <c>dlg.&lt;groupId&gt;.&lt;fieldId&gt;</c>.</param>
    /// <param name="Caption">The Danish label, with a repeat's key already substituted.</param>
    /// <param name="Control">How to present it.</param>
    /// <param name="Target">The element the value lives on — already resolved, never a tag to search for.</param>
    /// <param name="Attribute">The attribute on <paramref name="Target"/> holding the value.</param>
    /// <param name="Value">Its current EFFECTIVE value (the attribute, or its DTD default), presentation-adjusted.</param>
    /// <param name="ReadOnly">Whether the field may be edited at all.</param>
    /// <param name="Rule">The rule a new value must satisfy, or null when unconstrained.</param>
    /// <param name="Minimum">The numeric lower bound DERIVED from the target element, or null.</param>
    /// <param name="Maximum">The numeric upper bound derived from the target element, or null.</param>
    /// <param name="Suggestions">
    /// For a <see cref="DialogControlKind.ComboSuggest"/> field, the values already used for this attribute
    /// elsewhere in the OPEN PROJECT, sorted and de-duplicated; empty for every other control kind. Typing aids,
    /// never a closed list — a value not yet used anywhere must still be typeable (D07).
    /// </param>
    public sealed record DialogDescriptorField(
        string AutomationId,
        string Caption,
        DialogControlKind Control,
        ElementId Target,
        string Attribute,
        string? Value,
        bool ReadOnly,
        DialogValueRule? Rule,
        int? Minimum,
        int? Maximum,
        EquatableArray<string> Suggestions = default)
    {
        // Equality is the compiler's. This record is why the convention exists: its handwritten Equals once
        // omitted ColumnSpan, and every member added since had to be repeated into two more methods by hand.
        // EquatableArray<string> makes Suggestions structurally comparable, which is all that stood in the way.
        // Note that `Suggestions = default` needs no normalizing accessor: default IS empty for the wrapper.

        /// <summary>How many of the group's columns this field occupies — the preset's
        /// <see cref="DialogFieldModel.ColumnSpan"/>, clamped by the renderer to the group's width.</summary>
        public int ColumnSpan { get; init; } = 1;

        /// <summary>
        /// What the STORED value was divided by to produce <see cref="Value"/>, and what a committed one is
        /// multiplied by again — the preset's <see cref="DialogFieldModel.DisplayDivisor"/>, carried so the
        /// write-back can be the read's exact inverse without a second copy of the number. 1 for every field
        /// whose caption is in the file's own unit.
        /// </summary>
        public int DisplayDivisor { get; init; } = 1;
    }
}
