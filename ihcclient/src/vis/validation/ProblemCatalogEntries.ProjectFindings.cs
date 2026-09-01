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
        /// A number two rows both compare against is declared ONCE here and bound by both of their thresholds.
        /// <para>
        /// This is not a relocation of the fact out of the catalogue — each row still declares its own
        /// <see cref="DeclaredThreshold"/>, under its own name, with its own citation, and each rule still reads
        /// its OWN entry through <c>RuleAuthoring.Threshold</c>. What the constant removes is the second
        /// LITERAL: two rows that must agree used to hold the figure twice, so "the rows agree" was a claim
        /// about care rather than a property of the source, and an edit to one of them was a silent
        /// re-classification of the other.
        /// </para>
        /// <para>
        /// It is deliberately NOT the "lives below both" relocation ARCHITECTURE.md §5 prescribes for a fact a
        /// GESTURE must also enforce: nothing outside the whole-project run reads either figure, so both stay
        /// rule-only facts on their entries.
        /// </para>
        /// </summary>
        private const int Rs485BusMaxComponents = 32;

        /// <summary>
        /// Both version entries declare their own <c>SupportedVersionMajor</c> threshold but bind this constant,
        /// so <see cref="RootVersion"/>'s unsupported-major boundary and <see cref="RootVersionMinor"/>'s
        /// newer-minor gate cannot drift apart.
        /// </summary>
        private const int SupportedProjectVersionMajor = 4;

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
                RefusedOperations = [OperationCodes.Save],
                Diagnostic = "attribute '{attribute}' on '{tag}' has non-ISO-8859-1 text",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// A <c>#REQUIRED</c> attribute is missing.
        /// REFUSES: Save · Export.
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
                RefusedOperations = [OperationCodes.Save],
                Diagnostic = "required attribute '{attribute}' missing on '{tag}'",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// An attribute is declared neither in the element's inline-DTD block nor in the registry.
        /// REFUSES: Save · Export; also refused at edit-open.
        /// <para>
        /// The edit-open half is named on purpose, and was named here while §4's <c>Blocks</c> column still
        /// published the FILE LIFECYCLE only (Open/Save/Import/Export) and could not say it. That column now
        /// carries one word per head and publishes it too, but the discipline stands on its own: a comment that
        /// hides a real refusal is the defect this vocabulary exists to prevent.
        /// </para>
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
                RefusedOperations = [OperationCodes.Save, OperationCodes.EditOpen],
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
        /// A Voice Modem and an RS485 LED Dimmer cannot share a controller, so a project carrying both has one
        /// device that can never work.
        /// <para>
        /// RECLASSIFIED (⊘): IHC Visual 3.4 refuses the insert with a modal box reading <i>"Kan ikke indsætte
        /// Voice Modem og RS485 LED Dimmer i det samme projekt."</i> (driven live), so a file carrying both
        /// arrived by import or by hand.
        /// </para>
        /// <para>
        /// NO THRESHOLD, DELIBERATELY, unlike its two capacity siblings: this is an INCOMPATIBILITY, not a
        /// number. There is nothing to compare and nothing a reader could set differently — which is also why it
        /// binds no arguments: the sentence states the only condition it can state.
        /// </para>
        /// <para>
        /// THE VOICE MODEM IS IDENTIFIED BY ITS DEVICE-ROOT TAG, not by a catalog lookup, and that is forced
        /// rather than chosen. The built-in catalog ships NO voice-modem product at all (100 products: 73
        /// dataline, 24 airlink, 1 RS485 LED dimmer, 1 RS485 SMS modem, 1 S0), so a catalog-driven family test
        /// could not answer the question for any file — while the shared classifier already answers it for
        /// <c>product_rs485_modem</c> and, through its open-world fallback, for any other <c>*modem*</c> product
        /// tag that is not the SMS modem. That fallback is what covers the undocumented voice-modem types this
        /// SDK's catalog does not carry.
        /// </para>
        /// PREDICATE: the project contains at least one product of the Voice Modem family AND at least one of
        /// the RS485 LED Dimmer family.
        /// SUBJECT: every PRODUCT element, classified by device-root tag. The walk is scoped to products first:
        /// the classifier's open-world fallback is not itself product-guarded, so an SMS modem's own
        /// <c>sms_modem_settings</c> and <c>sms_modem_phonenumber</c> children would otherwise each answer
        /// "voice modem".
        /// EXCLUSION: the SMS modem, which is a different product. This is the exclusion that keeps the row
        /// quiet on authentic files: three committed projects carry an RS485 LED dimmer and an SMS modem side by
        /// side, so a rule reading "any modem" would report an Error on vendor-authored output.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — the two conflicting products are the repair
        /// sites, and naming either one of them would assert which is the mistake.
        /// ARGUMENTS: none. The sentence needs no data.
        /// </summary>
        private static ProblemCatalogEntry CapacityVoicemodemDimmerConflict =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-voicemodem-dimmer-conflict"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet indeholder både et Voice Modem og en RS485 LED-dæmper; de kan ikke anvendes i samme "
                + "projekt.")
            {
                Diagnostic = "The project contains both a Voice Modem and an RS485 LED Dimmer; the vendor tool "
                    + "refuses that combination, so one of them can never operate.",
                Evidence = EvidenceMark.Refused,
                RequiresControllerLimits = false,
            };

        /// <summary>
        /// More RS-485 components than the bus takes: a project past the limit cannot be fully commissioned
        /// however it is wired.
        /// <para>
        /// RECLASSIFIED (⊘): IHC Visual 3.4 refuses the insert that would exceed the limit, with a modal box
        /// reading <i>"Det maksimalt antal tilladte RS485 komponenter er 32 inkl. SMS modem"</i> (driven live).
        /// </para>
        /// <para>
        /// AN ERROR, NOT A WARNING, and the vendor's own wording is what settles it: <i>"maksimalt antal
        /// tilladte"</i> states a hard maximum. Contrast <see cref="CapacityWirelessExceeded"/>, which was
        /// corrected DOWN to Warning precisely because its vendor sentence says <i>"bør"</i> — a recommendation
        /// for response time, with the devices still binding.
        /// </para>
        /// <para>
        /// THE GUARD SENTENCE IS NOT A BOUNDARY PROBE. What was measured is that the box exists and what it
        /// says; no run established that 32 components commit and 33 do not. The declared evidence records that,
        /// and it matters because copying a guard's wording has already produced a false statement once in this
        /// catalogue's source material: the telephone-number box says <i>"skal være mere end 3 cifre"</i> while
        /// <c>123</c> — exactly three — commits and stores fine.
        /// </para>
        /// PREDICATE: the number of RS-485 bus components in the project exceeds the declared
        /// <c>MaximumRs485Components</c>. Strictly greater, because the sentence states a MAXIMUM.
        /// SUBJECT: every product the shared classifier places on the RS-485 bus — the LED dimmer, the voice
        /// modem and the SMS modem. THE SMS MODEM COUNTS: the vendor's own sentence says <i>inkl. SMS modem</i>,
        /// so excusing it would under-report by one on every bus that carries one.
        /// EXCLUSION: none. Data-line and wireless products are not on this bus and are counted by their own
        /// capacity rows.
        /// LOCATION: the project as a whole (<c>OneFinding</c>).
        /// ARGUMENTS: <c>used</c> and <c>limit</c> — the sentence states both, as the sibling capacity rows do.
        /// </summary>
        private static ProblemCatalogEntry CapacityRs485Exceeded =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-rs485-exceeded"),
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
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Projektet har {used} RS485-komponenter inkl. SMS-modem; det tilladte maksimum er {limit}.")
            {
                Diagnostic = "The project holds {used} RS-485 components including the SMS modem; the "
                    + "vendor-stated maximum is {limit}.",
                Evidence = EvidenceMark.Refused,
                RequiresControllerLimits = false,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "MaximumRs485Components",
                        Rs485BusMaxComponents,
                        ThresholdConfidence.VendorDocumented,
                        "IHC Visual 3.4 refuses the insert that would exceed the limit with a modal box reading "
                        + "\"Det maksimalt antal tilladte RS485 komponenter er 32 inkl. SMS modem\". The guard "
                        + "was driven and the number is the box's own sentence — but the boundary itself is "
                        + "UNCITED: no run established that 32 commits and 33 does not. The figure is bound "
                        + "from the one declared constant rs485-bus-installation's Rs485MaxComponents also "
                        + "binds, so the ceiling this row measures against is the ceiling that row publishes."),
                ]),
            };

        /// <summary>
        /// A second S0 product: only one can serve a controller, so the extras can never be commissioned and the
        /// file misdocuments the installation.
        /// <para>
        /// RECLASSIFIED (⊘): IHC Visual 3.4 refuses the second insert with a modal box reading <i>"Der kan kun
        /// være et S0 produkt i Visual projektet"</i> (driven live). A file carrying two arrived by import or by
        /// hand, which is what the whole-project face is for.
        /// </para>
        /// <para>
        /// IT DECLARES ITS LIMIT AS DATA, AND ITS SIBLING DOES NOT. <see cref="CapacityModemMultiple"/> keeps
        /// "one" in its predicate with no threshold at all. This row diverges DELIBERATELY rather than by
        /// oversight: the number here has a citable vendor sentence behind it, and a compared number with a
        /// source is declared. Do not describe the two rows as matching — they answer the same shape of question
        /// from different evidence.
        /// </para>
        /// PREDICATE: the number of S0 products in the project exceeds the declared
        /// <c>MaximumS0Products</c>.
        /// SUBJECT: every element the shared classifier calls an S0 product — the <c>s0_device</c> device root.
        /// EXCLUSION: none. Nothing about the meters' configuration, their addressing or their pulse counts
        /// enters this row; how many there are is the whole condition.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — the extra products are the repair sites, and
        /// anchoring on one of them would name an arbitrary meter as the wrong one.
        /// ARGUMENTS: <c>used</c> — how many the project holds, so the sentence states the distance to legal.
        /// </summary>
        private static ProblemCatalogEntry CapacityS0Multiple =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-s0-multiple"),
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
                "Projektet indeholder {used} S0-produkter; controlleren binder ét.")
            {
                Diagnostic = "The project contains {used} S0 products; the controller supports exactly one, so "
                    + "the extras can never be commissioned.",
                Evidence = EvidenceMark.Refused,
                RequiresControllerLimits = false,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "MaximumS0Products",
                        1,
                        ThresholdConfidence.VendorDocumented,
                        "IHC Visual 3.4 refuses the second insert with a modal box reading \"Der kan kun være "
                        + "et S0 produkt i Visual projektet\". The guard was driven and the number is the box's "
                        + "own sentence — but only that the box APPEARS was measured: no boundary run confirmed "
                        + "it appears at exactly two rather than at some higher count."),
                ]),
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
        /// CONDITION: one wireless RECEIVER takes part in more scenarios than the controller carries.
        /// A RECEIVER IS A WIRELESS PRODUCT THAT OWNS A SCENE CONTAINER — a definition the file decides, rather
        /// than a product list to keep current. A wireless unit with no such container cannot be commanded into
        /// a scene at all, so it is not a receiver and has no ceiling to be over. The corpus carries one such
        /// product, so the distinction is measured rather than hypothetical.
        /// COUNTED IN SCENE MEMBER ROWS, NOT CONTAINERS: a receiver with two channels has two containers and can
        /// still take part in one scenario. What the controller carries is the number of SCENARIOS.
        /// ENABLING, like <see cref="CapacityWirelessLinksPerUnit"/> and for the same reason: the ceiling is a
        /// controller capability, so with none declared the row is absent rather than measuring against a guess.
        /// DISPOSITION: Warning — the vendor states a recommendation rather than a refusal.
        /// LOCATION: the receiver — <c>OnePerOccurrence</c>. Two overloaded receivers are two to re-plan.
        /// ARGUMENTS: <c>product</c>, the <c>used</c> count and the <c>limit</c> it passed.
        /// </summary>
        private static ProblemCatalogEntry CapacityScenariosPerReceiver =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-scenarios-per-receiver"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_airlink", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Modtageren '{product}' indgår i {used} scenarier; anbefalingen er højst {limit} på én modtager.")
            {
                Diagnostic = "Wireless receiver '{product}' takes part in more scenarios than the declared "
                    + "controller supports on one receiver.",
                Evidence = EvidenceMark.Unknown,
                RequiresControllerLimits = true,
            };

        /// <summary>
        /// CONDITION: one wireless unit carries more follow-links than the controller supports — 32 ordinarily,
        /// 64 on a COMBI unit.
        /// THE ONE PHASE 7 ROW WITH AN ENABLING POSTURE, and the source says so outright. Every other row of the
        /// phase is an ERRATUM whose condition is in the file, so it reports with no context and a firmware
        /// target may only withhold it. This is not an erratum: the ceiling is a CONTROLLER CAPABILITY, so
        /// <c>RequiresControllerLimits</c> is true and with no controller declared the row is ABSENT rather than
        /// measuring against a guess. That is also why the limit is a member on
        /// <see cref="ControllerCapabilityLimits"/> and not a <c>DeclaredThreshold</c>: a threshold is for a
        /// project-only cap that needs no controller, and this is the converse case.
        /// THE COMBI CEILING IS ITS OWN DECLARED NUMBER, not a multiple of the ordinary one. That today's two
        /// figures differ by a factor of two is an observation, not a rule the vendor states.
        /// DISPOSITION: Warning, exactly as its sibling <see cref="CapacityWirelessExceeded"/> — the vendor
        /// states a recommendation rather than a refusal, and the field evidence is contradictory, with
        /// degradation reported at counts well below the published ceiling. An Error's consequence must hold
        /// whatever the author intended, and a slow-but-working installation does not qualify.
        /// LOCATION: the unit — <c>OnePerOccurrence</c>. Two overloaded units are two units to re-plan.
        /// ARGUMENTS: <c>product</c>, the <c>used</c> count and the <c>limit</c> it passed.
        /// </summary>
        private static ProblemCatalogEntry CapacityWirelessLinksPerUnit =>
            new ProblemCatalogEntry(
                new ProblemCode("capacity-wireless-links-per-unit"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_airlink", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("used", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("limit", ProblemArgumentType.Integer),
                ]),
                "Den trådløse enhed '{product}' har {used} links; anbefalingen er højst {limit} på én enhed.")
            {
                Diagnostic = "Wireless unit '{product}' carries more follow-links than the declared controller "
                    + "supports on one unit.",
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
                new RuleTarget(null, "address_dataline"),
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
                new RuleTarget(null, "address_dataline"),
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
                new RuleTarget(null, "address_dataline"),
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
        /// SCOPED BY MEASUREMENT to BLOCK VARIABLES ALONE: the same <i>Gem aktuel værdi</i>
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
        /// The project asks the controller to retain this many resource values across a power failure — a number
        /// the controller rations at upload, and which this row states so the reader can weigh it.
        /// <para>
        /// NO VERDICT AND NO THRESHOLD, DELIBERATELY. The retention CEILING is a controller question the source
        /// does not establish, so <see cref="ProblemCatalogEntry.RequiresControllerLimits"/> is NOT set and the
        /// row reports the count alone. Declaring a threshold would mean inventing a limit; declaring the
        /// controller context would mean asking for a fact nothing here would use. Exceeding the ration is a
        /// different condition, and this row does not claim the project does.
        /// </para>
        /// PREDICATE: at least one <c>resource_*</c> element carries <c>backup="yes"</c>; <c>count</c> is their
        /// total.
        /// SUBJECT: every <c>resource_*</c> element, of any kind — where <see cref="DevBackupMissing"/> is
        /// deliberately confined to the four BLOCK-VARIABLE kinds. The two read one attribute for different
        /// purposes: that row asks which variables an author forgot to mark, this one how large a retention
        /// budget the project is asking for.
        /// EXCLUSION: a TERMINAL. An output terminal ships <c>backup="yes"</c> as well, but <c>dataline_output</c>
        /// is not a <c>resource_*</c> element and is not counted. Whether its retained value consumes the same
        /// controller-side ration is NOT established anywhere in this row's source, and counting it would be
        /// asserting an equivalence nobody measured — so the count is scoped exactly as the source scopes it,
        /// and widening it later is a decision rather than a drift.
        /// Also excluded: a resource NOT marked for backup, which is simply not part of the budget.
        /// LOCATION: the project as a whole (<c>OneFinding</c>). Anchoring per resource would nag about each of
        /// them separately when the count is the fact.
        /// ARGUMENTS: <c>count</c> — how many values the project asks to be retained.
        /// </summary>
        private static ProblemCatalogEntry BackupRetainedCount =>
            new ProblemCatalogEntry(
                new ProblemCode("backup-retained-count"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                ]),
                "Projektet beder controlleren huske værdien af {count} ressourcer ved strømsvigt, et antal "
                + "controlleren begrænser ved overførsel.")
            {
                Diagnostic = "Project asks the controller to retain {count} resource values across a power "
                    + "failure; the feature is rationed at upload by the controller.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An initial value no physical unit can reach — 150 % relative humidity — is carried, rendered and
        /// shipped to the controller without a word from any layer of the vendor tool.
        /// <para>
        /// EVIDENCE IS <c>Authored</c>, MEASURED ON THIS VERY FAMILY, and it is what makes this row a Warning
        /// rather than a reclassified one: <c>resource_humidity_level inivalue="150.00"</c> loads, renders
        /// verbatim as <i>Fugtighed = 150,0% RH</i> and survives a plain resave untouched. The state is
        /// authorable, which the ⊘ rows in §4 are precisely not.
        /// </para>
        /// <para>
        /// THE BOUNDS ARE GUIDANCE, NOT A LIMIT. Vendor help states 0–100 for both kinds, and the same source
        /// measured that nothing in the load, display, commit or save path enforces it. Hence
        /// <see cref="ThresholdConfidence.VendorRecommendation"/>, and hence a Warning: the tool does not treat
        /// the range as a rule, so neither can this row.
        /// </para>
        /// PREDICATE: a percent-unit resource carries an <c>inivalue</c> that parses below
        /// <c>PercentMinimum</c> or above <c>PercentMaximum</c>. Both bounds inclusive.
        /// SUBJECT: <c>resource_humidity_level</c> (%RH) and <c>resource_light_level</c> (%) — and DELIBERATELY
        /// only those two. They are the kinds whose 0–100 range the format specification records (ch. 05 §5.1.3
        /// and §5.7.2's light-family note). The integer range (−32768…+32767) is equally citable and equally
        /// unenforced, so widening this row to carry a per-kind range is an ordinary re-scope rather than a
        /// blocked one — it is left out to keep one decision per diff, not for want of evidence.
        /// EXCLUSION: <c>resource_light</c>, whose sibling range is 0–60,000 LUX and which is therefore not a
        /// percent kind at all — reporting it against 0–100 would fire on every well-formed project carrying one.
        /// A counter's negative value, measured carried, with no source calling it illegal. A timer, whose value
        /// lives in <c>hour</c>/<c>minute</c>/<c>second</c> and never in <c>inivalue</c>, and whose out-of-range
        /// components the vendor writer re-expresses as a valid total rather than clamping. And an
        /// <c>inivalue</c> that does not parse as a number: there is nothing to compare, and a value the grammar
        /// admits but arithmetic cannot read is the schema layer's ground.
        /// LOCATION: the resource element.
        /// ARGUMENTS: <c>value</c> — the value AS WRITTEN; <c>variable</c> — which resource; <c>minimum</c> and
        /// <c>maximum</c> — the legal range.
        /// WHY <c>value</c> IS <c>AttributeValue</c>, where the sibling <c>scene-dimming-out-of-range</c> declares
        /// <c>Integer</c>: an <c>inivalue</c> can be a decimal form such as <c>150.00</c>, and that is the
        /// measured case. An <c>Integer</c> slot would silently reformat it to <c>150</c>, so the sentence would
        /// disagree with the bytes the reader is being asked to repair.
        /// SLOT ORDER IS <c>value, variable, minimum, maximum</c>: the template opens on <c>{value}</c>, and
        /// declared order is first-appearance order. The sibling <see cref="DevInivalueOverwritten"/> carries the
        /// same sentence shape and declares the same first two in the same order.
        /// </summary>
        private static ProblemCatalogEntry DevInivalueOutOfRange =>
            new ProblemCatalogEntry(
                new ProblemCode("dev-inivalue-out-of-range"),
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
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                "Startværdien {value} på '{variable}' er uden for det gyldige område {minimum}-{maximum}.")
            {
                Diagnostic = "Initial value {value} on '{variable}' is outside the vendor-help range "
                    + "{minimum}-{maximum}; nothing in the load, display, commit or save path of the vendor tool "
                    + "checks it, so it reaches the controller unexamined.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "PercentMinimum",
                        0,
                        ThresholdConfidence.VendorRecommendation,
                        "Vendor help states 0-100 %RH for resource_humidity_level and 0-100 % for "
                        + "resource_light_level. The same source measured the bound not enforced at any desktop "
                        + "layer — not on load, not on display, not on commit, not on save — so it is guidance "
                        + "rather than a limit."),
                    new DeclaredThreshold(
                        "PercentMaximum",
                        100,
                        ThresholdConfidence.VendorRecommendation,
                        "The upper half of the same vendor-help range, with the same caveat: measured not "
                        + "enforced anywhere in the desktop tool, which is why a file can carry 150.00 and "
                        + "render it."),
                ]),
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
                new RuleTarget(null, "address_dataline"),
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
                new RuleTarget(null, "cable_colour"),
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
                new RuleTarget("product_dataline", "cablenumber"),
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
                new RuleTarget("product_dataline", "cabletype"),
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
                new RuleTarget("product_dataline", "documentation_tag"),
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
                new RuleTarget("product_dataline", "position"),
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
                new RuleTarget("product_dataline", "power_group"),
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
                RefusedOperations = [OperationCodes.Save],
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
        /// EXCLUSIONS, the same two the authored-definition reader draws — a system table is not the author's to
        /// fill, and the data-tables definition is EMPTY until the first user-defined text is added, which is an
        /// ordinary state rather than an unfinished type.
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
                RefusedOperations = [OperationCodes.BridgeUpload],
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
        /// REFUSES: Edit-open.
        /// <para>
        /// The only row here refused at edit-open and NOWHERE else: a save writes the bytes happily and validate
        /// merely reports the condition, but editing addresses elements by id, so ambiguous ids would resolve
        /// first-match and target the wrong element. §4 publishes that refusal in its <c>Blocks</c> cell as
        /// "Edit-open" while its Severity cell reads Error: that column's Fatal wording is §2's FILE lifecycle,
        /// and this row refuses none of it (<c>EditOpenRefusalCodes.IdDuplicateToken</c>). The panel reads the
        /// declaration instead, so the row lists under <c>Fatale fejl</c> all the same.
        /// </para>
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
                RefusedOperations = [OperationCodes.EditOpen],
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
                RefusedOperations = [OperationCodes.ImportCatalog],
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
        /// the gap. That disagreement is recorded by <c>CatalogCompletenessTests.KnownUnimplemented</c>, which
        /// carries the reason, and the success it turns on is executed by
        /// <c>ImportBridgeRefusalTests.ReadingAFileAsTheWrongCatalogKindStillSucceedsToday</c>.
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
                // Declared though nothing raises it yet, unlike the empty template above. The two record
                // different things: the template is WORDS A USER READS, which may not be invented for a
                // condition that never occurs, while this names which operation a Refusal disposition refuses —
                // a fact the disposition already asserts and this merely spells. Leaving it empty would publish
                // a Fatal row that names no operation, which §7 forbids.
                RefusedOperations = [OperationCodes.ImportCatalog],
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
                RefusedOperations = [OperationCodes.BridgeDownload],
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
        /// A function block whose input pins are ALL unlinked: its trigger never arrives from the
        /// installation.
        /// PREDICATE: a <c>functionblock</c> declaring at least one <c>resource_input</c>, none of which
        /// carries a follow-link half, AND which has no autonomous start.
        /// SUBJECT: every function block. PER BLOCK rather than per pin, and that is the substance of this
        /// predicate: a catalog block ships every input its behaviour offers — thirteen on the vendor's own
        /// <i>Kip tænd sluk</i> — and the author wires the one they want, so a per-pin reading would state
        /// this row's consequence falsely once per alternative the author declined.
        /// EXCLUSION — AN AUTONOMOUS START: a block carrying an <c>event_power</c>, or an <c>event</c> whose
        /// <c>link1</c> resolves to an element outside its own <c>inputs</c> container, is not waiting for a wire.
        /// A clock block, a <i>Powerup - Altid tændt</i>, a home-simulation block: each starts from something that
        /// is not an input pin, so "the trigger never arrives" was simply false of it. Both halves are decidable
        /// from the file. A DANGLING <c>link1</c> does not count as an autonomous start — it names no element, so
        /// nothing can be said about where the trigger comes from, and the reference itself is
        /// <c>idref-dangling</c>'s finding.
        /// LOCATION: the block, with its unfed inputs as related locations.
        /// ARGUMENTS: the block's name.
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
        /// A product no pin of which is wired in either direction: the device is installed and the project does
        /// nothing with it.
        /// PREDICATE: a product element declaring at least one product input pin (<c>dataline_input</c>,
        /// <c>airlink_input</c> — the measured never-a-sink family minus the block pin) or output pin
        /// (<c>dataline_output</c>, <c>airlink_relay</c>, <c>airlink_dimming</c> — the outputs the scene mapping
        /// declares), NONE of which carries a follow-link or scene half.
        /// SUBJECT: every product with such a pin. PER PRODUCT, and that is the substance of the predicate — the
        /// same argument its two function-block neighbours make. A plate ships more terminals than an
        /// installation uses, so a per-pin reading would state this row's consequence falsely once per
        /// alternative the author declined: measured against a real installation, sixteen spare
        /// <i>Tryk (…)</i> buttons and seven pushbutton <i>LED (…)</i> indicators, on products that were
        /// wired and working.
        /// BOTH DIRECTIONS IN ONE ROW, because neither alone works: a pushbutton plate's indicator LEDs are that
        /// product's only outputs, so an outputs-only row would report every such plate whose buttons are wired.
        /// EXCLUSION: a <c>scenes</c> container naming one of the product's outputs as its <c>scene_resource</c>
        /// consumes it, so a scene-driven lamp module — which owns no follow-link at all — is not reported. The
        /// other legitimate readings, a device held in reserve and one driven from a controller-side
        /// integration, are not decidable from the file and stay as the Warning's noise.
        /// NO OVERLAP WITH <c>struct-product-no-terminals</c>: a product with nothing wirable on it declares no
        /// pin for this row to name, and stays that row's finding alone.
        /// LOCATION: the product, with its unwired pins as related locations — the reader has to see what the
        /// device offers to decide whether it should have been wired.
        /// ARGUMENTS: the product's name.
        /// </summary>
        private static ProblemCatalogEntry LinkProductUnwired =>
            new ProblemCatalogEntry(
                new ProblemCode("link-product-unwired"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.PrimaryWithRelated,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' har ingen forbundne ind- eller udgange.")
            {
                Diagnostic = "No input or output pin of the product owns a follow-link or scene half.",
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
        /// REFUSES: Open — declared even though the row is ruled out, because the head is a property of the
        /// CONDITION and not of the wiring: were this ever decidable it would refuse the open and nothing else.
        /// Every Refusal-disposition row names the operation it stops, so the absence of a head means "not a
        /// refusal" rather than "a refusal of something unstated"; §4 still omits the row, on its status.
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
                RefusedOperations = [OperationCodes.Load],
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
                RefusedOperations = [OperationCodes.Load],
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
        /// variable the library does not have at all is a structural difference and is deliberately nobody's
        /// finding — no shipped row compares block STRUCTURE against the library. Paired BY NAME rather than by id,
        /// because a placed block's ids are re-stamped at insert and share nothing with the library's.
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
        /// CONDITION: the project uses the v3 holiday (<i>helligdag</i>) schedule, which did not work AT ALL on
        /// controller firmware below 3.3.21.
        /// NARROWING, and this row is the first real one: the bound carries a <c>FixedIn</c>, so a profile that
        /// declares a firmware target at or past 3.3.21 withholds the finding. With NO target declared the row
        /// reports — narrowing context withholds, it never enables.
        /// CONFIDENCE: <c>VendorRecommendation</c>, because LK CLAIMS the release fixed it and this repository
        /// has not verified that. The grade is the honest one for a vendor claim, and it is why an unverified
        /// fix narrows only a target the caller states rather than deciding the default.
        /// LOCATION: the project — <c>OneFinding</c>. The reader's decision is one firmware upgrade for the
        /// installation, which four holiday resources do not make four of.
        /// ARGUMENTS: none. The version belongs in the sentence, not in a slot: it is a constant of the defect
        /// rather than a fact read from this project.
        /// </summary>
        private static ProblemCatalogEntry LogicHolidayScheduleFirmware =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-holiday-schedule-firmware"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("resource_holiday", null),
                FindingShape.OneFinding,
                EquatableArray<ProblemArgumentSlot>.Empty,
                "Projektet bruger helligdagsskemaet, som ifølge leverandøren først virker fra "
                + "controllerfirmware 3.3.21.")
            {
                Diagnostic = "The project uses the holiday schedule, which the vendor states does not work at "
                    + "all below controller firmware 3.3.21.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "HolidayScheduleFixedIn",
                    new ControllerFirmwareVersion(3, 3, 21),
                    ThresholdConfidence.VendorRecommendation,
                    "LK states the schedule did not work at all below 3.3.21 and claims the release fixed it. "
                    + "This repository has NOT verified the fix, which is why the grade is a vendor "
                    + "recommendation rather than a measurement. The bound is inclusive: 3.3.21 itself carries "
                    + "the claimed fix."),
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
        /// A program that carries work and no trigger: the commands are written and nothing can ever run them.
        /// PREDICATE: a <c>program_simple</c> whose <c>events</c> container is empty or absent AND which carries
        /// work — a non-empty <c>actions</c> container, or a <c>program_sub</c>/<c>program_case</c> child.
        /// SUBJECT SCOPED BY THE GRAMMAR, and this is the fact the row would be unusable without: only
        /// <c>program_simple</c> has events. All 746 <c>program_sub</c> elements in the corpus carry
        /// <c>conditions</c> and <c>actions</c> and NO <c>events</c> container, and <c>program_case</c> carries its
        /// branches — a sub-program is a conditional BRANCH inside a program, not a program missing its trigger. A
        /// rule walking every <c>program_*</c> element would report 746 of them, in every authentic file.
        /// EXCLUSION, and it is what the row's value now rests on: a program with NO WORK EITHER. Every block
        /// inserted from the library brings a program with neither trigger nor command, and measured against a
        /// real installation every hit of the untightened row was one of those — a statement that the author has
        /// not finished, which they can already see. The finding is about work STRANDED, so the subject is a
        /// program that has some. WORK INCLUDES A BRANCH: the commands may all sit inside a sub-program, and such
        /// a program is stranded just as completely.
        /// A block empty ALL THE WAY DOWN is still <c>logic-block-empty</c>'s finding.
        /// MEASURED after the narrowing: 1 across the authentic corpus, the error fixture's own designed witness.
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
                "Programmet '{program}' har kommandoer, men ingen hændelser.")
            {
                Diagnostic = "A program_simple carries work and declares no events.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program triggered by a variable it also assigns: it can retrigger itself, and an oscillating loop
        /// is the failure mode.
        /// PREDICATE: a top-level program whose trigger set and write set share a variable that is neither a
        /// <c>resource_timer</c> nor a <c>resource_counter</c>.
        /// ATTRIBUTED TO THE TOP-LEVEL PROGRAM: a sub-program assigning its parent's trigger is the same loop,
        /// because the parent is what starts again. A sub-program has no <c>events</c> container of its own — all
        /// 746 in the corpus carry conditions and commands only.
        /// EXCLUDED BY KIND: a timer re-armed by the program it woke is a DELAY, and a counter stepped by the
        /// program it counts for is a TALLY. Both are how those two kinds are meant to be used, and neither
        /// oscillates — a timer must elapse again, and a counter's step is not an edge the count re-fires — so
        /// this row's consequence was false of every one of them. Measured against a real installation, every
        /// self-trigger of those two kinds was one of the two idioms. The exclusion is by element kind alone: a
        /// flag, an output or an ordinary variable feeding itself back is still reported.
        /// AN EXCLUDED SELF-EDGE IS NOBODY'S FINDING, deliberately: <c>logic-block-recursive</c> excludes every
        /// direct self-edge, and widening it here would report the same deliberate idiom under a code whose
        /// consequence ("the call silently never runs") is false of it.
        /// MEASURED after the narrowing: 1 in the error fixture, 1 in <c>Project1</c> and 4 in <c>project3</c>.
        /// The authentic ones are the vendor's deliberate blink pattern over flags, which is precisely the row's
        /// stated reasonable disagreement ("deliberate self-terminating pattern").
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
        /// The pulse-counting block's constant must match the physical meter's rating plate, which the project
        /// cannot verify: an unchanged default silently mis-scales every reading if the meter differs.
        /// <para>
        /// THE CONSTANT IS BOUND FROM THE INSTANCE, NEVER FROM THE THRESHOLD. An earlier draft offered a
        /// fallback that reported any instance of the type while binding the DECLARED default — which renders
        /// "regner med 100 impulser" at a project that set 250, a sentence contradicting the project's own
        /// content. The threshold decides WHETHER to report; the message says what the project actually carries.
        /// </para>
        /// <para>
        /// THE CONTAINER IS <c>settings</c>, NOT <c>internalsettings</c>. On this block the internal group holds
        /// only timers and scratch integers, so a rule written against it would never fire at all — which is a
        /// failure mode no test of the reporting case would catch.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> with <c>master_type="4.2.03"</c> whose <c>resource_integer</c>
        /// named <c>1 Kwh/M3</c>, under the block's <c>settings</c> group, still carries
        /// <c>DefaultPulsesPerKwh</c>.
        /// SUBJECT: <c>functionblock</c> elements of that master type.
        /// EXCLUSION: an instance whose constant was CHANGED from the default — somebody has already made the
        /// decision this row asks for, and there is no fallback that reports it anyway. The source's own second
        /// half, that the parameter is editable without unlocking, is withdrawn as contradicted and is not
        /// carried. And, as for <c>fb-pir-dusk-gated</c>, a block whose <c>master_type</c> was stripped is silent
        /// by construction.
        /// LOCATION: the function block.
        /// ARGUMENTS: <c>name</c>, and <c>pulses</c> — the instance's OWN constant.
        /// NO CORPUS WITNESS: <c>4.2.03</c> appears in no committed project.
        /// </summary>
        private static ProblemCatalogEntry FbPulseConstantDefault =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-pulse-constant-default"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_type"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("pulses", ProblemArgumentType.Integer),
                ]),
                "Impulsblokken '{name}' regner med {pulses} impulser pr. kWh, og konstanten skal stemme "
                + "overens med den fysiske målers mærkeplade.")
            {
                Diagnostic = "Pulse-counting block '{name}' is configured for {pulses} pulses/kWh; the constant "
                    + "must match the physical meter's rating plate, which the project cannot verify.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "DefaultPulsesPerKwh",
                        100,
                        ThresholdConfidence.VendorDocumented,
                        "The mirrored 4.2.03.ifb (version b) ships the constant as "
                        + "<resource_integer name=\"1 Kwh/M3\" … inivalue=\"100\" />. Measured from the library "
                        + "rather than authored, which is what the grade records — but the SENTENCE binds the "
                        + "instance's own value, so this number never reaches a reader directly."),
                ]),
            };

        /// <summary>
        /// The PIR block only reacts to motion while its wired twilight input is ON, so a source that never
        /// turns ON makes the block appear dead — a wired-but-inert <c>Skumring</c> pin reads in the field as a
        /// broken PIR, and nothing is broken.
        /// <para>
        /// THE SENTENCE IS A CONSEQUENCE TO VERIFY, NOT A FAULT. Whether the linked source ever turns ON is a
        /// runtime question about another part of the installation, and the file cannot answer it. So the
        /// message states the gating behaviour and what it looks like when the gate stays shut, and stops —
        /// which is also why the row is Information: a gated PIR is exactly what this block is for.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> with <c>master_type="1.4.02"</c> whose input pin named
        /// <c>Skumring</c> carries a <c>link_to_resource</c>. The pin name is EXACT: the mirrored
        /// <c>1.4.02.ifb</c> ships inputs <c>PIR</c>, <c>Skumring</c>, <c>Tilbagemelding</c>, <c>Natdrift</c>,
        /// <c>Kip</c>, <c>Tænd</c>, <c>Sluk</c> and <c>Spærring</c>.
        /// SUBJECT: <c>functionblock</c> elements of that master type.
        /// EXCLUSION: an UNWIRED <c>Skumring</c> pin, which gates nothing — every instance of this block type
        /// has the pin, so wiring rather than existence is the condition.
        /// ALSO EXCLUDED BY CONSTRUCTION, and this must be written down rather than found later as a coverage
        /// hole: a block whose <c>master_type</c> was STRIPPED. Unlock and save-as remove it, so this row and
        /// <c>fb-pulse-constant-default</c> go silent on exactly the population
        /// <c>fb-provenance-rewritten</c> reports. That is correct — the rule cannot know which master an
        /// unlocked block came from, and guessing from pin names would report any block that happened to name a
        /// pin <c>Skumring</c>.
        /// LOCATION: the function block.
        /// ARGUMENTS: <c>name</c> — the block's name.
        /// </summary>
        private static ProblemCatalogEntry FbPirDuskGated =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-pir-dusk-gated"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_type"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "PIR-blokken '{name}' reagerer kun på bevægelse, mens dens skumringsindgang er tændt, så en "
                + "tilslutning der aldrig bliver tændt får blokken til at virke død.")
            {
                Diagnostic = "PIR block '{name}' only reacts to motion while its wired twilight input is ON; a "
                    + "source that never turns ON makes the block appear dead.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The block is frozen at one revision while the library ships another: behaviour can change materially
        /// between revisions of the same nominal block, and swapping is a manual re-commissioning job rather
        /// than a drop-in.
        /// <para>
        /// IT FIRES IN BOTH DIRECTIONS. Older than the library and newer than it are the same finding — what
        /// matters is that the two disagree, not which way. A rule reporting only "behind" would say nothing
        /// about a project carrying a revision the installed library has since dropped, which is the case a
        /// reader is least likely to have noticed.
        /// </para>
        /// <para>
        /// THE FREEZE RULE ITSELF IS NOT A ROW. "A library upgrade never touches a placed instance" is true of
        /// every project and would report every block in every file; the source withdrew it for exactly that
        /// reason. Only the MISMATCH fires, and the sentence states the freeze as the consequence of the
        /// mismatch rather than as the finding.
        /// </para>
        /// <para>
        /// THE LIBRARY MAY HOLD SEVERAL VERSIONS OF ONE TYPE, which is why the port answers plurally and why a
        /// block matching ANY held revision is in sync. Assuming a single version would report a perfectly
        /// current block whenever the library shipped a second revision of its type.
        /// </para>
        /// <para>
        /// A VERSION-LESS BLOCK IS IN SCOPE, and has to be: a large minority of the built-in library's entries
        /// ship a <c>master_type</c> and no <c>master_version</c> — <c>4.1.01</c> and <c>4.1.04</c> among them,
        /// both in the committed corpus — so requiring a letter made the row silent about all of them.
        /// An absent version reads as the version-less revision, which is in sync with a library holding that
        /// same form and differs from one holding only lettered ones. The sentence spells it
        /// <i>uden betegnelse</i> on either side rather than binding an empty slot.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> whose <c>master_type</c> the library holds, at a set of versions
        /// not containing the instance's <c>master_version</c>.
        /// SUBJECT: every <c>functionblock</c> carrying a <c>master_type</c>, whether or not it carries a
        /// <c>master_version</c>.
        /// EXCLUSION: a type the library does not hold at all, which is
        /// <c>fb-master-missing-from-library</c>'s row — a type that is gone has no revision to differ from.
        /// Skipped without a library, like its sibling.
        /// LOCATION: the function block.
        /// ARGUMENTS: <c>name</c>, <c>frozen</c> and <c>library</c> — the three facts the reader compares.
        /// <c>library</c> names EVERY revision the library holds, not an arbitrary one of them.
        /// NO CORPUS WITNESS: every master identity the committed files carry is in the built-in library at
        /// exactly that version, which is the same fact that leaves the sibling row silent.
        /// </summary>
        private static ProblemCatalogEntry FbMasterVersionDiffers =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-master-version-differs"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_version"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("frozen", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("library", ProblemArgumentType.AttributeValue),
                ]),
                "Funktionsblokken '{name}' er indsat som version {frozen}, mens blokbiblioteket nu indeholder "
                + "version {library}, og en indsat blok opdateres aldrig automatisk.")
            {
                Diagnostic = "Function block '{name}' is frozen at revision {frozen} while the library ships "
                    + "{library}; a placed instance is never re-synced from its master.",
                Evidence = EvidenceMark.Authored,
                RequiresLibrary = true,
            };

        /// <summary>
        /// CONDITION: a USER-AUTHORED function block carries a holiday input pin. One field report has the
        /// upload failing against an HW 7.1 controller.
        /// DISTINCT FROM <see cref="LogicHolidayScheduleFirmware"/>, and the source draws the line: that row is
        /// the project depending on the holiday schedule at all and narrows away on firmware 3.3.21; this one is
        /// a custom block carrying a holiday INPUT, with no established fix. A project can draw both.
        /// SUBJECT: "custom" is <see cref="FbUserAuthored"/>'s population, read through the SAME predicate. A
        /// second reading would be a second answer to one question, and the discriminator is subtle — a vendor
        /// block whose flag was stripped keeps its <c>master_name</c> and is not custom.
        /// SCOPE: the INPUT container only. An authentic file carries a holiday resource in each of a block's
        /// four containers, so walking the block rather than its input pins would report one that has no
        /// holiday input at all.
        /// DISPOSITION: Warning on section 8.1's third row — a single field report — even though no fixed
        /// release is established.
        /// LOCATION: the block — <c>OnePerOccurrence</c>, because each is separately re-authorable.
        /// ARGUMENTS: <c>name</c> — the block's name, so the reader can find it in the tree.
        /// </summary>
        private static ProblemCatalogEntry FbHolidayInputCustomBlock =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-holiday-input-custom-block"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", null),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "Den egenudviklede funktionsblok '{name}' har en helligdagsindgang, som er rapporteret at få "
                + "overførslen til controlleren til at mislykkes.")
            {
                Diagnostic = "User-authored function block '{name}' carries a holiday input, which is reported "
                    + "to make the upload to the controller fail.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "CustomBlockHolidayInputUploadFails",
                    null,
                    ThresholdConfidence.Authored,
                    "The upload failed on the reported HW 7.1 controller and no fixed release is established, "
                    + "so the bound carries no FixedIn and no firmware target withholds the finding. Declared "
                    + "rather than omitted to record that the firmware axis was CONSIDERED and found empty. "
                    + "TODO: unconfirmed — a single field report."),
            };

        /// <summary>
        /// CONDITION: the project embeds a block revision the MANUFACTURER confirmed defective — <c>1.1.01.c</c>,
        /// <c>6.3.02.d</c>, or <c>6.3.04</c> below revision <c>b</c>.
        /// <c>6.3.04</c> IS NOT A BARE TYPE. The source names it without a letter, and its remedy — replace with
        /// <c>6.3.04b</c> or later — is what resolves it: the affected revisions are everything BELOW <c>b</c>,
        /// which is <c>a</c> and the version-less form (a real shape; the library ships <c>6.3.05</c> with an
        /// empty version). Neither "every version of the type" nor one named revision would be correct.
        /// ERROR, AND THE EVIDENCE AXIS IS WHY. A defective revision embedded in the project is defective on
        /// every firmware — no controller upgrade rewrites it — and these three carry manufacturer confirmation.
        /// The community-reported revisions ship as a separate WARNING row for exactly that difference, so the
        /// two grades mean something to a reader.
        /// WHAT "CONFIRMED" MEANS, STATED SO THE GRADE IS NOT TAKEN FOR MORE THAN IT IS: LK acknowledged the
        /// defect, and for <c>6.3.02.d</c> supplied the fix. It does NOT mean anyone measured the behaviour on
        /// v3 — the source labels all three generation-unknown, and <c>1.1.01.c</c> additionally as confirmed
        /// historical v2-only. The Error rests on manufacturer confirmation of the DEFECT, not on a v3
        /// measurement.
        /// NO <c>RequiresLibrary</c>, AND THAT IS WHAT MAKES THE ROW SHIPPABLE: a placed block carries
        /// <c>master_type</c> and <c>master_version</c> in the <c>.vis</c>, so which revision the project embeds
        /// is decidable with no library present. Comparing a block's BODY against the library is
        /// <see cref="LogicBlockLockedContent"/>'s job, not this one's.
        /// LOCATION: the block — <c>OnePerOccurrence</c>, because each placement is separately replaceable.
        /// ARGUMENTS: <c>name</c> to find the block by, and <c>master</c> — the revision — because the reader
        /// has to know WHICH revision to replace.
        /// </summary>
        private static ProblemCatalogEntry FbRevisionDefectiveConfirmed =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-revision-defective-confirmed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_version"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("master", ProblemArgumentType.AttributeValue),
                ]),
                "Funktionsblokken '{name}' er indsat som revision {master}, som leverandøren har bekræftet er "
                + "fejlbehæftet; den skal udskiftes med en nyere revision.")
            {
                Diagnostic = "Function block '{name}' is frozen at revision {master}, which the manufacturer "
                    + "confirmed defective; it must be replaced with a later revision.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "DefectiveRevisionEmbeddedInProject",
                    null,
                    ThresholdConfidence.Authored,
                    "Deliberately no FixedIn, and the reason is structural rather than missing evidence: the "
                    + "defective revision is EMBEDDED in the project, so no controller upgrade rewrites it and "
                    + "no firmware target may withhold the finding. Declared rather than omitted to record that "
                    + "the firmware axis was considered and found not to apply."),
            };

        /// <summary>
        /// CONDITION: the project embeds one of the eight block revisions reported defective by the COMMUNITY
        /// rather than confirmed by the manufacturer.
        /// TWO ROWS, ONE SUBJECT, AND THE EVIDENCE IS WHY. <see cref="FbRevisionDefectiveConfirmed"/> covers the
        /// manufacturer-confirmed revisions and grades them Error; this one covers community reports and grades
        /// them Warning. A single row would have to pick one confidence for both populations, which would either
        /// overstate eight field reports or understate three the manufacturer acknowledged. The two DANISH
        /// SENTENCES differ too — one says <i>bekræftet</i>, this one <i>rapporteret</i> — so a reader knows
        /// which population a finding came from without consulting the catalogue.
        /// WHY A ROW NO AUTHENTIC FILE TRIGGERS IS STILL WORTH SHIPPING: these reports are mostly v2-only, so a
        /// v3 project reaches such a revision only by having been MIGRATED from v2 — which is exactly the case
        /// where nobody remembers which revisions came along.
        /// NO <c>RequiresLibrary</c>: the embedded revision is in the <c>.vis</c>, as for the sibling row.
        /// LOCATION: the block — <c>OnePerOccurrence</c>, because each placement is separately replaceable.
        /// ARGUMENTS: <c>name</c> to find the block by, and <c>master</c> — the revision to replace.
        /// </summary>
        private static ProblemCatalogEntry FbRevisionDefectiveReported =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-revision-defective-reported"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_version"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("master", ProblemArgumentType.AttributeValue),
                ]),
                "Funktionsblokken '{name}' er indsat som revision {master}, som er rapporteret fejlbehæftet af "
                + "andre brugere; overvej at udskifte den med en nyere revision.")
            {
                Diagnostic = "Function block '{name}' is frozen at revision {master}, which other users have "
                    + "reported as defective; consider replacing it with a later revision.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "ReportedDefectiveRevisionEmbeddedInProject",
                    null,
                    ThresholdConfidence.Authored,
                    "Deliberately no FixedIn, for the same structural reason as the manufacturer-confirmed row: "
                    + "the defective revision is EMBEDDED in the project, so no controller upgrade rewrites it "
                    + "and no firmware target may withhold the finding."),
            };

        /// <summary>
        /// CONDITION: a block at revision <c>1.2.03.d</c> whose <i>Max tid for kort tryk</i> setting is BELOW
        /// the block's own 0,4 s default.
        /// THE TRAP WORTH SHIPPING: <c>1.2.03.d</c> is the revision that <c>1.2.03.c</c>'s remedy recommends as
        /// ITS fix, so a user following one piece of advice lands squarely on this one. That cross-reference
        /// lives HERE and in the diagnostic, never in the Danish sentence — a user-facing message that explains
        /// the catalogue's internal cross-references is telling the reader about the tool rather than about
        /// their project.
        /// A CONJUNCTION, so both halves are excluded separately. The revision alone is not the condition —
        /// <c>1.2.03.d</c> at or above the default is a perfectly good block — and the value alone is not
        /// either, since another revision at 0,2 s is unaffected.
        /// THE BOUNDARY IS INCLUSIVE: 0,4 s IS the default, so a block sitting exactly on it is untouched and
        /// only something strictly below it reports.
        /// THRESHOLD: <c>ShortPressDefaultSeconds</c> = 0.4 at <c>VendorDocumented</c> — a shipped block's own
        /// declared default is a tool bound, which is the grade that reserves.
        /// LOCATION: the block — <c>OnePerOccurrence</c>.
        /// ARGUMENTS: <c>name</c>, the configured <c>value</c>, and the <c>default</c> it falls below, so the
        /// reader can act without opening the block. Both are in MILLISECONDS while the threshold is declared in
        /// seconds: the declaration carries the unit the source states, and the message the unit the file
        /// stores, which also keeps the sentence on whole numbers rather than needing a decimal separator.
        /// </summary>
        private static ProblemCatalogEntry FbShortPressBelowDefault =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-short-press-below-default"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_version"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.Number),
                    new ProblemArgumentSlot("default", ProblemArgumentType.Number),
                ]),
                "Funktionsblokken '{name}' har 'Max tid for kort tryk' sat til {value} ms, som er under "
                + "blokkens standardværdi på {default} ms, og korte tryk registreres derfor ikke pålideligt.")
            {
                Diagnostic = "Function block '{name}' at revision 1.2.03.d has its short-press maximum at "
                    + "{value} ms, below the block's own {default} ms default, and short presses are then "
                    + "unreliable. Worth reporting because 1.2.03.d is the revision 1.2.03.c's own remedy "
                    + "recommends: a user following that advice arrives here.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "ShortPressDefaultSeconds",
                        0.4,
                        ThresholdConfidence.VendorDocumented,
                        "The block's own declared default for 'Max tid for kort tryk', which is a tool bound "
                        + "rather than a measurement of this repository's. The comparison is strict: a block "
                        + "sitting exactly on the default is not below it."),
                ]),
                FirmwareBound = new DeclaredFirmwareBound(
                    "ShortPressBelowDefaultUnfixedByFirmware",
                    null,
                    ThresholdConfidence.Authored,
                    "No FixedIn: the condition is a parameter value embedded in the project, so no controller "
                    + "upgrade changes it and no firmware target may withhold the finding."),
            };

        /// <summary>
        /// The block references a master type the available library does not contain at ANY version: whole block
        /// types are dropped between Visual releases with no announcement, and a project depending on one that
        /// is gone cannot be rebuilt from a clean install.
        /// <para>
        /// THE TYPE-ONLY QUESTION IS WHY THE LIBRARY PORT WAS WIDENED. A body lookup keyed on an exact
        /// <c>(type, version)</c> identity cannot answer "absent at every version" — a miss there means only
        /// that this identity is unknown, which is equally true of a type present at another revision. So the
        /// port answers presence separately, and this row asks that question and no other.
        /// </para>
        /// <para>
        /// SKIPPED, NEVER GUESSED. <see cref="ProblemCatalogEntry.RequiresLibrary"/> is declared, so the PROFILE
        /// skips the rule when the caller supplies no library rather than the rule inventing an answer — the
        /// same posture the capacity rows take without controller limits.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> whose <c>master_type</c> is not present at all in the supplied
        /// library.
        /// SUBJECT: every <c>functionblock</c> carrying a <c>master_type</c>.
        /// EXCLUSION: blocks with NO <c>master_type</c>, which are the two provenance rows' populations —
        /// asking a library about a type that was never claimed would be asking the wrong question. And a type
        /// the library holds at a DIFFERENT version, which is <c>fb-master-version-differs</c>'s row and never
        /// this one.
        /// LOCATION: the function block.
        /// ARGUMENTS: <c>name</c> to find the block, <c>master</c> to search a library for the type.
        /// NO CORPUS WITNESS, and that is a fact about the corpus rather than a gap: every master identity the
        /// committed files carry is in the built-in library at exactly that version.
        /// </summary>
        private static ProblemCatalogEntry FbMasterMissingFromLibrary =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-master-missing-from-library"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_type"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("master", ProblemArgumentType.AttributeValue),
                ]),
                "Funktionsblokken '{name}' bygger på mastertypen {master}, som ikke findes i det tilgængelige "
                + "blokbibliotek, og projektet kan derfor ikke genskabes fra en nyinstallation.")
            {
                Diagnostic = "Function block '{name}' references master type {master}, which the available block "
                    + "library does not contain; the project cannot be rebuilt from a clean install.",
                Evidence = EvidenceMark.Authored,
                RequiresLibrary = true,
            };

        /// <summary>
        /// The block was a vendor block whose provenance trio has been stripped: without it the block cannot be
        /// checked against errata or against a fixed revision, and the operation that removed it is
        /// irreversible. Its <c>.ifb</c> should be archived with the project.
        /// <para>
        /// THE EXACT COMPLEMENT OF <see cref="FbUserAuthored"/>, and the two partition the population between
        /// them: that row needs BOTH provenance halves absent, this one needs the NAME present and the trio
        /// gone. No block reports both. Together they cover every block that arrived as a FILE — which is the
        /// point of carrying the archive advice on both halves rather than only on the from-scratch one: a
        /// downloaded block, or one exported with <i>Gem funktionsblok</i>, KEEPS <c>master_name</c> and so signs
        /// as this row, and keying the advice on the other row alone would miss precisely the blocks most likely
        /// to have arrived as files.
        /// </para>
        /// <para>
        /// THE CAUSE IS NAMED AS LIKELY, NEVER AS CERTAIN. Unlock and save-as both produce this shape, and the
        /// source measured that the two commands are not always distinguishable from the file — so the sentence
        /// says <i>typisk</i> and stops there rather than asserting which one ran.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> where <c>master_name</c> is present AND
        /// <c>master_schneider_electric</c>, <c>master_type</c> and <c>master_version</c> are all absent.
        /// SUBJECT: every <c>functionblock</c> element.
        /// EXCLUSION: from-scratch blocks, which are the other row's. A block keeping any part of the trio is
        /// not stripped and is nobody's finding here, so the two provenance rows do not overlap.
        /// LOCATION: the function block.
        /// ARGUMENTS: <c>name</c> — the block's name.
        /// </summary>
        private static ProblemCatalogEntry FbProvenanceRewritten =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-provenance-rewritten"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("functionblock", "master_name"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{name}' var en leverandørblok, men dens oprindelsesoplysninger er fjernet, "
                + "typisk ved oplåsning eller 'Gem funktionsblok', så leverandørens version ikke længere kan "
                + "spores, og blokkens .ifb-fil bør arkiveres sammen med projektet.")
            {
                Diagnostic = "Function block '{name}' was a vendor block whose provenance trio has been stripped "
                    + "(likely by unlock or save-as); the vendor revision is no longer traceable, and its .ifb "
                    + "should be archived with the project.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A function block whose program path reaches ITSELF: the recursion works perfectly in the simulator
        /// and does nothing at all on the controller — which is the worst shape a defect can take, because it
        /// tests clean and then fails silently in the field.
        /// <para>
        /// NOT <c>logic-self-trigger</c>, AND THE DIFFERENCE IS THE CONSEQUENCE. That row reports one program
        /// triggered by a variable it also assigns: the ring it finds RUNS, and the controller aborts it. This
        /// row reports a cycle through the block call graph, which silently never executes at all. Two different
        /// runtime outcomes are two rows, whatever their graph shapes have in common — and a reader meeting both
        /// on one project tells them apart by exactly that: one loop ran and was stopped, the other never
        /// started.
        /// </para>
        /// <para>
        /// THE DIRECT SELF-EDGE IS EXCLUDED so nothing is reported twice. A program that triggers itself is the
        /// other row's finding and is subtracted here; without that, every self-trigger in the corpus would gain
        /// a second finding under a code describing a different failure.
        /// </para>
        /// <para>
        /// NO FIRMWARE BOUND — <c>FixedIn</c> null. The defect is unfixed on every firmware the source knows, so
        /// no target however new withholds it, which is section 8.1's first row and why this is an Error.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> that lies on a cycle of the block call graph, where an edge
        /// A → B exists when a program in A writes a variable that triggers a program in B. Cycles of length one
        /// count — two programs in one block can close a loop — but only where some pair of distinct programs
        /// makes the edge, which is what excludes the single-program self-trigger.
        /// SUBJECT: every <c>functionblock</c>, through the run's existing <c>Usage</c> and <c>Topology</c>
        /// analyses. No second traversal of the tree.
        /// EXCLUSION: a one-way chain A → B, which is ordinary composition and the normal way blocks are built;
        /// and the direct self-edge, above.
        /// LOCATION: each block ON the cycle — every one of them is separately the place a reader could break it.
        /// ARGUMENTS: <c>name</c> — which block. The variable closing the loop is deliberately NOT a slot: a
        /// cycle may run through several, and naming one of them would suggest it is the culprit.
        /// </summary>
        private static ProblemCatalogEntry LogicBlockRecursive =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-block-recursive"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Error,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{name}' kan nå sig selv gennem programmerne, og en sådan rekursion udføres "
                + "slet ikke på controlleren, selv om den virker i simulatoren.")
            {
                Diagnostic = "Function block '{name}' can reach itself through the program call graph; such "
                    + "recursion runs in the simulator but does not execute at all on the controller.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "RecursiveBlockNeverExecutes",
                    null,
                    ThresholdConfidence.Authored,
                    "No release is known to fix this, so the bound carries no FixedIn and no firmware target "
                    + "withholds the finding. Declared rather than omitted to record that the firmware axis was "
                    + "CONSIDERED and found empty, which is the distinction Phase 7 draws between a defect with "
                    + "no known fix and a condition firmware has nothing to do with. TODO: unconfirmed — one "
                    + "community report, and no v3 measurement establishes the behaviour on current firmware."),
            };

        /// <summary>
        /// The block was built from scratch: no Visual install will ever re-supply it, so losing its
        /// <c>.ifb</c> means it can never be re-inserted elsewhere. The <c>.vis</c> carries its CONTENTS but not
        /// a reusable file.
        /// <para>
        /// THE DISCRIMINATOR AGAINST <c>fb-provenance-rewritten</c> IS THE WHOLE RISK, and it is not
        /// symmetric. Unlocking a vendor block, or saving one to the library, STRIPS the vendor flag but KEEPS
        /// <c>master_name</c> — so the flag's absence alone does not mean "not an LK block". This row needs BOTH
        /// halves absent, which is the from-scratch signature; a surviving <c>master_name</c> is the other row's
        /// population and never this one's.
        /// </para>
        /// <para>
        /// THE <c>no</c> BRANCH IS DEFENSIVE AND EXPECTED TO BE DEAD. <c>master_schneider_electric="no"</c>
        /// occurs nowhere in the committed corpus: the attribute's DTD default IS <c>no</c>, so
        /// default-omission drops it and only <c>="yes"</c> is ever written. It is kept as an honest read of an
        /// imported file that spells the default out.
        /// </para>
        /// PREDICATE: a <c>functionblock</c> where <c>master_schneider_electric</c> is absent or <c>no</c> AND
        /// <c>master_name</c> is absent.
        /// SUBJECT: every <c>functionblock</c> element.
        /// EXCLUSION: a block whose <c>master_name</c> survives — see the discriminator above.
        /// LOCATION: the function block. The target is <c>default</c> because the predicate is about an ABSENT
        /// attribute, which cannot be an anchor.
        /// ARGUMENTS: <c>name</c> — the block's name.
        /// EXPECT A LARGE CORPUS LOAD: user-built blocks are ordinary rather than exceptional, so this row is
        /// among the catalogue's biggest movers. That is the Information tier working as intended — it says
        /// something worth knowing about a perfectly correct project.
        /// </summary>
        private static ProblemCatalogEntry FbUserAuthored =>
            new ProblemCatalogEntry(
                new ProblemCode("fb-user-authored"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Logic,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "Funktionsblokken '{name}' er egenudviklet og følger ikke med nogen installation af IHC Visual, "
                + "så dens .ifb-fil bør arkiveres sammen med projektet.")
            {
                Diagnostic = "Function block '{name}' is user-built; no Visual install will ever re-supply it, "
                    + "so its .ifb should be archived with the project.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A program statement with no target does nothing that can be modelled — and IHC Visual, handed such a
        /// project, terminates without warning when the program is run.
        /// <para>
        /// RECLASSIFIED (⊘): the vendor editor always writes <c>link1</c> on the three statement tags — present on
        /// every one of the format specification's 6,441 <c>event</c>/<c>condition</c>/<c>action</c> elements
        /// (ch. 07 §7.1) and on all ~19,000 statements across every save in this repository's byte-oracle corpus.
        /// The state arrives only by hand-editing or a third-party writer, which is exactly what the whole-project
        /// face is for and what no commit-time check can ever see.
        /// </para>
        /// <para>
        /// WHY THE SCHEMA CANNOT COVER IT: <c>link1</c> is <c>#IMPLIED</c> in the vendor DTD, so
        /// <c>attr-required</c> can never fire on it — and making it required in the registry would be the wrong
        /// repair twice over. <c>attr-required</c> refuses Save and Export, so a repairable project would become
        /// one that opens and can never be saved again; and a <c>Refusal</c> reports nothing, so the row would
        /// leave the findings list altogether.
        /// </para>
        /// PREDICATE: an element whose tag is exactly <c>event</c>, <c>condition</c> or <c>action</c> carries no
        /// <c>link1</c> attribute. ABSENT, not dangling: a <c>link1</c> naming a missing id is
        /// <c>idref-dangling</c>'s finding. OPEN QUESTION, deliberately left open: whether the null token
        /// <c>link1="_0x0"</c> is the same defect, a different one, or legal here. Only the absent attribute was
        /// measured, and the predicate is not widened past the measurement without a fixture.
        /// SUBJECT: every <c>event</c>, <c>condition</c> and <c>action</c> in every program of every function
        /// block, matched BY EXACT TAG.
        /// EXCLUSION: <c>event_power</c>, which carries no <c>link1</c>, <c>link2</c> or <c>method</c> BY DESIGN
        /// — its element name is the discriminator, because its behaviour is hard-wired rather than selected by a
        /// method number (ch. 07 §7.7). It is authentic vendor output and shares <c>event</c>'s id type code
        /// <c>c8</c> AND its constant <c>icon="_0xc"</c> (ch. 07 §7.2), so a rule matching on the id suffix or on
        /// the icon reports every Powerup event in every authentic file. Also excluded: a statement whose
        /// <c>link1</c> is present but unresolvable, which is <c>idref-dangling</c>'s; and
        /// <c>case_action</c>/<c>program_case</c>, whose references are named <c>variable</c>, <c>value</c> and
        /// <c>link</c> and which carry no <c>link1</c> at all (ch. 07 §7.6.3).
        /// LOCATION: the statement element itself.
        /// ARGUMENTS: <c>tag</c> — which statement kind is broken, so the row can be found in the program; and
        /// <c>block</c> — the function block containing it, which is how the tree is navigated.
        /// </summary>
        private static ProblemCatalogEntry LogicStatementUnlinked =>
            new ProblemCatalogEntry(
                new ProblemCode("logic-statement-unlinked"),
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
                    new ProblemArgumentSlot("block", ProblemArgumentType.AuthoredName),
                ]),
                "Programlinjen <{tag}> i blokken '{block}' peger ikke på nogen ressource.")
            {
                // The crash is attributed to the MEASURED case and must not be reworded to claim it for the other
                // two tags: an <action> in this state was reproduced on two independently started processes, while
                // <event> and <condition> enter the row from the format specification's own check recipe.
                Diagnostic = "A <{tag}> statement in function block '{block}' carries no link1; it references "
                    + "nothing, and a measured <action> in that state terminates IHC Visual 3.4 outright when the "
                    + "program runs.",
                Evidence = EvidenceMark.Refused,
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
                new RuleTarget(null, "cablenumber"),
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
                new RuleTarget(null, "name"),
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
                new RuleTarget(null, "name"),
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
                new RuleTarget(null, "name"),
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
                new RuleTarget(null, "documentation_tag"),
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
                new RuleTarget("resource_input", "note"),
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
        /// The LED dimmer can report its own faults and this project throws that capability away: it exposes
        /// per-channel fault resources and none of them is linked, so a fault never surfaces in the program.
        /// <para>
        /// KEYED ON THE ELEMENT TAGS, NEVER ON THE DANISH NAMES. The format gives four dedicated tags —
        /// <c>rs485_led_dimmer_error_state_overcurrent</c>, <c>_overvoltage</c>, <c>_overheating</c> and
        /// <c>_loadfailure</c> — which are language-independent and not user-editable, while the
        /// <i>Fejl - Overstrøm</i> strings beside them are ordinary <c>name</c> values an author can change. A
        /// name-keyed predicate would both miss a renamed flag and report a dimmer whose ordinary resource
        /// happened to be named like one.
        /// </para>
        /// <para>
        /// THE RESOURCES ARE PER CHANNEL, NOT PER PRODUCT. They sit under each
        /// <c>rs485_led_dimmer_channel</c>, so a two-channel dimmer exposes EIGHT and the condition is "none of
        /// the eight". An earlier draft's wording implied the flags hang off the product; they do not.
        /// </para>
        /// PREDICATE: a <c>product_rs485_led_dimmer</c> none of whose descendant fault-state elements owns a
        /// link half.
        /// SUBJECT: <c>product_rs485_led_dimmer</c> elements, their channel children and the fault resources
        /// beneath them.
        /// EXCLUSION: a dimmer with at least ONE fault resource linked. Partial wiring is a design choice — an
        /// installation may reasonably surface load failure and ignore overheating — and this row is about the
        /// capability being discarded entirely.
        /// LOCATION: the dimmer instance. The fault resources that would be wired sit per channel beneath it,
        /// which is where a reader repairing this goes.
        /// ARGUMENTS: <c>name</c> — which dimmer.
        /// THE CORPUS WITNESSES ONLY THE UNLINKED STATE: every committed dimmer reports, so the negative half of
        /// this predicate is confirmed by unit tests alone.
        /// </summary>
        private static ProblemCatalogEntry Rs485DimmerFaultUnwired =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-dimmer-fault-unwired"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Wiring,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_rs485_led_dimmer", null),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                ]),
                "LED-lysdæmperen '{name}' stiller fejlressourcer for overstrøm, overspænding, overophedning og "
                + "belastningsfejl til rådighed, men ingen af dem er forbundet, så en fejl i dæmperen bliver "
                + "aldrig synlig i programmet.")
            {
                Diagnostic = "LED dimmer '{name}' exposes per-channel fault resources (overcurrent, overvoltage, "
                    + "overheat, load fault) and none is linked; a load fault will never surface to the user.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The RS-485 bus has installation rules no part of the file records — a component ceiling, a
        /// termination requirement and a shield-bonding rule — and sporadic dimmer log entries usually mean
        /// cabling rather than a failing module.
        /// <para>
        /// TERMINATION IS A DISJUNCTION AND BOTH BRANCHES STAY IN THE SENTENCE. The vendor writes: place the SMS
        /// modem last IF ONE EXISTS, <b>or</b> fit a resistor. An earlier draft dropped the SMS branch to keep
        /// the sentence short while keeping the SMS modem in this row's own trigger — which told an SMS-modem
        /// project to fit a resistor the vendor says it does not need.
        /// </para>
        /// <para>
        /// THE LENGTH THRESHOLD GOVERNS BONDING THE SHIELD, NOT WHETHER THE CABLE IS SHIELDED. Shielded cable is
        /// required for this bus unconditionally; what the 10 m decides is whether the shield is connected to the
        /// supply's 0 V. An earlier draft inverted that — "beyond about 10 m requires shielded cable" — and added
        /// an earth-at-one-end rule the vendor document does not state.
        /// </para>
        /// PREDICATE: at least one product sits on the RS-485 bus — the shared <c>RuleAuthoring.Rs485Products</c>
        /// population, which is the LED dimmer, the VOICE MODEM and the SMS modem alike, and the same population
        /// <see cref="CapacityRs485Exceeded"/> counts. Naming only the two families this row was first written
        /// for left a voice-modem-only project measured against a ceiling it had never been told, which is why
        /// the predicate names the population rather than a list of tags.
        /// SUBJECT: the RS-485 bus products.
        /// EXCLUSION: the dual-supply prerequisite that travels with these devices in the source. It is a
        /// separate condition and would need its own sentence; folding it in would make one finding say two
        /// unrelated things.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — there is one bus, however many products sit
        /// on it.
        /// ARGUMENTS: <c>maxdevices</c>, <c>termination</c> and <c>shieldlength</c>, all bound from declared
        /// thresholds, in the order the sentence first uses them.
        /// DELIBERATE OVERLAP with <see cref="CapacityRs485Exceeded"/>: this row publishes the ceiling as a FACT
        /// whenever a bus product exists, that one reports only when the count passes it. Both fire on an
        /// over-limit project — one states the rule, the other the breach — and that is intended rather than
        /// duplication.
        /// </summary>
        private static ProblemCatalogEntry Rs485BusInstallation =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-bus-installation"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("maxdevices", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("termination", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("shieldlength", ProblemArgumentType.Integer),
                ]),
                "RS-485-bussen, som projektets busprodukter sidder på, må højst bære {maxdevices} komponenter "
                + "og skal termineres for enden af strengen — enten af SMS-modulets indbyggede terminering "
                + "eller med en modstand på cirka {termination} ohm — og over cirka {shieldlength} meter "
                + "forbindes kabelskærmen til forsyningens 0 V.")
            {
                Diagnostic = "The RS-485 bus this project declares carries at most {maxdevices} components and "
                    + "must be terminated at the end of the string — by the SMS module's built-in terminator if "
                    + "one sits last, otherwise by a resistor of about {termination} ohm — and beyond about "
                    + "{shieldlength} m the cable shield is bonded to the supply's 0 V.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "Rs485MaxComponents",
                        Rs485BusMaxComponents,
                        ThresholdConfidence.VendorDocumented,
                        "The product's own vendor documentation: \"maks. 32 RS-485-komponenter\" — a second "
                        + "source for the figure capacity-rs485-exceeded cites the tool's own guard box for. "
                        + "The two rows BIND ONE DECLARED CONSTANT rather than restating it, so the ceiling "
                        + "this row publishes and the ceiling that row measures against cannot drift apart."),
                    new DeclaredThreshold(
                        "Rs485TerminationOhm",
                        120,
                        ThresholdConfidence.VendorDocumented,
                        "The same document: terminate the bus at the end of the string, \"placér SMS-modemet "
                        + "sidst, hvis det findes, eller montér en standard RTERM på ca. 120 Ω\". The sentence "
                        + "carries BOTH branches of that choice."),
                    new DeclaredThreshold(
                        "Rs485ShieldBondFromMeters",
                        10,
                        ThresholdConfidence.VendorDocumented,
                        "The same document: \"Ved kabellængde over 10 m forbindes skærmen til strømforsyningens "
                        + "0 V\". What this figure governs is BONDING the shield — shielded cable is required "
                        + "for the bus unconditionally, so the sentence must not make shielding conditional on "
                        + "it."),
                ]),
            };

        /// <summary>
        /// CONDITION: the project places the RS-485 LED dimmer, which suffered persistent link and upload errors
        /// on controller firmware below 03.03.33.
        /// SUBJECT: <c>("product_rs485_led_dimmer", "_0x4409")</c> — the catalogue's ONLY RS-485 LED dimmer, and
        /// it is the two-channel one. The order number the source also uses reaches the same single product.
        /// NARROWING: <c>FixedIn</c> 03.03.33, inclusive, at <c>VendorRecommendation</c> — the vendor states the
        /// release fixed it and this repository has not verified that.
        /// THREE ROWS CAN FIRE ON ONE DIMMER, AND THAT IS INTENDED. <see cref="Rs485BusInstallation"/> is one
        /// statement about the BUS the project puts something on; <see cref="Rs485DimmerPowerfailLevel"/> is
        /// about how THIS dimmer is configured; this row is about the CONTROLLER FIRMWARE the installation runs.
        /// Three independent facts about one device, and a reader who fixes one has not addressed the others.
        /// Stated here so the overlap is read as design rather than discovered later as duplication.
        /// LOCATION: the dimmer — <c>OnePerOccurrence</c>, because each is its own device on the bus.
        /// ARGUMENTS: <c>product</c> — the instance's name, so the reader can find the device in the tree.
        /// </summary>
        private static ProblemCatalogEntry Rs485DimmerFirmwareLinkErrors =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-dimmer-firmware-link-errors"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_rs485_led_dimmer", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "LED-dæmperen '{product}' har vedvarende forbindelses- og overførselsfejl på "
                + "controllerfirmware under 03.03.33.")
            {
                Diagnostic = "LED dimmer '{product}' suffers persistent link and upload errors on controller "
                    + "firmware below 03.03.33.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "Rs485DimmerLinkErrorsFixedIn",
                    new ControllerFirmwareVersion(3, 3, 33),
                    ThresholdConfidence.VendorRecommendation,
                    "The vendor reports persistent link and upload errors below 03.03.33 and states the release "
                    + "fixed them. This repository has NOT verified the fix, which is why the grade is a vendor "
                    + "recommendation rather than a measurement. The bound is inclusive: 03.03.33 itself "
                    + "carries the claimed fix."),
            };

        /// <summary>
        /// CONDITION: ONE scene commands SEVERAL affected RS-485 LED dimmers off at the same time. Only one of
        /// them may respond, because the quick successive channel commands cross-talk.
        /// "OFF" IS DECIDED FROM THE VALUE, NOT FROM A WORD, and that is the exclusion worth stating: a
        /// <c>scene_dimmer</c> row carries a <c>dimming_value</c> and never an on/off token, so off means the
        /// value is zero — the same reading <see cref="SceneAllOff"/> uses. Zero is ALSO the legal floor
        /// <see cref="SceneDimmingOutOfRange"/> accepts, so every row involved here is a perfectly valid row.
        /// This condition is about how many valid rows fire together, not about any one of them being wrong.
        /// "SEVERAL" IS COUNTED OVER DIMMERS, NOT OVER MEMBER ROWS. A dimmer has two channels and each can carry
        /// its own row, so counting rows would report a single device commanded off on both channels — which is
        /// one device responding, exactly the case that works.
        /// LOCATION: the scene — <c>OnePerOccurrence</c>. The scene is the thing to split up, however many
        /// dimmers it commands.
        /// ARGUMENTS: <c>scene</c> and <c>dimmers</c> — the name to find it by, and the count that tells the
        /// reader the scale of what has to be separated.
        /// </summary>
        private static ProblemCatalogEntry Rs485DimmerSceneMultiOff =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-dimmer-scene-multi-off"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("resource_scene", null),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("scene", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("dimmers", ProblemArgumentType.Integer),
                ]),
                "Scenariet '{scene}' slukker {dimmers} LED-dæmpere samtidig, men kun én af dem når at svare.")
            {
                Diagnostic = "Scene '{scene}' commands {dimmers} affected RS-485 LED dimmers off simultaneously; "
                    + "only one can respond, because the quick successive channel commands cross-talk.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "Rs485DimmerSceneCrossTalk",
                    null,
                    ThresholdConfidence.Authored,
                    "No release is known to fix this, so the bound carries no FixedIn and no firmware target "
                    + "withholds the finding. Declared rather than omitted to record that the firmware axis was "
                    + "CONSIDERED and found empty."),
            };

        /// <summary>
        /// CONDITION: an affected RS-485 LED dimmer is driven through SCENARIO RECALL — one of its channels
        /// carries a scene member row.
        /// WHY THE ROW EARNS ITS PLACE: the fix is <i>dimmer</i> firmware 01.01.40, which itself needs controller
        /// CTR.R.03.03.44, and an upload from the application never applies dimmer firmware. The user cannot fix
        /// this from Visual at all, so reporting it is the only way they learn the device needs re-flashing.
        /// NO <c>FixedIn</c>, AND THE ABSENCE IS THE POINT. The narrowing context compares a CONTROLLER version;
        /// a controller at CTR.R.03.03.44 still has an unpatched dimmer, so narrowing on that release would
        /// withhold a finding that still holds. Both versions are in the Danish sentence instead, per the rule
        /// that a bound the context cannot express belongs in the text the user reads.
        /// LOCATION: the dimmer — <c>OnePerOccurrence</c>. Two scene rows on one device are still one device to
        /// re-flash, so the finding is per dimmer rather than per row.
        /// ARGUMENTS: <c>product</c> — the dimmer's name, so the reader can find the device in the tree.
        /// </summary>
        private static ProblemCatalogEntry Rs485DimmerScenarioRecall =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-dimmer-scenario-recall"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_rs485_led_dimmer", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "LED-dæmperen '{product}' styres via scenarier, hvilket kræver dæmperfirmware 01.01.40 "
                + "(som selv kræver CTR.R.03.03.44) — og dæmperfirmware overføres ikke fra programmet.")
            {
                Diagnostic = "LED dimmer '{product}' is driven through scenario recall, which requires dimmer "
                    + "firmware 01.01.40 (itself requiring CTR.R.03.03.44); dimmer firmware is never applied by "
                    + "an upload from the application.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "Rs485DimmerScenarioRecallUnfixableFromVisual",
                    null,
                    ThresholdConfidence.Authored,
                    "Deliberately no FixedIn. The fix is DIMMER firmware 01.01.40, and the narrowing context "
                    + "compares a CONTROLLER version — a controller at CTR.R.03.03.44 still has an unpatched "
                    + "dimmer, so declaring that release would withhold a finding that still holds. The bound is "
                    + "declared rather than omitted to record that the firmware axis was considered and found "
                    + "inexpressible, which is a different fact from a defect no release fixes."),
            };

        /// <summary>
        /// The RS-485 LED dimmer does not retain on/off across a longer power failure: its channels come back at
        /// the configured level. A program that assumes "off after an outage" has to assert it explicitly, which
        /// is the reverse of what every other output does.
        /// <para>
        /// NO EXCLUSION IS POSSIBLE, and the entry says so rather than leaving the line empty: the behaviour is a
        /// property of the PRODUCT, not of a setting the file could inspect, so every placed dimmer reports and
        /// there is nothing a project could do to make it not apply.
        /// </para>
        /// <para>
        /// THE FACTORY LEVEL IS BOUND FROM THE THRESHOLD, never written into the template. It is a vendor number,
        /// and putting it in the Danish sentence as well would be the same fact in two places that can disagree.
        /// Note what the grade does NOT claim: <see cref="ThresholdConfidence.VendorDocumented"/> here says the
        /// vendor publishes the factory default, not that the predicate compares against it — this predicate
        /// compares nothing at all.
        /// </para>
        /// PREDICATE: any <c>product_rs485_led_dimmer</c> instance.
        /// SUBJECT: every <c>product_rs485_led_dimmer</c> element.
        /// EXCLUSION: none, and none is available — see above.
        /// LOCATION: the dimmer instance.
        /// ARGUMENTS: <c>name</c> — which dimmer; <c>level</c> — the factory level it returns at, from the
        /// declaration.
        /// DELIBERATE NEIGHBOURS: <c>rs485-bus-installation</c> and <c>rs485-dimmer-fault-unwired</c> also speak
        /// about a placed dimmer, and <c>capacity-rs485-exceeded</c> counts it. Four rows can touch one device;
        /// each says something the others do not.
        /// </summary>
        private static ProblemCatalogEntry Rs485DimmerPowerfailLevel =>
            new ProblemCatalogEntry(
                new ProblemCode("rs485-dimmer-powerfail-level"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_rs485_led_dimmer", null),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("name", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("level", ProblemArgumentType.Integer),
                ]),
                "LED-lysdæmperen '{name}' husker ikke tænd/sluk-tilstanden efter et længere strømsvigt, men "
                + "vender tilbage på sit konfigurerede niveau, fra fabrikken {level} %.")
            {
                Diagnostic = "LED dimmer '{name}' does not retain on/off across a longer power failure; channels "
                    + "return at the configured level, factory default {level} %.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "Rs485DimmerPowerfailLevel",
                        100,
                        ThresholdConfidence.VendorDocumented,
                        "The product's own vendor documentation states that after a power failure \"kanalerne "
                        + "tænder på det indstillede niveau (fabriksindstilling 100 %)\". Declared rather than "
                        + "written into the sentence because it is a vendor number and the sentence binds it."),
                ]),
            };

        /// <summary>
        /// The Controller Link moves a fixed budget of on/off signals per direction, occupies terminals on both
        /// controllers whether or not the signals are used, and cannot carry an analog value at all — so a design
        /// needing a MEASUREMENT on the other controller needs a different mechanism entirely.
        /// <para>
        /// THE SENTENCE DOES NOT QUANTIFY THE TERMINALS, and that is a correction rather than an omission. The
        /// familiar figure — 16 inputs and 16 outputs on each controller — holds only once a direction is FULLY
        /// POPULATED, which takes two OUT products against one IN: an input module has 16 inputs and an output
        /// module 8 (format specification ch. 04 §4.5). The file cannot tell the reader whether the direction is
        /// populated that way, so the message says the terminals are occupied without counting them.
        /// </para>
        /// <para>
        /// NO SYMMETRY IS ASSERTED EITHER. An earlier draft wrote <i>"optager tilsvarende faste ind- og
        /// udgange"</i>, which claims a symmetry the two products do not have: the OUT def declares 8 outputs and
        /// the IN def 16 inputs. That word is gone. The IN def's <c>Link 11</c>–<c>Link 18</c> naming is likewise
        /// not a gap at 9/10 — it is the vendor's input-terminal display convention, where logical positions 9–16
        /// print as 11–18 — so the sixteen names decode to logical 1–16 with nothing missing.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> carrying either Controller Link <c>product_identifier</c> exists.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: the terminal figure, above. Also the source's contested electrical-polarity disagreement
        /// and its unattributed RS-485-between-controllers error string, neither of which is settled enough to
        /// put in front of a user.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — the budget belongs to the LINK, and a project
        /// holding three link products has one link, not three budgets.
        /// ARGUMENTS: <c>signals</c>, bound FROM the declared threshold so the number in the sentence and the
        /// number in the catalogue cannot differ.
        /// </summary>
        private static ProblemCatalogEntry ControllerLinkBudget =>
            new ProblemCatalogEntry(
                new ProblemCode("controller-link-budget"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("signals", ProblemArgumentType.Integer),
                ]),
                "Controller Link-forbindelsen overfører højst {signals} tænd/sluk-signaler i hver retning, "
                + "optager faste ind- og udgange på begge controllere uanset hvor mange af signalerne der "
                + "bruges, og kan ikke overføre analoge værdier.")
            {
                Diagnostic = "The Controller Link moves at most {signals} on/off signals per direction, "
                    + "permanently occupies inputs and outputs on both controllers whether or not every signal "
                    + "is used, and cannot carry analog values at all.",
                Evidence = EvidenceMark.Authored,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no vendor publication states the figure. Confirm against a datasheet
                    // or by driving a fully populated link.
                    new DeclaredThreshold(
                        "LinkSignalsPerDirection",
                        16,
                        ThresholdConfidence.Authored,
                        "Two independent community reports give 16 signals per direction, corroborated by the "
                        + "module arithmetic of format specification ch. 04 §4.5: an input module has 16 inputs "
                        + "and an output module 8 outputs, so a full 16-signal direction is two OUT products "
                        + "against one IN product — which is exactly why those reports describe the link as "
                        + "blocking 16 inputs AND 16 outputs per controller. No vendor publication states the "
                        + "number. TODO: unconfirmed."),
                ]),
            };

        /// <summary>
        /// The smart sensor is not an analog input: it encodes its reading as a timed pulse train on a plain 24 V
        /// line, and pairing it with the older 24/24 module silently fails because that module does not speak the
        /// pulse protocol.
        /// <para>
        /// IT INFORMS ABOUT THE REQUIREMENT AND DOES NOT CHECK COMPLIANCE, because the file cannot see which
        /// physical input module a sensor lands on: the documentation modules are optional and carry no such
        /// binding. A rule that pretended otherwise would be asserting a wiring fact from a file that does not
        /// contain it — and would be wrong on every project that simply did not document its modules.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> whose <c>product_identifier</c> is one of the six smart-sensor
        /// inputs.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: which module the sensor actually lands on, above. Ordinary inputs — a pushbutton, a PIR —
        /// signal on/off rather than a reading and are not in the set.
        /// LOCATION: the product instance.
        /// ARGUMENTS: <c>product</c> — the instance's name.
        /// DELIBERATE OVERLAP: these six are also <c>migration-untested-product</c>'s sensor group, so a placed
        /// smart sensor reports both. What it needs in order to work at all, and what becomes of it in a
        /// conversion, are different questions with different readers — and the two file under different
        /// categories, which is what keeps them legible as two facts rather than one repeated.
        /// </summary>
        private static ProblemCatalogEntry ProductSensorPulseInput =>
            new ProblemCatalogEntry(
                new ProblemCode("product-sensor-pulse-input"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Sensoren '{product}' er ikke en analog indgang, men sender sin måling som impulser på en "
                + "almindelig 24 V-linje, og den kræver derfor indgangsmodulet 24 V/3 mA.")
            {
                Diagnostic = "Sensor '{product}' is not an analog input; it encodes its reading as a timed pulse "
                    + "train on a plain 24 V line and specifically requires the 24 V/3 mA input module.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The alarm PIR BREAKS its output on motion — normally-closed by design — which is the opposite of what
        /// a lighting block expects, so reusing one for lighting silently inverts the trigger sense.
        /// <para>
        /// THE ORDINARY PIR IS THE EXCLUSION THAT MATTERS, and it is not hypothetical: <c>_0x210e</c> is in the
        /// committed corpus, in two authentic files. Normally-open is the expected case for it, so a rule reading
        /// "any PIR" would report vendor-authored output — which is exactly the failure mode the characterization
        /// corpus exists to catch.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> carrying the alarm PIR's <c>product_identifier</c>.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: the ordinary PIR, above. Also the source's lag and daisy-chain clauses, which are
        /// installation advice — true of the hardware, but not something this project can be read for — and would
        /// turn a one-line correction into a commissioning note.
        /// LOCATION: the product instance.
        /// ARGUMENTS: <c>product</c> — the instance's name.
        /// DELIBERATE OVERLAP: this device is also in <c>migration-untested-product</c>'s alarm group, so a placed
        /// alarm PIR reports both — how its signal behaves, and what becomes of it in a conversion.
        /// </summary>
        private static ProblemCatalogEntry ProductPirAlarmPolarity =>
            new ProblemCatalogEntry(
                new ProblemCode("product-pir-alarm-polarity"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Alarm-PIR'en '{product}' bryder sit signal ved bevægelse, så indgangen går fra tændt til "
                + "slukket modsat en almindelig PIR, og signalet skal derfor typisk inverteres i programmet.")
            {
                Diagnostic = "Alarm PIR '{product}' breaks its output on motion (normally-closed by design), the "
                    + "opposite of what lighting blocks expect; the signal typically needs inverting in the "
                    + "program.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The code keypad's access codes live in the keypad itself: a handover or disaster-recovery plan that
        /// assumes a project backup carries them is wrong.
        /// <para>
        /// A DEVICE-SETTINGS ROW IN A PRODUCT MODULE, and both halves are deliberate. Its CATEGORY is
        /// <see cref="ValidationCategory.DeviceSettings"/> because what it reports is where a setting is stored;
        /// its MODULE is the product-advisory one because its subject is a placed product's published property.
        /// Modules here are organised by subject and categories by what the finding is about, so the two do not
        /// have to agree — and this row is the clearest case of that in the set.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> carrying the code keypad's <c>product_identifier</c>.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: the recovery folklore that goes with this device — a second keypad further along the daisy
        /// chain — which is installation advice rather than a fact about the project, and stays out of the
        /// sentence. The row corrects an assumption; it does not tell an installer how to work.
        /// LOCATION: the product instance.
        /// ARGUMENTS: <c>product</c> — the instance's name.
        /// DELIBERATE OVERLAP: this device is also in <c>migration-untested-product</c>'s alarm group, so a placed
        /// keypad reports both. Where its codes live and what becomes of it in a conversion are two independent
        /// statements about one device.
        /// </summary>
        private static ProblemCatalogEntry ProductKeypadCodesLocal =>
            new ProblemCatalogEntry(
                new ProblemCode("product-keypad-codes-local"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.DeviceSettings,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Adgangskoderne til kodetastaturet '{product}' er gemt i selve tastaturet og hverken i projektet "
                + "eller controlleren, så de følger ikke med en sikkerhedskopi af projektet.")
            {
                Diagnostic = "The access codes for keypad '{product}' live in the keypad itself, not in the "
                    + "project or controller; a project backup does not carry them.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The vendor records this sounder as not approved for statutory warning systems: if programs drive it as
        /// life-safety signalling, that is a compliance question the reader owns.
        /// <para>
        /// THE ROW DOES NOT DECIDE WHETHER THE SOUNDER IS USED FOR LIFE SAFETY, because the file cannot: a
        /// sounder driven by a program is a sounder driven by a program, whatever the installer meant it to
        /// signal, and no attribute records that intent. So it states the vendor's approval status and stops
        /// there. That is also why it is Information rather than a Warning — a Warning asks the author to judge,
        /// and this row is explicitly not asking about the thing that would need judging.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> whose <c>product_identifier</c> is one of the two recorded
        /// unapproved sounders.
        /// SUBJECT: every <c>product_dataline</c> element. This row's <c>Target</c> names the tag and the
        /// attribute together, which the two-root rows above cannot: one family, one attribute.
        /// EXCLUSION: none.
        /// LOCATION: the product instance — each device is its own compliance question.
        /// ARGUMENTS: <c>product</c> — the instance's name, so the reader can find the device.
        /// </summary>
        private static ProblemCatalogEntry ProductSounderNotAlarmApproved =>
            new ProblemCatalogEntry(
                new ProblemCode("product-sounder-not-alarm-approved"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Lydgiveren '{product}' er ifølge leverandøren ikke godkendt til varslingsanlæg og må derfor "
                + "ikke anvendes som lovpligtig varsling.")
            {
                Diagnostic = "Sounder '{product}' is not approved for statutory warning systems per the vendor.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// The vendor states this product cannot currently be reused in a conversion to KNX, and that it is
        /// still investigating whether a replacement will exist: the conversion cost of a project is decided by
        /// exactly these products.
        /// <para>
        /// THE VENDOR STATEMENT IS PROVISIONAL AND THE SENTENCE MUST NOT HARDEN IT. The source prefaces its whole
        /// list <i>"Nedenstående er foreløbige konklusioner, som vi arbejder videre med at forbedre og
        /// validere"</i>, and its three clauses say "cannot currently be replaced or used, still being
        /// investigated". Only ONE of the three groups — the sensors — carries even a recommendation to convert.
        /// An earlier draft rendered all eleven as <i>"må derfor redesignes"</i>, a verdict the vendor never
        /// gave; this sentence is the common denominator the vendor actually wrote.
        /// </para>
        /// <para>
        /// A LIFECYCLE STATEMENT CAN GO STALE, and this one says so about itself: the source is explicit that it
        /// is working to improve and validate its own conclusions. Re-read the letter before changing the set.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> whose <c>product_identifier</c> is in the declared
        /// not-currently-convertible set.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: the REUSABLE half — pushbuttons, link-10 cabling and PIR on/off — which is not a finding at
        /// all and on which nothing fires. The <c>Beo4</c>/<c>Beolink</c> keymaps belong in the set once their
        /// identifiers are resolved, exactly as they belong in
        /// <c>product-ir-generations-mixed</c>'s trigger.
        /// LOCATION: the product instance.
        /// ARGUMENTS: <c>product</c> — the instance's name.
        /// DELIBERATE OVERLAP: the two IR remotes are in this set AND in
        /// <c>product-ir-generations-mixed</c>'s pair, and the smart sensors are also
        /// <c>product-sensor-pulse-input</c>'s subject. Those are independent statements about one device — what
        /// it needs to work, and what becomes of it in a conversion — not one fact reported twice.
        /// </summary>
        private static ProblemCatalogEntry MigrationUntestedProduct =>
            new ProblemCatalogEntry(
                new ProblemCode("migration-untested-product"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' kan ifølge leverandøren for nuværende ikke genbruges ved en konvertering "
                + "til KNX, og leverandøren undersøger fortsat, om der kommer en erstatning.")
            {
                Diagnostic = "Product '{product}' cannot currently be reused in a conversion to KNX per the "
                    + "vendor, which states it is still investigating a replacement.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// Both IR transmitter generations are declared, and they need mutually incompatible receivers: the
        /// installation cannot serve both from one receiver, and the file is silently declaring that conflict.
        /// <para>
        /// CO-OCCURRENCE IS THE ONLY MECHANISABLE FORM of this question, and that is a property of the format
        /// rather than a compromise. The RECEIVER is what the two transmitters disagree about — 507N0034 against
        /// 506D6501 — and a receiver is not a product: it never appears in a project file at all. So the file can
        /// show that both transmitter generations are present and cannot show which receiver is fitted. Either
        /// transmitter ALONE is an ordinary installation with an ordinary receiver behind it, which is why
        /// reporting one on its own would be asserting a hardware conflict from half the evidence.
        /// </para>
        /// PREDICATE: a <c>product_dataline</c> carrying <c>_0x210d</c> AND one carrying <c>_0x211f</c> both
        /// exist in the project.
        /// SUBJECT: every <c>product_dataline</c> element.
        /// EXCLUSION: either identifier alone. Also excluded, for now, the <c>Beo4</c>/<c>Beolink</c> keymap
        /// products: extending the trigger to them needs their identifiers resolved first, and an under-scoped
        /// set would report some mixed installations and not others.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — the PAIR is the condition, so neither device is
        /// the site of it.
        /// ARGUMENTS: none. The sentence is complete: there is exactly one condition it can state, and nothing
        /// varies between two projects that carry the pair.
        /// </summary>
        private static ProblemCatalogEntry ProductIrGenerationsMixed =>
            new ProblemCatalogEntry(
                new ProblemCode("product-ir-generations-mixed"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet indeholder både den ældre IR-fjernbetjening med 16 tryk og den B&O-kompatible "
                + "IR-fjernbetjening med 8 tryk, som forudsætter hver sin indbyrdes inkompatible generation af "
                + "IR-modtager.")
            {
                Diagnostic = "Project declares both IR transmitter generations, which require mutually "
                    + "incompatible IR receiver generations (507N0034 vs 506D6501).",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// CONDITION: the project places the catalogue's 3-key push button, <c>_0x106</c> "Mini Modul 3 tryk".
        /// One field report has an upload aborting partway with the controller left in <i>fejltilstand</i>,
        /// recoverable only by reloading factory-default firmware.
        /// SUBJECT: identified by MEASUREMENT, not by name. Two 3-key products exist, and the other —
        /// <c>_0x2132</c>, the FUGA <i>Betjeningstryk</i> — is what the English source name suggests. The
        /// reporter's own fix decides it: three separate 1-key push buttons in its place. Only
        /// <c>_0x104</c> "Mini Modul 1 tryk" is a 1-key product; the FUGA family runs 2/4/6 keys and has no
        /// 1-key member, so the substitution is possible only inside Mini Modul.
        /// DISPOSITION: Warning, and deliberately not Error. No fixed release is known, which by itself argues
        /// Error — but the report is single-source, and suppression is foreclosed, so an Error would be
        /// permanent and undismissable for every installation that demonstrably works, with no narrowing
        /// firmware context to escape through. Revisit on a second report or a known fix.
        /// EXCLUSION: the reporter's recovery procedure. Reloading factory-default firmware is installation
        /// advice, not a fact about this project, and the sentence does not carry it.
        /// LOCATION: the product instance — <c>OnePerOccurrence</c>, because each placement is separately
        /// replaceable.
        /// ARGUMENTS: <c>product</c> — the instance's name, so the reader can find the device in the tree.
        /// </summary>
        private static ProblemCatalogEntry Product3keyUploadAbort =>
            new ProblemCatalogEntry(
                new ProblemCode("product-3key-upload-abort"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("product_dataline", "product_identifier"),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' er rapporteret at afbryde overførslen til controlleren undervejs og "
                + "efterlade den i fejltilstand.")
            {
                Diagnostic = "Product '{product}' is reported to abort the upload to the controller partway "
                    + "and leave it in a fault state.",
                Evidence = EvidenceMark.Authored,
                FirmwareBound = new DeclaredFirmwareBound(
                    "ThreeKeyPushButtonUploadAbort",
                    null,
                    ThresholdConfidence.Authored,
                    "No release is known to fix this, so the bound carries no FixedIn and no firmware target "
                    + "withholds the finding. Declared rather than omitted to record that the firmware axis was "
                    + "CONSIDERED and found empty. TODO: unconfirmed — a single field report, and no "
                    + "measurement establishes the behaviour on current firmware."),
            };

        /// <summary>
        /// This specific device is recorded discontinued by the vendor: a replacement has to be planned rather
        /// than assumed.
        /// <para>
        /// NINE IDENTIFIERS, NOT TEN, and the tenth is worth naming because an earlier draft included it.
        /// <c>_0x210d</c>, the 16-key IR remote, is NOT discontinued by any source: what the page records is that
        /// its RECEIVER <c>507N0034</c> is sold as a spare part only — and a receiver is not a product and never
        /// appears in a project file. The remote is covered by <c>product-ir-generations-mixed</c> and
        /// <c>migration-untested-product</c>; what it must not do is carry a vendor status no page states.
        /// </para>
        /// <para>
        /// A LIFECYCLE STATEMENT CAN GO STALE. The set is DATA, listed in one named constant beside the rule with
        /// each id's source in the comment, so re-checking it is a matter of re-reading nine pages rather than of
        /// re-deriving a predicate.
        /// </para>
        /// PREDICATE: a product instance whose (root element tag, <c>product_identifier</c>) pair is in the
        /// declared discontinued set.
        /// SUBJECT: every product element. THE KEY IS THE PAIR, per D11: product identifiers are not unique
        /// across root elements in this catalogue, so keying on the identifier alone would report a data-line
        /// product that happens to share a wireless product's number.
        /// EXCLUSION: the fleet-wide 2026 wireless phase-out, which is
        /// <see cref="ProductWirelessPhaseout"/>'s different condition — a wireless product inside that family
        /// but not on this list says nothing here. Products merely old but still sold. And <c>_0x210d</c>, above.
        /// LOCATION: the product instance — <c>OnePerOccurrence</c>, because each device is separately
        /// replaceable, unlike the phase-out's single project-wide decision.
        /// ARGUMENTS: <c>product</c> — the instance's name, so the reader can find the device in the tree.
        /// </summary>
        private static ProblemCatalogEntry ProductDiscontinued =>
            new ProblemCatalogEntry(
                new ProblemCode("product-discontinued"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "Produktet '{product}' er udgået hos leverandøren, og en tilsvarende erstatning kan være svær "
                + "eller umulig at skaffe.")
            {
                Diagnostic = "Product '{product}' is recorded as discontinued by the vendor; a like-for-like "
                    + "replacement may be unobtainable.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// A project standing on IHC Wireless hardware owns a procurement decision: the vendor has announced a
        /// sales stop for the whole family, so replacement units will stop being orderable.
        /// <para>
        /// TWO THINGS THE SENTENCE MUST NOT SAY, both of which an earlier draft got wrong.
        /// It must not claim installed devices STOP WORKING — the vendor publication states a sales stop and
        /// nothing more, and the harm is to spares rather than to the installation. And it must not give the
        /// phase-out a start date: the source says the products are phased out during 2026 and that <i>"datoen
        /// for selve eksekveringen … vil blive meldt ud i god tid"</i>, so the sentence carries the hedge rather
        /// than a date the vendor has not published.
        /// </para>
        /// <para>
        /// A LIFECYCLE STATEMENT CAN GO STALE, which is a property of this row rather than a caveat about it: it
        /// reports a vendor announcement, and vendor announcements are superseded. Re-read the mirrored status
        /// page before changing the wording or the year.
        /// </para>
        /// PREDICATE: at least one <c>product_airlink</c> exists.
        /// SUBJECT: every <c>product_airlink</c> in the project — read through the shared classifier's wireless
        /// test, which is product-guarded, rather than by tag prefix.
        /// EXCLUSION: none.
        /// LOCATION: the project as a whole (<c>OneFinding</c>) — the decision is the project's, not any one
        /// device's, and anchoring on a single product would suggest that product is the problem.
        /// ARGUMENTS: <c>count</c> — how many, so the reader sees the size of the exposure rather than only its
        /// existence.
        /// </summary>
        private static ProblemCatalogEntry ProductWirelessPhaseout =>
            new ProblemCatalogEntry(
                new ProblemCode("product-wireless-phaseout"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.ProjectStructure,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("count", ProblemArgumentType.Integer),
                ]),
                "Projektet indeholder {count} IHC Wireless-produkter, og leverandøren har varslet, at hele IHC "
                + "Wireless-familien udfases i 2026 på en dato, der endnu ikke er meldt ud, hvorefter "
                + "erstatningsenheder ikke længere kan købes.")
            {
                Diagnostic = "Project contains {count} IHC Wireless products; the vendor has announced a sales "
                    + "stop for the whole IHC Wireless family during 2026, with the execution date still to be "
                    + "announced.",
                Evidence = EvidenceMark.Authored,
            };

        /// <summary>
        /// An S0 metering input is a read-out instrument rather than an automation source: its count cannot feed a
        /// function block, and its pulse pair cannot share a terminal with an ordinary 24 V input.
        /// <para>
        /// THE CATALOGUE'S FIRST INFORMATION ROW, and it is the tier the row is FOR. Nothing about a placed S0
        /// meter is wrong — the device is correctly declared, correctly addressed and does exactly what it is
        /// sold to do. What the file cannot show is the design the author may be about to build on it, and by the
        /// time that design is written the terminal's limits are discovered at commissioning. A Warning would ask
        /// the author to judge whether a correct meter is a mistake; §2's Information tier asks for nothing, which
        /// is precisely the register this fact belongs in.
        /// </para>
        /// PREDICATE: any <c>s0_device</c> element. There is no attribute to read and no value to compare: the
        /// device root's PRESENCE is the whole condition, because the limitation is the terminal's, not the
        /// configuration's.
        /// SUBJECT: every <c>s0_device</c> — the S0 metering device root, which is a catalog product whose root
        /// tag carries no <c>product_</c> prefix.
        /// EXCLUSION: nothing about the meter's CONFIGURATION. Whether its pulse count is present and in range is
        /// <c>addr-s0-ticks-missing</c>, which walks the same elements and reads <c>ticks</c>; this row would be
        /// no less true of a perfectly configured meter, and both fire together on a mis-scaled one by design.
        /// LOCATION: the <c>s0_device</c> instance.
        /// ARGUMENTS: the meter's authored name, so a reader can find the terminal in a tree where several may
        /// stand side by side.
        /// </summary>
        private static ProblemCatalogEntry ProductS0InstrumentOnly =>
            new ProblemCatalogEntry(
                new ProblemCode("product-s0-instrument-only"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Addressing,
                CatalogDisposition.Info,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                new RuleTarget("s0_device", null),
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("product", ProblemArgumentType.AuthoredName),
                ]),
                "S0-måleindgangen '{product}' er en særskilt instrumenteringsindgang, hvis tælling ikke kan "
                + "indgå i funktionsblokke og ikke kan deles med et almindeligt indgangsmodul.")
            {
                Diagnostic = "S0 device '{product}' is a galvanically separate instrumentation input; its count "
                    + "cannot feed any function block and its pulse wire cannot be shared with a normal 24 V input.",
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
        /// <c>version_major</c> is above the highest supported major, declared here as
        /// <c>SupportedVersionMajor</c>.
        /// REFUSES: nothing today — and that is the point of this comment.
        /// <para>
        /// <c>ProjectReader</c> checks version PRESENCE only, so a <c>version_major="5"</c> file opens and this
        /// row reports it as an ordinary Error finding. Refusing the open is product decision D13, a ruling
        /// rather than a task, and is tripwired by
        /// <c>LoadRefusalTests.AProjectAboveVersionFourStillOpensToday</c>, which fails the day someone codes the
        /// refusal and forces the decision to be made consciously.
        /// </para>
        /// PREDICATE: root <c>version_major</c> parses to an integer ABOVE the declared
        /// <c>SupportedVersionMajor</c>. Strictly greater, because the supported major itself is what every
        /// committed file carries.
        /// SUBJECT: the root element's <c>version_major</c>.
        /// EXCLUSION: a <c>version_major</c> that is absent or does not parse, which is passed over rather than
        /// guessed at — exactly as <see cref="RootVersionMinor"/> passes over an unparseable minor. A file with
        /// no version at all is <c>ProjectReader</c>'s refusal, not this row's finding.
        /// LOCATION: the root element.
        /// ARGUMENTS: <c>version</c> — the token as written, so the sentence prints what the file says rather
        /// than a re-rendered number.
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
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // Catalogue evidence owns the compared bound instead of an uncited rule literal. The sentence
                    // keeps "version 4": re-slotting it would change the published argument contract and oracles.
                    new DeclaredThreshold(
                        "SupportedVersionMajor",
                        SupportedProjectVersionMajor,
                        ThresholdConfidence.VendorDocumented,
                        "The same scanned-every-file baseline the sibling root-version-minor declares: IHC "
                        + "Visual 03.04.72.03 writes file-format version 4.0, and the format specification "
                        + "(ch. 01 §10) verified version_major='4' by scripted scan of every committed file. "
                        + "Bound from the one declared constant that row's SupportedVersionMajor also binds, "
                        + "because the two rows partition this number between them: above it is this row, at "
                        + "it is that one."),
                ]),
            };

        /// <summary>
        /// A file written by a newer MINOR revision of the format can carry content this model does not know:
        /// opening it is safe, but a save can silently drop that content.
        /// <para>
        /// THE VENDOR ASKS THIS AT LOAD; this row answers it as a finding. IHC Visual's own prompt reads
        /// <i>"Projektet er fra en nyere LK IHC Visual ® version. / Projekt information kan muligvis gå tabt ved
        /// indlæsning. / Indlæs projekt?"</i> with Yes/No. A Warning the panel shows while the project is open
        /// honours the same contract without inventing an interactive load-time surface this SDK does not have.
        /// </para>
        /// <para>
        /// THIS SDK DOES NOT NORMALIZE AN OLDER VERSION, and nothing here may claim it does.
        /// <c>ProjectReader</c> refuses only a MISSING <c>version_major</c>, <c>Project.Version</c> returns the
        /// raw pair, and no writer rewrites either attribute — byte fidelity would forbid it. The silent 3→4
        /// upgrade belongs to IHC Visual, not to this codebase, so the exclusion below rests on the vendor
        /// contract alone.
        /// </para>
        /// PREDICATE: root <c>version_major</c> equals <c>SupportedVersionMajor</c> AND <c>version_minor</c>
        /// parses to an integer above <c>SupportedVersionMinor</c>.
        /// SUBJECT: the root element's version attributes.
        /// EXCLUSION: a major ABOVE the supported one, which is <see cref="RootVersion"/>'s row — reporting both
        /// would say one thing twice. A major BELOW it: the measured vendor contract is current-or-older yes,
        /// newer no, so an older major is accepted input and a minor on top of an already-superseded major says
        /// nothing useful. And a <c>version_minor</c> that does not parse, which this row passes over exactly as
        /// <see cref="RootVersion"/> passes over an unparseable major.
        /// A CONSEQUENCE WORTH STATING RATHER THAN HIDING: because the predicate requires the supported major, a
        /// v3-major file whose minor is ahead reports nothing at all. That is deliberate — the major already
        /// places the file outside what this row is about — but it is a real coverage edge, not a drafting
        /// accident.
        /// LOCATION: the root element.
        /// ARGUMENTS: <c>minor</c> and <c>supported</c> — what was found and what the model speaks. <c>minor</c>
        /// is <c>AttributeValue</c> so the sentence prints the token as written, which is what
        /// <see cref="RootVersion"/> does with its own <c>version</c>.
        /// OVERLAP: unknown elements and attributes such a file carries are ALSO reported by
        /// <c>element-undeclared</c> / <c>attr-undeclared</c>, which refuse the save. This row's value is that it
        /// names the CAUSE at file level even when the newer minor adds nothing the registry trips over — the
        /// case where the loss would otherwise be silent.
        /// </summary>
        private static ProblemCatalogEntry RootVersionMinor =>
            new ProblemCatalogEntry(
                new ProblemCode("root-version-minor"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OneFinding,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("minor", ProblemArgumentType.AttributeValue),
                    new ProblemArgumentSlot("supported", ProblemArgumentType.Integer),
                ]),
                "Projektets formatversion 4.{minor} er nyere end den understøttede 4.{supported}; ukendte "
                + "oplysninger kan gå tabt ved gemning.")
            {
                Diagnostic = "Root version_minor is {minor}, above the supported {supported}; content this model "
                    + "does not know may be silently dropped on save.",
                Evidence = EvidenceMark.Unknown,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    new DeclaredThreshold(
                        "SupportedVersionMinor",
                        0,
                        ThresholdConfidence.VendorDocumented,
                        "IHC Visual 03.04.72.03 writes file-format version 4.0, and the format specification "
                        + "(ch. 01 §10) verified version_major='4' version_minor='0' by scripted scan of every "
                        + "committed file; independent writers are told to emit exactly that."),
                    // Declared rather than written into the predicate: the rule body carries no numeric literal,
                    // whatever the number is for. The sibling root-version declares the same bound under the
                    // same name and binds the same constant, so neither rule holds a version number and the
                    // partition between the two rows cannot come apart.
                    new DeclaredThreshold(
                        "SupportedVersionMajor",
                        SupportedProjectVersionMajor,
                        ThresholdConfidence.VendorDocumented,
                        "The same scanned-every-file baseline: every committed project carries "
                        + "version_major='4'. This row compares the major as well as the minor, so the major is "
                        + "declared here rather than written as a literal in the rule."),
                ]),
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
                RefusedOperations = [OperationCodes.Save],
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
                RefusedOperations = [OperationCodes.Save],
                Diagnostic = "The destination could not be written — locked, read-only, missing, or out of "
                    + "space; nothing was changed on disk.",
                Evidence = EvidenceMark.Unknown,
            };

        /// <summary>
        /// The validation run that had to clear the write did not complete, because a rule threw.
        /// REFUSES: Save · Export, in <c>ProjectAppService.SerializeForSave</c> — ahead of the errors-found
        /// refusal, because a run carrying a fault has no verdict to read and its findings list is short by an
        /// unmeasurable amount. It is its own row rather than the errors-found one: that sentence counts the
        /// blocking errors a user must repair, and a faulted run with none would ask for zero repairs.
        /// </summary>
        private static ProblemCatalogEntry SaveValidationIncomplete =>
            new ProblemCatalogEntry(
                new ProblemCode("save-validation-incomplete"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.FileIntegrity,
                CatalogDisposition.Refusal,
                RuleKind.OperationOutcome,
                RuleFaces.None,
                default,
                FindingShape.OneFinding,
                default,
                "Projektet blev ikke gemt: kontrollen kunne ikke gennemføres.")
            {
                RefusedOperations = [OperationCodes.Save],
                Diagnostic = "A rule threw during the validation run that had to clear this write, so the "
                    + "findings are incomplete and the write is abandoned; the faults name the rules that failed.",
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
        /// A light level past 100 % means nothing to any dimmer — and the vendor tool silently zeroes it the
        /// first time the member's dialog is committed, so the value the author wrote quietly becomes 0.
        /// <para>
        /// RECLASSIFIED (⊘): the vendor's <i>Lysniveau</i> spinner cannot express an out-of-range value — it
        /// stops at 100 and does not wrap — so the state arrives by hand-edit or a defective writer. What the
        /// file layer CAN do is carry it: the tree renders <c>150%</c> quite happily.
        /// </para>
        /// <para>
        /// A WARNING, NOT AN ERROR, and §2's axis is what decides it. The demonstrated harm is the vendor
        /// dialog's silent zeroing on commit, and controller behaviour is explicitly untested. An Error's
        /// consequence must hold whatever the author intended; this one depends on which tool touches the row
        /// next, which is exactly the dependence that makes a finding advisory.
        /// </para>
        /// PREDICATE: a scene member's <c>dimming_value</c> parses to an integer below <c>DimmingMinimum</c> or
        /// above <c>DimmingMaximum</c>. Both bounds are inclusive: 0 and 100 are legal light levels.
        /// SUBJECT: every scene member row carrying a <c>dimming_value</c> attribute — in practice
        /// <c>scene_dimmer</c>, which is the only member kind the format gives one.
        /// EXCLUSION: a member with NO <c>dimming_value</c>, or one whose value does not parse. Unset is a
        /// different state and other rows own unwired and empty members; reading a missing attribute as 0 would
        /// report every relay row in the corpus. Relay and shutter rows carry no light level at all. Also
        /// excluded: <c>ramptime_ms</c> oddities, which are <c>scene-long-delay</c>'s axis — a sub-second ramp is
        /// a representability problem in the dialog rather than a range violation.
        /// LOCATION: the member row.
        /// ARGUMENTS: <c>member</c> — which row to repair; <c>value</c>, <c>minimum</c> and <c>maximum</c> — the
        /// violation and the legal range.
        /// WHY THE FLOOR IS A SLOT, where the model row <c>dataline-address-range</c> hard-codes its <c>1-</c>
        /// into the template and declares only the ceiling: this floor is <c>Authored</c> rather than measured,
        /// so it is a number a reader may reasonably want to see change.
        /// WHY <c>value</c> IS <c>Integer</c> HERE and <c>AttributeValue</c> on the sibling
        /// <c>dev-inivalue-out-of-range</c>: <c>dimming_value</c> is a whole percent — DTD default <c>"0"</c> —
        /// that an <c>Integer</c> slot renders faithfully, while an <c>inivalue</c> can be a decimal form such as
        /// <c>150.00</c> that an <c>Integer</c> would silently reformat.
        /// </summary>
        private static ProblemCatalogEntry SceneDimmingOutOfRange =>
            new ProblemCatalogEntry(
                new ProblemCode("scene-dimming-out-of-range"),
                ProblemCatalogSection.ProjectFindings,
                ValidationCategory.Scenes,
                CatalogDisposition.Warning,
                RuleKind.UserContentRule,
                RuleFaces.WholeProject,
                default,
                FindingShape.OnePerOccurrence,
                EquatableArray.Create<ProblemArgumentSlot>(
                [
                    new ProblemArgumentSlot("member", ProblemArgumentType.AuthoredName),
                    new ProblemArgumentSlot("value", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("minimum", ProblemArgumentType.Integer),
                    new ProblemArgumentSlot("maximum", ProblemArgumentType.Integer),
                ]),
                "Scenemedlemmet '{member}' har lysniveauet {value} %; det gyldige område er {minimum}-{maximum} %.")
            {
                Diagnostic = "Scene member '{member}' carries dimming_value {value}, outside {minimum}-{maximum}; "
                    + "IHC Visual's properties dialog cannot represent it and silently zeroes it on commit.",
                Evidence = EvidenceMark.Refused,
                Thresholds = EquatableArray.Create<DeclaredThreshold>(
                [
                    // TODO: unconfirmed — no source probes the lower bound. Confirm by driving the spinner
                    // downward, or by a controller measurement of what a negative level does.
                    new DeclaredThreshold(
                        "DimmingMinimum",
                        0,
                        ThresholdConfidence.Authored,
                        "No source probes the lower bound: the vendor spinner was driven UPWARD only, and 0 is "
                        + "the attribute's DTD default — the off state — rather than a measured floor. "
                        + "TODO: unconfirmed."),
                    new DeclaredThreshold(
                        "DimmingMaximum",
                        100,
                        ThresholdConfidence.VendorDocumented,
                        "The vendor's Lysniveau spinner does not advance past 100 and does not wrap: the tool's "
                        + "own bound, measured against the running application (format specification ch. 08 "
                        + "§8.4)."),
                ]),
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
            BackupRetainedCount,
            CapacityModemMultiple,
            CapacityModulesExceeded,
            CapacityInputModules,
            CapacityOutputModules,
            CapacityAddresses,
            CapacityInputAddresses,
            CapacityOutputAddresses,
            CapacityResourcesHigh,
            CapacityRs485Exceeded,
            CapacityS0Multiple,
            CapacityVoicemodemDimmerConflict,
            CapacityScenariosPerReceiver,
            CapacityWirelessExceeded,
            CapacityWirelessLinksPerUnit,
            Containment,
            ControllerLinkBudget,
            DatalineAddressDuplicate,
            DatalineAddressMalformed,
            DatalineAddressRange,
            DevBackupMissing,
            DevDimmerFadeZero,
            DevDimmerLoadModeAuto,
            DevDimmerMaxZero,
            DevDimmerRangeInverted,
            DevInivalueOutOfRange,
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
            EnumInivalue,
            EnumTypedef,
            ExportControllerDeclined,
            FbHolidayInputCustomBlock,
            FbLocalRef,
            FbMasterMissingFromLibrary,
            FbMasterVersionDiffers,
            FbPinContainer,
            FbPirDuskGated,
            FbRevisionDefectiveConfirmed,
            FbRevisionDefectiveReported,
            FbShortPressBelowDefault,
            FbPulseConstantDefault,
            FbProvenanceRewritten,
            FbUserAuthored,
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
            LinkFbInputUnfed,
            LinkFbOutputUnused,
            LinkOutputMultidriven,
            LinkPassThrough,
            LinkProductUnwired,
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
            LogicBlockRecursive,
            LogicCaseDuplicateValue,
            LogicCaseNoBranches,
            LogicCaseValueForeign,
            LogicCounterNeverReset,
            LogicDuplicateProgram,
            LogicFlagNeverCleared,
            LogicHolidayScheduleFirmware,
            LogicOutputNeverAssigned,
            LogicProgramNoActions,
            LogicProgramNoEvents,
            LogicSelfTrigger,
            LogicStatementUnlinked,
            LogicSubprogramNoConditions,
            LogicVariableReadOnly,
            LogicVariableWriteOnly,
            LuidCeiling,
            LuidLow,
            LuidMalformed,
            MigrationUntestedProduct,
            NameCableNumberDuplicate,
            NameDefault,
            NameDuplicateSiblings,
            NameEmpty,
            NameIdCodeDuplicate,
            NameNoteMissing,
            NamePowerGroupVariant,
            Product3keyUploadAbort,
            ProductDiscontinued,
            ProductIrGenerationsMixed,
            ProductKeypadCodesLocal,
            ProductPirAlarmPolarity,
            ProductS0InstrumentOnly,
            ProductSensorPulseInput,
            ProductSounderNotAlarmApproved,
            ProductWirelessPhaseout,
            ProgramShape,
            RootChildren,
            RootVersion,
            RootVersionMinor,
            Rs485BusInstallation,
            Rs485DimmerFaultUnwired,
            Rs485DimmerFirmwareLinkErrors,
            Rs485DimmerPowerfailLevel,
            Rs485DimmerScenarioRecall,
            Rs485DimmerSceneMultiOff,
            SaveRoundtripMismatch,
            SaveTargetUnwritable,
            SaveValidationIncomplete,
            SceneAllOff,
            SceneBijection,
            SceneDimmingOutOfRange,
            SceneDuplicateTarget,
            SceneLongDelay,
            SceneMemberUnwired,
            SceneUnreferenced,
            StructIconDefault,
            StructProductNoTerminals,
            NameHelpfileMissing,
            StructModifiedStale,
        ];
    }
}
