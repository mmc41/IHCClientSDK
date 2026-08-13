#nullable enable
using System.Collections.Immutable;
using static Ihc.Vis.Products.ProductDialogFragments;

namespace Ihc.Vis.Products
{
    /// <summary>
    /// The per-family dialog presets — what each product family's properties dialog contains.
    /// <para>Each is a short composition of <see cref="ProductDialogFragments"/>, so the differences between
    /// families are visible at a glance and every shared caption exists once. Shapes are taken from the recorded
    /// vendor oracle (2026-08-11, all 100 catalog products), not from a reading of the DTD: the DTD says which
    /// attributes exist, the dialog says which of them the installer is offered, and those are different sets.</para>
    /// </summary>
    public static class ProductDialogPresets
    {
        // Declared FIRST because static initializers run in textual order: a preset below that referenced this
        // group from above it would compose against a null.

        /// <summary>The wired families' terminal block: the two grids, plus the settings grid for the six
        /// products that have settings.</summary>
        private static DialogGroupModel WiredTerminals { get; } =
            Group("terminaler", null, 1,
                Widget("terminaler", DialogWidgetKind.TerminalGrids),
                // Gated on the SETTING marker, so it reaches the six sensors that have calibration settings
                // and none of the other 67 wired products -- which is what the vendor does: it draws
                // Indstillinger only where there is something in it, unlike the two terminal grids above,
                // which it shows ALWAYS, empty or not (US-012/T070).
                Widget("indstillinger", DialogWidgetKind.SettingsGrid, Setting));

        /// <summary>
        /// The wired identity fields in their MEASURED order — stated once, because two presets show them: the
        /// ordinary wired dialog and the one that appends the end-user-report checkbox to the same group.
        /// </summary>
        private static DialogFieldModel[] WiredIdentity { get; } =
            [Navn(), Placering, Note, Kabeltype, Kabelnummer, Identifikationskode, Lysgruppe];

        /// <summary>
        /// Wired data-line products — 73 of the 100 catalog entries, and the shape 74 of the 100 dialogs have.
        /// <para>Measured: one captioned group <i>Produkt egenskaber</i> laid out in two columns, reading
        /// Navn · Placering / Note / Kabeltype · Kabelnummer / Identifikationskode · Lysgruppe, followed by the
        /// terminal grids in their own captioned blocks.</para>
        /// </summary>
        public static ProductDialogModel Dataline { get; } = Dialog(
            Group("identitet", "Produkt egenskaber", 2, WiredIdentity),
            WiredTerminals);

        /// <summary>
        /// The catalog products MEASURED to show the <i>Inkluder produktet i slutbruger rapport</i> checkbox.
        ///
        /// <para><b>An evidence list, deliberately — not a derived rule.</b> Three candidate rules were tried
        /// against the recorded vendor dialogs and each was falsified by a capture (T098/T099):</para>
        /// <list type="bullet">
        /// <item><i>unlocked products get it</i> — falsified by product 042, the Diode, which is unlocked in the
        /// project (its <c>.def</c> misspells the attribute as <c>loced</c>) and shows no checkbox.</item>
        /// <item><i>the seven products whose <c>.def</c> writes <c>locked="no"</c></i> — falsified by products
        /// 065/066/067/099, all four of them <c>locked="no"</c> and all four without one.</item>
        /// <item><i>…and whose <c>.def</c> defaults <c>enduser_report</c> to yes</i> — falsified by product 067,
        /// <c>Brugerdefineret indgangsprodukt med logning</c>, which satisfies both conjuncts and shows none.</item>
        /// </list>
        /// <para>067 is the decisive one: its <c>.def</c> root is byte-for-byte the same as 064's but for the
        /// identifier, the name and two logging children, so nothing at the product root can separate them. One
        /// product in a hundred has this control, and what the vendor keys it on is not visible from the outside.
        /// An invented predicate would be a guess dressed as a rule, and it would decide 99 products the sweep
        /// has measured; a list decides only the one it has evidence for. The remaining question — what the
        /// vendor actually keys on, and whether the box even writes <c>enduser_report</c> — is T099b, a live
        /// tick-and-save byte experiment.</para>
        /// </summary>
        private static readonly ImmutableHashSet<string> MeasuredEndUserReport =
            ImmutableHashSet.Create("_0x2701");

