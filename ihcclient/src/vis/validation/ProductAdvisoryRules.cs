using System;
using System.Collections.Immutable;

using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The PRODUCT ADVISORY rows: what a placed product's own datasheet or lifecycle status says about the
    /// hardware, rather than what the file says about itself.
    ///
    /// <para><b>Why these are a module and not a category.</b> Every other rule module here is named for a
    /// question asked OF the project — is it addressed, is it wired, is its program shaped like a program. These
    /// rows ask the project nothing: the file is correct, and the fact worth reporting is a property of the device
    /// the author placed. Their subject is one thing — a placed product — and modules in this layer are organised
    /// by subject, so they share one. Each row still takes the CATEGORY of what it is about, which is why a module
    /// of one subject spans several categories.</para>
    ///
    /// <para><b>The catalogue's first INFORMATION row lands here</b>, and the tier is the point rather than an
    /// implementation detail. §2 separates the tiers by what the author is asked to do: an Error is wrong whatever
    /// was intended, a Warning asks the author to judge, and Information asks for nothing at all. A datasheet fact
    /// about a correctly placed device is the third of those — reporting it as a Warning would put a correct meter
    /// on a punch list.</para>
    /// </summary>
    public static class ProductAdvisoryRules
    {
        /// <summary>
        /// The products the vendor records as DISCONTINUED, keyed by (root element tag,
        /// <c>product_identifier</c>) — the pair D11 requires, because an identifier alone is not unique in this
        /// catalogue.
        /// <para>
        /// NINE, and the absentee is deliberate: <c>_0x210d</c> (the 16-key IR remote) is NOT here. Its status
        /// page records that the RECEIVER <c>507N0034</c> is sold as a spare part only — the receiver is not a
        /// product and never appears in a file — and describes the remote itself as an old IR system rather than
        /// as withdrawn. Two other rows already speak for it.
        /// </para>
        /// <para>
        /// Identifiers read in the <c>.vis</c> UNPADDED spelling (<c>_0x210c</c>), not the padded <c>.def</c>
        /// one. Each id's status is recorded on its own catalogue page; the six wireless ones read
        /// <i>"Udgået; hele IHC Wireless udfases i 2026"</i>, <c>_0x210c</c> reads <i>"Udgået (jf.
        /// katalogsiden)"</i>, and the remaining two sit under the catalogue's own
        /// <i>Specielle produkter / Udgåede produkter</i> folder.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<(string Tag, string Identifier)> DiscontinuedProducts =
        [
            ("product_airlink", "_0x4104"),
            ("product_airlink", "_0x4105"),
            ("product_airlink", "_0x4106"),
            ("product_airlink", "_0x4204"),
            ("product_airlink", "_0x4205"),
            ("product_airlink", "_0x4303"),
            ("product_dataline", "_0x21000004"),
            ("product_dataline", "_0x2114"),
            ("product_dataline", "_0x210c"),
        ];

        /// <summary>The data-line product root — the family both IR remotes belong to.</summary>
        private const string DatalineTag = "product_dataline";

        /// <summary>
        /// The two IR TRANSMITTER generations, which need mutually incompatible RECEIVERS (507N0034 against
        /// 506D6501, datasheet 507D0008).
        /// <para>Only the transmitters are here, because only transmitters are products: a receiver never appears
        /// in a project file, which is exactly why the rule that uses these asks about co-occurrence rather than
        /// about compatibility.</para>
        /// </summary>
        private const string SixteenKeyIrRemote = "_0x210d";

        private const string BeoCompatibleIrRemote = "_0x211f";

        /// <summary>
        /// The data-line sounders whose catalogue pages record them as NOT approved for statutory warning
        /// systems — both carry Status <i>"…ikke godkendt til varslingsanlæg"</i>.
        /// <para>Identifiers alone rather than (tag, identifier) pairs, unlike
        /// <see cref="DiscontinuedProducts"/>: this set belongs to ONE root element, and the rule that uses it
        /// walks that tag only, so the tag half of the key would be constant.</para>
        /// </summary>
        private static readonly ImmutableHashSet<string> UnapprovedSounders = ["_0x2203", "_0x2204"];

        /// <summary>
        /// The code keypad — the device whose access codes are held in the keypad itself rather than in the
        /// project or the controller. Also one of the three alarm products in
        /// <see cref="NotCurrentlyConvertible"/>, which is why a placed keypad reports two rows.
        /// </summary>
        private const string CodeKeypad = "_0x2111";

        /// <summary>
        /// The 3-key push button, "Mini Modul 3 tryk", reported to abort an upload partway.
        /// <para>
        /// THE OTHER 3-KEY PRODUCT IS <c>_0x2132</c>, "LK FUGA Betjeningstryk 3 tast 3 dioder", and it is NOT
        /// the subject — although the source's English "3-key Push Button" points straight at it. What settles
        /// it is the reporter's own fix: three separate 1-KEY push buttons in its place. The catalogue's only
        /// 1-key product is <c>_0x104</c> "Mini Modul 1 tryk"; the FUGA <i>Tryk</i> family runs 2/4/6 keys and
        /// has no 1-key member, so the substitution is possible only within Mini Modul.
        /// </para>
        /// </summary>
        private const string ThreeKeyPushButton = "_0x106";

        /// <summary>
        /// The ALARM PIR, whose output is normally-closed: it breaks on motion where an ordinary PIR makes.
        /// <para>
        /// THE ORDINARY PIR IS <c>_0x210e</c> — one identifier away, and NOT reported. It is in two authentic
        /// corpus files, so anything looser than an exact match here reports vendor-authored output.
        /// </para>
        /// <para>Also one of the three alarm products in <see cref="NotCurrentlyConvertible"/>.</para>
        /// </summary>
        private const string AlarmPir = "_0x210f";

        /// <summary>
        /// The SMART SENSORS: data-line inputs that are not analog at all, but encode a reading as a timed pulse
        /// train on a plain 24 V line, and therefore need the 24 V/3 mA input module rather than the older 24/24.
        /// <para>
        /// The SAME six the vendor's conversion letter reaches through its "lux value from PIR" clause, which is
        /// why <see cref="NotCurrentlyConvertible"/> spreads this set rather than respelling it: two rows say
        /// different things about one group of devices, and a divergence between the two lists would be a bug in
        /// whichever was edited second.
        /// </para>
        /// <para>
        /// DECLARED BEFORE the set that spreads it, and it has to be: a static field initialiser runs in
        /// declaration order, so the other way round this one would spread a null.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> SmartSensors =
            ["_0x2124", "_0x2125", "_0x2135", "_0x2138", "_0x2136", "_0x2139"];

        /// <summary>
        /// The two halves of the CONTROLLER LINK — the product that carries on/off signals between two
        /// controllers.
        /// <para>
        /// THE TWO ARE ASYMMETRIC BY DESIGN, which is why the row that uses this set counts neither: the OUT def
        /// declares 8 <c>dataline_output</c> and the IN def 16 <c>dataline_input</c>, so a full 16-signal
        /// direction is TWO OUT products against ONE IN product. Either half being present is enough to say the
        /// project uses the link.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> ControllerLinkProducts = ["_0x2704", "_0x2705"];

        /// <summary>
        /// The data-line products the vendor states cannot CURRENTLY be reused in a conversion to KNX, from
        /// Schneider's <i>IHC informationsbrev 1 – 2023</i>.
        /// <para>
        /// CITE LETTER 1, NOT LETTER 2. The source material attributes the alarm and IR-link clauses to letter 2,
        /// and letter 2 contains neither: both are in letter 1 throughout.
        /// </para>
        /// <para>
        /// THE SENSOR GROUP IS A PROXY, not a set the vendor enumerated. What the letter says cannot be used is
        /// the temperature sensors and <i>"lux value from PIR"</i> — a capability, not a product list — and
        /// <see cref="SmartSensors"/> is the closest file-detectable stand-in for it, because no catalogue
        /// product carries a lux output except <c>_0x2136</c> and <c>_0x2139</c>. The alarm and IR groups ARE
        /// enumerated as such.
        /// </para>
        /// <para>
        /// The letter calls its own contents <i>"foreløbige konklusioner, som vi arbejder videre med at forbedre
        /// og validere"</i>, so this set can go stale by the vendor's own account. Re-read the letter before
        /// changing it.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> NotCurrentlyConvertible =
        [
            // The proxy for "temperature, and lux value from PIR" — the same six the pulse-input row walks.
            .. SmartSensors,
            // IHC Alarm: "kan for nuværende ikke erstattes, men undersøges fortsat nærmere".
            "_0x210a", AlarmPir, CodeKeypad,
            // IHC IR-link: the same clause, verbatim, for IR.
            SixteenKeyIrRemote, BeoCompatibleIrRemote,
        ];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "product-s0-instrument-only", S0InstrumentOnly),
                Rule(catalog, "product-wireless-phaseout", WirelessPhaseout),
                Rule(catalog, "product-discontinued", Discontinued),
                Rule(catalog, "product-ir-generations-mixed", IrGenerationsMixed),
                Rule(catalog, "product-sounder-not-alarm-approved", SounderNotAlarmApproved),
                Rule(catalog, "migration-untested-product", MigrationUntested),
                Rule(catalog, "product-keypad-codes-local", KeypadCodesLocal),
                Rule(catalog, "product-3key-upload-abort", ThreeKeyUploadAbort),
                Rule(catalog, "product-pir-alarm-polarity", PirAlarmPolarity),
                Rule(catalog, "product-sensor-pulse-input", SensorPulseInput),
                Rule(catalog, "controller-link-budget", LinkBudget(catalog)),
                Rule(catalog, "rs485-dimmer-powerfail-level", DimmerPowerfailLevel(catalog)),
                Rule(catalog, "rs485-bus-installation", BusInstallation(catalog)),
                Rule(catalog, "rs485-dimmer-firmware-link-errors", DimmerFirmwareLinkErrors));
        }

        /// <summary>
        /// The project puts something on an RS-485 bus, which carries installation rules the file does not
        /// record: a component ceiling, a termination requirement and a shield-bonding length.
        /// <para>ONE FINDING FOR THE PROJECT: there is one bus, however many products sit on it.</para>
        /// <para>All three numbers are read from the entry. The ceiling is the SAME number
        /// <c>capacity-rs485-exceeded</c> compares against — that row reports the breach, this one publishes the
        /// rule — so both read a declaration rather than either holding a literal.</para>
        /// <para>AND THE SAME POPULATION, through <see cref="Rs485Products"/>: a project whose only bus device is
        /// the VOICE MODEM is a bus the breach row counts, so it is a bus this row has to publish the rules for.
        /// Naming the two families this row happened to be written for left that project told nothing about
        /// termination, shielding or the component ceiling it is measured against.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its three declared numbers, are declared in.</param>
        private static ProjectInspection BusInstallation(ProblemCatalog catalog)
        {
            int maxDevices = (int)Threshold(catalog, "rs485-bus-installation", "Rs485MaxComponents");
            int termination = (int)Threshold(catalog, "rs485-bus-installation", "Rs485TerminationOhm");
            int shieldLength = (int)Threshold(catalog, "rs485-bus-installation", "Rs485ShieldBondFromMeters");
            return inspection =>
            {
                if (Rs485Products(inspection.Analyses).Any())
                {
                    inspection.Report(null, Arguments(
                        ("maxdevices", maxDevices), ("termination", termination),
                        ("shieldlength", shieldLength)));
                }
            };
        }

        /// <summary>
        /// An RS-485 LED dimmer, which comes back at its configured level after a longer outage rather than
        /// remembering whether it was on.
        /// <para>NO CONDITION TO CHECK: the behaviour belongs to the product, so every placed dimmer reports.
        /// The factory level is read from the entry rather than written here, for the same reason it is a
        /// placeholder in the sentence rather than a number.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared factory level, are declared in.</param>
        private static ProjectInspection DimmerPowerfailLevel(ProblemCatalog catalog)
        {
            int level = (int)Threshold(catalog, "rs485-dimmer-powerfail-level", "Rs485DimmerPowerfailLevel");
            return inspection =>
            {
                foreach (ProjectElement dimmer in inspection.Analyses.WithTag(Rs485LedDimmerTag))
                {
                    inspection.Report(dimmer, Arguments(("name", Name(dimmer)), ("level", level)));
                }
            };
        }

        /// <summary>
        /// The project uses a Controller Link, whose signal budget, terminal cost and inability to carry an
        /// analog value are all fixed properties of the mechanism.
        /// <para>ONE FINDING FOR THE PROJECT: the budget belongs to the LINK, not to each product. A full
        /// direction is two OUT products against one IN, so counting products would report a number that means
        /// nothing.</para>
        /// <para>The signal count is BOUND FROM the entry rather than written into the sentence, so the number a
        /// reader sees and the number the catalogue declares cannot differ.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared budget, are declared in.</param>
        private static ProjectInspection LinkBudget(ProblemCatalog catalog)
        {
            int signals = (int)Threshold(catalog, "controller-link-budget", "LinkSignalsPerDirection");
            return inspection =>
            {
                if (inspection.Analyses.WithTag(DatalineTag).Any(product =>
                    product.GetAttribute(ProductIdentifierAttribute) is { } identifier
                    && ControllerLinkProducts.Contains(identifier)))
                {
                    inspection.Report(null, Arguments(("signals", signals)));
                }
            };
        }

        /// <summary>
        /// A smart sensor, which needs the pulse-capable input module rather than the older one.
        /// <para>IT STATES A REQUIREMENT AND CHECKS NOTHING: which module the sensor actually lands on is not in
        /// the file — the documentation modules are optional and bind nothing — so compliance is not decidable
        /// here and the row does not pretend it is.</para>
        /// </summary>
        private static void SensorPulseInput(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, SmartSensors.Contains);

        /// <summary>
        /// An alarm-grade PIR, whose output BREAKS on motion where a lighting block expects it to make.
        /// <para>THE ORDINARY PIR IS NOT REPORTED, and it sits one identifier away
        /// (<see cref="AlarmPir"/>'s comment names it). It is in two authentic corpus files, so a predicate that
        /// matched "PIR" rather than this exact identifier would report vendor-authored output.</para>
        /// </summary>
        private static void PirAlarmPolarity(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, identifier => identifier == AlarmPir);

        /// <summary>
        /// The RS-485 LED dimmer, which suffered persistent link and upload errors below controller firmware
        /// 03.03.33.
        /// <para>THE CATALOGUE HAS ONE RS-485 LED DIMMER, so the identifier check is a guard against a future
        /// second one rather than a discrimination the corpus needs today.</para>
        /// <para>THIS IS THE THIRD ROW THAT CAN FIRE ON ONE DIMMER, and the entry says why that is intended:
        /// the bus row is about the bus, the power-fail row about this dimmer's configuration, and this one
        /// about the controller firmware. The narrowing is the profile's business, not this predicate's.</para>
        /// </summary>
        private static void DimmerFirmwareLinkErrors(IProjectInspection inspection) =>
            ReportProducts(inspection, Rs485LedDimmerTag, identifier => identifier == Rs485LedDimmerId);

        /// <summary>
        /// The 3-key push button reported to abort an upload partway and leave the controller in a fault state.
        /// <para>THE SUBJECT IS <see cref="ThreeKeyPushButton"/> AND NOT THE OTHER 3-KEY PRODUCT, which is the
        /// whole difficulty of this row: the catalogue holds two, and the one the English source name suggests
        /// is the wrong one. Its comment carries how the pair was told apart.</para>
        /// <para>PER INSTANCE: each placement is separately replaceable.</para>
        /// </summary>
        private static void ThreeKeyUploadAbort(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, identifier => identifier == ThreeKeyPushButton);

        /// <summary>
        /// A code keypad, whose access codes live in the keypad rather than in the project or the controller.
        /// <para>PER INSTANCE: each keypad holds its own codes, so each is its own gap in a backup.</para>
        /// </summary>
        private static void KeypadCodesLocal(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, identifier => identifier == CodeKeypad);

        /// <summary>
        /// A product the vendor states cannot currently be reused in a conversion to KNX.
        /// <para>PER INSTANCE: each one is its own conversion cost, and the reader is sizing a job.</para>
        /// <para>The REUSABLE half of the installation — pushbuttons, cabling, PIR on/off — is not walked at all
        /// and produces nothing: the row reports what does not convert, not what does.</para>
        /// </summary>
        private static void MigrationUntested(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, NotCurrentlyConvertible.Contains);

        /// <summary>
        /// A sounder the vendor records as not approved for statutory warning systems.
        /// <para>IT INFORMS AND DOES NOT JUDGE. Whether this sounder is actually being used for life safety is
        /// not in the file — a sounder driven by a program is a sounder driven by a program — so the rule states
        /// the approval status and leaves the compliance question with the reader.</para>
        /// </summary>
        private static void SounderNotAlarmApproved(IProjectInspection inspection) =>
            ReportProducts(inspection, DatalineTag, UnapprovedSounders.Contains);

        /// <summary>
        /// Both IR transmitter generations in one project: they need mutually incompatible receivers, and no one
        /// receiver serves both.
        /// <para>THE PAIR IS THE CONDITION. Neither identifier says anything alone — the receiver they disagree
        /// about is not a product and never appears in the file, so co-occurrence is the only form of this
        /// question the format can answer.</para>
        /// </summary>
        private static void IrGenerationsMixed(IProjectInspection inspection)
        {
            bool sixteenKey = false;
            bool beoCompatible = false;
            foreach (ProjectElement product in inspection.Analyses.WithTag(DatalineTag))
            {
                switch (product.GetAttribute(ProductIdentifierAttribute))
                {
                    case SixteenKeyIrRemote:
                        sixteenKey = true;
                        break;
                    case BeoCompatibleIrRemote:
                        beoCompatible = true;
                        break;
                }

                if (sixteenKey && beoCompatible)
                {
                    inspection.Report(null, default);
                    return;
                }
            }
        }

        /// <summary>
        /// The shape every per-instance datasheet row shares: walk one product root and report each placement
        /// whose catalogue identifier the row is about.
        /// <para>Stated once because the REPORTING half is what the rows have in common — which element is
        /// anchored and which slot is bound — and a row that spelled it itself could drift from the rest without
        /// any gate noticing. What differs between rows is the identifier test, which is the parameter.</para>
        /// </summary>
        /// <param name="inspection">The run being inspected.</param>
        /// <param name="tag">The product device root the row walks.</param>
        /// <param name="matches">Whether a placement's catalogue identifier is one the row is about.</param>
        private static void ReportProducts(
            IProjectInspection inspection, string tag, Func<string, bool> matches)
        {
            foreach (ProjectElement product in inspection.Analyses.WithTag(tag))
            {
                if (product.GetAttribute(ProductIdentifierAttribute) is { } identifier
                    && matches(identifier))
                {
                    inspection.Report(product, Arguments(("product", Name(product))));
                }
            }
        }

        /// <summary>
        /// A placed device the vendor records as discontinued: its replacement has to be planned.
        /// <para>PER INSTANCE, unlike the family phase-out above: each device is separately replaceable, so each
        /// is separately worth naming.</para>
        /// <para>KEYED ON THE PAIR. See <see cref="DiscontinuedProducts"/> for why the identifier alone will not
        /// do, and for the one identifier deliberately absent from it.</para>
        /// </summary>
        private static void Discontinued(IProjectInspection inspection)
        {
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                if (product.GetAttribute(ProductIdentifierAttribute) is { } identifier
                    && DiscontinuedProducts.Contains((product.Tag, identifier)))
                {
                    inspection.Report(product, Arguments(("product", Name(product))));
                }
            }
        }

        /// <summary>
        /// The project stands on IHC Wireless hardware the vendor has announced a sales stop for.
        /// <para>THE COUNT IS THE ARGUMENT and the presence is the predicate: one wireless product is enough to
        /// own the decision, and how many there are is what tells the reader the size of it.</para>
        /// <para>Through <see cref="ProductClassifier.IsWireless"/>, which is product-guarded — not through a
        /// tag test of its own, and not through <c>Classify</c>, whose open-world fallback would answer for a
        /// wireless product's CHILDREN as well as for the product.</para>
        /// </summary>
        private static void WirelessPhaseout(IProjectInspection inspection)
        {
            int wireless = WirelessProducts(inspection.Analyses).Count();
            if (wireless > 0)
            {
                inspection.Report(null, Arguments(("count", wireless)));
            }
        }

        /// <summary>
        /// An S0 metering input is a read-out instrument, not an automation source: its count cannot feed a
        /// function block and its pulse pair cannot share a terminal with an ordinary 24 V input.
        /// <para>PRESENCE IS THE WHOLE PREDICATE. There is no attribute to read and no value to compare, because
        /// the limitation belongs to the terminal rather than to how it was configured — which is also why this
        /// row stays true of a meter that <c>addr-s0-ticks-missing</c> is perfectly happy with.</para>
        /// </summary>
        private static void S0InstrumentOnly(IProjectInspection inspection)
        {
            foreach (ProjectElement meter in inspection.Analyses.WithTag(S0DeviceTag))
            {
                inspection.Report(meter, Arguments(("product", Name(meter))));
            }
        }
    }
}
