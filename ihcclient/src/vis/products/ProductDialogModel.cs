#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed record ProductDialogModel(EquatableArray<DialogGroupModel> Groups)
    {
        /// <summary>The model for a family with no preset — the minimal fallback's starting point.</summary>
        public static ProductDialogModel Empty { get; } = new([]);

        /// <summary>True when the model declares no groups at all.</summary>
        public bool IsEmpty => Groups.IsEmpty;

        /// <summary>
        /// What the dialog's title appends to the product's catalog type name — empty for almost every family.
        /// <para>Measured across all 100 catalog products (2026-08-11): only the modem titles itself
        /// <c>"&lt;name&gt; Egenskaber"</c>; every other family is titled with the bare product name. A single rule
        /// would have been wrong for 99 families or for 1, so it is DATA rather than a comparison in the composer:
        /// a sixth family that titles differently is then a preset to author, not a composer to edit.</para>
        /// </summary>
        public string TitleSuffix { get; init; } = string.Empty;

        // Equality is the compiler's: EquatableArray<T> compares its elements structurally, so every member
        // declared above — and every member added later — is covered without a handwritten list.
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
        EquatableArray<DialogPartModel> Parts)
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
        /// When this group appears at all — <see cref="DialogPresence.Always"/> for a group every member of the
        /// family gets.
        /// <para>The same <see cref="DialogPresence"/> vocabulary a widget slot uses, lifted to a group so that a
        /// run of ordinary FIELDS can be family-optional: the jalousi products' <i>Persienne egenskaber</i> is two
        /// plain numbers that 22 of the 24 wireless products must not be offered (T119).</para>
        /// <para><b>Declared, not inferred from whether the bindings resolve.</b> Leaving the fields to drop
        /// themselves would render the same dialog — and would make a MISTYPED tag indistinguishable from a
        /// deliberately absent one, which is precisely what the catalog-wide descriptor gate exists to catch
        /// (it treats an unresolved preset field as a defect, and it should).</para>
        /// </summary>
        public DialogPresence Presence { get; init; } = DialogPresence.Always;

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

        /// <summary>
        /// Whether this field's stored value may be one of the vendor's own unresolved LOCALISATION KEYS rather
        /// than text to show — in which case the composer shows it blank, as the original does.
        /// <para>A property of the FIELD, not of a family or of an attribute name: it says "values of this field
        /// can be keys", which is what makes the blanking rule metadata rather than a comparison inside the
        /// composer. Declared on the shared <c>Note</c> fragment, so every family that shows a note inherits it
        /// from one statement, and a field that is merely SHAPED like a key — a documentation tag such as
        /// <c>A_1</c> — is never blanked, because its fragment does not claim this.</para>
        /// </summary>
        public bool HidesUnresolvedResourceKey { get; init; }
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
    public sealed record DialogWidgetModel(string Id, DialogWidgetKind Kind) : DialogPartModel(Id)
    {
        /// <summary>When this slot renders — <see cref="DialogPresence.Always"/> unless the preset says otherwise.
        /// Consulted for EVERY kind: a slot's presence is what its preset declares, never a property of which
        /// hand-written widget happens to fill it.</summary>
        public DialogPresence Presence { get; init; } = DialogPresence.Always;
    }

    /// <summary>
    /// When a group or a widget slot applies to a placed element — the ONE presence vocabulary, shared by both.
    /// <para>Presence is DATA. The two rules the catalog actually needs are shaped differently — the advanced-dimmer
    /// button is gated on a descendant TAG, the settings grid on a descendant MARKED <c>setting="yes"</c> whatever
    /// its resource type — and answering the second from the widget's KIND meant a preset could state a rule for
    /// that slot which was silently discarded. A third shape is a case added here, not a branch added to the
    /// composer.</para>
    /// </summary>
    public abstract record DialogPresence
    {
        /// <summary>Whether the rule is satisfied by the placed product's own subtree (itself and its descendants).</summary>
        public abstract bool IsPresentIn(IReadOnlyList<ProjectElement> subtree);

        /// <summary>Present for every member of the family — what a slot declaring nothing gets.</summary>
        public static DialogPresence Always { get; } = new Unconditional();

        private sealed record Unconditional : DialogPresence
        {
            public override bool IsPresentIn(IReadOnlyList<ProjectElement> subtree) => true;
        }

        /// <summary>Present when the product carries a descendant with this tag.</summary>
        public sealed record DescendantTag(string Tag) : DialogPresence
        {
            public override bool IsPresentIn(IReadOnlyList<ProjectElement> subtree) =>
                subtree.Any(e => string.Equals(e.Tag, Tag, StringComparison.Ordinal));
        }

        /// <summary>
        /// Present when the product carries a descendant whose <paramref name="Attribute"/> holds
        /// <paramref name="Value"/> — a MARKER rather than a tag, which is what the settings grid needs: a setting
        /// is any resource the catalog marked, and the six sensors that have them use three different resource
        /// types (<c>resource_temperature</c>, <c>resource_humidity</c>, <c>resource_light</c>), so no tag names
        /// the set.
        /// </summary>
        public sealed record DescendantMarked(string Attribute, string Value) : DialogPresence
        {
            /// <summary>Whether ONE element carries the marker — the same question per element, for a caller that
            /// needs the matching elements themselves rather than whether any exist.</summary>
            public bool Matches(ProjectElement element) =>
                string.Equals(element.GetAttribute(Attribute), Value, StringComparison.Ordinal);

            public override bool IsPresentIn(IReadOnlyList<ProjectElement> subtree) => subtree.Any(Matches);
        }
    }

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

        /// <summary>
        /// WHICH attribute this binding names, whichever end of the product it sits on. A property of the binding
        /// rather than a composer-side switch: a switch would answer with a silent default arm, so a third binding
        /// kind would compile and quietly get no answer, where an abstract member cannot be left unimplemented.
        /// </summary>
        public abstract string AttributeName { get; }

        /// <summary>An attribute on the product's own root element.</summary>
        public sealed record RootAttribute(string Name) : DialogBinding
        {
            /// <inheritdoc/>
            public override string AttributeName => Name;
        }

        /// <summary>An attribute on the first descendant carrying <paramref name="Tag"/>.</summary>
        public sealed record DescendantAttribute(string Tag, string Attribute = "value") : DialogBinding
        {
            /// <inheritdoc/>
            public override string AttributeName => Attribute;
        }
    }
}
