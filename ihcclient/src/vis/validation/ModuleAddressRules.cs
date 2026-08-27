#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Addressing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// MODULE and CHANNEL coherence: what the data-line modules of a project look like taken together, and whether
    /// an RS485 LED-dimmer's channels are addressable.
    ///
    /// <para><b>A module is a data line</b>, and its capacity is the direction's terminals-per-line — 16 in, 8 out
    /// — read from <see cref="DatalineAddress"/>, the single owner of that arithmetic. A terminal's module is the
    /// line half of its decoded address.</para>
    ///
    /// <para><b>Both module rules carry a DECLARED threshold, because both catalogue rows describe a matter of
    /// degree.</b> "Only partly used" is true of literally every module in every committed fixture (measured: 4 of
    /// 4, 4 of 4 and 5 of 5), so the literal condition is not a finding — it is a description of how installations
    /// are wired. What the row is actually about is its own second sentence, "a nearly-empty module can mean a
    /// mis-addressed product", and that needs a number. Same for "many distant localities": the count of distinct
    /// localities is decidable, the distance is not.</para>
    ///
    /// <para><b>One row of this task's five is RULED OUT rather than implemented</b> — <c>addr-unassigned</c>, whose
    /// condition <c>doc-address</c> already reports on the same elements. The entry carries the reasoning; there is
    /// no rule here for it, and a test pins that an unaddressed terminal produces exactly one finding.</para>
    /// </summary>
    public static class ModuleAddressRules
    {
        /// <summary>The RS485 LED-dimmer's channel element.</summary>
        private const string DimmerChannelTag = "rs485_led_dimmer_channel";

        /// <summary>The attribute carrying a dimmer channel's addressable identity.</summary>
        private const string ChannelIdAttribute = "channel_id";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "addr-module-partial", ModulePartial(catalog)),
                Rule(catalog, "addr-module-mixed-locality", ModuleMixedLocality(catalog)),
                Rule(catalog, "addr-dimmer-channel-unassigned", ChannelUnassigned),
                Rule(catalog, "addr-dimmer-channel-duplicate", ChannelDuplicate));
        }

        /// <summary>
        /// A module carrying almost nothing while another module of the same direction is also in use: the stray
        /// terminal is what a mis-addressed product looks like.
        /// <para>SUBJECT: each (direction, data line) pair in use. THRESHOLD: the entry's declared
        /// <c>MinimumUsedTerminals</c>. QUALIFIER: at least one OTHER module of the same direction must be in use —
        /// a project wired onto a single module is a small installation, not a mis-address.</para>
        /// </summary>
        private static ProjectInspection ModulePartial(ProblemCatalog catalog)
        {
            double minimum = Threshold(catalog, "addr-module-partial", "MinimumUsedTerminals");
            return inspection =>
            {
                foreach (IGrouping<bool, Module> direction in Modules(inspection).GroupBy(m => m.IsOutput))
                {
                    ImmutableArray<Module> modules = [.. direction];
                    if (modules.Length < 2)
                    {
                        continue;   // one module in a direction: nothing to have mis-addressed away from
                    }

                    foreach (Module module in modules.Where(m => m.Terminals.Length < minimum))
                    {
                        inspection.ReportGroup(module.Terminals[0], Tail(module), Arguments(
                            ("line", module.Line),
                            ("used", module.Terminals.Length),
                            ("capacity", DatalineAddress.TerminalsPerLine(module.IsOutput))));
                    }
                }
            };
        }

        /// <summary>
        /// A module serving terminals in more localities than the declared maximum: fault-finding on site means
        /// walking between rooms to trace one module.
        /// <para>SUBJECT: each module in use. THRESHOLD: the entry's declared <c>MaxLocalitiesPerModule</c>. The
        /// row says "many DISTANT localities" and distance is not in the file, so the decidable half is the
        /// COUNT.</para>
        /// </summary>
        private static ProjectInspection ModuleMixedLocality(ProblemCatalog catalog)
        {
            double maximum = Threshold(catalog, "addr-module-mixed-locality", "MaxLocalitiesPerModule");
            return inspection =>
            {
                ITopologyAnalysis topology = inspection.Analyses.Topology;
                foreach (Module module in Modules(inspection))
                {
                    ImmutableArray<ProjectElement> localities =
                        [.. module.Terminals
                            .Select(t => topology.NearestAncestorOrSelf(t, "group"))
                            .OfType<ProjectElement>()
                            .Distinct()];
                    if (localities.Length > maximum)
                    {
                        inspection.ReportGroup(module.Terminals[0], Tail(module), Arguments(
                            ("line", module.Line),
                            ("localities", localities.Length)));
                    }
                }
            };
        }

        /// <summary>
        /// A dimmer channel with no channel id: nothing can address it.
        /// <para>SUBJECT: every <c>rs485_led_dimmer_channel</c>. CONDITION: <c>channel_id</c> is absent, blank, or
        /// the NULL TOKEN — measured on the shipped catalog, whose dimmer template carries an empty
        /// <c>channel_id</c> that reads back as <c>_0x0</c> once the product is placed, exactly as an unaddressed
        /// terminal's <c>address_dataline</c> does. EXCLUSIONS: none — a channel assigned during commissioning is
        /// the legitimate reading this Warning is for.</para>
        /// </summary>
        private static void ChannelUnassigned(IProjectInspection inspection)
        {
            foreach (ProjectElement channel in Channels(inspection.Analyses))
            {
                if (!IsAssigned(channel))
                {
                    inspection.Report(channel, Arguments(("channel", Name(channel))));
                }
            }
        }

        /// <summary>
        /// Whether a channel carries an addressable id. The NULL TOKEN counts as unassigned, which the gate taught
        /// rather than the grammar: a freshly inserted catalog dimmer reads <c>_0x0</c> on both channels, so
        /// treating that as a real id made every placed dimmer an ERROR for sharing it.
        /// </summary>
        private static bool IsAssigned(ProjectElement channel) =>
            channel.GetAttribute(ChannelIdAttribute) is { } id
            && !string.IsNullOrWhiteSpace(id)
            && id != ElementId.NullToken;

        /// <summary>
        /// Two dimmer channels claiming one channel id: the controller cannot tell them apart. The ONE Error in
        /// this set, and it is the catalogue's rating.
        /// <para>SUBJECT: every channel carrying a channel id, compared across the whole project — a duplicate
        /// across two dimmers is the same collision as one inside a single dimmer. EXCLUSION: a blank id is the
        /// other row's finding. SHAPE: the second holder anchors and the first is a related location, so the
        /// reader sees both ends of the collision.</para>
        /// </summary>
        private static void ChannelDuplicate(IProjectInspection inspection)
        {
            Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
            foreach (ProjectElement channel in Channels(inspection.Analyses))
            {
                if (!IsAssigned(channel) || channel.GetAttribute(ChannelIdAttribute) is not { } id)
                {
                    continue;
                }

                if (seen.TryGetValue(id, out ProjectElement? first))
                {
                    inspection.ReportGroup(channel, [first], Arguments(
                        ("channel", Name(channel)), ("other", Name(first)), ("id", id)));
                }
                else
                {
                    seen[id] = channel;
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>One data-line module in use: its direction, its line number and the terminals on it.</summary>
        private readonly record struct Module(bool IsOutput, int Line, EquatableArray<ProjectElement> Terminals);

        /// <summary>
        /// The module's terminals EXCEPT its first — the related locations beside a finding anchored on that first
        /// one. Related means the OTHER sites: passing the whole group would list the anchor twice, sending a
        /// reader back to where they already are and making a count over the group one too high.
        /// <para><see cref="Modules"/> only yields modules with at least one terminal, so the skip is always safe,
        /// and a single-terminal module correctly reports an empty related set.</para>
        /// </summary>
        /// <param name="module">The module whose anchor is its first terminal.</param>
        private static EquatableArray<ProjectElement> Tail(Module module) => [.. module.Terminals.Skip(1)];

        /// <summary>
        /// Every module in use, in document order of its first terminal. A terminal whose address does not decode
        /// is skipped — a malformed or out-of-range address is <c>dataline-address-*</c>'s finding, and counting it
        /// into a module would put a phantom terminal on a line nobody addressed.
        /// </summary>
        private static IEnumerable<Module> Modules(IProjectInspection inspection)
        {
            Dictionary<(bool IsOutput, int Line), ImmutableArray<ProjectElement>.Builder> byModule = [];
            List<(bool IsOutput, int Line)> order = [];

            foreach (ProjectElement terminal in inspection.Analyses.Elements)
            {
                if (terminal.Tag is not ("dataline_input" or "dataline_output"))
                {
                    continue;
                }

                bool isOutput = terminal.Tag == "dataline_output";
                if (!DatalineAddress.TryParse(terminal.GetAttribute("address_dataline"), isOutput,
                        out DatalineAddress address))
                {
                    continue;
                }

                (bool, int) key = (isOutput, address.DataLine);
                if (!byModule.TryGetValue(key, out ImmutableArray<ProjectElement>.Builder? terminals))
                {
                    terminals = ImmutableArray.CreateBuilder<ProjectElement>();
                    byModule[key] = terminals;
                    order.Add(key);
                }

                terminals.Add(terminal);
            }

            return order.Select(key => new Module(key.IsOutput, key.Line, byModule[key].ToImmutable()));
        }

        private static IEnumerable<ProjectElement> Channels(IProjectAnalyses analyses) =>
            analyses.WithTag(DimmerChannelTag);
    }
}
