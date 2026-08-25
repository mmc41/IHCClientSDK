#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Io;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The three DATA-LINE ADDRESSING rules, split out of the single id the shipped engine emits for all three.
    /// <para>
    /// THE SPLIT IS THE POINT. One id covered three distinct conditions — an address that is not a token at all,
    /// one outside the legal module range, and one two terminals of the same direction both claim — and the
    /// catalogue always described them as three rows with three different consequences and three different
    /// repairs. A single id cannot be filtered, cannot be counted per condition, and cannot carry a Danish label
    /// that says anything specific, because the three conditions have nothing in common beyond the attribute.
    /// </para>
    /// <para>
    /// The old id is RETIRED rather than deleted: it stays in the catalogue, keeps its place, and can never be
    /// handed to a different condition. A speaking id that outgrows its condition is split and the old one
    /// retired — never silently re-pointed at one of its successors, which would leave a published id meaning
    /// something narrower than it used to.
    /// </para>
    /// <para>
    /// The 1..128 bound is read from <c>DatalineAddress</c>, the single owner of the range, rather than restated
    /// here. That is the same constant the terminal dialog offers and the commit path re-checks, so all three
    /// agree by construction.
    /// </para>
    /// </summary>
    public static class DatalineAddressRules
    {
        /// <summary>The id these three replace. Retired, reserved, and never re-pointed.</summary>
        public static ProblemCode RetiredPredecessor { get; } = new("dataline-address");

        /// <summary>The three rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "dataline-address-malformed", Malformed),
                Rule(catalog, "dataline-address-range", OutOfRange),
                Rule(catalog, "dataline-address-duplicate", Duplicates));
        }

        /// <summary>An address that is not a <c>_0x</c> hex token: it cannot be decoded at all.</summary>
        private static void Malformed(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, string address) in Addressed(inspection))
            {
                if (!HexToken.TryParseValue(address, out _))
                {
                    inspection.Report(element, Arguments(("value", address), ("tag", element.Tag)));
                }
            }
        }

        /// <summary>An address outside the legal module range: no module can answer to it.</summary>
        private static void OutOfRange(IProjectInspection inspection)
        {
            foreach ((ProjectElement element, string address) in Addressed(inspection))
            {
                // A token that never parsed is the other rule's business — one address, one fault.
                if (HexToken.TryParseValue(address, out long value)
                    && (value < 1 || value > DatalineAddress.MaxAddressValue))
                {
                    inspection.Report(element, Arguments(
                        ("value", address),
                        ("tag", element.Tag),
                        ("maximum", DatalineAddress.MaxAddressValue)));
                }
            }
        }

        /// <summary>
        /// Two terminals of the same DIRECTION claiming one address: both react to the same command and neither
        /// can be addressed alone. Per direction, because an input and an output may share a number.
        /// </summary>
        private static void Duplicates(IProjectInspection inspection)
        {
            // ONE finding per collision, which is what the row's PrimaryWithRelated shape declares. It used to
            // Report one finding per LATER holder, so a three-way collision was told twice with nothing relating
            // the two, and the declaration was a promise the rule did not keep.
            //
            // ANCHORED AT THE SECOND CLAIMANT, not the first: the first holds the address and the second is the
            // one to re-address, so that is the site a reader acts on. This is the opposite of the duplicate-ID
            // rows, where the collision is repaired by looking at both — and the difference is deliberate.
            Dictionary<(string Direction, long Address), List<ProjectElement>> holders = [];
            List<(string Direction, long Address)> order = [];
            foreach ((ProjectElement element, string address) in Addressed(inspection))
            {
                if (!HexToken.TryParseValue(address, out long value)
                    || value < 1
                    || value > DatalineAddress.MaxAddressValue)
                {
                    continue;
                }

                (string, long) key = (element.Tag, value);
                if (!holders.TryGetValue(key, out List<ProjectElement>? sharing))
                {
                    holders[key] = sharing = [];
                    order.Add(key);
                }

                sharing.Add(element);
            }

            foreach ((string Direction, long Address) key in order)
            {
                List<ProjectElement> sharing = holders[key];
                if (sharing.Count < 2)
                {
                    continue;
                }

                // The ANCHOR's own token, not the first holder's: the group is keyed on the PARSED address, so
                // two holders may spell it differently ("_0x5" and "_0x05") and the sentence must show the token
                // that belongs to the element its locator points at.
                inspection.ReportGroup(sharing[1], [sharing[0], .. sharing.Skip(2)], Arguments(
                    ("value", sharing[1].GetAttribute("address_dataline") ?? string.Empty),
                    ("count", sharing.Count),
                    ("tag", key.Direction)));
            }
        }

        /// <summary>
        /// Every terminal carrying an address. An UNADDRESSED terminal is skipped: that is the DTD default and a
        /// legal state while the installation is unconfigured, so reporting it would fire on every project mid-way
        /// through commissioning.
        /// </summary>
        private static IEnumerable<(ProjectElement Element, string Address)> Addressed(IProjectInspection inspection)
        {
            // Two tags, so the shared LIST is filtered rather than two WithTag buckets concatenated: the buckets
            // would come out grouped by tag instead of in document order.
            foreach (ProjectElement element in inspection.Analyses.Elements)
            {
                if (element.Tag is ("dataline_input" or "dataline_output")
                    && element.GetAttribute("address_dataline") is { } address
                    && address != ElementId.NullToken)
                {
                    yield return (element, address);
                }
            }
        }
    }
}
