#nullable enable

namespace Ihc.Vis.Products
{
    /// <summary>
    /// The shared building blocks the family presets are composed from — the DRY seam of the dialog metadata.
    /// <para>The five families overlap heavily: all declare <c>name</c>/<c>note</c>/<c>position</c>/
    /// <c>documentation_tag</c>, two declare <c>power_group</c>, and two declare a cable group differing only in
    /// which wires it holds. Hand-listing five presets would retype the same field five times and the same Danish
    /// caption six times, and the sixth copy is the one that ends up saying something slightly different.</para>
    /// <para>The rule, borrowed from <c>CatalogGrammarPresets</c> which solves the same problem for grammars:
    /// <b>a fragment is a VALUE when it never varies and a FUNCTION when it does</b>. Every model type is an
    /// immutable record, so a value fragment is a safe shared singleton — and sharing the instance is what makes
    /// "this caption is authored once" checkable rather than merely intended.</para>
    /// <para><b>Share by default; diverge with a stated reason.</b></para>
    /// </summary>
    internal static class ProductDialogFragments
    {
        // ── Level 1a: value fragments. Each owns its Danish caption, so a caption is authored ONCE. ──────

        /// <summary>Free text describing where in the room the device sits — never a locality (T014).</summary>
        public static readonly DialogFieldModel Placering =
            new("placering", "Placering", DialogControlKind.ComboSuggest, new DialogBinding.RootAttribute("position"));

        /// <summary>
        /// The free-text note — the ONE field whose stored value can be a vendor localisation KEY instead of
        /// prose, so it is the one that declares <see cref="DialogFieldModel.HidesUnresolvedResourceKey"/>.
        /// <para>Exactly one catalog product ships such a value: the S0 device's <c>.def</c> says
        /// <c>note="PRODUCT_2315_NOTE"</c>, and nothing in the IHC Visual install resolves that key — so the
        /// original's Note box is empty where OpenVisual printed the token at the installer (T131). Declared
        /// HERE, on the shared fragment, rather than tested for by attribute name in the composer: every family
        /// shows its note through this one instance, so one statement reaches all six presets and the open-world
        /// fallback, and no other field can acquire the rule by being bound to something merely called
        /// <c>note</c>. A documentation tag like <c>A_1</c> has the same SHAPE and is legitimate text, which is
        /// why the claim belongs to the field rather than to the shape alone.</para>
        /// </summary>
        // ColumnSpan 2: the vendor gives Note the WHOLE row in both the wired and the wireless dialog, so the
        // fields after it pair up beneath rather than beside it (measured on products 003/004/069, T038). The
        // span is clamped to the group's width, so it is inert in the modem's one-column identity block, which
        // shares this same fragment.
        public static readonly DialogFieldModel Note =
            new("note", "Note", DialogControlKind.ComboSuggest, new DialogBinding.RootAttribute("note"))
            { ColumnSpan = 2, HidesUnresolvedResourceKey = true };

        public static readonly DialogFieldModel Identifikationskode =
            new("idkode", "Identifikationskode", DialogControlKind.ComboSuggest,
                new DialogBinding.RootAttribute("documentation_tag"));

        // ComboSuggest, corrected T037 from the recorded oracle for _0x2101: the vendor renders SIX of the
        // wired product's seven fields as combos -- Kabeltype, Kabelnummer, Identifikationskode, Lysgruppe,
        // Placering, Note -- and only the (read-only) Navn as a plain edit. These two were Text because the
        // preset was written before the per-product sweep could contradict it.
        //
        // Lysgruppe and Identifikationskode carry ZERO items on a fresh project and are still combos: the
        // kind is the affordance, and an empty suggestion list is a combo with nothing in it yet, which is
        // exactly what a project-sourced list (D07) yields on a new project.
        public static readonly DialogFieldModel Lysgruppe =
            new("lysgruppe", "Lysgruppe", DialogControlKind.ComboSuggest,
                new DialogBinding.RootAttribute("power_group"));

        public static readonly DialogFieldModel Kabeltype =
            new("kabeltype", "Kabeltype", DialogControlKind.ComboSuggest, new DialogBinding.RootAttribute("cabletype"));

        public static readonly DialogFieldModel Kabelnummer =
            new("kabelnummer", "Kabelnummer", DialogControlKind.ComboSuggest,
                new DialogBinding.RootAttribute("cablenumber"));

