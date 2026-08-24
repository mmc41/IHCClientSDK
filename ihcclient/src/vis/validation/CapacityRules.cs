#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Products;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The four CAPACITY rows: what the target controller can hold, and the one limit that is the file's own.
    ///
    /// <para><b>Three of the four are not evaluated without a declared capability profile</b>, and that is the
    /// whole point of D21's controller case: their limit is not in the <c>.vis</c> file, so validating against a
    /// default would mean the same project is valid on one workstation and invalid on another. Each declares
    /// <see cref="ProblemCatalogEntry.RequiresControllerLimits"/>, so the profile skips it rather than the rule
    /// having to handle absence.</para>
    ///
    /// <para><b>Their limits are DATA, never constants here.</b> 8 input modules, 16 output modules and 128
    /// addresses per direction come from the datasheet and are corroborated by the address chooser's own bounds; 64
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
        /// <summary>The four rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "capacity-input-modules", Modules(isOutput: false)),
                Rule(catalog, "capacity-output-modules", Modules(isOutput: true)),
                Rule(catalog, "capacity-addresses", AddressesExceeded),
                Rule(catalog, "capacity-wireless-exceeded", WirelessExceeded),
                Rule(catalog, "capacity-modem-multiple", ModemMultiple),
                Rule(catalog, "capacity-resources-high", ResourcesHigh(catalog)));
        }

        private static RuleDefinition Rule(ProblemCatalog catalog, string code, ProjectInspection body) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Inspect(body).Build()
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>
        /// More data lines of one direction addressed than the target controller holds.
        /// <para>
        /// ONE DIRECTION PER RULE (D2). These were one rule with the terminals check, sharing the sentence
        /// "Projektet bruger {used} af {limit} moduler." — which was false of the terminals count and could not be
        /// made true, because a direction is a WORD and the argument contract carries data. The three also could
        /// not be filtered or counted apart while they shared an id. Splitting them is the same move this
        /// catalogue already made for <c>dataline-address</c>, for the same reason.
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
        private static void AddressesExceeded(IProjectInspection inspection)
        {
            if (inspection.Controller is not { } limits)
            {
                return;
            }

            foreach (bool isOutput in new[] { false, true })
            {
                int addressed = Addresses(inspection.Analyses, isOutput).Count();
                if (addressed > limits.AddressesPerDirection)
                {
                    inspection.Report(null, Arguments(
                        ("used", addressed), ("limit", limits.AddressesPerDirection)));
                }
            }
        }

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

        private static double Threshold(ProblemCatalog catalog, string code, string name) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
            && entry.Thresholds.FirstOrDefault(t => t.Name == name) is { } threshold
                ? threshold.Value
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        private static EquatableArray<ProblemArgument> Arguments(params (string Name, object Value)[] bindings) =>
            [.. bindings.Select(b => new ProblemArgument(b.Name, b.Value))];
    }
}
