#nullable enable
using System;
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
    public sealed record ProductDialogDescriptor(string Title, ImmutableArray<DialogDescriptorGroup> Groups)
    {
        /// <summary>Every field of every group, flattened — the shape a write-back iterates.</summary>
        public ImmutableArray<DialogDescriptorField> AllFields =>
            [.. System.Linq.Enumerable.SelectMany(Groups, g => g.Fields)];

        public bool Equals(ProductDialogDescriptor? other) =>
            other is not null
            && string.Equals(Title, other.Title, StringComparison.Ordinal)
            && ImmutableArrayValue.Equal(Groups, other.Groups);

        public override int GetHashCode() => HashCode.Combine(Title, ImmutableArrayValue.Hash(Groups));
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
        ImmutableArray<DialogDescriptorField> Fields,
        ImmutableArray<DialogWidgetKind> Widgets)
    {
        /// <summary>Whether the columns read DOWN rather than across — the preset's
        /// <see cref="DialogGroupModel.ColumnMajor"/>, carried through so the renderer can honour it.
        /// A layout hint only: <see cref="Fields"/> stays in DECLARED order either way.</summary>
        public bool ColumnMajor { get; init; }

        public bool Equals(DialogDescriptorGroup? other) =>
            other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Caption, other.Caption, StringComparison.Ordinal)
            && Columns == other.Columns
            && ColumnMajor == other.ColumnMajor
            && ImmutableArrayValue.Equal(Fields, other.Fields)
            && ImmutableArrayValue.Equal(Widgets, other.Widgets);

        public override int GetHashCode() =>
            HashCode.Combine(Id, Caption, Columns, ColumnMajor,
                ImmutableArrayValue.Hash(Fields), ImmutableArrayValue.Hash(Widgets));
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
        ImmutableArray<string> Suggestions = default)
    {
        /// <summary>How many of the group's columns this field occupies — the preset's
        /// <see cref="DialogFieldModel.ColumnSpan"/>, clamped by the renderer to the group's width.</summary>
        public int ColumnSpan { get; init; } = 1;

        /// <summary>The suggestions, never <c>default</c> — an empty array when the field offers none.</summary>
        public ImmutableArray<string> SuggestionsOrEmpty =>
            Suggestions.IsDefault ? ImmutableArray<string>.Empty : Suggestions;

        public bool Equals(DialogDescriptorField? other) =>
            other is not null
            && string.Equals(AutomationId, other.AutomationId, StringComparison.Ordinal)
            && string.Equals(Caption, other.Caption, StringComparison.Ordinal)
            && Control == other.Control
            && Target == other.Target
            && string.Equals(Attribute, other.Attribute, StringComparison.Ordinal)
            && string.Equals(Value, other.Value, StringComparison.Ordinal)
            && ReadOnly == other.ReadOnly
            && Equals(Rule, other.Rule)
            && Minimum == other.Minimum
            && Maximum == other.Maximum
            && ColumnSpan == other.ColumnSpan
            && ImmutableArrayValue.Equal(Suggestions, other.Suggestions);

        public override int GetHashCode() =>
            HashCode.Combine(
                HashCode.Combine(AutomationId, Caption, Control, Target, Attribute, Value),
                HashCode.Combine(ReadOnly, Rule, Minimum, Maximum, ColumnSpan, ImmutableArrayValue.Hash(Suggestions)));
    }
}