        /// <summary>
        /// Whether the product appears in the end-user report — the catalog's only checkbox.
        /// <para>Offered on the one product measured to show it (see
        /// <see cref="ProductDialogPresets.DatalineEndUserReport"/>), full width, last in the identity group:
        /// the vendor draws it beneath <c>Identifikationskode</c>/<c>Lysgruppe</c> across both columns
        /// (measured on product 064, 2026-08-12).</para>
        /// </summary>
        public static readonly DialogFieldModel SlutbrugerRapport =
            new("slutbrugerrapport", "Inkluder produktet i slutbruger rapport", DialogControlKind.Checkbox,
                new DialogBinding.RootAttribute("enduser_report"))
            { ColumnSpan = 2 };

        // Family-specific values, declared beside the shared ones because they are values too — a fragment need
        // not be shared to be a fragment.

        /// <summary>
        /// The SIM PIN. <see cref="DialogControlKind.Number"/> with NO rule: its range is derived at compose time
        /// from the placed element's own <c>minimum</c>/<c>maximum</c> (the catalog seeds 0–9999), never declared
        /// here — a preset that hardcoded the bounds would go stale the moment a catalog changed them.
        /// </summary>
        public static readonly DialogFieldModel Pinkode =
            new("pinkode", "Pin Kode", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("sms_modem_pincode"));

        /// <summary>
        /// The modem's telephone slots — ONE repeat, not thirty declarations. Expands over every
        /// <c>sms_modem_phonenumber</c> descendant in <c>address</c> order, so the count follows the product
        /// rather than a constant (F-52).
        /// </summary>
        public static readonly DialogRepeatModel Telefonnumre =
            new("nummer", "Nummer {0}", "sms_modem_phonenumber", "address", "phonenumber",
                DialogControlKind.Text, DialogValueRule.PhoneNumber);

