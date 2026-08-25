#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The five remaining ADDRESSING rows: whether a wireless device is identified, whether two of them claim one
    /// address, whether a modem can reach anybody, whether an S0 meter can scale its readings, and whether a
    /// telephone number the modem holds is one it can actually dial.
    ///
    /// <para><b>The last of those is DECLARATIVE, and the only one here that is.</b> A phone number is a value
    /// with a predicate over it rather than a walk over the graph, so it registers as a <c>Constrain</c> row and
    /// the same object answers both the whole-project finding and the dialog's "what would be acceptable?".
    /// The other four are traversals and use <c>Inspect</c>.</para>
    ///
    /// <para><b>Every one of these predicates was decided by MEASURING the shipped catalog and the vendor corpus,
    /// not by reading the row.</b> Three defaults would each have made a rule fire on every authentic file:
    /// a wireless product ships with <c>serialnumber="_0x0"</c>, its first pin with <c>address_channel="_0x01"</c>,
    /// and a modem with thirty blank phone-number slots. A rule that treated any of those as a defect would report
    /// on files IHC Visual authored and accepts — which is exactly what the characterization corpus exists to
    /// catch.</para>
    ///
    /// <para><b>The S0 range is DECLARED, not written here.</b> va-ana G6 measured that the pulse count has no
    /// bounds anywhere in this codebase while the vendor refuses one outside 1–10000, so the range lands where
    /// T023 put every other bound: on the catalogue entry, as data the rule reads.</para>
    /// </summary>
    public static class DeviceAddressRules
    {
        /// <summary>The wireless pin families that carry an <c>address_channel</c>.</summary>
        private const string ChannelAttribute = "address_channel";

        /// <summary>The attribute identifying the physical wireless device a product stands for.</summary>
        private const string SerialAttribute = "serialnumber";

        /// <summary>The modem's phone-number slot element, and the attribute one holds.</summary>
        private const string PhoneSlotTag = "sms_modem_phonenumber";

        private const string PhoneNumberAttribute = "phonenumber";

        /// <summary>The S0 meter device and the attribute carrying its pulses-per-unit.</summary>
        private const string MeterTag = "s0_device";

        private const string TicksAttribute = "ticks";

        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "addr-wireless-not-commissioned", NotCommissioned),
                Rule(catalog, "addr-wireless-channel-shared", ChannelShared),
                Rule(catalog, "addr-modem-phonenumber-blank", ModemUnreachable),
                Rule(catalog, "addr-s0-ticks-missing", MeterTicks(catalog)),
                Constraint(catalog, "addr-modem-phonenumber-malformed", (entry, cat) => new PhoneNumberShape(
                    entry.Code,
                    (int)Threshold(cat, entry.Code.Value, "MinPhoneNumberLength"),
                    (int)Threshold(cat, entry.Code.Value, "MaxPhoneNumberLength"))));
        }

        /// <summary>
        /// The declarative sibling of
        /// <see cref="RuleAuthoring.Rule(ProblemCatalog, string, ProjectInspection)"/>: a row whose body is a
        /// value constraint rather than a traversal, so the whole-project face and the field-metadata face
        /// read ONE definition.
        /// </summary>
        private static RuleDefinition Constraint(
            ProblemCatalog catalog,
            string code,
            Func<ProblemCatalogEntry, ProblemCatalog, IValueConstraint> make) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Constrain(make(entry, catalog)).Build()
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>
        /// A wireless product with no serial number, or the placeholder one: the device cannot be bound to the
        /// installation.
        /// <para>SUBJECT: every product declaring the serial attribute. CONDITION: absent, blank, or the null
        /// token — measured, the state every wireless product in every committed vendor fixture is in, because
        /// none of them is commissioned. That is the row's own legitimate reading ("entered during planning,
        /// commissioned later"), so it fires there and is a Warning.</para>
        /// </summary>
        private static void NotCommissioned(IProjectInspection inspection)
        {
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                if (product.GetAttribute(SerialAttribute) is not null && !IsCommissioned(product))
                {
                    inspection.Report(product, Arguments(("product", Name(product))));
                }
            }
        }

        /// <summary>
        /// Two wireless elements in DIFFERENT products whose products share one serial number and whose channel is
        /// the same: both react to the same command.
        /// <para>
        /// TWO EXCLUSIONS, both measured, and without either this rule reports on every authentic file:
        /// </para>
        /// <list type="bullet">
        /// <item><description>An UNCOMMISSIONED product is skipped. A placed wireless product carries the
        /// placeholder serial and the catalog's own <c>_0x01</c> on its first pin, so channel 1 is shared by three
        /// or four products in every vendor fixture measured. Those products are not addressed yet — which
        /// <c>addr-wireless-not-commissioned</c> reports — and a channel index means nothing until they
        /// are.</description></item>
        /// <item><description>Two pins of ONE product are skipped: a shutter product's up/down pins deliberately
        /// reuse their first input's channel, which is the vendor's own encoding rather than a
        /// collision.</description></item>
        /// </list>
        /// <para>So the identity that can actually collide is (serial, channel), which is how a wireless device is
        /// addressed: the serial names the device and the channel names a function within it.</para>
        /// </summary>
        private static void ChannelShared(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            Dictionary<(string Serial, string Channel), (ProjectElement Pin, ProjectElement Product)> seen = [];

            foreach (ProjectElement pin in inspection.Analyses.Elements)
            {
                if (pin.GetAttribute(ChannelAttribute) is not { } channel || !IsAssigned(channel)
                    || NearestProduct(topology, pin) is not { } product
                    || !IsCommissioned(product)
                    || product.GetAttribute(SerialAttribute) is not { } serial)
                {
                    continue;
                }

                (string, string) key = (serial, Normalise(channel));
                if (!seen.TryGetValue(key, out (ProjectElement Pin, ProjectElement Product) first))
                {
                    seen[key] = (pin, product);
                    continue;
                }

                if (!ReferenceEquals(first.Product, product))
                {
                    inspection.ReportGroup(pin, [first.Pin], Arguments(
                        ("pin", Name(pin)), ("other", Name(first.Pin)), ("channel", channel)));
                }
            }
        }

        /// <summary>
        /// A modem with no phone number anywhere: the alarm path is dead.
        /// <para>SUBJECT: every product holding phone-number slots. PER MODEM, not per slot, and that is the
        /// predicate's substance: a modem declares THIRTY slots and an installer fills a few, so a per-slot reading
        /// would report twenty-seven times per modem and state the row's consequence falsely every time — the path
        /// is dead only when no slot carries a number at all.</para>
        /// </summary>
        private static void ModemUnreachable(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            HashSet<ProjectElement> reported = new(ReferenceEqualityComparer.Instance);

            foreach (ProjectElement slot in inspection.Analyses.WithTag(PhoneSlotTag))
            {
                if (NearestProduct(topology, slot) is not { } modem || !reported.Add(modem))
                {
                    continue;
                }

                ImmutableArray<ProjectElement> slots =
                    [.. modem.DescendantsAndSelf().Where(e => e.Tag == PhoneSlotTag)];
                if (slots.All(s => string.IsNullOrWhiteSpace(s.GetAttribute(PhoneNumberAttribute))))
                {
                    inspection.ReportGroup(modem, slots, Arguments(
                        ("modem", Name(modem)), ("slots", slots.Length)));
                }
            }
        }

        /// <summary>
        /// An S0 meter whose pulses-per-unit is missing or outside the declared range: readings cannot be scaled.
        /// <para>SUBJECT: every <c>s0_device</c>. The RANGE is declared on the entry (va-ana G6: the vendor refuses
        /// anything outside it, and nothing in this codebase reproduced that), read here rather than written.
        /// BOUNDARY: the bounds are inclusive — the declared minimum and maximum are both acceptable.</para>
        /// </summary>
        private static ProjectInspection MeterTicks(ProblemCatalog catalog)
        {
            double minimum = Threshold(catalog, "addr-s0-ticks-missing", "MinimumTicks");
            double maximum = Threshold(catalog, "addr-s0-ticks-missing", "MaximumTicks");
            return inspection =>
            {
                foreach (ProjectElement meter in inspection.Analyses.WithTag(MeterTag))
                {
                    string? raw = meter.GetAttribute(TicksAttribute);
                    bool valid = long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                        && ticks >= minimum
                        && ticks <= maximum;
                    if (!valid)
                    {
                        inspection.Report(meter, Arguments(
                            ("meter", Name(meter)), ("minimum", minimum), ("maximum", maximum)));
                    }
                }
            };
        }

        /// <summary>
        /// The DECLARATIVE half of the phone-number rule, and the repository's first registered value constraint.
        /// <para>The PREDICATE is not restated here: it is <see cref="DialogValueRule.PhoneNumber"/>, the same
        /// object the product dialog and the commit path consult, so the three cannot disagree about what a valid
        /// number is. This type adds the two things that object does not have: a catalogue code, and the inverse
        /// reading a dialog can bind to.</para>
        /// <para>NON-PUBLIC deliberately: <see cref="DeviceAddressRules"/> is a public static class, so a public
        /// nested type would move the shipped PublicAPI baseline.</para>
        /// <para><see cref="DialogValueRule.IsSatisfiedBy"/> already answers true for an empty value, so
        /// <see cref="Check"/> needs no blank special case — <c>Required</c> stays false and <c>Blank</c> stays at
        /// its default, which is what an optional slot means. An empty slot is
        /// <c>addr-modem-phonenumber-blank</c>'s question, and about the modem rather than the slot.</para>
        /// </summary>
        private sealed class PhoneNumberShape(ProblemCode code, int minimumLength, int maximumLength)
            : IValueConstraint
        {
            /// <inheritdoc/>
            public ProblemCode Code => code;

            /// <inheritdoc/>
            public ValueConstraintVerdict Check(string? rawValue) =>
                DialogValueRule.PhoneNumber.IsSatisfiedBy(rawValue)
                    ? ValueConstraintVerdict.Ok
                    : ValueConstraintVerdict.Failed(Arguments(("value", rawValue ?? string.Empty)));

            /// <summary>
            /// The same constraint as bindable data. Built from
            /// <see cref="FieldConstraintMetadata.Unconstrained"/> and never from a fresh constructor:
            /// <c>Unconstrained</c> is deliberately the LOOSEST value so the field-metadata merge can only
            /// tighten, and constructing directly is how a Required or a Blank policy gets asserted by accident.
            /// </summary>
            public FieldConstraintMetadata Describe() => FieldConstraintMetadata.Unconstrained with
            {
                MinimumLength = minimumLength,
                MaximumLength = maximumLength,
                WhitespaceForbidden = true,
            };
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>Whether a product names a real device: a serial that is neither blank nor the null token.</summary>
        private static bool IsCommissioned(ProjectElement product) =>
            product.GetAttribute(SerialAttribute) is { } serial && IsAssigned(serial);

        /// <summary>Whether an address-shaped attribute value is an assignment rather than a placeholder.</summary>
        private static bool IsAssigned(string value) =>
            !string.IsNullOrWhiteSpace(value) && Normalise(value) != Normalise(ElementId.NullToken);

        /// <summary>
        /// One spelling per value. The catalog writes <c>_0x01</c> where a saved file writes <c>_0x1</c> — the
        /// canonicalizer normalises hex tokens on save — so comparing the raw strings would call two spellings of
        /// channel 1 different channels.
        /// </summary>
        private static string Normalise(string token) =>
            HexToken.TryParseValue(token, out long value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : token.Trim();

        /// <summary>The product an element belongs to, through the shared classifier so an unprefixed device root
        /// (<c>s0_device</c>) is found like any other product.</summary>
        private static ProjectElement? NearestProduct(ITopologyAnalysis topology, ProjectElement element)
        {
            ProjectElement? current = element;
            while (current is not null && !ProductClassifier.IsProduct(current.Tag))
            {
                current = topology.Parent(current);
            }

            return current;
        }
    }
}
