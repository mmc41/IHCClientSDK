#nullable enable
using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The PROJECT-FINDINGS section of the catalogue: every condition the SDK can report about a <c>.vis</c>
    /// project file, one compiled declaration per row.
    /// <para>
    /// These are ordinary C# declarations, maintained like any other code: a new row is a new declaration, a
    /// re-classification is an edit, and review happens in the diff of this file. Nothing parses a markdown table
    /// to produce them, which is why a malformed entry cannot exist and why argument arity and type are the
    /// compiler's problem rather than a gate's.
    /// </para>
    /// <para>
    /// Each declaration carries its PREDICATE as its doc-comment — the condition, and, once the row is
    /// implemented, the subject it walks and the exclusions it makes. Several rows arrived from a self-declared
    /// unconfirmed draft naming a condition without a decidable predicate, so writing that predicate down is the
    /// first half of implementing a row; a test written before it would freeze whatever the implementer
    /// improvised.
    /// </para>
    /// <para>
    /// Three entries carry <see cref="ProblemCodeStatus.RuledOut"/>. They are not withheld rows awaiting
    /// evidence: each was investigated and is positively not a finding OF ITS OWN — two because the condition
    /// does not exist, and one (<c>load-truncated</c>) because it is not separately decidable and is already
    /// reported under another id. The entry exists so that deleting the row does not lose the finding that it
    /// is not a finding.
    /// </para>
    /// </summary>
    internal static partial class ProblemCatalogEntries
    {
        /// <summary>
        /// Two LED-dimmer channels claiming one channel id: the controller cannot tell them apart. The one
        /// ERROR of this set, as the catalogue rates it — the consequence holds whatever the author intended.
        /// PREDICATE: two <c>rs485_led_dimmer_channel</c> elements carrying the same non-blank
        /// <c>channel_id</c>, compared across the WHOLE project: a collision between two dimmers is the same
        /// defect as one inside a single dimmer.
        /// SUBJECT: every channel carrying an id. EXCLUSION: an unassigned id — blank or the null token
        /// <c>_0x0</c> — is <c>addr-dimmer-channel-unassigned</c>'s finding, so two freshly placed channels are
        /// not reported as colliding. That exclusion is not cosmetic: without it every inserted catalog dimmer
        /// produced an ERROR, which is how the gate found the null-token spelling.
        /// LOCATION: the second holder in document order, with the first as a related location — the reader has
        /// to see both ends of a collision to decide which one to change.
        /// ARGUMENTS: both channel names and the id they share.
        /// </summary>
        private static ProblemCatalogEntry AddrDimmerChannelDuplicate =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-dimmer-channel-duplicate"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("channel", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("other", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.AttributeValue),
                ]),
                "Kanalerne '{channel}' og '{other}' deler kanal-id {id}.")
            {
                Diagnostic = "Two rs485_led_dimmer_channel elements carry the same channel_id.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An RS485 LED-dimmer channel with no channel id: nothing can address it.
        /// PREDICATE: an <c>rs485_led_dimmer_channel</c> whose <c>channel_id</c> is absent, blank, or the NULL
        /// TOKEN <c>_0x0</c> — measured: the shipped catalog's dimmer template carries an empty <c>channel_id</c>
        /// that reads back as <c>_0x0</c> once placed, exactly as an unaddressed terminal's address does.
        /// NOT the sibling <c>channel</c> attribute, which carries the physical channel index (<c>_0x00</c>,
        /// <c>_0x01</c>) and is assigned by the catalog rather than by the installer.
        /// SUBJECT: every dimmer channel. EXCLUSIONS: none — a channel assigned during commissioning is the
        /// legitimate reading this Warning exists for.
        /// LOCATION: the channel. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry AddrDimmerChannelUnassigned =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-dimmer-channel-unassigned"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("channel", ProblemArgumentType.AuthoredName),
                ]),
                "Kanalen '{channel}' har ingen kanal-id.")
            {
                Diagnostic = "An rs485_led_dimmer_channel carries no channel_id.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A modem with no phone number anywhere: the alarm and notification path is dead.
        /// PREDICATE: a product holding <c>sms_modem_phonenumber</c> slots, none of which carries a number.
        /// PER MODEM, not per slot, and that is the predicate's substance: the modem declares THIRTY slots and
        /// an installer fills a few, so a per-slot reading would report twenty-seven times per modem and state
        /// this row's consequence falsely every time — the path is dead only when NO slot carries a number.
        /// SUBJECT: products holding phone slots. EXCLUSIONS: none.
        /// LOCATION: the modem, with its slots as related locations. ARGUMENTS: its name and how many slots it
        /// offers, so the reader knows where to put the number.
        /// </summary>
        private static ProblemCatalogEntry AddrModemPhonenumberBlank =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-modem-phonenumber-blank"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("modem", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("slots", ProblemArgumentType.Integer),
                ]),
                "Modemet '{modem}' har intet telefonnummer i nogen af sine {slots} pladser.")
            {
                Diagnostic = "No sms_modem_phonenumber slot of the modem carries a number.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A telephone slot holding a number the modem cannot dial.
        /// PREDICATE: a non-empty <c>sms_modem_phonenumber/@phonenumber</c> that is not 3–20 non-whitespace
        /// characters beginning with <c>'+'</c> and a digit. An EMPTY slot is NOT this row — the modem declares
        /// thirty and an installer fills a few; "no slot at all carries a number" is
        /// <c>addr-modem-phonenumber-blank</c>, a different question about a different subject.
        /// SUBJECT: one slot. LOCATION: the slot. ARGUMENTS: the offending value.
        /// DECLARATIVE, not a traversal: this is the repository's first registered <c>Constrain</c> row, so the
        /// same object answers the whole-project finding and the dialog's bounds question. The predicate is not
        /// restated in the rule body — it delegates to <c>DialogValueRule.PhoneNumber</c>, which the dialog and
        /// the commit path already consult, so the three cannot disagree about what a valid number is.
        /// WHAT THE TWO STRICTNESSES ARE, and why only two of the four bounds are thresholds: the whitespace ban
        /// and the country-code requirement are not numbers, so they carry no <c>DeclaredThreshold</c> — a
        /// threshold's value is a <c>double</c>. Both are recorded in the length thresholds' evidence below.
        /// KNOWN LIMITATION (D18(ii)): <c>Describe()</c> can advertise the lengths and the whitespace ban but has
        /// no member for the <c>'+'</c>-prefix that <c>Check</c> enforces, so a caller binding the metadata ALONE
        /// under-advertises this row. Latent today — no shipped dialog binds rule-declared metadata — and
        /// deliberately not fixed by widening the shipped <c>FieldConstraintMetadata</c>.
        /// </summary>
        private static ProblemCatalogEntry AddrModemPhonenumberMalformed =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-modem-phonenumber-malformed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject | RuleFaces.DialogMetadata,
                new RuleTarget("sms_modem_phonenumber", "phonenumber"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                ]),
                "Telefonnummeret '{value}' skal være på 3-20 tegn uden mellemrum og begynde med en landekode, "
                + "f.eks. +45.")
            {
                Diagnostic =
                    "phonenumber='{value}' is not 3-20 non-whitespace characters beginning with +<digit>.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "MinPhoneNumberLength",
                        3,
                        ThresholdConfidence.VendorDocumented,
                        "LK IHC Visual 03.04.72.03, measured 2026-08-12: it refuses a 2-character number with "
                        + "\"Ugyldigt telefonnummer på talværdi 1 / skal være mere end 3 cifre\" and accepts a "
                        + "3-character one. Mirrors DialogValueRule.PhoneNumber.MinLength, which is the operative "
                        + "predicate; the two are pinned equal by a test."),
                    new DeclaredThreshold(
                        "MaxPhoneNumberLength",
                        20,
                        ThresholdConfidence.Authored,
                        "An OpenVisual strictness, registered in applications/ihc_openvisual/docs/product.md: the "
                        + "same measurement shows the vendor ACCEPTS a 60-digit number. The whitespace ban and "
                        + "the country-code requirement are two further OpenVisual strictnesses of the same "
                        + "predicate — the vendor silently strips spaces and accepts a number with no country "
                        + "code — and neither is a number, so neither takes a threshold of its own. "
                        + "CONSEQUENCE, recorded rather than discovered later: authentic vendor files carrying "
                        + "country-code-less numbers now warn. Severity is Warning, never blocking. Mirrors "
                        + "DialogValueRule.PhoneNumber.MaxLength under the same pin."),
                ]),
            };

        /// <summary>
        /// One module serving terminals in more localities than the declared maximum: tracing that module on
        /// site means walking between rooms.
        /// PREDICATE: for each module in use, the number of DISTINCT nearest-ancestor localities of its
        /// terminals exceeds the declared <c>MaxLocalitiesPerModule</c>.
        /// WHAT IS NOT DECIDABLE: the row says "many DISTANT localities". Distance is not in the file — there
        /// are no coordinates and no adjacency — so the predicate keeps the half that is: the COUNT.
        /// THRESHOLD: authored; two localities on one module is ordinary (adjacent rooms fed from one module),
        /// so "many" begins at three.
        /// SUBJECT: modules in use. EXCLUSION: a terminal whose address does not decode, as above.
        /// LOCATION: the module's first terminal, with the rest as related locations. ARGUMENTS: the line and
        /// how many localities it spans.
        /// </summary>
        private static ProblemCatalogEntry AddrModuleMixedLocality =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-module-mixed-locality"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("line", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("localities", ProblemArgumentType.Integer),
                ]),
                "Datalinje {line} betjener klemmer i {localities} lokaliteter.")
            {
                Diagnostic = "A data-line module serves terminals in more localities than the declared maximum.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no vendor source states how many localities one module may span.
                    new DeclaredThreshold(
                        "MaxLocalitiesPerModule",
                        2,
                        ThresholdConfidence.Authored,
                        "No vendor source states a limit. TWO localities on one module is ordinary — adjacent "
                        + "rooms are commonly fed from one module — so the row's own word 'many' is read as "
                        + "three or more. Measured: no committed fixture has a module spanning more than two. "
                        + "TODO: unconfirmed."),
                ]),
            };

        /// <summary>
        /// A data-line module carrying almost nothing while another module of the same direction is in use:
        /// the stray terminal is what a mis-addressed product looks like.
        /// PREDICATE: for each (direction, data line) in use, the number of addressed terminals is below the
        /// declared <c>MinimumUsedTerminals</c>, and at least one OTHER module of the same direction is in use.
        /// WHY NOT THE LITERAL CONDITION: "only partly used" is true of every module in every committed
        /// fixture — measured 4 of 4, 4 of 4 and 5 of 5 — so it describes how installations are wired rather
        /// than a defect. What the row is about is its own second sentence, the nearly-empty case, and that
        /// needs a number.
        /// THRESHOLD: authored (no vendor source states one); its rationale is on the declaration below.
        /// SUBJECT: modules in use. EXCLUSIONS: a direction with only one module in use (a small installation
        /// is not a mis-address), and a terminal whose address does not decode — that is
        /// <c>dataline-address-malformed</c>'s or <c>-range</c>'s finding, and counting it would put a phantom
        /// terminal on a line nobody addressed.
        /// LOCATION: the module's first terminal, with the rest as related locations. ARGUMENTS: the line, how
        /// many terminals are used, and the direction's capacity.
        /// </summary>
        private static ProblemCatalogEntry AddrModulePartial =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-module-partial"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("line", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("capacity", ProblemArgumentType.Integer),
                ]),
                "Datalinje {line} bruger kun {used} af {capacity} klemmer.")
            {
                Diagnostic = "A data-line module carries fewer terminals than the declared minimum while another module is in use.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no vendor source states how empty a module may be. Confirm against a
                    // real installation or a vendor source.
                    new DeclaredThreshold(
                        "MinimumUsedTerminals",
                        2,
                        ThresholdConfidence.Authored,
                        "No vendor source states a minimum fill. Reporting below TWO makes the row the "
                        + "nearly-empty case its own consequence names: a single terminal on a fresh module "
                        + "while another module of the same direction is in use is the shape a mis-addressed "
                        + "product takes. The literal 'partly used' condition was measured to hold for every "
                        + "module in every committed fixture. TODO: unconfirmed."),
                ]),
            };

        /// <summary>
        /// An S0 meter whose pulses-per-unit is missing or outside the declared range: readings cannot be
        /// scaled.
        /// PREDICATE: an <c>s0_device</c> whose <c>ticks</c> does not parse as an integer within the declared
        /// <c>MinimumTicks</c>..<c>MaximumTicks</c>, inclusive at both ends.
        /// WHY THE RANGE AND NOT ONLY ABSENCE (va-ana G6): the pulse count has no bounds anywhere in this
        /// codebase — <c>s0_device</c> declares neither <c>minimum</c> nor <c>maximum</c>, so the composer's
        /// derived range is empty and the commit check has nothing to enforce — while the vendor refuses
        /// anything outside 1..10000 with <i>"Antallet af pulser skal være mellem 1 og 10000"</i>. The bounds
        /// are therefore DECLARED here, as data the rule reads, exactly as T023's exemplar puts every other
        /// bound on the entry that owns it. A value of 0 or a blank fails the lower bound, which is why one row
        /// covers both the missing and the nonsensical case.
        /// RECLASSIFIED (⊘): the vendor's dialog refuses a blank on commit, so this state cannot be AUTHORED —
        /// it arrives by import or hand-editing, which is precisely what the whole-project face is for and what
        /// no commit-time check can ever see.
        /// SUBJECT: every S0 meter. LOCATION: the meter. ARGUMENTS: its name and both declared bounds.
        /// </summary>
        private static ProblemCatalogEntry AddrS0TicksMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-s0-ticks-missing"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("meter", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Number),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Number),
                ]),
                "Måleren '{meter}' mangler et antal pulser mellem {minimum} og {maximum}.")
            {
                Diagnostic = "An s0_device carries no ticks value within the declared range.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "MinimumTicks",
                        1,
                        ThresholdConfidence.VendorDocumented,
                        "IHC Visual refuses a pulse count outside this range on commit, with the message "
                        + "\"Antallet af pulser skal være mellem 1 og 10000\" (measured, va-ana G6). It is the "
                        + "tool's own bound, not an authored guess."),
                    new DeclaredThreshold(
                        "MaximumTicks",
                        10000,
                        ThresholdConfidence.VendorDocumented,
                        "The upper half of the same measured vendor message. Note the measurement's own caveat: "
                        + "the vendor dialog appears to enforce only the lower bound, so a file can carry a "
                        + "larger value — which is why this row reads it as a finding rather than as impossible."),
                ]),
            };

        /// <summary>
        /// A wired terminal with no data-line address — RULED OUT, because another row already reports it.
        /// MEASURED: <c>doc-address</c> fires on exactly this condition, over exactly these elements — every
        /// <c>dataline_input</c>/<c>dataline_output</c> inside a <c>product_dataline</c> whose
        /// <c>address_dataline</c> does not decode — and reaches the user as <i>Mangler Adresse</i> in the Fuld
        /// report appendix. The catalogue's own row admits the overlap in its consequence column ("also reported
        /// as <i>Mangler Adresse</i>").
        /// So this id would be a SECOND finding for one observation: two sentences, two categories, one repair.
        /// The id stays occupied and can never be handed to a different condition, which is what
        /// <see cref="ProblemCodeStatus.RuledOut"/> is for; the observation lives on the documentation row.
        /// IF an addressing-category view of the same fact is ever wanted, the honest change is to re-categorise
        /// the DOC row or to render one finding under two headings — not to emit two findings.
        /// PREDICATE: none. There is no rule for this id.
        /// </summary>
        private static ProblemCatalogEntry AddrUnassigned =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-unassigned"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "")
            {
                Diagnostic = "Ruled out: doc-address reports this condition on the same elements.",
                Evidence = EvidenceMark.Authored,
                Status = ProblemCodeStatus.RuledOut,
            };

        /// <summary>
        /// Two wireless elements claiming one device address: both react to the same command.
        /// PREDICATE: two pins in DIFFERENT products whose products carry the same COMMISSIONED serial number
        /// and whose <c>address_channel</c> is the same value. Channel tokens are compared by VALUE, because the
        /// catalog writes <c>_0x01</c> where a saved file writes <c>_0x1</c>.
        /// TWO EXCLUSIONS, both measured, and the row reports on every authentic file without either:
        /// (1) an UNCOMMISSIONED product — a placed wireless product carries the placeholder serial and the
        /// catalog's own <c>_0x01</c> on its first pin, so channel 1 is shared by three or four products in
        /// every fixture measured; a channel index means nothing until the device is bound, and the binding is
        /// the other row's finding. (2) two pins of ONE product — a shutter product's up/down pins deliberately
        /// reuse their first input's channel, which is the vendor's own encoding.
        /// So the identity that can collide is (serial, channel): the serial names the device, the channel a
        /// function within it.
        /// LOCATION: the second pin, with the first as a related location. ARGUMENTS: both pin names and the
        /// channel they share.
        /// </summary>
        private static ProblemCatalogEntry AddrWirelessChannelShared =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-wireless-channel-shared"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("pin", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("other", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("channel", ProblemArgumentType.AttributeValue),
                ]),
                "Klemmerne '{pin}' og '{other}' deler kanal {channel}.")
            {
                Diagnostic = "Two pins of different products with one commissioned serial share a channel address.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A wireless product with no serial number, or the placeholder one: the device cannot be bound.
        /// PREDICATE: a product declaring <c>serialnumber</c> whose value is absent, blank, or the null token.
        /// MEASURED: every wireless product in every committed vendor fixture is in exactly this state
        /// (<c>serialnumber="_0x0"</c>, three per project), because none of those projects is commissioned. That
        /// is the row's own legitimate reading — "entered during planning, commissioned later" — so the rule
        /// fires there and is a Warning, not an Error.
        /// SUBJECT: products carrying the attribute. EXCLUSIONS: none.
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry AddrWirelessNotCommissioned =>
            new ProblemCatalogEntry(
                new ProblemCode("addr-wireless-not-commissioned"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' har intet serienummer.")
            {
                Diagnostic = "A wireless product carries no serial number, or the placeholder null token.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An enumerated attribute holds a value outside its declared set.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry AttrEnumRange =>
            new ProblemCatalogEntry(
                new ProblemCode("attr-enum-range"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("allowed", ProblemArgumentType.AttributeValue),
                ]),
                "Ugyldig værdi '{value}' i attributten '{attribute}' på <{tag}>. Tilladte værdier: {allowed}.")
            {
                Diagnostic = "attribute {attribute}='{value}' on '{tag}' is not one of ({allowed})",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An attribute value carries text outside ISO-8859-1.
        /// REFUSES: Save · Export.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry AttrLatin1 =>
            new ProblemCatalogEntry(
                new ProblemCode("attr-latin1"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Tegn kan ikke gemmes i attributten '{attribute}' på <{tag}>.")
            {
                Diagnostic = "attribute '{attribute}' on '{tag}' has non-ISO-8859-1 text",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A <c>#REQUIRED</c> attribute is missing.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry AttrRequired =>
            new ProblemCatalogEntry(
                new ProblemCode("attr-required"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Den påkrævede attribut '{attribute}' mangler på <{tag}>.")
            {
                Diagnostic = "required attribute '{attribute}' missing on '{tag}'",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An attribute is declared neither in the element's inline-DTD block nor in the registry.
        /// REFUSES: Save · Export.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry AttrUndeclared =>
            new ProblemCatalogEntry(
                new ProblemCode("attr-undeclared"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Ukendt attribut '{attribute}' på <{tag}>.")
            {
                Diagnostic = "attribute '{attribute}' on '{tag}' is not declared in the element's inline-DTD block or the schema registry (serialization will fail)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A second modem: the controller binds one, so the extra entries can never be commissioned.
        /// PREDICATE: more than one modem product in the project.
        /// NO PROFILE NEEDED, unlike its three siblings: the limit is ONE, it is not a configurable capability, and
        /// the file either carries two modems or it does not.
        /// RECLASSIFIED (⊘), measured live: IHC Visual refuses the second insert with <i>"Modem er allerede indsat.
        /// Der kan kun indsættes et modem i projektet"</i> and OpenVisual with <i>"Et projekt må højst indeholde ét
        /// modem…"</i>, each leaving the tree unchanged. A file carrying two arrived by import or by hand — which is
        /// exactly why the file-level check earns its place beside the two refusals.
        /// THE SAME LIMIT, TWO FACES: the edit refusal is <c>edit-modem-limit</c>, whose Danish sentence the GUI
        /// hardcoded until the SDK owned the coded problem; this row is the whole-project half.
        /// </summary>
        private static ProblemCatalogEntry CapacityModemMultiple =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-modem-multiple"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                ]),
                "Projektet indeholder {used} modemer; controlleren binder ét.")
            {
                Diagnostic = "The project holds more than one modem product.",
                Evidence = EvidenceMark.Refused,
                RequiresControllerLimits = false,
            };

        /// <summary>
        /// RETIRED (D2). This one id covered three distinct conditions — too many INPUT data lines, too many
        /// OUTPUT data lines, and too many addressed terminals in one direction — under one Danish sentence,
        /// <i>"Projektet bruger {used} af {limit} moduler."</i>, which was false of the third: 200 terminals over a
        /// 128 limit read as "uses 200 of 128 modules". Its entry claimed "the arguments say which one and by how
        /// much", but the only arguments were <c>used</c> and <c>limit</c>, and neither names a quantity. The rule
        /// also looped per direction, so it could emit TWO findings against a declared
        /// <see cref="FindingShape.OneFinding"/>. It SPLIT into <c>capacity-input-modules</c>,
        /// <c>capacity-output-modules</c> and <c>capacity-addresses</c>.
        /// <para>
        /// It stays here rather than being deleted, and it is never re-pointed at one of its successors, for the
        /// reason <c>dataline-address</c> states: a published id that quietly came to mean something narrower is
        /// worse than one that is gone. Keeping the row also keeps the id reserved.
        /// </para>
        /// PREDICATE: none. Nothing implements a retired code.
        /// </summary>
        private static ProblemCatalogEntry CapacityModulesExceeded =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-modules-exceeded"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "",
                ProblemCodeStatus.Retired)
            {
                Diagnostic = "Split into capacity-input-modules, capacity-output-modules and capacity-addresses; "
                    + "this id is reserved and never re-used.",
                Evidence = EvidenceMark.Unknown,

                // No RequiresControllerLimits, and no other functional flag: nothing implements a retired code, so
                // an evaluability declaration on it is metadata no profile will ever be asked about. The retired
                // `dataline-address` row carries none either.
            };

        /// <summary>
        /// More INPUT data lines addressed than the target controller holds: the project cannot be uploaded as it
        /// stands.
        /// PREDICATE: with a declared capability profile, the count of DISTINCT data lines carrying at least one
        /// addressed <c>dataline_input</c> is above <c>InputModules</c>.
        /// ITS OWN ROW (D2), because a module is not a terminal: this counts modules, says "moduler", and can be
        /// filtered and counted apart from its two siblings.
        /// THE LIMIT IS DATA, NOT A CONSTANT HERE: 8 comes from the vendor datasheet and is corroborated by the
        /// address chooser's own 1-8 bound. It lives in the capability profile, so a caller with a different
        /// controller supplies a different number rather than editing a rule.
        /// NOT EVALUATED WITHOUT A PROFILE (D21): the limit is not in the file, and validating against a default
        /// would make the same project valid on one workstation and invalid on another.
        /// UNWITNESSABLE AT ANY PRACTICAL FIXTURE SIZE — the address encoding itself caps a data line at 8 or 16,
        /// so exceeding the datasheet figures needs a controller that declares LESS than the format allows. The
        /// boundary tests therefore declare a low limit instead of building a giant fixture.
        /// </summary>
        private static ProblemCatalogEntry CapacityInputModules =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-input-modules"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet bruger {used} af {limit} indgangsmoduler.")
            {
                Diagnostic = "Addressed input data lines exceed the declared controller limit.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// More OUTPUT data lines addressed than the target controller holds: the project cannot be uploaded as it
        /// stands.
        /// PREDICATE: with a declared capability profile, the count of DISTINCT data lines carrying at least one
        /// addressed <c>dataline_output</c> is above <c>OutputModules</c>.
        /// SEE <c>capacity-input-modules</c> for why the two directions are two rows rather than one with a
        /// direction argument, and for the limits-are-data and no-profile-no-evaluation notes, which apply
        /// unchanged with 16 in place of 8.
        /// </summary>
        private static ProblemCatalogEntry CapacityOutputModules =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-output-modules"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet bruger {used} af {limit} udgangsmoduler.")
            {
                Diagnostic = "Addressed output data lines exceed the declared controller limit.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// RETIRED. It counted addressed terminals in ONE direction but named neither, so a project over on both
        /// produced two findings a reader could tell apart only by their numbers — and the numbers are the one
        /// thing that does not say which direction they are about.
        /// <para>
        /// Its own entry argued the direction "is not an argument, because a direction is a word and an argument
        /// is data", and that argument still holds: the fix is not to smuggle a word into a slot but to SPLIT the
        /// row, exactly as <c>capacity-modules-exceeded</c> split into a per-direction pair. Each successor then
        /// names its direction in its own Danish sentence and carries only numbers as arguments.
        /// </para>
        /// <para>
        /// It stays here rather than being deleted, and is never re-pointed at one of its successors, for the
        /// reason its own predecessor states: a published id that quietly came to mean something narrower is
        /// worse than one that is gone. Keeping the row keeps the id reserved.
        /// </para>
        /// PREDICATE: none. Nothing implements a retired code.
        /// </summary>
        private static ProblemCatalogEntry CapacityAddresses =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-addresses"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "",
                ProblemCodeStatus.Retired)
            {
                Diagnostic = "Split into capacity-input-addresses and capacity-output-addresses; this id is "
                    + "reserved and never re-used.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// More addressed INPUT terminals than the target controller holds: the project cannot be uploaded as it
        /// stands.
        /// PREDICATE: with a declared capability profile, the count of addressed input terminals is above
        /// <c>AddressesPerDirection</c>.
        /// TWO ROWS RATHER THAN ONE WITH A DIRECTION ARGUMENT, following its module siblings: a direction is a
        /// WORD, and a word in an argument slot makes the message untranslatable and the row's arguments something
        /// other than data. Naming it in the sentence is what lets the arguments stay numbers.
        /// THE LIMIT IS DATA: 128 comes from the vendor datasheet and is corroborated by the address encoding
        /// (8x16 and 16x8 both land on 128).
        /// NOT EVALUATED WITHOUT A PROFILE (D21), and unwitnessable at any practical fixture size; the boundary
        /// tests declare a low limit instead.
        /// </summary>
        private static ProblemCatalogEntry CapacityInputAddresses =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-input-addresses"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet bruger {used} af {limit} indgangsklemmer.")
            {
                Diagnostic = "Addressed input terminals exceed the declared controller limit.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// More addressed OUTPUT terminals than the target controller holds.
        /// SEE <c>capacity-input-addresses</c> for why the two directions are two rows rather than one with a
        /// direction argument, and for the limits-are-data and no-profile-no-evaluation notes, which apply
        /// unchanged.
        /// </summary>
        private static ProblemCatalogEntry CapacityOutputAddresses =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-output-addresses"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet bruger {used} af {limit} udgangsklemmer.")
            {
                Diagnostic = "Addressed output terminals exceed the declared controller limit.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// The project's resource count is at or past the controller's ceiling: further growth will fail late,
        /// at upload time.
        /// PREDICATE: with a declared capability profile, a resource count at or above
        /// <c>HighWaterFraction</c> of the profile's <c>Resources</c> ceiling — with NO upper bound (D1). It once
        /// stopped at the ceiling, on the claim that a project past it was the modules row's business; that claim
        /// was false, because <c>capacity-modules-exceeded</c> counts data lines and terminal addresses and never
        /// counts <c>resource_*</c>. Nothing covered the over-ceiling case, so the only project that certainly
        /// cannot be uploaded was the one this row stayed silent about. The sentence — "Projektet bruger {used} af
        /// {limit} ressourcer." — is true above the limit as well as below it, which is why widening this row was
        /// the fix rather than minting a second one. It stays a WARNING, and deliberately: the ceiling has no
        /// vendor source, and an Error's consequence must hold whatever the author intended.
        /// BOTH NUMBERS ARE AUTHORED, AND BOTH SAY SO. TODO: unconfirmed. Neither the datasheet nor the vendor help
        /// states a controller's resource-table size, so the ceiling is authored per D20 and carries its marker
        /// where it lives (<c>ControllerCapabilityLimits.AuthoredResourceCeiling</c>); the fraction at which
        /// "approaching" begins is the threshold below. This is the only row in the capacity set with no vendor
        /// evidence at all, and D21(d) asks for the marker in the CODE rather than only in a backlog entry.
        /// NOT EVALUATED WITHOUT A PROFILE (D21), and unwitnessable at any practical fixture size.
        /// </summary>
        private static ProblemCatalogEntry CapacityResourcesHigh =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-resources-high"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet bruger {used} af {limit} ressourcer.")
            {
                Diagnostic = "The resource count is at or above the declared high-water fraction.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no vendor source states where "approaching" begins.
                    new DeclaredThreshold(
                        "HighWaterFraction",
                        0.9,
                        ThresholdConfidence.Authored,
                        "Authored (D20). The row's consequence is that further growth fails LATE, so the warning "
                        + "has to arrive while there is still room to act: nine tenths of the ceiling leaves a "
                        + "tenth of the table, which is more than any single product or block consumes. A lower "
                        + "fraction would warn about ordinary projects; a higher one would warn too late to be "
                        + "worth reading. TODO: unconfirmed."),
                ]),
            };

        /// <summary>
        /// More wireless products than the controller should carry: response time degrades.
        /// PREDICATE: with a declared capability profile, wireless products above <c>WirelessDevices</c>.
        /// A WARNING, NOT AN ERROR, and the source's own wording is why: the vendor help says a controller
        /// <i>"bør maksimalt"</i> be connected to 64 wireless products, <i>"af hensyn til en fornuftig
        /// responstid"</i> — a recommendation about response time. The devices DO bind; the system merely answers
        /// more slowly, and an Error's consequence must hold whatever the author intended. The catalogue's original
        /// Error was corrected on that evidence.
        /// NOT EVALUATED WITHOUT A PROFILE (D21), and unwitnessable at any practical fixture size — the boundary
        /// tests declare a low limit.
        /// </summary>
        private static ProblemCatalogEntry CapacityWirelessExceeded =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-wireless-exceeded"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet har {used} trådløse produkter; anbefalingen er højst {limit}.")
            {
                Diagnostic = "Wireless products exceed the declared controller recommendation.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// An element sits outside the modeled containment rules.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry Containment =>
            new ProblemCatalogEntry(
                new ProblemCode("containment"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("parent", ProblemArgumentType.SchemaName),
                ]),
                "Uventet placering: <{tag}> ligger under <{parent}>.")
            {
                Diagnostic = "<{tag}> under <{parent}> is outside the modeled containment rules (spec ch. 03/04/06)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Two terminals of the same direction claim the same data-line address.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry DatalineAddressDuplicate =>
            new ProblemCatalogEntry(
                new ProblemCode("dataline-address-duplicate"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                // ONE sentence for the whole collision, because the row declares one finding for it. The
                // terminals' own names are not lost: each site of the group carries its own text, so the reader
                // gets them beside the locators they can navigate to instead of two of them spliced into a
                // sentence that could only ever name two.
                "Dobbelt klemmeadresse '{value}': {count} klemmer på <{tag}> deler adressen.")
            {
                Diagnostic = "address_dataline='{value}' is claimed by {count} '{tag}' terminals (addresses are unique per direction)",
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// <c>address_dataline</c> is not a <c>_0x</c> hex token.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry DatalineAddressMalformed =>
            new ProblemCatalogEntry(
                new ProblemCode("dataline-address-malformed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Ugyldig klemmeadresse '{value}' på <{tag}>.")
            {
                Diagnostic = "address_dataline='{value}' on '{tag}' is not a _0x hex token",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// <c>address_dataline</c> is outside the legal 1–128 module range.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry DatalineAddressRange =>
            new ProblemCatalogEntry(
                new ProblemCode("dataline-address-range"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                "Klemmeadressen '{value}' på <{tag}> er uden for det gyldige område 1-{maximum}.")
            {
                Diagnostic = "address_dataline='{value}' on '{tag}' is outside the legal 1-{maximum} module range",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A block state variable not marked to survive a power failure, in a block where another variable IS
        /// marked: the installation returns to its initial state after an outage.
        /// PREDICATE: a block variable of a state-holding kind (<c>resource_flag</c>, <c>resource_counter</c>,
        /// <c>resource_integer</c>, <c>resource_enum</c> — the four that declare a <c>backup</c> attribute
        /// defaulting to <c>no</c>) whose <c>backup</c> is not <c>yes</c>, where at least one OTHER state
        /// variable of the same block is marked.
        /// SCOPED BY MEASUREMENT to BLOCK VARIABLES ALONE (error-list §8): the same <i>Gem aktuel værdi</i>
        /// control appears on terminals, but every <c>dataline_output</c> and <c>airlink_relay</c> ships
        /// <c>backup="yes"</c> and an input terminal declares no such attribute — so a walk over every
        /// backup-capable element would report most of a project. A test asserts that no terminal is ever
        /// reported by this row.
        /// THE QUALIFIER, and the reason there is no threshold: block variables default to unmarked, so an
        /// unmarked one says nothing on its own. It becomes an omission only where the author demonstrably used
        /// the feature — which is the contrast the vendor error fixture carries on purpose.
        /// LOCATION: the variable. ARGUMENTS: its name and its block's.
        /// </summary>
        private static ProblemCatalogEntry DevBackupMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-backup-missing"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Variablen '{variable}' i '{block}' gemmes ikke ved strømsvigt.")
            {
                Diagnostic = "An unmarked state variable in a block that marks another one.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A dimmer whose stored fade rates are BOTH zero: it switches hard instead of fading.
        /// PREDICATE: a product with a <c>dimmer_settings</c> group storing <c>value="0"</c> on BOTH
        /// <c>dimmer_setting_fade_rate_up</c> and <c>_down</c>.
        /// BOTH, as the row says: one hard direction is an asymmetry a dimmer can be set to; both is the row's
        /// stated consequence.
        /// STORED, not effective: the catalog ships these setting elements with an id and no <c>value</c>, and a
        /// project's inline DTD defaults that value to 0 — while the vendor's dialog shows its factory default
        /// there (700 ms / 100 % / 120 s). An effective read would therefore call every freshly placed product's
        /// setting zero. An absent value is an UNCOMMISSIONED setting, which is <c>dev-setting-default</c>'s row.
        /// RECLASSIFIED (⊘): the field clamps to a 200 ms minimum, so zero cannot be AUTHORED on this family —
        /// it arrives by import or hand-editing, which is what the whole-project face is for.
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry DevDimmerFadeZero =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-dimmer-fade-zero"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Lysdæmperen '{product}' skifter hårdt i begge retninger.")
            {
                Diagnostic = "Both stored fade rates of the dimmer are zero.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An LED dimmer left on automatic load detection: automatic can mis-drive an LED load.
        /// PREDICATE: a <c>product_rs485_led_dimmer</c> whose <c>dimmer_setting_load_mode</c> is EFFECTIVELY
        /// <c>auto</c> — stored as <c>auto</c>, or absent where the project declares <c>auto</c> as the default.
        /// EFFECTIVE HERE, and the exception to this set's stored-value reading: for a fade rate the vendor's
        /// dialog substitutes a factory default, so an absent value is not zero; for the load mode the project's
        /// own declared default IS the running mode, and it is what the dimmer dialog displays. Every authentic
        /// vendor file declares <c>value (auto | rc | rl) "auto"</c> and materializes the LED family's catalog
        /// default (<c>rc</c>) onto the instance instead, so absence reports there — but the reading goes through
        /// the project's schema view rather than a hard-coded <c>auto</c>, because the format is open-world and
        /// the rule and the dialog must not be able to disagree about one dimmer.
        /// SUBJECT: the LED family alone, which is the family whose load type is KNOWN and the one this row's
        /// consequence names. Every other dimmer family ships on automatic as the vendor's own choice, and
        /// reporting those would contradict the catalogue's own "why it may be fine".
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry DevDimmerLoadModeAuto =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-dimmer-load-mode-auto"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "LED-dæmperen '{product}' står på automatisk lastregistrering.")
            {
                Diagnostic = "An LED dimmer product runs on automatic load detection.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A dimmer whose stored maximum level is zero: the load can never be lit.
        /// PREDICATE: a product storing <c>dimmer_setting_maximum_value</c> as 0.
        /// A SEPARATE row from the inverted range, and it fires beside it when the minimum is zero too. That is
        /// deliberate: "the range is empty" and "the load can never be lit" are two facts a reader acts on
        /// differently — one is a range to widen, the other a channel that is off.
        /// STORED, not effective: the catalog ships these setting elements with an id and no <c>value</c>, and a
        /// project's inline DTD defaults that value to 0 — while the vendor's dialog shows its factory default
        /// there (700 ms / 100 % / 120 s). An effective read would therefore call every freshly placed product's
        /// setting zero. An absent value is an UNCOMMISSIONED setting, which is <c>dev-setting-default</c>'s row.
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry DevDimmerMaxZero =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-dimmer-max-zero"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Lysdæmperen '{product}' har maksimum 0 % og kan aldrig tænde.")
            {
                Diagnostic = "The dimmer's stored maximum level is zero.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A dimmer whose minimum level is at or above its maximum: the dimming range is empty or inverted.
        /// PREDICATE: a product storing both <c>dimmer_setting_minimum_value</c> and
        /// <c>dimmer_setting_maximum_value</c>, where minimum >= maximum.
        /// BOUNDARY: EQUAL counts — a range from 40 to 40 has no room to dim in.
        /// STORED, not effective: the catalog ships these setting elements with an id and no <c>value</c>, and a
        /// project's inline DTD defaults that value to 0 — while the vendor's dialog shows its factory default
        /// there (700 ms / 100 % / 120 s). An effective read would therefore call every freshly placed product's
        /// setting zero. An absent value is an UNCOMMISSIONED setting, which is <c>dev-setting-default</c>'s row.
        /// LOCATION: the product. ARGUMENTS: its name and both levels, so the reader sees which way round they
        /// are.
        /// </summary>
        private static ProblemCatalogEntry DevDimmerRangeInverted =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-dimmer-range-inverted"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                "Lysdæmperen '{product}' har minimum {minimum} % og maksimum {maximum} %.")
            {
                Diagnostic = "The dimmer's stored minimum level is at or above its stored maximum.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A variable whose chosen initial value a power-up program assigns again at every start: the initial
        /// value is meaningless.
        /// PREDICATE: an <c>action</c> inside a program whose events include <c>event_power</c>, whose
        /// <c>link1</c> resolves to a resource that STORES an <c>inivalue</c>.
        /// STORED MEANS NON-DEFAULT, and that is the canonicalizer's own rule rather than an assumption: a value
        /// equal to the DTD default is elided on save, so an <c>inivalue</c> present in the file is one the
        /// author chose — which is exactly what makes overwriting it at every start worth reporting. A variable
        /// left at its default initial value is not this row.
        /// LOCATION: the variable, where the reader decides which of the two — the initial value or the program
        /// — is the redundant one. ARGUMENTS: its name and the initial value it stores.
        /// </summary>
        private static ProblemCatalogEntry DevInivalueOverwritten =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-inivalue-overwritten"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Startværdien '{value}' på '{variable}' sættes af et program ved hver opstart.")
            {
                Diagnostic = "A power-up program assigns a variable that stores its own initial value.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A device setting still at its factory default on a product whose other settings WERE configured:
        /// the device may not have been commissioned at all.
        /// PREDICATE: a product with a settings group where at least one setting stores no <c>value</c> and at
        /// least <c>MinimumConfiguredSettings</c> of its settings DO store one.
        /// WHAT "AT ITS FACTORY DEFAULT" MEANS, and why no default value appears anywhere in this predicate: the
        /// vendor writes a setting's <c>value</c> only when the installer changes it — the catalog ships these
        /// elements carrying an id and nothing else — so a setting that stores NO value is at its factory
        /// default, whatever that default is per family. The backlog required the per-family defaults to come
        /// from the catalog definition rather than from literals; they are not needed at all, which is the
        /// stronger answer.
        /// THE THRESHOLD is what "otherwise configured" needs, and it is the only number here: without it the row
        /// reports every freshly placed product, where nothing is configured and nothing is forgotten.
        /// LOCATION: the product, with its untouched settings as related locations. ARGUMENTS: the product's
        /// name, how many settings are untouched and how many it has.
        /// </summary>
        private static ProblemCatalogEntry DevSettingDefault =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-setting-default"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("untouched", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("settings", ProblemArgumentType.Integer),
                ]),
                "Produktet '{product}' har {untouched} af {settings} indstillinger på fabriksværdien.")
            {
                Diagnostic = "A configured product still carries settings that store no value.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — the catalogue states no figure for "otherwise configured".
                    new DeclaredThreshold(
                        "MinimumConfiguredSettings",
                        1,
                        ThresholdConfidence.Authored,
                        "The row's condition is a CONTRAST, not a count: one configured setting beside an "
                        + "untouched one is already evidence that the installer worked on this product and "
                        + "stopped. Requiring more would hide exactly the half-commissioned device the row is "
                        + "about, and requiring none would report every freshly placed product. TODO: "
                        + "unconfirmed."),
                ]),
            };

        /// <summary>
        /// A shutter with a stored travel time of zero in either direction: position control cannot work.
        /// PREDICATE: a product holding <c>shutter_setting_travel_time_up</c> or <c>_down</c>, either of which
        /// stores 0.
        /// EITHER direction, unlike the dimmer's fade PAIR: a shutter that cannot time one direction cannot
        /// position itself at all, while a dimmer with one hard direction still dims in the other.
        /// STORED, not effective: the catalog ships these setting elements with an id and no <c>value</c>, and a
        /// project's inline DTD defaults that value to 0 — while the vendor's dialog shows its factory default
        /// there (700 ms / 100 % / 120 s). An effective read would therefore call every freshly placed product's
        /// setting zero. An absent value is an UNCOMMISSIONED setting, which is <c>dev-setting-default</c>'s row.
        /// The row's own legitimate reading — "times measured and entered during commissioning" — is exactly the
        /// absent-value state this predicate skips.
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry DevShutterTraveltimeZero =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-shutter-traveltime-zero"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Gardinet '{product}' har en køretid på 0 sekunder.")
            {
                Diagnostic = "A stored shutter travel time is zero in at least one direction.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program command assigning a variable declared read-only: the assignment is refused or ignored at
        /// runtime. The one ERROR of this set, as the catalogue rates it.
        /// PREDICATE: an <c>action</c> whose <c>link1</c> resolves to a resource whose <c>access</c> is
        /// <c>readonly</c>.
        /// EXCLUSIONS: <c>writeonly</c> and <c>readwrite</c> — writing to a write-only variable is what it is
        /// for.
        /// RECLASSIFIED (⊘): no variable dialog carries an accessibility control, so a block variable cannot be
        /// marked read-only from the GUI, and programs are block-local so none can reach a product's read-only
        /// resource. The state arrives from a catalog definition that declares it, or by hand-editing — which is
        /// what the whole-project face is for.
        /// LOCATION: the action, which is the thing to change. ARGUMENTS: the action's name and the variable's.
        /// </summary>
        private static ProblemCatalogEntry DevWriteToReadOnly =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-write-to-read-only"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("action", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Kommandoen '{action}' skriver til den skrivebeskyttede variabel '{variable}'.")
            {
                Diagnostic = "A program action assigns a resource whose access is readonly.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Adresse — a wired terminal has no decodable data-line address.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocAddress =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-address"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Adresse")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Ledningsfarve — a wired terminal carries no wire colour.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocCableColour =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-cable-colour"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Ledningsfarve")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Kabelnummer — a product carries no cable number.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocCablenumber =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-cablenumber"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Kabelnummer")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Kabeltype — a product carries no cable type.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocCabletype =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-cabletype"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Kabeltype")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Id-kode — a product carries no identification code.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocDocumentationTag =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-documentation-tag"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Id-kode")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Ingen produkter til slutbrugerdokumentation — no product is flagged for the end-user report, so that
        /// report comes out empty.
        /// PREDICATE: the project holds at least one product, and no product carries <c>enduser_report="yes"</c>.
        /// THE GUARD IS THE ROW'S OWN CONSEQUENCE: an empty report is a fault only where there was something to
        /// put in it. Three corpus files hold no products at all, and a project without products is not
        /// under-documented — it is not finished.
        /// UNWITNESSED BY EVERY AUTHENTIC FIXTURE, and the reason is recorded rather than assumed: IHC Visual
        /// writes <c>enduser_report="yes"</c> on each of the catalogue's two shutter products at insert time (no
        /// <c>.def</c> declares it) and no airlink dialog carries the checkbox that clears it, so any project
        /// witnessing a shutter travel time also carries a flagged product. Witnessing this row needs a project
        /// with no shutter — the state is reachable, it just cannot share a fixture with the shutter rows.
        /// LOCATION: the project root; the finding is about the project, not about any one product.
        /// </summary>
        private static ProblemCatalogEntry DocNoEnduserProducts =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-no-enduser-products"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Ingen produkter til slutbrugerdokumentation")
            {
                Diagnostic = "The project holds products and none is flagged for the end-user report.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Ikke forbundet — a wired terminal owns no link.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocNotLinked =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-not-linked"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Ikke forbundet")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Placering — a product carries no placement text.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocPosition =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-position"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Placering")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Lysgruppe — a product carries no light group.
        /// PREDICATE: implemented today by the documentation pass.
        /// </summary>
        private static ProblemCatalogEntry DocPowerGroup =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-power-group"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Lysgruppe")
            {
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler projektoplysninger — the project carries no masthead information at all, so every report
        /// masthead renders <c>--</c>.
        /// PREDICATE: none of <c>project_info</c>, <c>customer_info</c> and <c>installer_info</c> carries a single
        /// non-blank attribute (an absent block counts as blank).
        /// ALL THREE, NOT THE ROW'S LITERAL "OR", and the row's own stated CONSEQUENCE is what settles it: EVERY
        /// masthead renders the placeholder, which happens only when nothing is filled in anywhere. The literal
        /// reading reports 15 of the 20 corpus files, because the vendor leaves <c>customer_info</c> entirely
        /// blank in nearly all of them — an installer's own project without customer details, which is the
        /// ordinary state and which the row's disagreement column names ("internal project never handed over").
        /// WITNESS: the error fixture, whose three blocks were deliberately CLEARED — the catalogue records that
        /// IHC Visual pre-fills <c>programmer</c> with the Windows user name, so a blank <c>project_info</c> is
        /// something an author has to do on purpose.
        /// LOCATION: the project root. There is no element to navigate to, because a missing block is missing.
        /// </summary>
        private static ProblemCatalogEntry DocProjectInfoBlank =>
            new ProblemCatalogEntry(
                new ProblemCode("doc-project-info-blank"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Mangler projektoplysninger")
            {
                Diagnostic = "No masthead metadata block carries a value.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An element type is declared neither in the file's inline DTD nor in the schema registry.
        /// REFUSES: Save · Export.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry ElementUndeclared =>
            new ProblemCatalogEntry(
                new ProblemCode("element-undeclared"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.SchemaSerializationGuard,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Ukendt elementtype <{tag}>.")
            {
                Diagnostic = "element type '{tag}' is not declared in the project's inline DTD or the schema registry (cannot be serialized)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Two values of one enumerator type at the same index: the stored value is ambiguous. The set's one
        /// ERROR, as the catalogue rates it.
        /// PREDICATE: two <c>enum_value</c> children of one <c>enum_definition</c> whose EFFECTIVE index is equal.
        /// EFFECTIVE INDEX, and this is the fact the row would be wrong without: an ABSENT <c>index</c> IS zero,
        /// because the canonicalizer omits a value equal to the DTD default. Every definition's first value in the
        /// corpus is stored that way — 318 of 417 values carry an index, and the 99 that do not are all first
        /// values — so a predicate comparing the raw attribute would miss the collision between an absent index
        /// and an explicit <c>index="0"</c>, which is exactly the shape a hand-edited file produces.
        /// EXCLUSION: an index that is not a number at all. That is a schema fault with its own row.
        /// RECLASSIFIED (⊘): the enum editor has no reorder and no index field — values append and their indices
        /// follow insertion order — so no gesture in the application can produce this.
        /// LOCATION: the second value, with the first as a related location. ARGUMENTS: the type's name and the
        /// index they share.
        /// </summary>
        private static ProblemCatalogEntry EnumDefDuplicateIndex =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-def-duplicate-index"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("index", ProblemArgumentType.Integer),
                ]),
                "Enumerator typen '{enum}' har to værdier med indeks {index}.")
            {
                Diagnostic = "Two enum values of one definition resolve to the same index.",
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// Two values of one enumerator type share a name: the two states are indistinguishable to a reader.
        /// PREDICATE: two <c>enum_value</c> children of one <c>enum_definition</c> whose <c>name</c> is the same
        /// non-empty string, compared ordinally.
        /// SUBJECT: EVERY definition, the format's own system tables included. A duplicate in a shipped table
        /// would be a defect too, and since the editor cannot produce one anywhere, a file carrying one was not
        /// written by the editor.
        /// RECLASSIFIED (⊘): the enum editor answers <i>"Vælg et andet navn"</i> and refuses the commit,
        /// A/B-verified against a unique name in the same dialog. The state arrives by hand-editing or from a
        /// foreign file — which is what the whole-project face is for.
        /// LOCATION: the second value, with the first as a related location. ARGUMENTS: the type's name and the
        /// value name they share.
        /// </summary>
        private static ProblemCatalogEntry EnumDefDuplicateName =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-def-duplicate-name"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.AuthoredName),
                ]),
                "Enumerator typen '{enum}' har to værdier med navnet '{value}'.")
            {
                Diagnostic = "Two enum values of one definition share a name.",
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// An authored enumerator type with no values: no variable of that type can hold a meaningful value.
        /// PREDICATE: an authored <c>enum_definition</c> with no <c>enum_value</c> child.
        /// EXCLUSIONS: the same two as <c>enum-def-unused</c> — a system table is not the author's to fill, and
        /// the data-tables definition is EMPTY until the first user-defined text is added, which is an ordinary
        /// state rather than an unfinished type.
        /// WITNESSED on authentic content, not only in the error fixture: <c>project3</c> carries an authored
        /// <c>TestEnum</c> with no values at all.
        /// LOCATION: the definition. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry EnumDefEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-def-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                ]),
                "Enumerator typen '{enum}' har ingen værdier.")
            {
                Diagnostic = "An authored enum definition declares no values.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An authored enumerator type with exactly one value: a variable of that type can never change.
        /// PREDICATE: an authored <c>enum_definition</c> with exactly one <c>enum_value</c> child.
        /// EXCLUSIONS: the same two again, and the data-tables one matters here specifically — a project with ONE
        /// user-defined text would otherwise be told its text table can never change.
        /// LOCATION: the definition. ARGUMENTS: its name and the one value it declares, because the reader's
        /// question is which state the variable is stuck in.
        /// </summary>
        private static ProblemCatalogEntry EnumDefSingleValue =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-def-single-value"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.AuthoredName),
                ]),
                "Enumerator typen '{enum}' har kun én værdi, '{value}'.")
            {
                Diagnostic = "An authored enum definition declares exactly one value.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An authored enumerator type no variable declares itself of: a dead type in the project and in the
        /// reports.
        /// PREDICATE: an authored <c>enum_definition</c> whose <c>id</c> appears as no element's <c>typedef</c>.
        /// THE ONE REFERENCE FORM, measured rather than assumed: <c>resource_enum/@typedef</c>, 598 occurrences
        /// across the corpus, and NO other attribute in any committed project names a definition's id.
        /// EXCLUSIONS, both measured and both load-bearing: a <c>typeid</c>-bearing SYSTEM table (40 of the
        /// corpus's 109 definitions) is shipped with the format and read-only in the application, and most
        /// projects reference none of them — <i>Logning</i> is unreferenced in 8 committed projects — so including
        /// them would fire on nearly every authentic file. And the data-tables definition
        /// (<c>User-defined texts</c>) holds the project's user-defined TEXTS rather than a type's values, so no
        /// variable is ever declared of it and it is permanently "unused".
        /// WHAT THIS ROW REALLY REPORTS, and it took the error fixture's own record (M-14) to see it: IHC Visual
        /// CANNOT BIND a user-created enumerator type to a variable at all — <i>Indsæt ▸ Variable</i> offers a
        /// fixed 21 entries and none of them is an enumerator. So an authored definition is unreferenced for one of
        /// two reasons: the user created it in the enum editor, in which case it is necessarily dead and cannot be
        /// bound from the application; or it arrived with a library function block and lost its last reference when
        /// that block was deleted, which is the actionable case. The row is correct in both, and honest about only
        /// one being fixable in the GUI. Suppression is foreclosed (D07), so that distinction is not a feature
        /// waiting to be built: it is what a reader needs in order to dismiss the row by hand, and what the
        /// findings-UI backlog would have to weigh if it ever reopens the question.
        /// LOCATION: the definition. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry EnumDefUnused =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-def-unused"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                ]),
                "Enumerator typen '{enum}' bruges ikke af nogen variabel.")
            {
                Diagnostic = "An authored enum definition is referenced by no typedef.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A <c>resource_enum</c>'s <c>inivalue</c> is not a value of its own typedef.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry EnumInivalue =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-inivalue"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("inivalue", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("typedef", ProblemArgumentType.AuthoredName),
                ]),
                "Ugyldig starttilstand '{inivalue}' på enumerator-variablen '{name}': den findes ikke i enumeratortypen '{typedef}'.")
            {
                Diagnostic = "inivalue='{inivalue}' on resource_enum '{name}' is not a value of its typedef enum '{typedef}'",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A <c>resource_enum</c>'s <c>typedef</c> references something that is not an <c>enum_definition</c>.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry EnumTypedef =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-typedef"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("typedef", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Enumeratortype mangler: typedef='{typedef}' på enumerator-variablen '{name}' peger på <{tag}>, ikke på en enumeratortype.")
            {
                Diagnostic = "typedef='{typedef}' on resource_enum '{name}' references a <{tag}>, not an enum_definition",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A declared enum value nothing ever tests or assigns: a state the logic never uses.
        /// PREDICATE: an <c>enum_value</c> of an AUTHORED definition whose id appears in no <c>inivalue</c>.
        /// THE ONE REFERENCE FORM is <c>inivalue</c> — measured at 598 occurrences across the corpus, and no other
        /// attribute in any committed project names an <c>enum_value</c>. It covers both halves of "tested or
        /// assigned": a variable's initial value and a case branch's inline operand are stored the same way, which
        /// is why one set answers both.
        /// FIRING ON EVERY VALUE OF A USER-CREATED TYPE IS CORRECT, and the error fixture's record measured why
        /// (M-14): IHC Visual cannot bind a user-created enumerator type to a variable at all, so its values can
        /// never be referenced. The row states a true fact about such a type; what it cannot do is offer the user a
        /// way to fix it.
        /// EXCLUDED: the format's own <c>typeid</c> system tables and the data-tables definition — read-only
        /// furniture whose 11 unreferenced values would otherwise be reported in every project, the empty one
        /// included.
        /// LOCATION: the value. ARGUMENTS: its name and its type's.
        /// </summary>
        private static ProblemCatalogEntry EnumValueUnused =>
            new ProblemCatalogEntry(
                new ProblemCode("enum-value-unused"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                ]),
                "Værdien '{value}' i enumerator typen '{enum}' bruges ikke.")
            {
                Diagnostic = "An authored enum value appears in no inivalue.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The controller refused to store the uploaded project.
        /// REFUSES: Export, at <c>ProjectAppService.UploadTo</c> when the controller answers a store with
        /// <c>false</c> after change mode was already entered. The consequence is why this refuses loudly rather
        /// than returning a flag: the controller has no .BAK to roll back to, so its project state is UNCERTAIN
        /// and must be re-checked before a retry.
        /// </summary>
        private static ProblemCatalogEntry ExportControllerDeclined =>
            new ProblemCatalogEntry(
                new ProblemCode("export-controller-declined"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Controlleren afviste projektet")
            {
                Diagnostic = "The controller declined StoreProject after entering change mode; its project state is uncertain and must be re-checked before retrying.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A programming reference points outside its own function block.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry FbLocalRef =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-local-ref"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Reference uden for blokken: {attribute}='{value}' på <{tag}> peger uden for funktionsblokken.")
            {
                Diagnostic = "{attribute}='{value}' on '{tag}' references an element outside its function block (programming references must stay within one functionblock)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A pin sits under the wrong variable container.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry FbPinContainer =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-pin-container"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("expected", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("actual", ProblemArgumentType.SchemaName),
                ]),
                "Klemme i forkert beholder: <{tag}> i funktionsblokken '{id}' skal ligge under <{expected}>, ikke under <{actual}>.")
            {
                Diagnostic = "functionblock '{id}': {tag} must be under {expected}, not {actual}",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A <c>programs</c> container holds something other than <c>program_simple</c>.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry FbPrograms =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-programs"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Ugyldigt programindhold i funktionsblokken '{id}': programbeholderen indeholder <{tag}>, men må kun indeholde simple programmer.")
            {
                Diagnostic = "functionblock '{id}': programs contains '{tag}', but programs may hold only program_simple",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A function block does not hold exactly the five containers in their fixed order.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry FbShape =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-shape"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("expected", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("actual", ProblemArgumentType.SchemaName),
                ]),
                "Forkert blokopbygning i funktionsblokken '{id}': forventet [{expected}], men fandt [{actual}].")
            {
                Diagnostic = "functionblock '{id}' must contain exactly the five containers [{expected}] in that order, but has [{actual}]",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Two ids share a counter.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry IdDuplicateCounter =>
            new ProblemCatalogEntry(
                new ProblemCode("id-duplicate-counter"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                ]),
                "Dobbelt id-tæller i '{id}' på <{tag}>: {count} id'er deler samme tæller.")
            {
                Diagnostic = "duplicate id counter in '{id}' (element '{tag}')",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Two elements carry the same id token.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry IdDuplicateToken =>
            new ProblemCatalogEntry(
                new ProblemCode("id-duplicate-token"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                ]),
                "Dobbelt id '{id}' på <{tag}>: {count} elementer deler dette id.")
            {
                Diagnostic = "duplicate id token '{id}' (element '{tag}')",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An id's type-code disagrees with its element tag.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry IdTypecode =>
            new ProblemCatalogEntry(
                new ProblemCode("id-typecode"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("actual", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("expected", ProblemArgumentType.Integer),
                ]),
                "Forkert id-typekode i '{id}' på <{tag}>: typekoden er {actual}, men skulle være {expected}.")
            {
                Diagnostic = "id '{id}' on '{tag}' has a type-code that disagrees with the element's kind",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An <c>id</c> is not a well-formed <c>_0x</c> hex token in the legal packed range.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry IdWellformed =>
            new ProblemCatalogEntry(
                new ProblemCode("id-wellformed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Ugyldigt id '{id}' på <{tag}>.")
            {
                Diagnostic = "id '{id}' on '{tag}' is not a well-formed _0x hex token in the legal packed range (spec ch. 02)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A reference attribute names an id no element carries.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry IdrefDangling =>
            new ProblemCatalogEntry(
                new ProblemCode("idref-dangling"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                ]),
                "Reference uden mål: {attribute}='{value}' på <{tag}> peger ikke på noget element.")
            {
                Diagnostic = "dangling {attribute}='{value}' on '{tag}' (no element has that id)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A <c>.def</c> / <c>.ifb</c> catalog file cannot be parsed.
        /// REFUSES: Import, at <c>CatalogReader.ParseCatalogFile</c> — the one wrap shared by the runtime
        /// single-file import and the install-directory scan, so a malformed file names itself either way
        /// instead of surfacing as a bare XmlException that says which of hundreds failed.
        /// </summary>
        private static ProblemCatalogEntry ImportCatalogUnparsable =>
            new ProblemCatalogEntry(
                new ProblemCode("import-catalog-unparsable"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Ugyldig katalogfil")
            {
                Diagnostic = "The catalog definition file could not be parsed; nothing can be taken from it and the import is abandoned whole.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The imported file is not the catalog kind it is offered as.
        /// POSTURE PRESERVED (D13), and MEASURED rather than assumed: this condition SUCCEEDS today. Reading a
        /// <c>.ifb</c> as a product yields a ProductDefinition with an empty <c>product_identifier</c> and a
        /// <c>functionblock</c> body; reading a <c>.def</c> as a block yields an empty <c>master_type</c> and a
        /// <c>product_dataline</c> body. Nothing refuses, so there is no throw site to give an identity to, and
        /// this backlog introduces no new refusal — the row and the code disagree until a product ruling closes
        /// the gap, which is what the severity-times-operation matrix records.
        /// <para>
        /// ITS EMPTY TEMPLATE IS THE RECORD, not an omission: a row has a Danish sentence exactly when something
        /// can raise it, and nothing raises this one. Authoring words here would assert a raiser that does not
        /// exist. It stays Active rather than becoming <c>RuledOut</c> for the same reason — RuledOut is POSITIVE
        /// knowledge ("examined, and not a defect"), which this row explicitly disclaims: the gap is undecided,
        /// not closed. <c>NoRowHasWordsWithoutARaiserOrARaiserWithoutWords</c> holds both halves.
        /// </para>
        /// </summary>
        private static ProblemCatalogEntry ImportCatalogWrongKind =>
            new ProblemCatalogEntry(
                new ProblemCode("import-catalog-wrong-kind"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "")
            {
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The controller holds no stored project to download.
        /// REFUSES: Import, at <c>ProjectAppService.DownloadFrom</c> when the controller answers with no project
        /// data — which is what an empty controller does, and the reason this is named rather than left to
        /// become a NullReferenceException one frame later.
        /// </summary>
        private static ProblemCatalogEntry ImportControllerNoProject =>
            new ProblemCatalogEntry(
                new ProblemCode("import-controller-no-project"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Intet projekt på controlleren")
            {
                Diagnostic = "The controller returned no project — it likely has none stored; check IsIHCProjectAvailable() before downloading.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An embedded constant is not referenced by its parent's <c>link2</c> / <c>value</c>.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry InlineConstant =>
            new ProblemCatalogEntry(
                new ProblemCode("inline-constant"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("parent", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("attribute", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                ]),
                "Ubrugt indlejret konstant <{tag}> '{id}' i <{parent}>: forælderens {attribute} er '{value}' og peger ikke på den.")
            {
                Diagnostic = "embedded constant <{tag}> '{id}' inside <{parent}> must be referenced by its parent's {attribute} (found '{value}')",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A follow-link half is unwired, names a missing partner, has a partner of the wrong kind, or is not linked
        /// back reciprocally.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LinkBijection =>
            new ProblemCatalogEntry(
                new ProblemCode("link-bijection"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                ]),
                "Forbindelsen er ensidig: <{tag}> '{id}' er ikke forbundet begge veje til en partner af den modsatte type.")
            {
                Diagnostic = "{noun} {tag} '{id}' is not reciprocally linked to a live partner of the complementary kind",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A follow-link whose two ends sit in different localities — usually intended, but a common
        /// copy/paste slip.
        /// PREDICATE: a wired <c>link_from_resource</c> half whose partner resolves, where the nearest
        /// enclosing <c>group</c> of the half and of the partner are different elements.
        /// SUBJECT: the FROM half only, so one wire is reported once rather than once per end.
        /// EXCLUSIONS: an end outside every locality, and a half whose partner does not resolve — a broken
        /// reference is <c>idref-dangling</c>'s finding, not this one.
        /// LOCATION: the from-half. ARGUMENTS: the two locality names.
        /// </summary>
        private static ProblemCatalogEntry LinkCrossesLocality =>
            new ProblemCatalogEntry(
                new ProblemCode("link-crosses-locality"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("from", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("to", ProblemArgumentType.AuthoredName),
                ]),
                "Følg-linket går mellem lokaliteterne '{from}' og '{to}'.")
            {
                Diagnostic = "A follow-link's two ends sit in different localities.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A function block whose input pins are ALL unlinked: its trigger never arrives from the
        /// installation.
        /// PREDICATE: a <c>functionblock</c> declaring at least one <c>resource_input</c>, none of which
        /// carries a follow-link half.
        /// SUBJECT: every function block. PER BLOCK rather than per pin, and that is the substance of this
        /// predicate: a catalog block ships every input its behaviour offers — thirteen on the vendor's own
        /// <i>Kip tænd sluk</i> — and the author wires the one they want, so a per-pin reading would state
        /// this row's consequence falsely once per alternative the author declined.
        /// LOCATION: the block, with its unfed inputs as related locations.
        /// ARGUMENTS: the block's name and how many inputs it declares.
        /// </summary>
        private static ProblemCatalogEntry LinkFbInputUnfed =>
            new ProblemCatalogEntry(
                new ProblemCode("link-fb-input-unfed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{block}' har ingen forbundne indgange.")
            {
                Diagnostic = "No input pin of the function block owns a follow-link half.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A function block whose outputs are ALL unconsumed: it computes a result nothing reads.
        /// PREDICATE: a <c>functionblock</c> declaring at least one output pin (<c>resource_output</c> or
        /// <c>resource_scene</c>), none of which carries a follow-link or scene half. A scenario counts as a
        /// consumer, which is why the scene pin is in the subject set.
        /// SUBJECT: every function block. PER BLOCK, for the reason its input twin states.
        /// LOCATION: the block, with its unconsumed outputs as related locations.
        /// ARGUMENTS: the block's name and how many outputs it declares.
        /// </summary>
        private static ProblemCatalogEntry LinkFbOutputUnused =>
            new ProblemCatalogEntry(
                new ProblemCode("link-fb-output-unused"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{block}' har ingen forbundne udgange.")
            {
                Diagnostic = "No output pin of the function block owns a follow-link or scene half.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product input (wired or wireless) owns no link: the button or sensor does nothing anywhere.
        /// PREDICATE: a product input pin (<c>dataline_input</c>, <c>airlink_input</c> — the measured
        /// never-a-sink family minus the block pin) carries no follow-link half of either direction.
        /// SUBJECT: every such pin in the document. EXCLUSIONS: none — a spare terminal on an installed
        /// product is the legitimate reading this row is a Warning for, not a case to suppress.
        /// LOCATION: the pin. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LinkInputUnconnected =>
            new ProblemCatalogEntry(
                new ProblemCode("link-input-unconnected"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("pin", ProblemArgumentType.AuthoredName),
                ]),
                "Indgangen '{pin}' er ikke forbundet.")
            {
                Diagnostic = "A product input pin owns no follow-link half.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product output is driven by more than one source: the last writer wins and behaviour depends
        /// on timing.
        /// PREDICATE: a product output pin carries MORE THAN ONE <c>link_to_resource</c> half.
        /// BOUNDARY: one driver is the normal case; two is the finding.
        /// SUBJECT: every product output pin. EXCLUSIONS: none.
        /// LOCATION: the pin, with every driving half as a related location — the repair is to remove one of
        /// them and the reader cannot choose without seeing them all.
        /// ARGUMENTS: the pin's name and how many sources drive it.
        /// </summary>
        private static ProblemCatalogEntry LinkOutputMultidriven =>
            new ProblemCatalogEntry(
                new ProblemCode("link-output-multidriven"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("pin", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("drivers", ProblemArgumentType.Integer),
                ]),
                "Udgangen '{pin}' styres af {drivers} kilder.")
            {
                Diagnostic = "A product output pin carries more than one incoming follow-link half.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product output owns no link and no scenario drives it: nothing can ever switch it.
        /// PREDICATE: a product output pin (<c>dataline_output</c>, <c>airlink_relay</c>,
        /// <c>airlink_dimming</c> — the outputs the scene mapping declares) carries no follow-link half.
        /// SUBJECT: every such pin. EXCLUSION: an output named by a <c>scenes</c> container's
        /// <c>scene_resource</c> IS driven when the scenario fires, so this row's stated consequence would be
        /// false of it. The other two legitimate readings — an output held in reserve, one driven from a
        /// controller-side integration — are not decidable from the file and stay as the Warning's noise.
        /// LOCATION: the pin. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LinkOutputUndriven =>
            new ProblemCatalogEntry(
                new ProblemCode("link-output-undriven"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("pin", ProblemArgumentType.AuthoredName),
                ]),
                "Udgangen '{pin}' styres ikke af noget.")
            {
                Diagnostic = "A product output pin owns no follow-link half and no scenario names it.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A block whose only logic copies one input straight to one output, where the two neighbours
        /// could be linked directly instead.
        /// PREDICATE: exactly one program; its <c>events</c> holds exactly one <c>event</c> naming one of the
        /// block's own <c>resource_input</c> pins; its top-level <c>actions</c> holds exactly one
        /// <c>action</c> naming one of its own <c>resource_output</c> pins, and nothing else — no condition,
        /// no sub-program, no case.
        /// EXCLUSION, and it is what makes the row TRUE: the bypass must be legal. IHC routes every
        /// product-to-product path through a block, so a block between a button and a lamp cannot be removed
        /// and "the two devices could be linked through a simpler path" would be false of it. The measured
        /// link-role model decides that over the upstream source and the downstream sink.
        /// NOT EXAMINED: what the action does with the value (assign, invert, pulse) — the consequence holds
        /// for any single-event-to-single-action mapping, and the operation vocabulary belongs to the program
        /// rows.
        /// SUBJECT: every function block. LOCATION: the block. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LinkPassThrough =>
            new ProblemCatalogEntry(
                new ProblemCode("link-pass-through"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{block}' kopierer kun én indgang til én udgang.")
            {
                Diagnostic = "A block's only logic copies one input straight to one output, and the bypass would be legal.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A link ends on a block that carries no programs: the signal enters the block and stops there.
        /// PREDICATE: a <c>functionblock</c> whose <c>programs</c> container holds no <c>program_simple</c>
        /// or <c>program_sub</c>, and which contains at least one <c>link_to_resource</c> half.
        /// SUBJECT: every function block. EXCLUSION: an empty block nothing links INTO is merely unused —
        /// that is the input twin's finding; this row is about a wire that leads nowhere. Reachable only
        /// after the author DELETES the default program every inserted block ships with.
        /// LOCATION: the block, with the incoming halves as related locations — the repair is to write the
        /// logic or remove those wires. ARGUMENTS: the block's name and how many links end there.
        /// </summary>
        private static ProblemCatalogEntry LinkThroughEmptyBlock =>
            new ProblemCatalogEntry(
                new ProblemCode("link-through-empty-block"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{block}' har ingen programmer, men modtager signaler.")
            {
                Diagnostic = "A follow-link ends on a function block that carries no programs.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A UTF-16 byte-order mark precedes the document.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadBomUtf16 =>
            new ProblemCatalogEntry(
                new ProblemCode("load-bom-utf16"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen har et UTF-16-BOM")
            {
                Diagnostic = "A UTF-16 byte-order mark precedes the document; every byte offset is wrong.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A UTF-8 byte-order mark precedes the document.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadBomUtf8 =>
            new ProblemCatalogEntry(
                new ProblemCode("load-bom-utf8"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen har et UTF-8-BOM")
            {
                Diagnostic = "A UTF-8 byte-order mark precedes the document; .vis is ISO-8859-1 with no BOM.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An element contains character data.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadCharacterData =>
            new ProblemCatalogEntry(
                new ProblemCode("load-character-data"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen indeholder tekst i et element")
            {
                Diagnostic = "An element contains character data; the model is attribute-only, so opening would silently drop the text at the next save.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Element nesting exceeds the supported depth.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadDepth =>
            new ProblemCatalogEntry(
                new ProblemCode("load-depth"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "For dyb elementstruktur")
            {
                Diagnostic = "Element nesting exceeds the supported depth; a legal project never nests that deep.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The inline DTD block cannot be parsed.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadDtdMalformed =>
            new ProblemCatalogEntry(
                new ProblemCode("load-dtd-malformed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Ugyldig indbygget DTD")
            {
                Diagnostic = "The inline DTD block cannot be parsed, so nothing can be validated or written back.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The stream holds no bytes.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("load-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen er tom")
            {
                Diagnostic = "The stream holds no bytes — not a project file; a zero-length copy or a failed transfer.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The XML declaration names an encoding other than ISO-8859-1.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadEncodingDeclared =>
            new ProblemCatalogEntry(
                new ProblemCode("load-encoding-declared"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Forkert tegnkodning")
            {
                Diagnostic = "The XML declaration names an encoding other than ISO-8859-1, so text would be read in one encoding and written back in another.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The content is gzip-compressed.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadGzip =>
            new ProblemCatalogEntry(
                new ProblemCode("load-gzip"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen er komprimeret")
            {
                Diagnostic = "The content is gzip-compressed: a raw controller project blob that was never decompressed.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The document is not well-formed XML.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadNotXml =>
            new ProblemCatalogEntry(
                new ProblemCode("load-not-xml"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen er ikke gyldig XML")
            {
                Diagnostic = "The document is not well-formed XML: truncation, a partial write, or not a project file.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The root element is not <c>&lt;utcs_project&gt;</c>.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadRootTag =>
            new ProblemCatalogEntry(
                new ProblemCode("load-root-tag"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Ikke en projektfil")
            {
                Diagnostic = "The root element is not the project root; another XML file was opened as a project.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The document ends inside an open element.
        /// RULED OUT: the condition is real but is not separately decidable, and it is already reported. An
        /// XML parser at <c>ConformanceLevel.Document</c> refuses a truncated document before the reader can
        /// look at it — MEASURED: a project cut off inside <c>&lt;groups&gt;</c> refuses as
        /// <c>load-not-xml</c>, whose own text already names truncation as a cause. Telling the two apart
        /// would mean matching <c>XmlException.Message</c>, which is a LOCALIZED .NET resource string, or
        /// re-scanning the bytes with a second parser; neither is worth a different Danish sentence. The
        /// reader keeps its own end-of-document guard, and that guard now refuses under
        /// <c>load-not-xml</c> with its precise English diagnostic intact. Kept as an entry so nobody
        /// re-proposes the row.
        /// </summary>
        private static ProblemCatalogEntry LoadTruncated =>
            new ProblemCatalogEntry(
                new ProblemCode("load-truncated"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen er afkortet",
                ProblemCodeStatus.RuledOut)
            {
                Diagnostic = "The document ends inside an open element; the missing tail cannot be reconstructed.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The root carries no <c>version_major</c>.
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LoadVersionMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("load-version-missing"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Mangler projektversion")
            {
                Diagnostic = "The root carries no version_major, so the file cannot be identified as a project of any version.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A function block with no programs: it never does anything.
        /// PREDICATE: a <c>functionblock</c> whose <c>programs</c> container holds no <c>program_simple</c>.
        /// MEASURED: every block inserted through the application ships with a default <c>Program</c>, so this
        /// state requires the author to have DELETED it. It fires twice in the error fixture — <c>Tom blok</c> and
        /// <c>Kobling</c>, both recorded there as having had their default program deleted — and on no authentic
        /// project.
        /// LOCATION: the block. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicBlockEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-block-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Blokken '{block}' har ingen programmer.")
            {
                Diagnostic = "A function block declares no programs.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A locked block whose stored content no longer matches the library body it claims: the lock no longer
        /// reflects the state it was meant to protect.
        /// PREDICATE: a block with <c>locked="yes"</c> and a resolvable <c>master_type</c> whose library body the
        /// caller's <c>ILibraryBlockSource</c> holds, carrying a variable whose STORED value (a setting's
        /// <c>value</c>, a declared variable's <c>inivalue</c>) differs from the same-named variable's in that body.
        /// REQUIRES THE LIBRARY (D27), and that is the whole story of this row: nothing in the file distinguishes an
        /// edited value from a library default, so T055 could not write it and the id-ordering proxy it tried was
        /// refuted by measurement (it fired on nearly every locked product in every authentic project, because
        /// links and terminals legitimately receive their ids after the product was placed). The ruling gave the
        /// validation context a library port, declared the way controller limits are declared: a caller without one
        /// does not evaluate this row rather than guessing.
        /// WHY THE COMPARISON IS SO NARROW: the vendor's lock disables a block's <c>Navn</c> field but NOT its
        /// variables' initial values, so a stored value is exactly the surface a locked block still exposes. A
        /// variable the library does not have at all is a structural difference and stays
        /// <c>logic-master-block-modified</c>'s finding; paired BY NAME rather than by id, because a placed block's
        /// ids are re-stamped at insert and share nothing with the library's.
        /// WITNESS: the error fixture's <i>Kip tænd sluk (lokalt tilpasset)</i>, whose <c>Timer</c> setting was
        /// edited from 3 to 5 minutes under <c>locked="yes"</c> — the state §3 recorded and no rule could see.
        /// LOCATION: the variable, which is the thing to put back. ARGUMENTS: the block's name and the variable's,
        /// and DELIBERATELY NOT the library's value: a timer's value is four attributes, so rendering it inside a
        /// Danish sentence produces `hour=0;minute=3;second=0` — machine text in user-facing prose. The comparison
        /// detail belongs in the English diagnostic, which is where the log reads it.
        /// </summary>
        private static ProblemCatalogEntry LogicBlockLockedContent =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-block-locked-content"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Den låste blok '{block}' har ændret '{variable}'.")
            {
                Diagnostic = "A locked block stores a value its library body does not.",
                Evidence = EvidenceMark.Authored,
                RequiresLibrary = true,
            };

        /// <summary>
        /// A function block with neither inputs nor outputs: nothing outside it can reach it.
        /// PREDICATE: a <c>functionblock</c> whose <c>inputs</c> and <c>outputs</c> containers are both empty.
        /// READ LITERALLY, and deliberately so: unlike the two documentation rows whose literal condition
        /// contradicted their own consequence, this condition is stated in terms of the file and matches its
        /// consequence exactly. A block with no pins genuinely cannot be reached from outside, whatever was
        /// intended.
        /// MEASURED: 15 blocks across the corpus, every one a freshly inserted empty block left in place. The
        /// row's reasonable-disagreement column covers the deliberate case (a block driven entirely by timers or
        /// internal state).
        /// LOCATION: the block. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicBlockNoPins =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-block-no-pins"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Blokken '{block}' har hverken ind- eller udgange.")
            {
                Diagnostic = "A function block declares no inputs and no outputs.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Two branches of one switch testing the same value: whichever of the two the author meant, one of them
        /// never runs. The set's one ERROR, as the catalogue rates it.
        /// PREDICATE: two <c>case_action</c> branches of one <c>program_case</c> whose <c>value</c> is the same
        /// non-empty token.
        /// EXCLUSION: a branch carrying no value at all, which tests nothing and is therefore not a collision.
        /// UNWITNESSED BY THE CORPUS, and the catalogue records why rather than leaving it open:
        /// <c>Indsæt ▸ Ny case værdi</c> writes its branch under the LEFT PANE's caret instead of into the selected
        /// case node, and the left pane never holds a <c>program_case</c> in any view — four routes were driven,
        /// including the vendor's own documented right-click gesture delivered as real keyboard input. The state is
        /// reachable in principle (<c>project5</c> carries correctly nested branches), so this is an unfound route
        /// rather than a refusal, and a hand-edited file can carry the duplicate.
        /// LOCATION: the second branch, with the first as a related location. ARGUMENTS: the case node's name.
        /// </summary>
        private static ProblemCatalogEntry LogicCaseDuplicateValue =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-case-duplicate-value"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                ]),
                "Case-noden '{program}' tester den samme værdi i to grene.")
            {
                Diagnostic = "Two case_action branches of one program_case share a value.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A case node with no branches: the switch does nothing.
        /// PREDICATE: a <c>program_case</c> whose subtree holds no <c>case_action</c>.
        /// BRANCHES ARE COUNTED WHEREVER THEY SIT: the corpus stores <c>case_action</c> both as a DIRECT child of
        /// the case node and inside its <c>actions</c> container, so the walk is over the subtree rather than one
        /// container. A predicate reading only <c>actions</c> would miss half of them.
        /// MEASURED: 2 in the error fixture and none in any authentic project.
        /// LOCATION: the case node. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicCaseNoBranches =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-case-no-branches"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                ]),
                "Case-noden '{program}' har ingen case-værdier.")
            {
                Diagnostic = "A program_case carries no case_action branch.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A case branch testing a value that is not one of its switch variable's enum values: the branch can
        /// never be taken.
        /// THE CHAIN, measured rather than guessed: a branch's <c>value</c> names an INLINE OPERAND element, and
        /// that operand's <c>inivalue</c> is the value actually tested. The switch variable's <c>typedef</c> names
        /// its enum definition, whose <c>enum_value</c> children are the legal set. A predicate comparing the
        /// branch's own <c>value</c> against the definition's values would match nothing anywhere and report every
        /// case branch in the corpus.
        /// SKIPPED: a switch that is not enum-typed (an integer switch stores a literal in the same place), and a
        /// branch whose operand or switch does not resolve — a broken reference is <c>idref-dangling</c>'s finding.
        /// UNWITNESSED: no committed project carries a foreign case value. The catalogue records that the vendor's
        /// own insert gesture cannot even place a branch under the selected case node, so a mismatched value is a
        /// hand-edit or the residue of a re-typed enum — which is the row's stated reasonable disagreement.
        /// LOCATION: the branch. ARGUMENTS: the branch's name and the enum type's.
        /// </summary>
        private static ProblemCatalogEntry LogicCaseValueForeign =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-case-value-foreign"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("enum", ProblemArgumentType.AuthoredName),
                ]),
                "Case-grenen '{program}' tester en værdi, der ikke findes i '{enum}'.")
            {
                Diagnostic = "A case branch tests a value its switch type does not declare.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Two programs assigning one variable from unrelated triggers: which value survives depends on event
        /// order.
        /// PREDICATE: a variable written by two or more TOP-LEVEL programs, where every program has at least one
        /// trigger, the commands used are not all the same, and the programs' transitive TRIGGER-ANCESTOR sets are
        /// pairwise disjoint.
        /// "UNRELATED" IS A DATAFLOW QUESTION, and this is the row that taught it: a library block's standard shape
        /// is one program setting an output ON and another setting it OFF, each triggered by its own pulse flag —
        /// and both pulse flags are written by programs triggered by the SAME button. Comparing trigger VARIABLES
        /// reports that shape on every library block (24 findings on <c>project3</c>, 9 on <c>Project1</c>, a
        /// project with two blocks). Comparing the transitive ancestor sets — who writes my trigger, and what
        /// triggers them — reports 4 and 2, and those are the real ones: a timer driven from two sources, a
        /// blocking flag, a clock output.
        /// TWO FURTHER REQUIREMENTS, both from the stated consequence: each program must HAVE a trigger (a program
        /// that never starts cannot contend, and <c>logic-program-no-events</c> already reports it), and the
        /// commands must DIFFER (two programs both setting one output to ON do not depend on order).
        /// ATTRIBUTED TO TOP-LEVEL PROGRAMS: two branches of one program are mutually exclusive, not contending.
        /// Attributing to the nearest enclosing program instead reported 17 contentions on <c>Project1</c>, because
        /// every sub-program's trigger set is empty and empty sets are trivially disjoint.
        /// LOCATION: the variable, with how many programs write it. ARGUMENTS: its name and that count.
        /// </summary>
        private static ProblemCatalogEntry LogicContendingWriters =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-contending-writers"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("writers", ProblemArgumentType.Integer),
                ]),
                "Variablen '{variable}' tilskrives af {writers} programmer med uafhængige udløsere.")
            {
                Diagnostic = "A variable is written by several programs with disjoint trigger ancestries.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A counter that only ever steps and is never assigned: the count grows without bound and never returns
        /// to a known state.
        /// PREDICATE: a <c>resource_counter</c> whose every write is SELF-MODIFYING.
        /// NO TOKEN TABLE NEEDED, because the format already says it: an increment is <c>%P = %P + 1</c> or
        /// <c>%P = %P + %S</c> and a reset is a plain assignment (<c>%P = 0</c>, <c>%P = Initialværdi</c>,
        /// <c>%P = %S</c>), so the same self-modifying test the shared read model uses answers this row. A
        /// DECREMENT-only counter is the same fault and is reported too.
        /// MEASURED: 1 in the error fixture and 0 in every authentic project — a library block's counters are all
        /// reset somewhere.
        /// LOCATION: the counter. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicCounterNeverReset =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-counter-never-reset"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Tælleren '{variable}' tælles op, men nulstilles aldrig.")
            {
                Diagnostic = "Every write to a counter is self-modifying.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Two programs of one block with the same events and the same commands: one of them is redundant.
        /// PREDICATE: two <c>program_simple</c> children of one block whose SUBTREE SIGNATURES are equal — every
        /// element's tag plus every attribute except <c>id</c>, <c>name</c>, <c>icon</c> and <c>note</c>, in
        /// document order.
        /// WHY THOSE FOUR ARE IGNORED: the operands and methods are what make two programs the same program. A
        /// re-labelled or re-noted copy is still a copy, and a program's rendered name is derived from its operands
        /// anyway.
        /// MEASURED: one pair in the whole corpus, in the error fixture's <c>Zoo</c> block, and none in any
        /// authentic project — so the signature is neither too loose nor too tight on real content.
        /// LOCATION: the second program, which is the one to delete, with the first as a related location.
        /// ARGUMENTS: the block's name.
        /// </summary>
        private static ProblemCatalogEntry LogicDuplicateProgram =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-duplicate-program"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                // PrimaryWithRelated, because that is what the rule EMITS: the duplicate program as the primary
                // and the program it copies as its related site. It declared OnePerOccurrence, which promised a
                // consumer there was nothing else to navigate to — and the copy is the one thing a reader needs
                // to see before deleting anything.
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Blokken '{block}' har to identiske programmer.")
            {
                Diagnostic = "Two programs of one block share a structural signature.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A flag some program sets and no program clears: it latches on and the logic never returns to its
        /// earlier state.
        /// PREDICATE: a <c>resource_flag</c> whose every write is <c>%P = ON</c> (method <c>_0xa</c>).
        /// WHY THE COMMAND AND NOT THE VALUE: measured over the corpus, a flag is only ever written by
        /// <c>%P = ON</c> or <c>%P = OFF</c> — 40 and 47 occurrences, nothing else — so "cleared by none" is
        /// decidable from the commands themselves without evaluating what the flag holds. Every other bool command
        /// CAN clear: <c>%P = OFF</c> always, <c>Kip %P</c> half the time, and a two-operand assign whenever its
        /// source is off.
        /// MEASURED: 2 in the error fixture, 0 in every authentic project.
        /// LOCATION: the flag. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicFlagNeverCleared =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-flag-never-cleared"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Flaget '{variable}' sættes, men nulstilles aldrig.")
            {
                Diagnostic = "Every write to a flag is a set command.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A block that keeps its library identity while its name no longer matches it: the block no longer
        /// matches the library version it claims to be.
        /// PREDICATE: a block carrying <c>master_name</c> whose insert name
        /// (<c>{master_type}.{master_version}. {master_name}</c>) is reconstructible and whose <c>name</c> differs
        /// from it.
        /// WITNESS, and it is the fixture's own designed one: <i>Kip tænd sluk (lokalt tilpasset)</i> — inserted
        /// from the library, then renamed and re-noted while still locked, with <c>Nummer 1.1.01</c>,
        /// <c>Version e</c>, <c>Oprettet 17/05/2017</c> and <c>Udviklet af Schneider Electric</c> all surviving.
        /// NOT A BLOCK THE USER SAVED TO THE LIBRARY: those keep <c>master_name</c> but get no <c>master_type</c>,
        /// so no insert name can be reconstructed and they are never reported — correct, since such a block IS its
        /// own library entry.
        /// WHAT IT CANNOT SEE: a block whose LOGIC diverges from the library while its name stays put. Deciding
        /// that needs the library definition. <c>logic-block-locked-content</c> reaches one now (D27) and compares
        /// stored VALUES with it; comparing program bodies is still nobody's row.
        /// A BORDER WORTH KNOWING: <c>name-default</c> reports a library block still AT its insert name and this
        /// row reports one moved away from it, so between them every reconstructible library block draws exactly
        /// one advisory. Both are dismissible per their own disagreement columns; this is a consequence of the
        /// catalogue carrying both rows, not a defect in either.
        /// LOCATION: the block. ARGUMENTS: its current name and the library entry's name.
        /// </summary>
        private static ProblemCatalogEntry LogicMasterBlockModified =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-master-block-modified"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("master", ProblemArgumentType.AuthoredName),
                ]),
                "Blokken '{block}' er ændret lokalt i forhold til biblioteksblokken '{master}'.")
            {
                Diagnostic = "A library block carries master identity but not its insert name.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An output pin wired to something but assigned by no program: the physical output can never change
        /// state.
        /// PREDICATE: a <c>resource_output</c> in a block's <c>outputs</c> container that OWNS a follow-link half
        /// and is written by no program.
        /// THE LINK IS THE POINT, and it is what separates this row from its wiring twin: an UNLINKED output is
        /// <c>link-fb-output-unused</c>'s finding — nothing consumes it. Here something does consume it and nothing
        /// produces it, which is the fault the physical installation notices.
        /// MEASURED: 3 in the error fixture, 0 in every authentic project — a library block always drives the
        /// outputs it exposes.
        /// LOCATION: the pin. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicOutputNeverAssigned =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-output-never-assigned"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Udgangen '{variable}' tilskrives ikke af noget program.")
            {
                Diagnostic = "A linked output pin is written by no program.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program with events and no commands: it starts and does nothing.
        /// PREDICATE: a <c>program_simple</c> with at least one event and an empty or absent <c>actions</c>
        /// container.
        /// EVENTS MUST BE PRESENT — the row says "declares events but no commands", and that requirement is what
        /// keeps this row from re-reporting the empty default program <c>logic-program-no-events</c> already names.
        /// The two rows never both fire on one program.
        /// MEASURED: exactly one program in the whole corpus has events and no commands, in the error fixture. A
        /// predicate that dropped the events requirement would have reported 16 more, all of them empty defaults.
        /// LOCATION: the program. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicProgramNoActions =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-program-no-actions"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                ]),
                "Programmet '{program}' har hændelser, men ingen kommandoer.")
            {
                Diagnostic = "A program_simple declares events and no actions.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program with no events: it never starts.
        /// PREDICATE: a <c>program_simple</c> whose <c>events</c> container is empty or absent.
        /// SUBJECT SCOPED BY THE GRAMMAR, and this is the fact the row would be unusable without: only
        /// <c>program_simple</c> has events. All 746 <c>program_sub</c> elements in the corpus carry
        /// <c>conditions</c> and <c>actions</c> and NO <c>events</c> container, and <c>program_case</c> carries its
        /// branches — a sub-program is a conditional BRANCH inside a program, not a program missing its trigger. A
        /// rule walking every <c>program_*</c> element would report 746 of them, in every authentic file.
        /// MEASURED: 16 across the authentic corpus, every one either a freshly inserted block's default empty
        /// program or a hand-built program in the token fixtures — which is precisely the row's own
        /// reasonable-disagreement case ("program under construction").
        /// LOCATION: the program. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicProgramNoEvents =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-program-no-events"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                ]),
                "Programmet '{program}' har ingen hændelser.")
            {
                Diagnostic = "A program_simple declares no events.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program triggered by a variable it also assigns: it can retrigger itself, and an oscillating loop
        /// is the failure mode.
        /// PREDICATE: a top-level program whose trigger set and write set share a variable.
        /// ATTRIBUTED TO THE TOP-LEVEL PROGRAM: a sub-program assigning its parent's trigger is the same loop,
        /// because the parent is what starts again. A sub-program has no <c>events</c> container of its own — all
        /// 746 in the corpus carry conditions and commands only.
        /// MEASURED: 1 in the error fixture, 1 in <c>Project1</c> and 4 in <c>project3</c>. The authentic ones are
        /// the vendor's deliberate blink pattern, which is precisely the row's stated reasonable disagreement
        /// ("deliberate self-terminating pattern").
        /// LOCATION: the program, because that is what the reader has to look at. ARGUMENTS: the program's name and
        /// the variable's.
        /// </summary>
        private static ProblemCatalogEntry LogicSelfTrigger =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-self-trigger"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Programmet '{program}' udløses af '{variable}', som det selv tilskriver.")
            {
                Diagnostic = "A program triggers on a variable it also writes.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A sub-program with no conditions: the conditional branch always takes the same path.
        /// PREDICATE: a <c>program_sub</c> whose <c>conditions</c> container is empty.
        /// THE CONTAINER IS ALWAYS THERE — 746 of 746 sub-programs in the corpus carry one — so this row is about
        /// an EMPTY container, which is what an author leaves behind when a branch is added and never filled in.
        /// MEASURED: 1 to 3 per file in 8 of the 20 committed projects, so it reports real leftovers rather than
        /// the format's furniture.
        /// LOCATION: the sub-program. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicSubprogramNoConditions =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-subprogram-no-conditions"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("program", ProblemArgumentType.AuthoredName),
                ]),
                "Underprogrammet '{program}' har ingen betingelser.")
            {
                Diagnostic = "A program_sub declares no conditions.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A declared timer no program ever starts: the timer never runs.
        /// PREDICATE: a <c>resource_timer</c> with no write whose method is one of the three ACTIVATION commands
        /// (<c>_0xbe</c> activate count-down with initial value, <c>_0xc8</c> activate count-up, <c>_0xd2</c> bare
        /// activate count-down), cited from <c>ProgramMethodCatalog.TimerCommands</c> rather than guessed.
        /// STARTING IS NOT ASSIGNING, which is why this row is not "never written": setting a timer to zero
        /// (<c>_0xa</c>), to its initial value (<c>_0x19</c>) or to another pin's value (<c>_0x1e</c>) does not
        /// start it, and stopping it (<c>_0xdc</c>) certainly does not. The corpus carries all five kinds.
        /// MEASURED: 1 in the error fixture, 4 in <c>project2</c> and 5 in <c>project3</c> — declared timers no
        /// program activates, which is the row's own "timer reserved for later".
        /// LOCATION: the timer. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry LogicTimerUnused =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-timer-unused"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                ]),
                "Timeren '{variable}' startes ikke af noget program.")
            {
                Diagnostic = "A timer has no activation command.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An internal variable programs read and never assign: the logic always sees its initial value.
        /// PREDICATE: a variable in a block's <c>internalsettings</c> container that is read or triggered on, never
        /// written, and not linked.
        /// SCOPED TO <c>internalsettings</c> ALONE, one container tighter than its two siblings: a
        /// <c>settings</c> variable is configured from the dialog and is SUPPOSED to keep its configured value, so
        /// reporting one here would report the whole point of a setting. Measured — <c>project3</c>'s 36 read-only
        /// candidates are 29 pins and 7 settings, and not one internal variable.
        /// MEASURED after the scoping: 1 in the error fixture and 2 in <c>project2</c>.
        /// LOCATION: the variable. ARGUMENTS: its name and its block's.
        /// </summary>
        private static ProblemCatalogEntry LogicVariableReadOnly =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-variable-read-only"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Variablen '{variable}' i '{block}' læses, men tilskrives aldrig.")
            {
                Diagnostic = "A block internal variable is read and never written.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A declared state variable no program touches and no link reaches: a dead declaration, and noise in
        /// the block and in the reports.
        /// PREDICATE: a <c>resource_*</c> in a block's <c>settings</c> or <c>internalsettings</c> container that is
        /// neither triggered on, read nor written by any program, and owns no follow-link or scene-link half.
        /// Evaluated over the shared program read model, not a traversal of its own.
        /// THE SUBJECT BOUNDARY THIS ROW SHARES WITH ITS TWO SIBLINGS: a block's PINS are its interface and its
        /// <c>settings</c>/<c>internalsettings</c> are its own state, so these rows are about the state
        /// containers. An input pin's producer and an output pin's consumer live OUTSIDE the block, and the wiring
        /// set already owns them (<c>link-fb-input-unfed</c>, <c>link-fb-output-unused</c>). Measured: including
        /// pins takes project3 from 9 findings to 64, of which 47 are pins behaving exactly as pins do.
        /// REPORTED ONCE PER VARIABLE, never once per program — the catalogue's own deliberate-non-findings
        /// section says so in as many words ("a block with more variables than its programs read … reported once,
        /// as <c>logic-variable-unused</c>").
        /// MEASURED: 4 in the error fixture, 9 in <c>project3</c>, 23 in <c>project2</c> — that fixture declares
        /// one variable of every kind on purpose, which is what a type zoo looks like to this row.
        /// LOCATION: the variable. ARGUMENTS: its name and its block's.
        /// </summary>
        private static ProblemCatalogEntry LogicVariableUnused =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-variable-unused"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Variablen '{variable}' i '{block}' bruges ikke af noget program.")
            {
                Diagnostic = "A block state variable has no program usage and no link.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A state variable programs assign and nothing ever reads: the value is computed and thrown away.
        /// PREDICATE: a state variable that IS written and is neither read, triggered on, nor linked.
        /// THE SUBJECT BOUNDARY THIS ROW SHARES WITH ITS TWO SIBLINGS: a block's PINS are its interface and its
        /// <c>settings</c>/<c>internalsettings</c> are its own state, so these rows are about the state
        /// containers. An input pin's producer and an output pin's consumer live OUTSIDE the block, and the wiring
        /// set already owns them (<c>link-fb-input-unfed</c>, <c>link-fb-output-unused</c>). Measured: including
        /// pins takes project3 from 9 findings to 64, of which 47 are pins behaving exactly as pins do.
        /// A LINK COUNTS AS A READER: the value leaves the block through it, so a linked variable is consumed
        /// even though no program reads it. The row's disagreement column covers the rest — a value read by the
        /// controller API, the app or a scene.
        /// MEASURED: 3 in the error fixture and 5 in <c>project5</c>; zero in <c>project1</c> and <c>project3</c>,
        /// whose write-only candidates were all output PINS.
        /// LOCATION: the variable. ARGUMENTS: its name and its block's.
        /// </summary>
        private static ProblemCatalogEntry LogicVariableWriteOnly =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-variable-write-only"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("variable", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Variablen '{variable}' i '{block}' tilskrives, men læses aldrig.")
            {
                Diagnostic = "A block state variable is written and never read.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// <c>last_unique_id</c> exceeds the 24-bit counter ceiling.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LuidCeiling =>
            new ProblemCatalogEntry(
                new ProblemCode("luid-ceiling"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                ]),
                "Id-tælleren er opbrugt: last_unique_id '{value}' overskrider loftet for 24-bit id-tællere.")
            {
                Diagnostic = "last_unique_id '{value}' exceeds the 24-bit id counter ceiling (0xffffff)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// <c>last_unique_id</c> is absent or below the highest counter present.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LuidLow =>
            new ProblemCatalogEntry(
                new ProblemCode("luid-low"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Id-tælleren er for lav")
            {
                Diagnostic = "last_unique_id is absent or below the highest counter present",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// <c>last_unique_id</c> is not a <c>_0x</c> hex token.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry LuidMalformed =>
            new ProblemCatalogEntry(
                new ProblemCode("luid-malformed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("value", ProblemArgumentType.AttributeValue),
                ]),
                "Ugyldig id-tæller: last_unique_id '{value}' er ikke et _0x-hextoken.")
            {
                Diagnostic = "last_unique_id '{value}' is not a _0x hex token",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Dobbelt Kabelnummer — two products or wired terminals carry the same cable number, so the number
        /// cannot be traced to one cable.
        /// PREDICATE: two products or <c>dataline_*</c> terminals whose <c>cablenumber</c> is the same non-blank
        /// value.
        /// PROJECT-WIDE, for the same reason as the identification code: a cable number is traced across a whole
        /// installation.
        /// EXCLUSION: a blank number, which is <c>doc-cablenumber</c>'s finding.
        /// SHAPE: the second holder is the location, the first a related location.
        /// </summary>
        private static ProblemCatalogEntry NameCableNumberDuplicate =>
            new ProblemCatalogEntry(
                new ProblemCode("name-cable-number-duplicate"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                default,
                "Dobbelt Kabelnummer")
            {
                Diagnostic = "Two cable-number holders carry the same value.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Uændret standardnavn — an element still carries the name it was given at insertion, so the reports
        /// read as unfinished.
        /// PREDICATE: a locality named <c>Lokalitet</c>, or a function block named either <c>Tom blok</c> or its
        /// own <c>{master_type}.{master_version}. {master_name}</c>.
        /// WHAT A TEMPLATE NAME IS, WITHOUT A CATALOG: a placed library block's name at insert is
        /// <c>{master_type}.{master_version}. {master_name}</c>, and the block carries all three parts, so the
        /// insert name is RECONSTRUCTED from the element itself. That is why this row needs no catalog inside the
        /// validation pass and why a renamed block cannot be mistaken for an untouched one — <i>Kip tænd sluk
        /// (lokalt tilpasset)</i> is not equal to <i>1.1.01.e. Kip tænd sluk</i>.
        /// NOT PRODUCTS, deliberately: a product's insert name is its catalog display name, which the file does not
        /// carry. There is no in-file evidence to read, and guessing from the product identifier would report
        /// renamed products too.
        /// THE TWO LITERALS are the format's own placeholders rather than a language choice — the empty-block
        /// template is named <c>Tom blok</c> in the catalog's <c>fb.def</c>, and a new locality is named
        /// <c>Lokalitet</c> in the file.
        /// LOCATION: the element still at its template name.
        /// </summary>
        private static ProblemCatalogEntry NameDefault =>
            new ProblemCatalogEntry(
                new ProblemCode("name-default"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Uændret standardnavn")
            {
                Diagnostic = "An element still carries its insertion or template name.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Dobbelt navn — two siblings share a name, so a reference to either one is ambiguous in the reports and
        /// on site.
        /// PREDICATE: two nameable children of ONE parent with the same non-blank name, compared ordinally.
        /// SCOPED TO SIBLINGS, which is the row's own wording and also the only scope that survives contact with
        /// real installations: two rooms may each hold a <i>Loftlampe</i>, and that is how installations are named.
        /// Project-wide would report ordinary practice as a fault.
        /// EXCLUSION: a blank name, which is <c>name-empty</c>'s finding — two blanks are two missing names, not a
        /// collision.
        /// SHAPE: a collision is ONE fault at N sites, so the second holder is the location and the first is a
        /// related location.
        /// </summary>
        private static ProblemCatalogEntry NameDuplicateSiblings =>
            new ProblemCatalogEntry(
                new ProblemCode("name-duplicate-siblings"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                default,
                "Dobbelt navn")
            {
                Diagnostic = "Two nameable siblings share a name.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Navn — a locality, product, terminal, block or block variable carries no name, so nothing in
        /// the reports or on site identifies it.
        /// PREDICATE: a nameable element whose <c>name</c> is absent, empty or whitespace only.
        /// SUBJECT, and why it is a list rather than "every element with a name": the format's structural
        /// containers (<c>inputs</c>, <c>outputs</c>, <c>programs</c>, <c>scenes</c>, <c>groups</c>, …) carry names
        /// too, and nobody authors or reads those — a walk over every named element would report the skeleton of
        /// every project. The list is the kinds a person names and then reads back: localities
        /// (<c>group</c>), products, terminals (<c>dataline_*</c>, <c>airlink_*</c>), function blocks and block
        /// variables (<c>resource_*</c>).
        /// WHITESPACE COUNTS AS ABSENT, which is the same reading <c>RequiredFieldConstraint</c> applies at the
        /// dialog — a name of three spaces prints as nothing in a report, so the two faces must not disagree about
        /// it.
        /// LOCATION: the unnamed element. A FIXED LABEL in the register the documentation appendix already uses,
        /// like the <c>doc-*</c> rows beside it: the reader gets the element from the location, not from the
        /// sentence.
        /// </summary>
        private static ProblemCatalogEntry NameEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("name-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Navn")
            {
                Diagnostic = "A nameable element carries no name.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Dobbelt Id-kode — two products carry the same identification code, so the code identifies neither.
        /// PREDICATE: two products whose <c>documentation_tag</c> is the same non-blank value.
        /// PROJECT-WIDE, unlike <c>name-duplicate-siblings</c>: an identification code is a documentation-wide
        /// handle for one product, while a name only has to distinguish a product from its siblings.
        /// EXCLUSION: a blank code, which is <c>doc-documentation-tag</c>'s finding.
        /// SHAPE: the second holder is the location, the first a related location.
        /// </summary>
        private static ProblemCatalogEntry NameIdCodeDuplicate =>
            new ProblemCatalogEntry(
                new ProblemCode("name-id-code-duplicate"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                default,
                "Dobbelt Id-kode")
            {
                Diagnostic = "Two products carry the same documentation_tag.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Mangler Note — a function-block input carries no note, so the function report has nothing to
        /// describe the function with.
        /// PREDICATE: a <c>resource_input</c> declared in a block's <c>inputs</c> container whose <c>note</c> is
        /// absent, empty or whitespace only.
        /// SCOPED TO INPUTS, as the row says, and to the container that declares them: the FB report prints a note
        /// column for pins, and an input is the pin whose note explains what the block is FOR. Outputs, settings
        /// and internal variables are not this row.
        /// MEASURED: every library block ships the vendor's own notes on its pins — 32 of 32 across
        /// <c>project3</c> — so this row reports hand-authored blocks, which is exactly where a missing
        /// description is. The error fixture witnesses five, and each <c>project2</c> variant one.
        /// LOCATION: the pin. A FIXED LABEL, in the register the appendix already uses.
        /// </summary>
        private static ProblemCatalogEntry NameNoteMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("name-note-missing"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Mangler Note")
            {
                Diagnostic = "A function-block input carries no note.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Afvigende stavning af lysgruppe — two products name one light group with two spellings, so the
        /// reports group one physical circuit under two headings.
        /// PREDICATE: a product whose <c>power_group</c> normalises to the same value as an earlier product's but
        /// is not spelled identically. NORMALISATION: trimmed, inner whitespace runs collapsed to one space,
        /// folded to lower case invariantly — the three ways a re-typed group name differs without meaning
        /// anything different.
        /// WHAT IT DOES NOT MATCH: <c>Stue</c> against <c>Stuen</c>. That is a different word, and the row's own
        /// reasonable-disagreement column allows deliberately distinct group names; only case and spacing are
        /// treated as accidents.
        /// EXCLUSION: a blank light group, which is <c>doc-power-group</c>'s finding.
        /// MEASURED: exactly one light group in the whole corpus is spelled two ways — <c>Stue</c> and
        /// <c>stue</c> in the error fixture, seeded on purpose. Authentic files are silent.
        /// LOCATION: each product whose spelling differs from the FIRST one seen, which is the set to re-type;
        /// the first spelling is not reported, because it is not wrong, it is just first.
        /// </summary>
        private static ProblemCatalogEntry NamePowerGroupVariant =>
            new ProblemCatalogEntry(
                new ProblemCode("name-power-group-variant"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "Afvigende stavning af lysgruppe")
            {
                Diagnostic = "A light-group value differs from an earlier one only in case or spacing.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program does not carry the vendor skeleton (<c>events</c>/<c>actions</c>, or
        /// <c>conditions</c>/<c>actions</c>/<c>actions</c>).
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry ProgramShape =>
            new ProblemCatalogEntry(
                new ProblemCode("program-shape"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                    new ProblemArgumentSlot("expected", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("actual", ProblemArgumentType.SchemaName),
                ]),
                "Uventet programopbygning i <{tag}> '{id}': forventet [{expected}], men fandt [{actual}].")
            {
                Diagnostic = "'{tag}' '{id}' does not have the vendor skeleton [{expected}] (found [{actual}])",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The root's children are not the seven fixed children in the fixed order.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry RootChildren =>
            new ProblemCatalogEntry(
                new ProblemCode("root-children"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("actual", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("expected", ProblemArgumentType.SchemaName),
                ]),
                "Uventet rækkefølge i roden: rodens børn er [{actual}]; forventet [{expected}].")
            {
                Diagnostic = "the root's children are [{actual}]; a vendor file has the seven fixed children [{expected}] in that order",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// <c>version_major</c> is above the highest supported version (4).
        /// REFUSES: Open.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry RootVersion =>
            new ProblemCatalogEntry(
                new ProblemCode("root-version"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("version", ProblemArgumentType.AttributeValue),
                ]),
                "Nyere projektversion: version_major='{version}' er nyere end version 4, som dette værktøj understøtter.")
            {
                Diagnostic = "version_major='{version}': IHC Visual silently rejects project versions above 4 (spec ch. 10 10.5)",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Re-reading the just-written bytes does not reproduce the project.
        /// REFUSES: Save · Export, at <c>ProjectRoundTripVerifier.Verify</c> — the write self-check that runs
        /// on the bytes BEFORE they are handed back, so a lossy file is never returned rather than being
        /// detected later by someone re-opening it.
        /// </summary>
        private static ProblemCatalogEntry SaveRoundtripMismatch =>
            new ProblemCatalogEntry(
                new ProblemCode("save-roundtrip-mismatch"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet kan ikke gemmes uden tab")
            {
                Diagnostic = "Re-parsing the written bytes does not reproduce the in-memory project; the model "
                    + "holds state the .vis format cannot represent, and the write is abandoned.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The destination cannot be written (locked, read-only, missing, or out of space).
        /// REFUSES: Save · Export, at the atomic writer — which writes a temp file and swaps it in, so the
        /// refusal happens with the existing file still intact and no half-written project on disk.
        /// </summary>
        private static ProblemCatalogEntry SaveTargetUnwritable =>
            new ProblemCatalogEntry(
                new ProblemCode("save-target-unwritable"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Filen kunne ikke skrives")
            {
                Diagnostic = "The destination could not be written — locked, read-only, missing, or out of "
                    + "space; nothing was changed on disk.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// Every member of a scene switches its output off: an "all off" scene, or an unfinished one.
        /// PREDICATE: a scene with at least one resolvable member row, every one of which is off — a
        /// <c>scene_relay</c> with <c>relay_value</c> absent or <c>off</c>, or a <c>scene_dimmer</c> at
        /// <c>dimming_value</c> zero.
        /// SUBJECT: every scene. EXCLUSION: a scene holding a <c>scene_shutter</c> member is skipped — a
        /// shutter position is up or down and neither is "off", so the condition cannot be decided for it.
        /// LOCATION: the scene, with its member rows as related locations. ARGUMENTS: the scene's name and how
        /// many members are off, so the reader sees the scale of what would change.
        /// </summary>
        private static ProblemCatalogEntry SceneAllOff =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-all-off"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("scene", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("members", ProblemArgumentType.Integer),
                ]),
                "Scenariet '{scene}' slukker alle {members} medlemmer.")
            {
                Diagnostic = "Every resolvable member row of the scene switches its output off.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A scene row names a missing partner, a partner of the wrong kind, or is not linked back reciprocally.
        /// PREDICATE: authored by the task that implements this row.
        /// </summary>
        private static ProblemCatalogEntry SceneBijection =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-bijection"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("tag", ProblemArgumentType.SchemaName),
                    new ProblemArgumentSlot("id", ProblemArgumentType.ElementIdentity),
                ]),
                "Scenerækken er ensidig: <{tag}> '{id}' er ikke forbundet begge veje til en partner af den modsatte type.")
            {
                Diagnostic = "{noun} {tag} '{id}' is not reciprocally linked to a live partner of the complementary kind",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// One scene drives the same output through two member rows: the rows contradict each other.
        /// PREDICATE: two or more <c>scene_link</c> halves of ONE scene whose member rows sit in containers
        /// binding the same output resource.
        /// SUBJECT: the members of one scene, grouped by bound output. EXCLUSION: the same output in a
        /// DIFFERENT scene is legitimate and is not this row (the editor accepts it and refuses only the
        /// same-scene case). A half whose member or output does not resolve is skipped — that is
        /// <c>scene-bijection</c>'s finding.
        /// LOCATION: the scene, with the colliding member rows as related locations — the repair is to delete
        /// one of them. ARGUMENTS: the scene's name and the output's.
        /// </summary>
        private static ProblemCatalogEntry SceneDuplicateTarget =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-duplicate-target"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("scene", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("output", ProblemArgumentType.AuthoredName),
                ]),
                "Scenariet '{scene}' styrer udgangen '{output}' i flere rækker.")
            {
                Diagnostic = "Two member rows of one scene bind the same output resource.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A scene carries no members: activating it changes nothing.
        /// PREDICATE: a <c>resource_scene</c> pin holding no <c>scene_link</c> child.
        /// SUBJECT: every scene pin. EXCLUSIONS: none — a scene being built is the legitimate reading this
        /// Warning exists for. LOCATION: the scene pin. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry SceneEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("scene", ProblemArgumentType.AuthoredName),
                ]),
                "Scenariet '{scene}' har ingen medlemmer.")
            {
                Diagnostic = "A resource_scene pin holds no scene_link member.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A member row whose ramp time is unusually long: the installation appears unresponsive while the
        /// scene runs.
        /// PREDICATE: a <c>scene_dimmer</c> member row whose <c>ramptime_ms</c> exceeds the declared
        /// <c>MaxRampSeconds</c> threshold below.
        /// THRESHOLD, per D20: the catalogue states no figure for this row, so one is declared here as DATA
        /// with its derivation — the longest fade the product itself declares anywhere is the catalog's own
        /// <c>dimmer_setting_fade_rate_up</c> maximum of 60000 ms, so a scene ramp beyond that minute is
        /// longer than any fade the vendor's own presets allow. It is AUTHORED, not documented: no vendor
        /// source states a scene-ramp maximum.
        /// BOUNDARY: a ramp exactly AT the threshold is not reported; one millisecond past it is.
        /// SUBJECT: dimmer member rows. EXCLUSION: a relay or shutter member has no ramp at all.
        /// LOCATION: the member row. ARGUMENTS: the ramp in seconds and the declared limit, so the sentence
        /// states both the value and what it is measured against.
        /// </summary>
        private static ProblemCatalogEntry SceneLongDelay =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-long-delay"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("seconds", ProblemArgumentType.Number),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Number),
                ]),
                "Ramptiden {seconds} sekunder er længere end de tilladte {limit}.")
            {
                Diagnostic = "A scene_dimmer member row carries a ramp time above the declared maximum.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no vendor source states a maximum scene ramp time; derived from the
                    // catalog's own longest declared fade. Confirm against a controller or vendor source.
                    new DeclaredThreshold(
                        "MaxRampSeconds",
                        60,
                        ThresholdConfidence.Authored,
                        "No vendor source states a scene-ramp maximum. 60 s is the catalog's own longest declared "
                        + "fade (dimmer_setting_fade_rate_up maximum=60000 ms), so a scene ramp past it exceeds "
                        + "every fade the vendor's presets allow. TODO: unconfirmed."),
                ]),
            };

        /// <summary>
        /// A member row whose container names no output: the row carries a value for nothing.
        /// PREDICATE: a member row (<c>scene_relay</c>/<c>scene_dimmer</c>/<c>scene_shutter</c>) whose
        /// <c>scenes</c> container has no <c>scene_resource</c>, or names one that is not in the project.
        /// SUBJECT: every member row. EXCLUSION: the row's own <c>link</c> — its scene half — is NOT examined;
        /// a one-sided pair is <c>scene-bijection</c>'s finding, and reporting it twice would say one defect
        /// two ways.
        /// LOCATION: the member row. ARGUMENTS: the product it sits in, because the row's own name is the
        /// format's generic "Scenarie link" and would identify nothing.
        /// </summary>
        private static ProblemCatalogEntry SceneMemberUnwired =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-member-unwired"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Scenarierækken i '{product}' peger ikke på nogen udgang.")
            {
                Diagnostic = "A scene member row sits in a container that binds no resolvable output.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An output a scene drives that a follow-link also drives: the two fight over it.
        /// PREDICATE: an output named by a <c>scenes</c> container's <c>scene_resource</c> that also owns a
        /// <c>link_to_resource</c> half.
        /// SUBJECT: every bound scene output, reported once however many containers bind it.
        /// EXCLUSIONS: none — the legitimate reading (a scene sets a preset, a link overrides on demand) is
        /// why this is a Warning rather than an Error.
        /// LOCATION: the output pin, where both drivers meet. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry SceneOutputAlsoLinked =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-output-also-linked"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("output", ProblemArgumentType.AuthoredName),
                ]),
                "Udgangen '{output}' styres både af et scenarie og af et følg-link.")
            {
                Diagnostic = "A scene-bound output also owns an incoming follow-link half.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A scene nothing can fire: no program row names it.
        /// PREDICATE: a <c>resource_scene</c> pin whose id appears in no program operand — no
        /// <c>event</c>/<c>event_power</c>/<c>condition</c>/<c>action</c>/<c>case_action</c>/<c>program_case</c>
        /// <c>link1</c>, <c>link2</c>, <c>variable</c> or <c>value</c>.
        /// SUBJECT: every scene pin. MEASURED: a scene's own halves never name the pin — a <c>scene_link</c>
        /// names the member ROW and the row names the half back — so no exclusion is needed for them, and only a
        /// program operand can make a scene reachable. The legitimate reading (fired from the controller app or
        /// an external integration) is why this is a Warning.
        /// LOCATION: the scene pin. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry SceneUnreferenced =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-unreferenced"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("scene", ProblemArgumentType.AuthoredName),
                ]),
                "Scenariet '{scene}' kaldes ikke fra noget program.")
            {
                Diagnostic = "No program operand names the resource_scene pin.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An element left with the format's null icon where other elements of the same kind carry a real one:
        /// the tree and the reports read inconsistently.
        /// PREDICATE: an authored element whose effective icon is the null token, where another element of the SAME
        /// TAG carries a real icon.
        /// THE DEFAULT IS THE NULL TOKEN, from the DTD itself — <c>icon CDATA "_0x0"</c> on nearly every element —
        /// and the canonicalizer omits an attribute equal to its default, so "left with the default icon" means
        /// "carries <c>_0x0</c>, or carries none at all". This is the same omit-if-default reading the device and
        /// enum rows rest on.
        /// THE CONTRAST IS WHAT "OTHERWISE CHOSEN" MEANS. Without it the row would report every element of a kind
        /// the format never gives an icon to; with it, the row asks the question the sentence actually asks.
        /// RECLASSIFIED (⊘): not one element-properties dialog in the application carries an icon picker, so there
        /// is no "otherwise chosen" to deviate from — the state arrives by hand-editing or import, and NO committed
        /// project contains it. Implemented anyway, for the same reason as the other ⊘ rows: the whole-project face
        /// exists for files the editor did not write.
        /// SUBJECT: the elements a person authors and reads back (<c>AuthoredElements</c>), the same population
        /// <c>name-empty</c> asks about — a container's icon and a program operand's are furniture.
        /// LOCATION: the element. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry StructIconDefault =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-icon-default"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("element", ProblemArgumentType.AuthoredName),
                ]),
                "Elementet '{element}' har ikke fået et ikon.")
            {
                Diagnostic = "An authored element carries the null icon where its kind otherwise has one.",
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// A locality holding neither a product nor a block: an empty room in the tree and in the reports.
        /// PREDICATE: a <c>group</c> with no product child and no <c>functionblock</c> child.
        /// MEASURED, and the figure is worth knowing before reading a report: 8 to 10 per project, because a new
        /// project ships with TEN named localities and an installer fills the ones the building actually has. The
        /// row is still true — those rooms are empty in the tree and in the reports, and deleting the unused ones is
        /// what a careful author does before handing documentation over — which is what the row's own "room planned
        /// but not yet fitted" disagreement is for.
        /// NOT THE SAME as the catalogue's deliberate non-finding "a locality holding no BLOCKS": a room with
        /// products and no logic is ordinary, and this row needs BOTH to be absent.
        /// LOCATION: the locality. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry StructLocalityEmpty =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-locality-empty"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("locality", ProblemArgumentType.AuthoredName),
                ]),
                "Lokaliteten '{locality}' indeholder hverken produkter eller blokke.")
            {
                Diagnostic = "A locality holds neither a product nor a block.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A locality holding blocks and no hardware: the room has logic but nothing to act on, which is often a
        /// mis-drop.
        /// PREDICATE: a <c>group</c> with at least one <c>functionblock</c> child and no product child.
        /// MEASURED: 1 to 3 per project, and 10 in the token-capture fixture whose blocks all live in their own
        /// rooms. Its reasonable disagreement — a deliberate "logic room" holding central blocks — is a real
        /// pattern, which is why this is a Warning and not an Error.
        /// LOCATION: the locality. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry StructLocalityNoDevices =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-locality-no-devices"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("locality", ProblemArgumentType.AuthoredName),
                ]),
                "Lokaliteten '{locality}' indeholder kun funktionsblokke.")
            {
                Diagnostic = "A locality holds blocks and no products.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A block nothing links to and nothing references: it is isolated from the rest of the installation.
        /// PREDICATE: a <c>functionblock</c> with no link half anywhere inside it AND no id inside it named by any
        /// attribute outside it.
        /// TWO WAYS TO BE REACHED, both checked: a wire (a link half exists only once the wire is made) and a
        /// REFERENCE — a program in another block, a scene, a documentation pointer. Checking only wires would
        /// report a block driven entirely through references.
        /// MEASURED: 0 in <c>Project1</c>, 1 in <c>project5</c>, and 8 of 9 blocks in <c>project3</c>. That last
        /// figure is not a false positive: <c>project3</c> carries only three wired pin pairs in the whole file, so
        /// its library blocks really were placed for the report fixtures and never wired.
        /// LOCATION: the block. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry StructOrphanBlock =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-orphan-block"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Blokken '{block}' er ikke forbundet til resten af installationen.")
            {
                Diagnostic = "A function block is neither wired nor referenced from outside.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product with nothing wirable on it at all: nothing on the product can be connected.
        /// PREDICATE: a product with no child that is a terminal, a channel or a bus resource.
        /// WIRABLE IS WIDER THAN "TERMINAL", and measurement is why: an RS485 LED dimmer exposes
        /// <c>rs485_led_dimmer_channel</c> children and a bus sensor exposes <c>resource_*</c> measurements, and
        /// both are exactly what an author wires. Counting only <c>dataline_*</c>/<c>airlink_*</c> children reports
        /// every dimmer and every logging sensor in the corpus (3 to 4 per project); counting anything wirable
        /// reports the SMS modem and nothing else — which is the witness the error fixture was built to carry, and
        /// its record names it.
        /// THE ROW'S DISAGREEMENT IS ITS OWN ANSWER: "product family that genuinely has none" is what a modem is,
        /// and the row still earns its place — a modem in a project is worth one glance, because nothing about it
        /// is wired and its whole configuration is in its settings.
        /// LOCATION: the product. ARGUMENTS: its name.
        /// </summary>
        private static ProblemCatalogEntry StructProductNoTerminals =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-product-no-terminals"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' har ingen klemmer.")
            {
                Diagnostic = "A product carries nothing wirable.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A product or block references a help file that is not available.
        /// RULED OUT: the stated consequence is false. <c>helpfile</c> is never read - help resolves the document from
        /// the block’s own <c>master_type</c>, proven by two tamper oracles in which a nonexistent path and a different
        /// existing path both opened the same correct document; on products the attribute is populated zero times
        /// across every committed project and all 100 catalog <c>.def</c> templates. Kept as an entry so nobody re-
        /// proposes it.
        /// </summary>
        private static ProblemCatalogEntry NameHelpfileMissing =>
            new ProblemCatalogEntry(
                new ProblemCode("name-helpfile-missing"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Documentation,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "",
                ProblemCodeStatus.RuledOut)
            {
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// The stored modification stamp is far older than the project content suggests.
        /// RULED OUT: no decidable predicate exists. <c>&lt;modified&gt;</c> is re-stamped on every save and no edit
        /// route touches it, so in any saved file the stamp is current by construction and the condition cannot arise.
        /// Unlike an unauthorable state that arrives by import or hand-editing, there is no state to detect at all.
        /// Kept as an entry so nobody re-proposes it.
        /// </summary>
        private static ProblemCatalogEntry StructModifiedStale =>
            new ProblemCatalogEntry(
                new ProblemCode("struct-modified-stale"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "",
                ProblemCodeStatus.RuledOut)
            {
                Evidence = EvidenceMark.Refused,
            };

        /// <summary>
        /// RETIRED. This one id used to cover three distinct conditions — an address that is not a token, one
        /// outside the legal module range, and one two terminals of the same direction both claim — which the
        /// catalogue always described as three rows with three consequences and three repairs. It SPLIT into
        /// <c>dataline-address-malformed</c>, <c>dataline-address-range</c> and
        /// <c>dataline-address-duplicate</c>.
        /// <para>
        /// It stays here rather than being deleted, and it is never re-pointed at one of its successors. A
        /// published id that quietly came to mean something narrower is worse than one that is gone: a consumer
        /// filtering on it would silently start seeing a third of what it used to. Keeping the row is also what
        /// keeps the id reserved, since the duplicate-code invariant refuses a second entry claiming it.
        /// </para>
        /// PREDICATE: none. Nothing implements a retired code.
        /// </summary>
        private static ProblemCatalogEntry DatalineAddress =>
            new ProblemCatalogEntry(
                new ProblemCode("dataline-address"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                default,
                "",
                ProblemCodeStatus.Retired)
            {
                Diagnostic = "Split into dataline-address-malformed, dataline-address-range and "
                    + "dataline-address-duplicate; this id is reserved and never re-used.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>Every project-findings declaration, in code order.</summary>
        private static ProblemCatalogEntry[] ProjectFindings =>
        [
            DatalineAddress,
            AddrDimmerChannelDuplicate,
            AddrDimmerChannelUnassigned,
            AddrModemPhonenumberBlank,
            AddrModemPhonenumberMalformed,
            AddrModuleMixedLocality,
            AddrModulePartial,
            AddrS0TicksMissing,
            AddrUnassigned,
            AddrWirelessChannelShared,
            AddrWirelessNotCommissioned,
            AttrEnumRange,
            AttrLatin1,
            AttrRequired,
            AttrUndeclared,
            CapacityModemMultiple,
            CapacityModulesExceeded,
            CapacityInputModules,
            CapacityOutputModules,
            CapacityAddresses,
            CapacityInputAddresses,
            CapacityOutputAddresses,
            CapacityResourcesHigh,
            CapacityWirelessExceeded,
            Containment,
            DatalineAddressDuplicate,
            DatalineAddressMalformed,
            DatalineAddressRange,
            DevBackupMissing,
            DevDimmerFadeZero,
            DevDimmerLoadModeAuto,
            DevDimmerMaxZero,
            DevDimmerRangeInverted,
            DevInivalueOverwritten,
            DevSettingDefault,
            DevShutterTraveltimeZero,
            DevWriteToReadOnly,
            DocAddress,
            DocCableColour,
            DocCablenumber,
            DocCabletype,
            DocDocumentationTag,
            DocNoEnduserProducts,
            DocNotLinked,
            DocPosition,
            DocPowerGroup,
            DocProjectInfoBlank,
            ElementUndeclared,
            EnumDefDuplicateIndex,
            EnumDefDuplicateName,
            EnumDefEmpty,
            EnumDefSingleValue,
            EnumDefUnused,
            EnumInivalue,
            EnumTypedef,
            EnumValueUnused,
            ExportControllerDeclined,
            FbLocalRef,
            FbPinContainer,
            FbPrograms,
            FbShape,
            IdDuplicateCounter,
            IdDuplicateToken,
            IdTypecode,
            IdWellformed,
            IdrefDangling,
            ImportCatalogUnparsable,
            ImportCatalogWrongKind,
            ImportControllerNoProject,
            InlineConstant,
            LinkBijection,
            LinkCrossesLocality,
            LinkFbInputUnfed,
            LinkFbOutputUnused,
            LinkInputUnconnected,
            LinkOutputMultidriven,
            LinkOutputUndriven,
            LinkPassThrough,
            LinkThroughEmptyBlock,
            LoadBomUtf16,
            LoadBomUtf8,
            LoadCharacterData,
            LoadDepth,
            LoadDtdMalformed,
            LoadEmpty,
            LoadEncodingDeclared,
            LoadGzip,
            LoadNotXml,
            LoadRootTag,
            LoadTruncated,
            LoadVersionMissing,
            LogicBlockEmpty,
            LogicBlockLockedContent,
            LogicBlockNoPins,
            LogicCaseDuplicateValue,
            LogicCaseNoBranches,
            LogicCaseValueForeign,
            LogicContendingWriters,
            LogicCounterNeverReset,
            LogicDuplicateProgram,
            LogicFlagNeverCleared,
            LogicMasterBlockModified,
            LogicOutputNeverAssigned,
            LogicProgramNoActions,
            LogicProgramNoEvents,
            LogicSelfTrigger,
            LogicSubprogramNoConditions,
            LogicTimerUnused,
            LogicVariableReadOnly,
            LogicVariableUnused,
            LogicVariableWriteOnly,
            LuidCeiling,
            LuidLow,
            LuidMalformed,
            NameCableNumberDuplicate,
            NameDefault,
            NameDuplicateSiblings,
            NameEmpty,
            NameIdCodeDuplicate,
            NameNoteMissing,
            NamePowerGroupVariant,
            ProgramShape,
            RootChildren,
            RootVersion,
            SaveRoundtripMismatch,
            SaveTargetUnwritable,
            SceneAllOff,
            SceneBijection,
            SceneDuplicateTarget,
            SceneEmpty,
            SceneLongDelay,
            SceneMemberUnwired,
            SceneOutputAlsoLinked,
            SceneUnreferenced,
            StructIconDefault,
            StructLocalityEmpty,
            StructLocalityNoDevices,
            StructOrphanBlock,
            StructProductNoTerminals,
            NameHelpfileMissing,
            StructModifiedStale,
        ];
    }
}