        /// <summary>
        /// The jalousi products' two travel times, in seconds.
        /// <para>Bound to their own <c>shutter_setting_*</c> descendants, which is also what GATES them: a
        /// wireless product without a shutter has neither element, so neither field resolves and the composer
        /// drops the whole group. That is presence gating expressed in the binding rather than in a second
        /// flag — the same reason the 24-strong wireless family can share one preset (T119).</para>
        /// <para><see cref="DialogControlKind.Number"/> with no rule: 0–240 is derived at compose time from
        /// each element's own <c>minimum</c>/<c>maximum</c>, exactly as the SIM PIN's range is.</para>
        /// <para>The captions name the DIRECTION OF TRAVEL, not the element: <i>fra bund til top</i> is the
        /// <c>_up</c> element and <i>fra top til bund</i> is <c>_down</c>. Getting that pair backwards would
        /// swap two values that are equal by default (both 120 s) and so look right until someone changes
        /// one — which is why the mapping is stated here and asserted per element in the tests.</para>
        /// </summary>
        public static readonly DialogFieldModel VandringstidOp =
            new("vandringop", "Vandringstid fra bund til top [sekunder]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("shutter_setting_travel_time_up"));

        /// <inheritdoc cref="VandringstidOp"/>
        public static readonly DialogFieldModel VandringstidNed =
            new("vandringned", "Vandringstid fra top til bund [sekunder]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("shutter_setting_travel_time_down"));

        /// <summary>
        /// The wireless dimmer's six advanced settings, bound to their own <c>dimmer_setting_*</c> descendants.
        /// <para>Ordinary fields of the product dialog, exactly as the vendor presents them: an <i>Avanceret</i>
        /// disclosure expands them in place. They were a separate modal window here, which is a shape divergence
        /// rather than a capability one — the same six values, reached differently.</para>
        /// <para>The descendants are also what GATES them, as the shutter times are gated: a wireless product
        /// with no dimmer carries none of these elements, so no field resolves and the composer drops the whole
        /// group.</para>
        /// </summary>
        public static readonly DialogFieldModel LysstyrkeOp =
            new("fadeop", "Optoningstid [ms]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("dimmer_setting_fade_rate_up"));

        /// <inheritdoc cref="LysstyrkeOp"/>
        public static readonly DialogFieldModel LysstyrkeNed =
            new("fadened", "Nedtoningstid [ms]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("dimmer_setting_fade_rate_down"));

        /// <summary>
        /// The manual ramp. STORED IN MILLISECONDS and captioned in seconds, which is why it declares a divisor —
        /// see <see cref="DialogFieldModel.DisplayDivisor"/>. Without it the box shows 5000 under a seconds label
        /// and a typed 7 commits an out-of-range 7 ms.
        /// </summary>
        /// <inheritdoc cref="LysstyrkeOp"/>
        public static readonly DialogFieldModel Manuel =
            new("manuel", "Manuel op-/nedtoningstid [s]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("dimmer_setting_dimming_rate")) { DisplayDivisor = 1000 };

        /// <inheritdoc cref="LysstyrkeOp"/>
        public static readonly DialogFieldModel MinimumLysstyrke =
            new("minimum", "Minimum lysstyrke [%]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("dimmer_setting_minimum_value"));

        /// <inheritdoc cref="LysstyrkeOp"/>
        public static readonly DialogFieldModel MaksimumLysstyrke =
            new("maksimum", "Maksimum lysstyrke [%]", DialogControlKind.Number,
                new DialogBinding.DescendantAttribute("dimmer_setting_maximum_value"));

        /// <summary>
        /// The load type. A CLOSED combo, not free text: the attribute is enumerated in the DTD
        /// (<c>auto | rc | rl</c>), so the list is the declaration's own and anything else is unwritable.
        /// </summary>
        /// <inheritdoc cref="LysstyrkeOp"/>
        public static readonly DialogFieldModel Belastningstype =
            new("belastning", "Belastningstype", DialogControlKind.ComboFixed,
                new DialogBinding.DescendantAttribute("dimmer_setting_load_mode"));

        /// <summary>
        /// The S0 meter's pulse constant. Captions are the vendor's, including the spacing of "pr 1 kW".
        /// </summary>
        public static readonly DialogFieldModel AntalPulser =
            new("pulser", "Antal pulser pr 1 kW", DialogControlKind.Number, new DialogBinding.RootAttribute("ticks"));

        /// <summary>
        /// The S0 meter's two wire colours. The captions are <b>lower-case initial</b> — <c>ledningsfarve S0-</c>,
        /// not <c>Ledningsfarve S0-</c> — because that is what the original shows (measured 2026-08-12). The vendor
        /// is inconsistent with its own modem dialog here; caption text is data, so the inconsistency is reproduced
        /// rather than tidied.
        /// </summary>
        public static readonly DialogFieldModel LedningsfarveS0Minus =
            new("lfs0min", "ledningsfarve S0-", DialogControlKind.ComboSuggest,
                new DialogBinding.RootAttribute("cable_colour_minus"));

        /// <inheritdoc cref="LedningsfarveS0Minus"/>
        public static readonly DialogFieldModel LedningsfarveS0Plus =
            new("lfs0plus", "ledningsfarve S0+", DialogControlKind.ComboSuggest,
                new DialogBinding.RootAttribute("cable_colour_plus"));

        // ── Level 1b: function fragments — parameterized by exactly what differs, and nothing more. ──────

        /// <summary>
        /// The product's name. Read-only on the families whose dialog greys it (measured: every family's
        /// <c>Navn</c> is disabled on a freshly inserted catalog product), editable where a preset says so.
        /// </summary>
        public static DialogFieldModel Navn(bool readOnly = false) =>
            new("navn", "Navn", DialogControlKind.Text, new DialogBinding.RootAttribute("name"), ReadOnly: readOnly)
            { ReadOnlyWhenLocked = true };

        /// <summary>
        /// One cable-colour field of the modem's <i>Kabling</i> group.
        /// <para><b>Captions are the ones the DIALOG shows, measured 2026-08-11, not the ones
        /// <c>InstallationReportBuilder</c> prints.</b> D11 says to inherit the four verbatim from the report
        /// builder rather than author them a third time, but that decision assumed the two agreed. They do not:
        /// the report prints <c>Ledningsfarve RS485Minus</c>/<c>RS485Plus</c> while the dialog shows
        /// <c>Ledningsfarve RS485 minus</c>/<c>RS485 plus</c> — a space, and lower case. D14 makes measured vendor
        /// behaviour the governing default, and reproducing the report's spelling here would put a caption on
        /// screen that the original never shows. See the backlog Discoveries for the conflict.</para>
        /// </summary>
        public static DialogFieldModel Ledningsfarve(string id, string caption, string attribute) =>
            new(id, caption, DialogControlKind.ComboSuggest, new DialogBinding.RootAttribute(attribute));

        /// <summary>The modem's four wire fields, in the order the dialog shows them, with the captions it shows.
        /// Named here rather than inline in the preset so the caption ruling above has one home.</summary>
        public static DialogFieldModel[] ModemWires() =>
        [
            Ledningsfarve("lf0v", "Ledningsfarve 0V", "cablecolour_0V"),
            Ledningsfarve("lf24v", "Ledningsfarve 24V", "cablecolour_24V"),
            Ledningsfarve("lfmin", "Ledningsfarve RS485 minus", "cablecolour_RS485Minus"),
            Ledningsfarve("lfplus", "Ledningsfarve RS485 plus", "cablecolour_RS485Plus"),
        ];

        // ── Level 2: the two node constructors, matching the two node kinds. ─────────────────────────────

        /// <summary>A group of parts. A null caption is an uncaptioned block — no group box — but it still carries
        /// an id, because every automation id inside it is derived from that id.</summary>
        public static DialogGroupModel Group(string id, string? caption, int columns, params DialogPartModel[] parts) =>
            new(id, caption, columns, [.. parts]);

        /// <summary>A group whose columns read DOWN rather than across — see
        /// <see cref="DialogGroupModel.ColumnMajor"/>. A named constructor rather than a bool parameter on
        /// <see cref="Group"/>, so a preset states the direction it measured at the call site instead of
        /// carrying a bare <c>true</c> whose meaning has to be looked up.</summary>
        public static DialogGroupModel GroupReadingDown(
            string id, string? caption, int columns, params DialogPartModel[] parts) =>
            new(id, caption, columns, [.. parts]) { ColumnMajor = true };

        /// <summary>A group only the family members satisfying <paramref name="presence"/> are offered — see
        /// <see cref="DialogGroupModel.Presence"/>. A named constructor rather than an optional parameter on
        /// <see cref="Group"/>, so the gate is visible at the call site next to the fields it gates.</summary>
        public static DialogGroupModel GroupPresentWhen(
            string id, string? caption, int columns, DialogPresence presence, params DialogPartModel[] parts) =>
            new(id, caption, columns, [.. parts]) { Presence = presence };

        /// <summary>A slot for one of the hand-written composite widgets, rendered whenever
        /// <paramref name="presence"/> is satisfied — unconditionally when the preset states no rule.</summary>
        public static DialogWidgetModel Widget(string id, DialogWidgetKind kind, DialogPresence? presence = null) =>
            new(id, kind) { Presence = presence ?? DialogPresence.Always };

        // ── Presence vocabulary: the two shapes the catalog needs, named for the call sites. ─────────────

        /// <summary>Present only on the family members carrying a descendant with <paramref name="tag"/>.</summary>
        public static DialogPresence Carrying(string tag) => new DialogPresence.DescendantTag(tag);

        /// <summary>
        /// A configurable SETTING: any resource the catalog marked <c>setting="yes"</c>, whatever its resource
        /// type — the six sensors that have them use <c>resource_temperature</c>, <c>resource_humidity</c> and
        /// <c>resource_light</c>, so no tag names the set (T070).
        /// <para>Stated once, and used two ways: it gates the <i>Indstillinger</i> slot, and the same rule picks
        /// the rows that go in it (<c>ProductView.SettingElements</c>). Two literals would let "what counts as a
        /// setting" drift between the grid's presence and its contents. The marker itself is the model-layer
        /// <see cref="Schema.ProductRows.SettingAttribute"/>, not a copy: the tree hides exactly the rows this
        /// grid shows.</para>
        /// </summary>
        public static readonly DialogPresence.DescendantMarked Setting =
            new(Schema.ProductRows.SettingAttribute, Schema.ProductRows.SettingValue);

        /// <summary>Assembles a model from its groups. Named <c>Dialog</c>, not <c>Model</c>: the latter collides
        /// with the <c>Ihc.Vis.Model</c> namespace at every call site that imports these statically.</summary>
        public static ProductDialogModel Dialog(params DialogGroupModel[] groups) => new([.. groups]);
    }
}
