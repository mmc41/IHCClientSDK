#nullable enable
using System;
using System.Collections.Immutable;
using Ihc.Vis.Model;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// What a product's properties dialog CONTAINS, as data: its groups, their fields, and where each field's value
    /// lives in the project. A renderer turns one of these into controls; the write-back turns the same one into
    /// attribute writes. Neither hardcodes a family.
    /// <para>Authored per FAMILY as a preset, before any element exists — which is why a binding names a tag and an
    /// attribute rather than an <c>ElementId</c>. The composer resolves those against a placed element.</para>
    /// <para><b>Never serialized.</b> This describes a dialog, not a project: it is attached to a
    /// <see cref="ProductDefinition"/> in memory the way <c>Documentation</c> is, and no writer reads it. That is
    /// what keeps the byte-fidelity guarantee independent of anything decided here.</para>
    /// </summary>
    public sealed record ProductDialogModel(ImmutableArray<DialogGroupModel> Groups)
    {
        /// <summary>The model for a family with no preset — the minimal fallback's starting point.</summary>
        public static ProductDialogModel Empty { get; } = new(ImmutableArray<DialogGroupModel>.Empty);

        /// <summary>True when the model declares no groups at all.</summary>
        public bool IsEmpty => Groups.IsDefaultOrEmpty;

        /// <summary>
        /// What the dialog's title appends to the product's catalog type name — empty for almost every family.
        /// <para>Measured across all 100 catalog products (2026-08-11): only the modem titles itself
        /// <c>"&lt;name&gt; Egenskaber"</c>; every other family is titled with the bare product name. A single rule
        /// would have been wrong for 99 families or for 1, so it is DATA rather than a comparison in the composer:
        /// a sixth family that titles differently is then a preset to author, not a composer to edit.</para>
        /// </summary>
        public string TitleSuffix { get; init; } = string.Empty;

        // A record compares an ImmutableArray member by its backing-array REFERENCE, so two independently
        // constructed but identical models would be unequal — the pitfall ProjectModelEqualityTests exists for.
        // ImmutableArrayValue restores the by-value semantics a record is expected to have, exactly as
        // ProjectElement and CatalogGrammar do.
        public bool Equals(ProductDialogModel? other) =>
            other is not null
            && string.Equals(TitleSuffix, other.TitleSuffix, StringComparison.Ordinal)
            && ImmutableArrayValue.Equal(Groups, other.Groups);

        public override int GetHashCode() => HashCode.Combine(TitleSuffix, ImmutableArrayValue.Hash(Groups));

        public override string ToString() => $"ProductDialogModel({Groups.Length} groups)";
    }

    /// <summary>
    /// One captioned block of a dialog.
    /// </summary>
    /// <param name="Id">Stable identifier, and the stem of every automation id inside it. Never null — a caption may
    /// be absent, an identity may not.</param>
    /// <param name="Caption">The group box's Danish title, or null for an uncaptioned run of fields.</param>
    /// <param name="Columns">How many columns the group's fields flow into; 1 unless measured otherwise (the modem's
    /// telephone-number group is 3, matching the original).</param>
    /// <param name="Parts">The group's contents, in the order the dialog shows them.</param>
    public sealed record DialogGroupModel(
        string Id,
        string? Caption,
        int Columns,
        ImmutableArray<DialogPartModel> Parts)
    {
        /// <summary>
        /// Whether a multi-column group reads DOWN each column rather than across each row.
        /// <para>Both directions occur and neither is a house style, so it is declared per group: the SMS
        /// modem's <i>Telefon numre</i> reads down — 1–10, 11–20, 21–30 — while the S0 device's seven
        /// fields read across (measured 2026-08-11/12, T035). One global choice fixes one family and
        /// breaks the other, which is why this is metadata rather than a renderer decision.</para>
        /// <para>Purely a LAYOUT hint, like <see cref="Columns"/>: it changes the order fields are DRAWN
        /// in, never the order the descriptor declares them in. Slot <i>n</i> stays at index <i>n-1</i>,
        /// because the write-back and the validation tests address slots by position.</para>
        /// </summary>
        public bool ColumnMajor { get; init; }

        /// <summary>
        /// A descendant tag the placed element must carry for this group to appear at all, or null for a group
        /// every member of the family gets.
        /// <para>The same mechanism <see cref="DialogWidgetModel.PresenceTag"/> gives a widget, lifted to a
        /// group so that a run of ordinary FIELDS can be family-optional: the jalousi products' <i>Persienne
        /// egenskaber</i> is two plain numbers that 22 of the 24 wireless products must not be offered (T119).
        /// </para>
        /// <para><b>Declared, not inferred from whether the bindings resolve.</b> Leaving the fields to drop
        /// themselves would render the same dialog — and would make a MISTYPED tag indistinguishable from a
        /// deliberately absent one, which is precisely what the catalog-wide descriptor gate exists to catch
        /// (it treats an unresolved preset field as a defect, and it should).</para>
        /// </summary>
        public string? PresenceTag { get; init; }

        public bool Equals(DialogGroupModel? other) =>
            other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Caption, other.Caption, StringComparison.Ordinal)
            && Columns == other.Columns
            && ColumnMajor == other.ColumnMajor
            && string.Equals(PresenceTag, other.PresenceTag, StringComparison.Ordinal)
            && ImmutableArrayValue.Equal(Parts, other.Parts);

        public override int GetHashCode() =>
            HashCode.Combine(Id, Caption, Columns, ColumnMajor, PresenceTag, ImmutableArrayValue.Hash(Parts));

        public override string ToString() =>
            $"DialogGroupModel({Id}, {Parts.Length} parts{(Caption is null ? "" : ", \"" + Caption + "\"")})";
    }

    /// <summary>One item inside a group: a field, a repeat, or a widget slot. Three cases, closed by design (D12).</summary>
    public abstract record DialogPartModel(string Id);

    /// <summary>
    /// A single labelled field.
    /// </summary>
    /// <param name="Id">Stable identifier; the leaf of the field's automation id.</param>
    /// <param name="Caption">Non-nullable: the fragment that declares a field owns its caption, so a field can never
    /// reach a renderer unlabelled.</param>
    /// <param name="Control">How the value is presented and edited.</param>
    /// <param name="Binding">Where the value lives, relative to the placed element.</param>
    /// <param name="Rule">What the value must look like, or null for unconstrained free text.</param>
    /// <param name="ReadOnly">
    /// Declared, not derived. <c>product_rs485_sms_modem</c> declares no <c>locked</c> attribute at all, so the
    /// original's disabled <i>Navn</i> cannot be read off element state — it is a property of the DIALOG. Families
    /// that do declare <c>locked</c> still contribute it as an additional read-only source at compose time.
    /// </param>
    public sealed record DialogFieldModel(
        string Id,
        string Caption,
        DialogControlKind Control,
        DialogBinding Binding,
        DialogValueRule? Rule = null,
        bool ReadOnly = false) : DialogPartModel(Id)
    {
        /// <summary>
        /// How many of the group's columns this field occupies. 1 unless measured otherwise.
        /// <para><c>Note</c> is the case: the vendor gives it the WHOLE row in both the wired and the
        /// wireless dialog, so the fields after it pair up beneath rather than beside it. Flowing every
        /// field into a uniform grid instead puts <c>Kabeltype</c> next to <c>Note</c> and shifts every
        /// later field into the wrong cell (measured on products 003/004/069, T038).</para>
        /// <para>Clamped to the group's column count at layout time, so a shared fragment declaring 2 is
        /// harmless in a one-column group — which is how the modem keeps its single-column identity block
        /// while sharing the same <c>Note</c>.</para>
        /// </summary>
        public int ColumnSpan { get; init; } = 1;
    }

    /// <summary>
    /// A field repeated once per matching DESCENDANT of the placed element — the modem's 30 telephone slots are one
    /// of these, not thirty declarations.
    /// </summary>
    /// <param name="Id">Stable identifier; each expanded field's automation id appends its key.</param>
    /// <param name="CaptionPattern">A composite format taking the key as <c>{0}</c>, e.g. <c>"Nummer {0}"</c>.</param>
    /// <param name="DescendantTag">The child element type to expand over, e.g. <c>sms_modem_phonenumber</c>.</param>
    /// <param name="KeyAttribute">The attribute that orders the expansion and supplies the caption argument.</param>
    /// <param name="ValueAttribute">The attribute on each descendant that holds the field's value.</param>
    /// <param name="Control">How each expanded field is presented and edited.</param>
    /// <param name="Rule">What each expanded value must look like, or null for unconstrained free text.</param>
    public sealed record DialogRepeatModel(
        string Id,
        string CaptionPattern,
        string DescendantTag,
        string KeyAttribute,
        string ValueAttribute,
        DialogControlKind Control,
        DialogValueRule? Rule = null) : DialogPartModel(Id);

    /// <summary>
    /// A slot for a hand-written composite widget that metadata does not try to describe — the terminal grids and the
    /// advanced-dimmer button. One preset can serve a family whose members differ in whether they have the thing.
    /// </summary>
    /// <param name="Id">Stable identifier; the stem of the widget's automation id.</param>
    /// <param name="Kind">Which hand-written widget fills the slot.</param>
    /// <param name="PresenceTag">A descendant tag that must exist on the placed element for the slot to render, or
    /// null to render unconditionally.</param>
    public sealed record DialogWidgetModel(
        string Id,
        DialogWidgetKind Kind,
        string? PresenceTag = null) : DialogPartModel(Id);

    /// <summary>
    /// How a field is presented and edited. Five kinds, each with a measured consumer: the four D12 froze,
    /// plus <see cref="Checkbox"/>, which one product's dialog turned out to need (T098).
    /// </summary>
    public enum DialogControlKind
    {
        /// <summary>A one-line free-text box.</summary>
        Text,

        /// <summary>A multi-line free-text box.</summary>
        TextMultiline,

        /// <summary>
        /// An EDITABLE combo over a free-text attribute, offering the open project's existing values as typing
        /// suggestions. Never a closed list — a value not yet used in the project must still be typeable (D07).
        /// </summary>
        ComboSuggest,

        /// <summary>A numeric box; its range is derived from the placed element, not declared here.</summary>
        Number,

        /// <summary>
        /// A two-state tick box over a <c>yes</c>/<c>no</c> attribute, labelled by its own caption.
        /// <para>Added 2026-08-12 (T098) for <i>Inkluder produktet i slutbruger rapport</i>, the only
        /// checkbox in any of the 100 product dialogs. It could not be expressed with the four existing
        /// kinds: rendering the flag as text would put the literal <c>yes</c> on screen and let anything
        /// be typed into an enumerated attribute.</para>
        /// <para>The <c>yes</c>/<c>no</c> spelling is the FILE's, and it stays that way through the
        /// descriptor and the write-back — the renderer is the only place that knows a tick means "yes",
        /// exactly as it is the only place that knows a combo means free text.</para>
        /// </summary>
        Checkbox,
    }

    /// <summary>The hand-written composite widgets a slot can name. Three kinds, each with an existing
    /// implementation — the two D12 froze, plus <see cref="SettingsGrid"/> (T070).</summary>
    public enum DialogWidgetKind
    {
        /// <summary>The product dialog's input/output terminal grids.</summary>
        TerminalGrids,

        /// <summary>The wireless dimmer's <i>Avanceret</i> button and its sub-dialog.</summary>
        AdvancedDimmerButton,

        /// <summary>
        /// The sensors' <i>Indstillinger</i> grid: a third terminal-style list beneath Indgange and
        /// Udgange, one row per calibration setting with a name, a note and a value.
        /// <para>Added 2026-08-12 (T070), making three widget kinds where D12 froze two. The vendor draws
        /// it for the six catalog products carrying <c>setting="yes"</c> resources — the temperature,
        /// humidity and lux sensors — and OpenVisual drew nothing, so their calibration offsets were
        /// unreachable. It could not be expressed with the existing vocabulary: it is neither a field nor
        /// a repeat (its rows are a grid with their own columns) and neither of the two widget kinds.</para>
        /// </summary>
        SettingsGrid,
    }

    /// <summary>
    /// WHERE a field's value lives, declared against the family's SHAPE. A preset is authored before any element
    /// exists, so a binding cannot name an <c>ElementId</c>; the composer resolves each one against the placed
    /// element. Two kinds, closed by design (D12).
    /// </summary>
    public abstract record DialogBinding
    {
        // Non-public constructor: the two nested cases are the whole vocabulary, and an external subclass would be a
        // third binding kind that no composer, renderer or write-back knows how to handle.
        private protected DialogBinding() { }

        /// <summary>An attribute on the product's own root element.</summary>
        public sealed record RootAttribute(string Name) : DialogBinding;

        /// <summary>An attribute on the first descendant carrying <paramref name="Tag"/>.</summary>
        public sealed record DescendantAttribute(string Tag, string Attribute = "value") : DialogBinding;
    }
}
