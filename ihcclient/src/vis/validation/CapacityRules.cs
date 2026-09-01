using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The CAPACITY rows: what the target controller can hold, and the limits that are the file's own.
    ///
    /// <para><b>A row whose limit comes from the CONTROLLER is not evaluated without a declared capability
    /// profile</b>, and that is the
    /// whole point of D21's controller case: their limit is not in the <c>.vis</c> file, so validating against a
    /// default would mean the same project is valid on one workstation and invalid on another. Each declares
    /// <see cref="ProblemCatalogEntry.RequiresControllerLimits"/>, so the profile skips it rather than the rule
    /// having to handle absence.</para>
    ///
    /// <para><b>Their limits are DATA, never constants here.</b> 8 input modules, 16 output modules and 128
    /// addresses per direction come from the datasheet and are corroborated by the address chooser's own bounds
    /// (the module and address limits are one row PER DIRECTION, which is how each names its direction in its own
    /// Danish sentence instead of leaving a reader to tell two findings apart by their numbers); 64
    /// wireless devices comes from vendor help — and because that source says <i>"bør maksimalt … af hensyn til en
    /// fornuftig responstid"</i>, a RECOMMENDATION, the wireless row is a Warning rather than the Error the
    /// catalogue first stated. The resource ceiling has NO vendor source and says so where it lives.</para>
    ///
    /// <para><b><c>capacity-modem-multiple</c> needs no profile:</b> the limit is "one", the file either carries two
    /// modems or it does not, and no editor will author that state — both refuse the second insert. A file with two
    /// arrived by import or by hand, which is what the whole-project face is for.</para>
    /// </summary>
    public static class CapacityRules
    {
        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "capacity-input-modules", Modules(isOutput: false)),
                Rule(catalog, "capacity-output-modules", Modules(isOutput: true)),
                Rule(catalog, "capacity-input-addresses", AddressesExceeded(isOutput: false)),
                Rule(catalog, "capacity-output-addresses", AddressesExceeded(isOutput: true)),
                Rule(catalog, "capacity-wireless-exceeded", WirelessExceeded),
                Rule(catalog, "capacity-wireless-links-per-unit", WirelessLinksPerUnit),
                Rule(catalog, "capacity-scenarios-per-receiver", ScenariosPerReceiver),
                Rule(catalog, "capacity-modem-multiple", ModemMultiple),
                Rule(catalog, "capacity-s0-multiple", S0Multiple(catalog)),
                Rule(catalog, "capacity-rs485-exceeded", Rs485Exceeded(catalog)),
                Rule(catalog, "capacity-voicemodem-dimmer-conflict", VoicemodemDimmerConflict),
                Rule(catalog, "capacity-resources-high", ResourcesHigh(catalog)));
        }

        /// <summary>
        /// More data lines of one direction addressed than the target controller holds.
        /// <para>
        /// ONE DIRECTION PER RULE (D2). These were one rule with the terminals check, sharing the sentence
        /// "Projektet bruger {used} af {limit} moduler." — which was false of the terminals count and could not be
        /// made true, because a direction is a WORD and the argument contract carries data. They also could not be
        /// filtered or counted apart while they shared an id. Splitting them is the same move this catalogue
        /// already made for <c>dataline-address</c>, and made AGAIN for the terminals count itself: the successor
        /// row still named no direction, so it split per direction too.</para>
        /// <para>
        /// Four rows now, two per quantity: <c>capacity-input-modules</c> / <c>capacity-output-modules</c> and
        /// <c>capacity-input-addresses</c> / <c>capacity-output-addresses</c>.
        /// </para>
        /// </summary>
        /// <param name="isOutput">Which direction's data lines this rule counts.</param>
        private static ProjectInspection Modules(bool isOutput) => inspection =>
        {
            if (inspection.Controller is not { } limits)
            {
                return;   // unreachable: the profile skips a rule declaring RequiresControllerLimits
            }

            int limit = isOutput ? limits.OutputModules : limits.InputModules;
            int modules = Addresses(inspection.Analyses, isOutput).Select(a => a.DataLine).Distinct().Count();
            if (modules > limit)
            {
                inspection.Report(null, Arguments(("used", modules), ("limit", limit)));
            }
        };

        /// <summary>
        /// More addressed terminals in one direction than the target controller holds.
        /// <para>
        /// EVALUATED PER DIRECTION and reported per direction, which is why the entry declares
        /// <c>OnePerOccurrence</c>: a project can be over on both, and the old shared row declared
        /// <c>OneFinding</c> while its loop could already emit two.
        /// </para>
        /// <para>
        /// UNCONDITIONAL, unlike the <c>else if</c> it replaces. The terminals check used to run only when the
        /// module count was within its limit, so a project over BOTH reported the modules alone and the reader
        /// repaired one fault to discover the other.
        /// </para>
        /// </summary>
        private static ProjectInspection AddressesExceeded(bool isOutput) => inspection =>
        {
            if (inspection.Controller is not { } limits)
            {
                return;
            }

            // ONE DIRECTION PER RULE, like the module rows above. Looping both directions inside one rule made a
            // project over on both emit two findings under one code, distinguishable only by their numbers — and
            // the numbers are the one thing that cannot say which direction they count.
            int addressed = Addresses(inspection.Analyses, isOutput).Count();
            if (addressed > limits.AddressesPerDirection)
            {
                inspection.Report(null, Arguments(
                    ("used", addressed), ("limit", limits.AddressesPerDirection)));
            }
        };

        /// <summary>
        /// More wireless products than the controller should carry: response time degrades.
        /// <para>A WARNING, from the source's own wording: the vendor states the figure as a recommendation for
        /// response-time reasons, and the devices do bind. An Error's consequence has to hold whatever the author
        /// intended, and "answers more slowly" does not.</para>
        /// </summary>
        private static void WirelessExceeded(IProjectInspection inspection)
        {
            if (inspection.Controller is not { } limits)
            {
                return;
            }

            int wireless = inspection.Analyses.Elements
                .Count(e => ProductClassifier.IsWireless(e.Tag));
            if (wireless > limits.WirelessDevices)
            {
                inspection.Report(null, Arguments(("used", wireless), ("limit", limits.WirelessDevices)));
            }
        }

        /// <summary>
        /// One wireless unit carrying more follow-links than the controller supports on a single unit.
        /// <para>ABSENT WITHOUT A CONTROLLER, like every row in this module: with no declared limits there is no
        /// ceiling to be over, and reporting against a default would be indistinguishable from guessing.</para>
        /// <para>A COMBI UNIT IS MEASURED AGAINST ITS OWN DECLARED CEILING, read from a second member rather
        /// than derived from the first — that the two figures differ by a factor of two today is an observation,
        /// not a rule the vendor states.</para>
        /// <para>PER UNIT: two overloaded units are two units to re-plan.</para>
        /// </summary>
        private static void WirelessLinksPerUnit(IProjectInspection inspection)
        {
            if (inspection.Controller is not { } limits)
            {
                return;
            }

            foreach (ProjectElement unit in WirelessProducts(inspection.Analyses))
            {
                int limit = CombiUnits.Contains(unit.GetAttribute(ProductIdentifierAttribute) ?? string.Empty)
                    ? limits.LinksPerCombiUnit
                    : limits.LinksPerWirelessUnit;
                int links = unit.DescendantsAndSelf()
                    .Count(e => ReciprocalTags.FollowLinkHalfTags.Contains(e.Tag));
                if (links > limit)
                {
                    inspection.Report(unit, Arguments(
                        ("product", Name(unit)), ("used", links), ("limit", limit)));
                }
            }
        }

        /// <summary>
        /// One wireless receiver taking part in more scenarios than the controller carries.
        /// <para>A RECEIVER IS A WIRELESS PRODUCT THAT OWNS A SCENE CONTAINER, which the file decides — a
        /// wireless unit with no container cannot be commanded into a scene at all, so it is not a receiver and
        /// has no ceiling to be over. The corpus carries one such product.</para>
        /// <para>COUNTED IN MEMBER ROWS ACROSS ALL ITS CONTAINERS: a two-channel receiver has two containers and
        /// can still be in one scenario, so containers are not the quantity the controller bounds.</para>
        /// </summary>
        private static void ScenariosPerReceiver(IProjectInspection inspection)
        {
            if (inspection.Controller is not { } limits)
            {
                return;
            }

            foreach (ProjectElement receiver in WirelessProducts(inspection.Analyses))
            {
                // One pass over the unit's subtree: the containers are counted, not collected, because the only
                // things asked of them are whether any exists and how many member rows they hold between them.
                bool isReceiver = false;
                int scenarios = 0;
                foreach (ProjectElement container in receiver.DescendantsAndSelf())
                {
                    if (container.Tag != ReciprocalTags.SceneContainerTag)
                    {
                        continue;
                    }

                    isReceiver = true;
                    scenarios += container.Children.Count(row => ReciprocalTags.SceneMemberTags.Contains(row.Tag));
                }

                if (isReceiver && scenarios > limits.ScenariosPerReceiver)
                {
                    inspection.Report(receiver, Arguments(
                        ("product", Name(receiver)), ("used", scenarios),
                        ("limit", limits.ScenariosPerReceiver)));
                }
            }
        }

        /// <summary>
        /// The wireless COMBI units, which carry their own higher link ceiling. All four are
        /// <c>product_airlink</c> products whose catalogue names carry <i>Kombi</i>.
        /// </summary>
        private static readonly ImmutableHashSet<string> CombiUnits =
            ["_0x4404", "_0x4406", "_0x4407", "_0x4408"];

        /// <summary>
        /// A second modem: the controller binds one, so the extra entries can never be commissioned.
        /// <para>NO PROFILE NEEDED — the limit is one, and it is the controller's rather than a configurable
        /// capability. RECLASSIFIED (⊘): measured live, IHC Visual refuses the second insert (<i>"Modem er allerede
        /// indsat…"</i>) and so does OpenVisual, each leaving the tree unchanged, so a file carrying two arrived by
        /// import or by hand.</para>
        /// </summary>
        private static void ModemMultiple(IProjectInspection inspection)
        {
            ImmutableArray<ProjectElement> modems =
            [
                .. inspection.Analyses.Elements.Where(e => ProductClassifier.IsModem(e.Tag)),
            ];
            if (modems.Length > 1)
            {
                inspection.Report(null, Arguments(("used", modems.Length)));
            }
        }

        /// <summary>
        /// A second S0 product: only one can serve a controller, so the extras can never be commissioned.
        /// <para>NO PROFILE NEEDED, for the same reason the modem row needs none — the limit is the controller's
        /// rather than a configurable capability. Unlike that row, the number is READ from the entry: it has a
        /// vendor sentence behind it, so it is data rather than a literal here.</para>
        /// <para>The family test is the shared classifier's, which already answers <c>s0_device</c> — an S0
        /// product's device root carries no <c>product_</c> prefix, so a tag-prefix test would miss it.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared maximum, are declared in.</param>
        private static ProjectInspection S0Multiple(ProblemCatalog catalog)
        {
            int maximum = (int)Threshold(catalog, "capacity-s0-multiple", "MaximumS0Products");
            return inspection =>
            {
                // Over PRODUCTS, for the reason RuleAuthoring.Rs485Products states: Classify's open-world
                // fallback is not product-guarded. S0Device is an exact match today and so is safe either way,
                // but the subject is "every S0 PRODUCT" and the walk says so.
                int meters = AllProducts(inspection.Analyses)
                    .Count(e => ProductClassifier.Classify(e.Tag) == ProductFamily.S0Device);
                if (meters > maximum)
                {
                    inspection.Report(null, Arguments(("used", meters)));
                }
            };
        }

        /// <summary>
        /// More RS-485 components than the bus takes: past the limit the project cannot be fully commissioned.
        /// <para>NO PROFILE NEEDED: the limit belongs to the BUS, not to the controller, so it is the same
        /// number on every workstation — which is exactly the test D21's controller case applies.</para>
        /// <para>ALL THREE RS-485 FAMILIES COUNT, the SMS modem included, because the vendor's guard sentence
        /// says <i>inkl. SMS modem</i> in so many words.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and its declared maximum, are declared in.</param>
        private static ProjectInspection Rs485Exceeded(ProblemCatalog catalog)
        {
            int maximum = (int)Threshold(catalog, "capacity-rs485-exceeded", "MaximumRs485Components");
            return inspection =>
            {
                int components = Rs485Products(inspection.Analyses).Count();
                if (components > maximum)
                {
                    inspection.Report(null, Arguments(("used", components), ("limit", maximum)));
                }
            };
        }

        /// <summary>
        /// A Voice Modem and an RS485 LED Dimmer in one project: the two cannot share a controller, so one of
        /// them can never operate.
        /// <para>NO NUMBERS AT ALL — an incompatibility rather than a capacity. It reports nothing about how
        /// many of either there are, because one of each is already the whole condition.</para>
        /// <para>THE SMS MODEM IS NOT THE VOICE MODEM. <see cref="ProductClassifier"/> separates
        /// <see cref="ProductFamily.Rs485SmsModem"/> from <see cref="ProductFamily.Rs485Modem"/> by exact tag
        /// before its <c>*modem*</c> fallback runs, which is what keeps this row silent on the three committed
        /// projects that carry an SMS modem beside a dimmer.</para>
        /// </summary>
        private static void VoicemodemDimmerConflict(IProjectInspection inspection)
        {
            bool voiceModem = false;
            bool ledDimmer = false;
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                switch (ProductClassifier.Classify(product.Tag))
                {
                    case ProductFamily.Rs485Modem:
                        voiceModem = true;
                        break;
                    case ProductFamily.Rs485LedDimmer:
                        ledDimmer = true;
                        break;
                    default:
                        break;
                }
            }

            if (voiceModem && ledDimmer)
            {
                inspection.Report(null, default);
            }
        }

        /// <summary>
        /// The project's resource count is approaching the controller's ceiling: further growth will fail late, at
        /// upload time.
        /// <para>
        /// BOTH NUMBERS ARE AUTHORED AND BOTH SAY SO. The ceiling has no vendor source (see
        /// <see cref="ControllerCapabilityLimits.AuthoredResourceCeiling"/>) and the fraction at which "approaching"
        /// starts is a declared threshold on the entry. TODO: unconfirmed — this is D20's authored case, and the
        /// marker is in the code because D21(d) asks for it here rather than only in a backlog entry.
        /// </para>
        /// </summary>
        private static ProjectInspection ResourcesHigh(ProblemCatalog catalog)
        {
            double fraction = Threshold(catalog, "capacity-resources-high", "HighWaterFraction");
            return inspection =>
            {
                if (inspection.Controller is not { } limits)
                {
                    return;
                }

                int resources = inspection.Analyses.Elements
                    .Count(e => e.Tag.StartsWith("resource_", StringComparison.Ordinal));
                // NO UPPER BOUND (D1). It once stopped at the ceiling, on the reasoning that a project past it was
                // "the modules row's business at upload" — but capacity-modules-exceeded counts data lines and
                // terminal addresses and never counts resource_*, so nothing covered the over-ceiling case and
                // 2500 of 2000 reported nothing. The row's own sentence stays true past the limit.
                int threshold = (int)Math.Ceiling(limits.Resources * fraction);
                if (resources >= threshold)
                {
                    inspection.Report(null, Arguments(("used", resources), ("limit", limits.Resources)));
                }
            };
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// Every decodable terminal address of one direction, read through the SDK's own address owner — the same
        /// reading the module rules use, so "which module is that terminal on" has one answer.
        /// </summary>
        private static IEnumerable<DatalineAddress> Addresses(IProjectAnalyses analyses, bool isOutput)
        {
            string tag = isOutput ? "dataline_output" : "dataline_input";
            foreach (ProjectElement terminal in analyses.WithTag(tag))
            {
                if (DatalineAddress.TryParse(terminal.GetAttribute("address_dataline"), isOutput,
                    out DatalineAddress address))
                {
                    yield return address;
                }
            }
        }
    }
}