        /// <summary>
        /// The wired dialog of the product(s) in <see cref="MeasuredEndUserReport"/> — <see cref="Dataline"/>
        /// plus the end-user-report checkbox, which the vendor draws full width at the bottom of
        /// <i>Produkt egenskaber</i> (measured on product 064, 2026-08-12).
        /// <para><b>Why a second preset rather than a flag on the field.</b> Preset selection is where a
        /// dialog's SHAPE is already chosen, and this is a shape difference; a per-field visibility flag would
        /// have added a second conditional vocabulary to the model for one field (D12).</para>
        /// <para>The identity group is re-composed because the checkbox is appended to it — but from the SAME
        /// <see cref="WiredIdentity"/> field list and the same terminal group instance, which is what keeps the
        /// two presets from drifting below the fold.</para>
        /// </summary>
        public static ProductDialogModel DatalineEndUserReport { get; } = Dialog(
            Group("identitet", "Produkt egenskaber", 2, [.. WiredIdentity, SlutbrugerRapport]),
            WiredTerminals);

        /// <summary>
        /// LK IHC Wireless (airlink) products — 24 catalog entries, all sharing ONE field set with no exceptions
        /// (measured across all 24, T008).
        /// <para>It is exactly <see cref="Dataline"/>'s identity group minus the two cabling fields: a wireless
        /// product has no cable to describe, but it does keep <c>Lysgruppe</c>. That is the seam the two presets
        /// are factored along, and it is why every field here is the SAME fragment instance the dataline preset
        /// uses.</para>
        /// <para><c>serialnumber</c> is deliberately absent: the vendor's wireless dialog does not surface it in
        /// any form (T008, checked three ways). The attribute lives in the file and is not user-editable here.</para>
        /// <para>The <see cref="DialogWidgetKind.AdvancedDimmerButton"/> slot is declared but PRESENCE-GATED on
        /// <c>dimmer_settings</c>, so it applies to the wireless dimmers and to none of the other airlink products
        /// — the field set above really is shared by all 24. The BUTTON itself is parity: the vendor draws one
        /// captioned <i>Avanceret</i> on the dimmer's bottom row (measured on product 080, T114 — correcting an
        /// earlier note here that claimed no vendor capture carried that caption). What remains a registered
        /// DIFFERENCE is what pressing it does: the vendor expands its advanced settings in place inside the
        /// product dialog (a group box <i>Avancerede Dimmer egenskaber</i>, with a <i>Normal</i> button that
        /// collapses it again) where OpenVisual opens a sub-dialog. Declared anyway because the alternative is
        /// worse — routing the family through
        /// the generic dialog (T030) with the slot absent would silently delete a reachable capability, which is
        /// not a thing a routing task is allowed to do (T029's lesson). Reshaping it to the vendor's in-place form
        /// is a separate, deliberate piece of work.</para>
        /// </summary>
        /// <para>The <i>Persienne egenskaber</i> group reaches the two jalousi products and no others
        /// (measured on product 085, T119), gated on <c>shutter_settings</c> — DECLARED, even though both its
        /// fields bind to shutter descendants and would drop themselves. Leaving them to drop renders the same
        /// dialog but tells the catalog-wide descriptor gate nothing: that gate reports an unresolved preset
        /// field as a defect, which is how a mistyped tag is caught, and self-gating fields would make every
        /// typo look intentional. So the identity group above really is shared by all 24.</para>
        public static ProductDialogModel Airlink { get; } = Dialog(
            Group("identitet", "Produkt egenskaber", 2,
                Navn(), Placering, Note, Identifikationskode, Lysgruppe),
            GroupPresentWhen("persienne", "Persienne egenskaber", 2, Carrying("shutter_settings"),
                VandringstidOp, VandringstidNed),
            Group("avanceret", null, 1,
                Widget("avanceret", DialogWidgetKind.AdvancedDimmerButton, Carrying("dimmer_settings"))));

        /// <summary>
        /// The RS485 SMS modem — the largest dialog in the catalog, at 39 fields.
        /// <para>Measured 2026-08-11: FOUR captioned groups. <i>Modem egenskaber</i> is one column and orders its
        /// fields <b>Navn · Note · Placering · Identifikationskode</b> — note that <c>Note</c> comes BEFORE
        /// <c>Placering</c> here, the opposite of every other family, which is why this group is composed
        /// explicitly rather than reusing the wired ordering.</para>
        /// <para>The 30 telephone slots are one <see cref="DialogRepeatModel"/> over the product's own
        /// <c>sms_modem_phonenumber</c> children, laid out in three columns as the original does — and
        /// reading DOWN each column: 1–10, 11–20, 21–30 (measured from the composite, T035). That is the
        /// opposite of the S0 device's two-column group, which reads across, so the direction is declared
        /// here rather than assumed by the renderer.</para>
        /// </summary>
        /// <para>It is also the ONE family the original titles <c>"&lt;name&gt; Egenskaber"</c> rather than with the
        /// bare product name (measured across all 100 products) — declared here, beside the rest of its shape.</para>
        public static ProductDialogModel Rs485SmsModem { get; } = Dialog(
            Group("identitet", "Modem egenskaber", 1,
                Navn(readOnly: true), Note, Placering, Identifikationskode),
            Group("kabling", "Kabling", 1, ModemWires()),
            Group("indstillinger", "Indstillinger", 1, Pinkode),
            GroupReadingDown("telefonnumre", "Telefon numre", 3, Telefonnumre)) with
        {
            TitleSuffix = " Egenskaber",
        };

