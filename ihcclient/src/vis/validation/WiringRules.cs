using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The WIRING rules: what the follow-links of a project say about whether the installation actually
    /// does anything.
    ///
    /// <para><b>None of them is an ERROR, and that is the whole reason their predicates are narrow.</b> Each
    /// of these conditions has a legitimate reading — a spare terminal, a block still being built — so the value
    /// of the row lies entirely in NOT firing on the legitimate case. A rule
    /// here that reports something a reader will dismiss trains them to dismiss the next one too.</para>
    ///
    /// <para><b>Direction is not guessed.</b> A source pin owns a <c>link_from_resource</c> half and a sink owns a
    /// <c>link_to_resource</c> one, and which pin kinds may occupy which end is <see cref="LinkRoles"/>'s measured
    /// answer (a 15-cell vendor matrix plus 397 links across 21 vendor-authored projects). These rules read that
    /// model rather than restating it, so a pin family nobody measured stays out of their way.</para>
    ///
    /// <para><b>No row here reports per PIN, and the predicates say why.</b> A catalog function block ships with
    /// every input its behaviour offers — thirteen on the vendor's own <i>Kip tænd sluk</i> — and the author wires
    /// the one they want; a product plate ships more terminals than an installation uses. "Nothing reaches this" is
    /// worth saying about a whole block or a whole device, and said once per unwired pin it is a true statement
    /// about a spare terminal repeated once per alternative the author declined. So the subjects are the block
    /// (<c>link-fb-input-unfed</c>, <c>link-fb-output-unused</c>) and the product (<c>link-product-unwired</c>),
    /// each reported once with its unwired pins as related sites.</para>
    ///
    /// <para><b>The multi-driven row is the exception, and legitimately.</b> Two sources on ONE pin is a fault of
    /// that pin, not of the device carrying it, and the repair is to remove one of them.</para>
    /// </summary>
    public static class WiringRules
    {
        /// <summary>
        /// PRODUCT INPUT pins — the world drives them, software cannot: a wired terminal and its wireless
        /// counterpart. DERIVED from <see cref="LinkRoles"/>'s measured never-a-sink set minus the function-block
        /// pin, so this list cannot drift from the model the editor enforces — it is not a copy of it. The claim
        /// was made in this comment before it was true of the code, and a claim nothing enforces is the shape this
        /// catalogue exists to remove.
        /// </summary>
        private static readonly ImmutableHashSet<string> ProductInputTags =
            [.. LinkRoles.NeverASink.Where(tag => !LinkRoles.IsFunctionBlockPin(tag))];

        /// <summary>
        /// PRODUCT OUTPUT pins: the wired terminal and the wireless output family. DERIVED from
        /// <see cref="SceneRules"/>' measured output-to-scene-kind mapping, which is the shipped statement of which
        /// pins are outputs a scenario can drive — so the switch there and this set cannot disagree.
        /// </summary>
        private static readonly ImmutableHashSet<string> ProductOutputTags =
            [.. SceneRules.OutputTagsWithPinnedMember];

        /// <summary>
        /// Both pin directions in one set, because <c>link-product-unwired</c> judges a device by its whole wirable
        /// surface and asks the question once per descendant rather than once per direction.
        /// </summary>
        private static readonly ImmutableHashSet<string> ProductPinTags =
            ProductInputTags.Union(ProductOutputTags);

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "link-product-unwired", ProductUnwired),
                Rule(catalog, "link-output-multidriven", OutputMultidriven),
                Rule(catalog, "link-fb-input-unfed", BlockInputsUnfed),
                Rule(catalog, "link-fb-output-unused", BlockOutputsUnused),
                Rule(catalog, "link-through-empty-block", ThroughEmptyBlock),
                Rule(catalog, "link-pass-through", PassThrough),
                Rule(catalog, "rs485-dimmer-fault-unwired", DimmerFaultUnwired));
        }

        /// <summary>
        /// The RS-485 LED dimmer's four per-channel FAULT-STATE resources, by element tag.
        /// <para>
        /// TAGS, NOT NAMES. These four are language-independent and not user-editable, while the Danish
        /// <i>Fejl - …</i> strings beside them are ordinary <c>name</c> values. Keying on the name would both
        /// miss a renamed flag and report a dimmer whose ordinary resource happened to be named like one.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> DimmerFaultTags =
        [
            "rs485_led_dimmer_error_state_overcurrent",
            "rs485_led_dimmer_error_state_overvoltage",
            "rs485_led_dimmer_error_state_overheating",
            "rs485_led_dimmer_error_state_loadfailure",
        ];

        /// <summary>
        /// A dimmer none of whose fault resources is linked: the product can report its own faults and the
        /// project discards that capability.
        /// <para>
        /// PER CHANNEL, so the walk is over DESCENDANTS rather than children — a two-channel dimmer exposes
        /// eight flags — and the condition is "none of them", because partial wiring is a design choice.
        /// </para>
        /// <para>
        /// Link participation is read with the same <see cref="OwnsAnyLink"/> the other rows in this module use,
        /// so a fault resource counts as wired on exactly the terms every other pin does.
        /// </para>
        /// </summary>
        private static void DimmerFaultUnwired(IProjectInspection inspection)
        {
            foreach (ProjectElement dimmer in inspection.Analyses.WithTag(Rs485LedDimmerTag))
            {
                // Two booleans rather than a collected list: the condition is "some fault pin, and none of them
                // wired", so a wired one ends the walk and nothing here ever needs the pins themselves.
                bool anyFault = false;
                bool anyWired = false;
                foreach (ProjectElement fault in dimmer.Descendants())
                {
                    if (!DimmerFaultTags.Contains(fault.Tag))
                    {
                        continue;
                    }

                    anyFault = true;
                    if (OwnsAnyLink(fault))
                    {
                        anyWired = true;
                        break;
                    }
                }

                if (anyFault && !anyWired)
                {
                    inspection.Report(dimmer, Arguments(("name", Name(dimmer))));
                }
            }
        }

        /// <summary>
        /// A product NO pin of which is wired in either direction: the device is installed and the project does
        /// nothing with it.
        /// <para>SUBJECT: every product declaring at least one input or output pin. PER PRODUCT, and that is the
        /// predicate's substance — the same argument the two block rows make. A plate ships with more terminals
        /// than an author uses; said once per unwired pin, "nothing reaches this" would be a true statement about
        /// a spare terminal and a useless one, once per alternative the author declined. It is the whole DEVICE
        /// being unreachable that is worth a reader's attention.</para>
        /// <para>BOTH DIRECTIONS IN ONE ROW, because neither alone works: a pushbutton plate's indicator LEDs are
        /// that product's only outputs, so an outputs-only reading would report every such plate whose buttons are
        /// wired and whose LEDs are spare.</para>
        /// <para>EXCLUSION: a scenario naming one of the product's outputs consumes it, so the product is driven
        /// even though it owns no follow-link — a lamp module recalled by a scene carries no link at all. That is
        /// decidable from the file; a product held in reserve, or driven from a controller-side integration, is
        /// not, and stays as the Warning's noise.</para>
        /// <para>SHAPE: the product is the primary location and its pins the related ones — the reader has to see
        /// what the device offers to decide whether it should have been wired.</para>
        /// </summary>
        private static void ProductUnwired(IProjectInspection inspection)
        {
            IReadOnlySet<string> sceneTargets = SceneTargetIds(inspection);
            foreach (ProjectElement product in AllProducts(inspection.Analyses))
            {
                // One pass that stops at the first wired pin: the reported case is the rare one, so the pin list
                // is only built for a product that is still a candidate when the walk ends.
                List<ProjectElement>? unwired = null;
                bool wired = false;
                foreach (ProjectElement pin in product.Descendants())
                {
                    if (!ProductPinTags.Contains(pin.Tag))
                    {
                        continue;
                    }

                    if (OwnsAnyLink(pin) || IsSceneTarget(pin, sceneTargets))
                    {
                        wired = true;
                        break;
                    }

                    (unwired ??= []).Add(pin);
                }

                // No pin at all is struct-product-no-terminals' subject, not this one — the two do not overlap.
                if (wired || unwired is null)
                {
                    continue;
                }

                inspection.ReportGroup(product, [.. unwired], Arguments(("product", Name(product))));
            }
        }

        /// <summary>
        /// A product output driven by more than one source: two blocks assign the same physical output and the last
        /// writer wins, so behaviour depends on timing.
        /// <para>SUBJECT: every product output pin's incoming halves. BOUNDARY: one driver is the normal case, two
        /// is the finding. SHAPE: the pin is the primary location and every driving half is a related one — the
        /// repair is to remove one of them, and the reader cannot choose without seeing them all.</para>
        /// </summary>
        private static void OutputMultidriven(IProjectInspection inspection)
        {
            foreach (ProjectElement pin in Pins(inspection, ProductOutputTags))
            {
                ImmutableArray<ProjectElement> drivers =
                    [.. pin.Children.Where(c => c.Tag == ReciprocalTags.FollowLinkToTag)];
                if (drivers.Length > 1)
                {
                    inspection.ReportGroup(pin, drivers,
                        Arguments(("pin", Name(pin)), ("drivers", drivers.Length)));
                }
            }
        }

        /// <summary>
        /// A function block NONE of whose input pins owns a link: the block's trigger never arrives from the
        /// installation, so its logic can never run from a physical action.
        /// <para>SUBJECT: every function block that declares at least one input pin. PER BLOCK, not per pin, and
        /// that is the predicate's substance: a catalog block ships every input its behaviour offers and the author
        /// wires one, so a per-pin reading would state this row's consequence falsely once per alternative the
        /// author declined. SHAPE: the block is the primary location, its unfed inputs the related ones.</para>
        /// <para>EXCLUSION — <see cref="StartsWithoutAnInputPin"/>: a block that starts itself is not waiting for a
        /// wire, so "the trigger never arrives" is false of it. A clock block, one that runs at power-up, a
        /// home-simulation block: each starts from something other than an input pin, and the file says which.</para>
        /// </summary>
        private static void BlockInputsUnfed(IProjectInspection inspection) =>
            ReportUnwiredSide(inspection, "inputs", pin => pin.Tag == "resource_input",
                block => !StartsWithoutAnInputPin(inspection.Analyses.Topology, block));

        /// <summary>
        /// A function block NONE of whose outputs is consumed: it computes a result nothing reads.
        /// <para>SUBJECT: every function block that declares at least one output pin. A scenario counts as a
        /// consumer — a <c>resource_scene</c> pin carrying a <c>scene_link</c> is read when the scenario fires — so
        /// a block that only drives scenes is not reported. PER BLOCK for the same reason as its input twin, and
        /// with the same shape. EXCLUSIONS: none beyond that — a block that starts itself still computes a result,
        /// so its twin's exclusion has nothing to say here.</para>
        /// </summary>
        private static void BlockOutputsUnused(IProjectInspection inspection) =>
            ReportUnwiredSide(inspection, "outputs", pin => pin.Tag is "resource_output" or "resource_scene");

        /// <summary>
        /// A function block that receives a link but carries no programs: the signal enters the block and stops
        /// there.
        /// <para>SUBJECT: every function block whose <c>programs</c> container holds no program. EXCLUSION: a block
        /// nothing links INTO is merely unused and is the input twin's finding, not this one — this row is about a
        /// wire that leads nowhere. SHAPE: the block is the primary location and the incoming halves are the
        /// related ones, since the repair is to write the logic or remove those wires.</para>
        /// </summary>
        private static void ThroughEmptyBlock(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (Programs(block).Length > 0)
                {
                    continue;
                }

                ImmutableArray<ProjectElement> incoming =
                    [.. block.DescendantsAndSelf().Where(e => e.Tag == ReciprocalTags.FollowLinkToTag)];
                if (incoming.Length > 0)
                {
                    // No count argument (D3): the sentence never named one, and the number of incoming halves
                    // is the related-location count the reader already sees.
                    inspection.ReportGroup(block, incoming, Arguments(("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// A block whose only logic copies one input straight to one output, AND whose two neighbours could be
        /// linked directly instead: the block adds nothing to the path.
        /// <para>
        /// SUBJECT: every function block. CONDITION: exactly one program; that program's <c>events</c> holds
        /// exactly one <c>event</c> naming one of the block's input pins; its top-level <c>actions</c> holds exactly
        /// one <c>action</c> naming one of the block's output pins, and nothing else — no condition, no
        /// sub-program, no case.
        /// </para>
        /// <para>
        /// EXCLUSION, and it is what makes the row true: the bypass must be LEGAL. IHC routes every
        /// product-to-product path through a block, so a block between a button and a lamp cannot be removed and
        /// "the two devices could be linked through a simpler path" would be false of it.
        /// <see cref="LinkRoles.CanLink"/> — the shipped measured model — decides that, over the upstream source
        /// that feeds the input and the downstream sink the output drives.
        /// </para>
        /// <para>
        /// NOT EXAMINED: what the action DOES with the value (assign, invert, pulse). The row's consequence holds
        /// for any single-event-to-single-action mapping, and the operation vocabulary is the program rows' own
        /// concern rather than this one's.
        /// </para>
        /// </summary>
        private static void PassThrough(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (SingleCopyProgram(block) is not (ProjectElement input, ProjectElement output))
                {
                    continue;
                }

                if (UpstreamSource(topology, input) is not { } source || DownstreamSink(topology, output) is not { } sink)
                {
                    continue;
                }

                if (LinkRoles.CanLink(source.Tag, sink.Tag))
                {
                    inspection.Report(block, Arguments(("block", Name(block))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>One block-level finding per unwired side, shared by the two block rows so both judge alike.</summary>
        /// <param name="inspection">The run being reported into.</param>
        /// <param name="container">The pin container to judge — <c>inputs</c> or <c>outputs</c>.</param>
        /// <param name="isPin">Which of that container's members count as pins.</param>
        /// <param name="subject">
        /// Which blocks the row is about at all; null for every block. It is a SUBJECT filter rather than a second
        /// pin test, because the input row's exclusion is a fact about the block and not about any of its pins.
        /// It is asked LAST although it is a subject test, because it is the only expensive one here — it reads
        /// the block's programs, while the two above it read one container's children — and a block that owns a
        /// wired pin is dropped either way.
        /// </param>
        private static void ReportUnwiredSide(
            IProjectInspection inspection, string container, Func<ProjectElement, bool> isPin,
            Func<ProjectElement, bool>? subject = null)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                if (block.FindChild(container) is not { } section)
                {
                    continue;
                }

                ImmutableArray<ProjectElement> pins = [.. section.Children.Where(isPin)];
                if (pins.Length == 0 || pins.Any(OwnsAnyLink))
                {
                    continue;
                }

                if (subject is not null && !subject(block))
                {
                    continue;
                }

                // No count argument (D3): see link-through-empty-block for the reasoning.
                inspection.ReportGroup(block, pins, Arguments(("block", Name(block))));
            }
        }

        /// <summary>The block's single input-to-output copy program, as the (input pin, output pin) pair it wires.</summary>
        private static (ProjectElement Input, ProjectElement Output)? SingleCopyProgram(ProjectElement block)
        {
            ImmutableArray<ProjectElement> programs = Programs(block);
            if (programs.Length != 1
                || programs[0].FindChild("events") is not { } events
                || programs[0].FindChild("actions") is not { } actions)
            {
                return null;
            }

            if (events.Children is not [{ Tag: "event" } trigger])
            {
                return null;
            }

            // ONE action and nothing else: a sub-program, a case or a second command is logic, not a copy.
            if (actions.Children is not [{ Tag: "action" } command])
            {
                return null;
            }

            ProjectElement? input = PinOf(block, "inputs", trigger.GetAttribute("link1"), "resource_input");
            ProjectElement? output = PinOf(block, "outputs", command.GetAttribute("link1"), "resource_output");
            return input is not null && output is not null ? (input, output) : null;
        }

        /// <summary>The block's own pin of the given kind carrying that id token, or null.</summary>
        private static ProjectElement? PinOf(ProjectElement block, string container, string? token, string tag) =>
            token is null || block.FindChild(container) is not { } section
                ? null
                : section.Children.FirstOrDefault(p => p.Tag == tag && p.GetAttribute("id") == token);

        /// <summary>The pin driving this sink pin, resolved through its incoming half's partner.</summary>
        private static ProjectElement? UpstreamSource(ITopologyAnalysis topology, ProjectElement sink) =>
            Partnered(topology, sink, ReciprocalTags.FollowLinkToTag);

        /// <summary>The pin this source pin drives, resolved through its outgoing half's partner.</summary>
        private static ProjectElement? DownstreamSink(ITopologyAnalysis topology, ProjectElement source) =>
            Partnered(topology, source, ReciprocalTags.FollowLinkFromTag);

        private static ProjectElement? Partnered(ITopologyAnalysis topology, ProjectElement pin, string halfTag) =>
            pin.Children.FirstOrDefault(c => c.Tag == halfTag) is { } half
            && topology.ByToken(half.GetAttribute("link")) is { } partner
                ? topology.Parent(partner)
                : null;

        /// <summary>
        /// Whether the block has a trigger that does not come from one of its own input pins: an
        /// <c>event_power</c>, or an <c>event</c> whose <c>link1</c> resolves to something outside its
        /// <c>inputs</c> container — its own clock or state variable, or a resource another block owns.
        /// <para>
        /// A DANGLING <c>link1</c> DOES NOT COUNT. It names no element, so nothing can be said about where the
        /// trigger comes from, and the reference itself is <c>idref-dangling</c>'s finding.
        /// </para>
        /// </summary>
        private static bool StartsWithoutAnInputPin(ITopologyAnalysis topology, ProjectElement block)
        {
            ProjectElement? inputs = block.FindChild("inputs");
            return block.FindDescendantOrSelf(statement =>
                statement.Tag == "event_power"
                || (statement.Tag == "event"
                    && topology.ByToken(statement.GetAttribute("link1")) is { } trigger
                    && (inputs is null || !ReferenceEquals(topology.Parent(trigger), inputs)))) is not null;
        }

        /// <summary>Whether the pin owns a follow-link half of either direction.</summary>
        private static bool OwnsAnyLink(ProjectElement pin) =>
            pin.Children.Any(c => ReciprocalTags.FollowLinkHalfTags.Contains(c.Tag)
                || c.Tag == ReciprocalTags.SceneLinkTag);

        /// <summary>Whether a scenario names this pin as the output it drives.</summary>
        private static bool IsSceneTarget(ProjectElement pin, IReadOnlySet<string> sceneTargets) =>
            pin.GetAttribute("id") is { } id && sceneTargets.Contains(id);

        /// <summary>The output resources every <c>scenes</c> container names — a scenario's own targets.</summary>
        private static HashSet<string> SceneTargetIds(IProjectInspection inspection)
        {
            HashSet<string> targets = new(StringComparer.Ordinal);
            foreach (ProjectElement scenes in inspection.Analyses.WithTag(ReciprocalTags.SceneContainerTag))
            {
                if (scenes.GetAttribute("scene_resource") is { } target)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }

        /// <summary>
        /// Pins of any of the given tags, in DOCUMENT order — filtered off the shared walk rather than
        /// concatenated from per-tag buckets, because concatenation would order by tag and the executor carries a
        /// rule's emission order into its findings.
        /// </summary>
        private static IEnumerable<ProjectElement> Pins(IProjectInspection inspection, ImmutableHashSet<string> tags) =>
            inspection.Analyses.Elements.Where(e => tags.Contains(e.Tag));


        private static ImmutableArray<ProjectElement> Programs(ProjectElement block) =>
            block.FindChild("programs") is { } programs
                ? [.. programs.Children.Where(p => p.Tag is "program_simple" or "program_sub")]
                : [];

    }
}