        /// <summary>
        /// The RS485 LED dimmer — the SMALLEST dialog in the catalog, at three fields (measured 2026-08-11:
        /// 1034x369 px, one group box, Navn · Placering · Note).
        /// <para><b>No advanced-settings slot, and no channel selector</b> — per D26 (T007, owner-ruled). The
        /// vendor's dialog exposes neither, so the product's two <c>rs485_led_dimmer_channel</c> containers and
        /// their <c>dimmer_settings</c> stay unreachable from here, exactly as today. That gap is registered as a
        /// difference with a pin rather than filled with a surface the original does not have.</para>
        /// <para>No cabling group, no <c>Identifikationskode</c>, no <c>Lysgruppe</c>: the family declares none of
        /// them (which is also why committing its dialog used to throw — T012).</para>
        /// </summary>
        public static ProductDialogModel Rs485LedDimmer { get; } = Dialog(
            Group("identitet", "Produkt egenskaber", 2,
                Navn(), Placering, Note));

        /// <summary>
        /// The S0 metering device — seven fields in two columns (measured 2026-08-12).
        /// <para>Reading order is row-major: <b>Navn · ledningsfarve S0- / Identifikationskode · ledningsfarve S0+
        /// / Placering · Antal pulser pr 1 kW / Note</b>.</para>
        /// <para><b>Its ten metering resources do not appear at all</b> (T006). The device carries kWh and W
        /// resources in the file; the vendor's dialog is documentation, two wire colours and one pulse constant,
        /// and nothing else. A preset that listed the resources would invent a surface the original lacks.</para>
        /// </summary>
        public static ProductDialogModel S0Device { get; } = Dialog(
            Group("identitet", "Produkt egenskaber", 2,
                Navn(), LedningsfarveS0Minus,
                Identifikationskode, LedningsfarveS0Plus,
                Placering, AntalPulser,
                Note));

        /// <summary>
        /// THE preset lookup. Every path that produces a <see cref="ProductDefinition"/> — the five named builder
        /// factories, the open-world <c>Create(rootTag, …)</c>, and <c>CatalogReader</c> reading a <c>.def</c> —
        /// resolves its dialog through this one function, so a definition's shape never depends on how it was
        /// obtained.
        /// <para>Keyed on the device-root TAG rather than on <see cref="ProductFamily"/>: the tag is what every
        /// construction path already has in hand, and it is what a <c>.def</c> carries. An unknown tag yields
        /// <see cref="ProductDialogModel.Empty"/> — the open-world case, which the composer turns into the minimal
        /// fallback rather than an error (a product family the SDK has never seen must still open a dialog).</para>
        /// <para>Returns SHARED instances. Two definitions of the same family carry the same model object, which is
        /// what lets a caller compare them by reference as well as by value.</para>
        /// <para><paramref name="productIdentifier"/> selects <see cref="DatalineEndUserReport"/> for the one
        /// product measured to carry the end-user-report checkbox (see <see cref="MeasuredEndUserReport"/>).
        /// It is the PLACED element's own <c>product_identifier</c>, which means both the read side and the
        /// write-back can compute the same answer from the same datum — no caller has to be trusted with a
        /// flag, and omitting it yields the shape 99 of the 100 products have.</para>
        /// </summary>
        public static ProductDialogModel ForRootTag(string? rootTag, string? productIdentifier = null) => rootTag switch
        {
            "product_dataline" => productIdentifier is not null && MeasuredEndUserReport.Contains(productIdentifier)
                ? DatalineEndUserReport
                : Dataline,
            "product_airlink" => Airlink,
            "product_rs485_sms_modem" => Rs485SmsModem,
            "product_rs485_led_dimmer" => Rs485LedDimmer,
            "s0_device" => S0Device,
            _ => ProductDialogModel.Empty,
        };
    }
}
