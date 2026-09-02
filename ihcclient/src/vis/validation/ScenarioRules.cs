using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The SCENARIO rules: whether a scene can fire, what it does when it does, and whether anything can
    /// fire it.
    ///
    /// <para><b>The shape of a scene, since every predicate here rests on it.</b> A SCENE is a function block's
    /// <c>resource_scene</c> pin. Its MEMBERS are the <c>scene_link</c> rows it holds, each pointing at a member
    /// row — <c>scene_relay</c> / <c>scene_dimmer</c> / <c>scene_shutter</c> — inside some product's
    /// <c>scenes</c> container. That container names the OUTPUT the row drives, in its own
    /// <c>scene_resource</c> attribute: a member row carries the VALUE, the container carries the target.</para>
    ///
    /// <para><b>What is deliberately not here.</b> A one-sided pair is <c>scene-bijection</c>'s finding and stays
    /// there; these rules assume nothing about reciprocity and simply skip a half they cannot resolve, so a broken
    /// reference is reported once by the rule that is about broken references rather than twice.</para>
    ///
    /// <para><b>The one number.</b> <c>scene-long-delay</c>'s threshold is declared DATA on its catalogue entry
    /// with its derivation and its unconfirmed status, never a literal here — the row says "unusually long" and
    /// names no figure, so the figure has to be citable.</para>
    /// </summary>
    public static class ScenarioRules
    {
        /// <summary>The scene pin — a function block's scenario output.</summary>
        private const string ScenePinTag = "resource_scene";

        /// <summary>The attribute naming the output a <see cref="ReciprocalTags.SceneContainerTag"/> drives.</summary>
        private const string BoundOutputAttribute = "scene_resource";

        /// <summary>The threshold name the long-delay entry declares, referenced rather than repeated.</summary>
        private const string RampThresholdName = "MaxRampSeconds";

        /// <summary>The light level a dimmer member row drives its output to, in whole percent.</summary>
        private const string DimmingValueAttribute = "dimming_value";

        /// <summary>
        /// The program attributes that can NAME a scene pin and thereby fire it: an event's or condition's or
        /// action's operand, and a case branch's variable. Listed here because "reachable" is exactly "some
        /// program row names it", and a rule that walked every attribute would count the scene's own members.
        /// </summary>
        private static readonly ImmutableArray<string> ProgramOperandAttributes =
            ["link1", "link2", "variable", "value"];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "scene-duplicate-target", DuplicateTarget),
                Rule(catalog, "scene-member-unwired", MemberUnwired),
                Rule(catalog, "scene-unreferenced", Unreferenced),
                Rule(catalog, "scene-all-off", AllOff),
                Rule(catalog, "scene-long-delay", LongDelay(catalog)),
                Rule(catalog, "scene-dimming-out-of-range", DimmingOutOfRange(catalog)),
                Rule(catalog, "rs485-dimmer-scenario-recall", DimmerScenarioRecall),
                Rule(catalog, "rs485-dimmer-scene-multi-off", DimmerSceneMultiOff));
        }

        /// <summary>
        /// An affected RS-485 LED dimmer driven through scenario recall — the defect the user cannot fix from
        /// the application, because it takes DIMMER firmware and an upload never applies that.
        /// <para>DRIVEN MEANS A MEMBER ROW EXISTS under one of the dimmer's channels. Placing the dimmer is not
        /// enough: <c>rs485-dimmer-firmware-link-errors</c> already reports mere placement, and this row is about
        /// what the project asks the device to DO. An authentic corpus file carries a dimmer with empty scene
        /// containers, so the distinction is measured rather than hypothetical.</para>
        /// <para>PER DIMMER, not per row: two scene rows on one device are still one device to re-flash.</para>
        /// </summary>
        private static void DimmerScenarioRecall(IProjectInspection inspection)
        {
            foreach (ProjectElement dimmer in inspection.Analyses.WithTag(Rs485LedDimmerTag))
            {
                if (dimmer.GetAttribute(ProductIdentifierAttribute) == Rs485LedDimmerId
                    && dimmer.Children.Any(channel => channel.Children
                        .Any(c => c.Tag == ReciprocalTags.SceneContainerTag && c.Children
                            .Any(row => ReciprocalTags.SceneMemberTags.Contains(row.Tag)))))
                {
                    inspection.Report(dimmer, Arguments(("product", Name(dimmer))));
                }
            }
        }

        /// <summary>
        /// One scene commanding SEVERAL affected LED dimmers off at once: only one of them can respond, because
        /// the quick successive channel commands cross-talk.
        /// <para>OFF IS THE VALUE, NOT A WORD — a <c>scene_dimmer</c> row carries a <c>dimming_value</c> and
        /// never an on/off token, so this reuses <see cref="IsOff"/>, the same reading <c>scene-all-off</c> uses.
        /// A row at zero is perfectly LEGAL (it is the floor <c>scene-dimming-out-of-range</c> accepts); the
        /// condition is how many legal rows fire together.</para>
        /// <para>COUNTED OVER DIMMERS, NOT ROWS. A dimmer has two channels and each can carry its own row, so a
        /// row count would report one device commanded off on both channels — one device responding, which is
        /// the case that works.</para>
        /// </summary>
        private static void DimmerSceneMultiOff(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement scene in Scenes(inspection))
            {
                // Allocated only once a scene is known to command an affected dimmer off: the overwhelming
                // majority touch none, and the set is what distinguishes DEVICES from the rows naming them.
                HashSet<ProjectElement>? dimmers = null;
                foreach (ProjectElement half in Members(scene))
                {
                    if (MemberOf(topology, half) is { } member
                        && IsOff(member)
                        && topology.Parent(member) is { Tag: ReciprocalTags.SceneContainerTag } container
                        && topology.NearestAncestorOrSelf(container, Rs485LedDimmerTag) is { } dimmer
                        && dimmer.GetAttribute(ProductIdentifierAttribute) == Rs485LedDimmerId)
                    {
                        dimmers ??= new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);
                        dimmers.Add(dimmer);
                    }
                }

                if (dimmers is { Count: > 1 })
                {
                    inspection.Report(scene, Arguments(("scene", Name(scene)), ("dimmers", dimmers.Count)));
                }
            }
        }

        /// <summary>
        /// One scene driving the same output through two member rows: the rows contradict each other for that
        /// output.
        /// <para>SUBJECT: the members of one scene, grouped by the output their container binds. The same output in
        /// a DIFFERENT scene is legitimate and is not this row. SHAPE: the scene anchors, the colliding member rows
        /// are the related locations — the repair is to delete one of them.</para>
        /// </summary>
        private static void DuplicateTarget(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement scene in Scenes(inspection))
            {
                foreach (IGrouping<ProjectElement, ProjectElement> group in Members(scene)
                    .Select(half => (Half: half, Output: OutputOf(topology, half)))
                    .Where(pair => pair.Output is not null)
                    // BY IDENTITY: two member rows can carry identical content and still be two rows.
                    //
                    // The cast is load-bearing and not noise. ReferenceEqualityComparer implements
                    // IEqualityComparer<object?>, so handing it over bare lets generic inference settle on
                    // TKey = object; the loop's declared element type then compiles as an EXPLICIT foreach
                    // conversion and throws InvalidCastException at run time. Pinning TKey here keeps the
                    // conversion where the compiler can check it.
                    .GroupBy(pair => pair.Output!, pair => pair.Half,
                        (IEqualityComparer<ProjectElement>)ReferenceEqualityComparer.Instance))
                {
                    ImmutableArray<ProjectElement> halves = [.. group];
                    if (halves.Length > 1)
                    {
                        inspection.ReportGroup(scene, halves,
                            Arguments(("scene", Name(scene)), ("output", Name(group.Key))));
                    }
                }
            }
        }

        /// <summary>
        /// A member row whose container names no output: the row carries a value for nothing.
        /// <para>SUBJECT: every member row in a <c>scenes</c> container. CONDITION: the container's
        /// <c>scene_resource</c> is absent, empty, or names an element that is not in the project. EXCLUSION: the
        /// member's own <c>link</c> — its scene half — is NOT examined here; a one-sided pair is
        /// <c>scene-bijection</c>'s finding. ARGUMENTS: the product the row sits in, because the row's own name is
        /// the format's generic "Scenarie link" and would identify nothing.</para>
        /// </summary>
        private static void MemberUnwired(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement container in Containers(inspection))
            {
                if (topology.ByToken(container.GetAttribute(BoundOutputAttribute)) is not null)
                {
                    continue;
                }

                foreach (ProjectElement member in container.Children
                    .Where(c => ReciprocalTags.SceneMemberTags.Contains(c.Tag)))
                {
                    inspection.Report(member, Arguments(("product", Name(Owner(topology, container)))));
                }
            }
        }

        /// <summary>
        /// A scene nothing can fire: no program row anywhere names it.
        /// <para>SUBJECT: every <c>resource_scene</c> pin. CONDITION: no <c>event</c>/<c>condition</c>/<c>action</c>
        /// operand and no case-branch variable names the pin's id.
        /// MEASURED, and it is why the scan needs no exclusion: a scene's own halves never name the pin. A
        /// <c>scene_link</c>'s <c>link</c> names the member ROW and the row's names the half back, so the members
        /// cannot make their own scene look reachable — only a program operand can. The legitimate reading (fired
        /// from the controller app or an external integration) is why this is a Warning.</para>
        /// </summary>
        private static void Unreferenced(IProjectInspection inspection)
        {
            HashSet<string> fired = ProgramOperands(inspection);
            foreach (ProjectElement scene in Scenes(inspection))
            {
                if (scene.GetAttribute("id") is { } id && !fired.Contains(id))
                {
                    inspection.Report(scene, Arguments(("scene", Name(scene))));
                }
            }
        }

        /// <summary>
        /// A scene every member of which switches its output off or to zero — an "all off" scene, or an unfinished
        /// one.
        /// <para>SUBJECT: every scene with at least one resolvable member. EXCLUSION: a scene holding a
        /// <c>scene_shutter</c> member is skipped — a shutter position is up or down and neither is "off", so
        /// "every member sets its output off" cannot be decided for it. ARGUMENTS: the scene's name and how many
        /// members are off, so the reader can see the scale of what would change.</para>
        /// </summary>
        private static void AllOff(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (ProjectElement scene in Scenes(inspection))
            {
                ImmutableArray<ProjectElement> members =
                    [.. Members(scene).Select(half => MemberOf(topology, half)).OfType<ProjectElement>()];
                if (members.Length == 0 || members.Any(m => m.Tag == "scene_shutter"))
                {
                    continue;
                }

                if (members.All(IsOff))
                {
                    inspection.ReportGroup(scene, members,
                        Arguments(("scene", Name(scene)), ("members", members.Length)));
                }
            }
        }

        /// <summary>
        /// A member row whose ramp time exceeds the declared maximum: the installation appears unresponsive while
        /// the scene runs.
        /// <para>SUBJECT: every <c>scene_dimmer</c> member row carrying a <c>ramptime_ms</c>. THRESHOLD: the
        /// entry's declared <c>MaxRampSeconds</c> — read from the catalogue, never written here. BOUNDARY: a ramp
        /// exactly AT the threshold is not reported; one millisecond past it is. EXCLUSION: a relay or shutter
        /// member has no ramp at all.</para>
        /// </summary>
        private static ProjectInspection LongDelay(ProblemCatalog catalog)
        {
            double limitSeconds = Threshold(catalog, "scene-long-delay", RampThresholdName);
            return inspection =>
            {
                foreach (ProjectElement member in inspection.Analyses.WithTag("scene_dimmer"))
                {
                    if (Milliseconds(member.GetAttribute("ramptime_ms")) is not { } ms)
                    {
                        continue;
                    }

                    double seconds = ms / 1000d;
                    if (seconds > limitSeconds)
                    {
                        inspection.Report(member, Arguments(
                            ("seconds", Math.Round(seconds, 1)), ("limit", limitSeconds)));
                    }
                }
            };
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        private static EquatableArray<ProjectElement> Scenes(IProjectInspection inspection) =>
            inspection.Analyses.WithTag(ScenePinTag);

        private static EquatableArray<ProjectElement> Containers(IProjectInspection inspection) =>
            inspection.Analyses.WithTag(ReciprocalTags.SceneContainerTag);

        /// <summary>The scene's own member halves — the <c>scene_link</c> rows it holds.</summary>
        private static ImmutableArray<ProjectElement> Members(ProjectElement scene) =>
            [.. scene.Children.Where(c => c.Tag == ReciprocalTags.SceneLinkTag)];

        /// <summary>The member ROW a scene half points at, or null when the reference does not resolve.</summary>
        private static ProjectElement? MemberOf(ITopologyAnalysis topology, ProjectElement half) =>
            topology.ByToken(half.GetAttribute("link")) is { } member
            && ReciprocalTags.SceneMemberTags.Contains(member.Tag)
                ? member
                : null;

        /// <summary>The OUTPUT a scene half ultimately drives: its member row's container's bound resource.</summary>
        private static ProjectElement? OutputOf(ITopologyAnalysis topology, ProjectElement half) =>
            MemberOf(topology, half) is { } member
            && topology.Parent(member) is { Tag: ReciprocalTags.SceneContainerTag } container
                ? topology.ByToken(container.GetAttribute(BoundOutputAttribute))
                : null;

        /// <summary>The element a container hangs under — the product, for naming a finding.</summary>
        private static ProjectElement Owner(ITopologyAnalysis topology, ProjectElement container) =>
            topology.Parent(container) ?? container;

        /// <summary>Whether a member row switches its output OFF: a relay to off, a dimmer to zero.</summary>
        private static bool IsOff(ProjectElement member) => member.Tag switch
        {
            "scene_relay" => member.GetAttribute("relay_value") is null or "off",
            "scene_dimmer" => (Milliseconds(member.GetAttribute(DimmingValueAttribute)) ?? 0) == 0,
            _ => false,
        };

        /// <summary>Every id token a program row names as an operand — what can FIRE a scene.</summary>
        private static HashSet<string> ProgramOperands(IProjectInspection inspection)
        {
            HashSet<string> operands = new(StringComparer.Ordinal);
            foreach (ProjectElement row in inspection.Analyses.Elements)
            {
                if (row.Tag is not ("event" or "event_power" or "condition" or "action" or "case_action"
                    or "program_case"))
                {
                    continue;
                }

                foreach (string attribute in ProgramOperandAttributes)
                {
                    if (row.GetAttribute(attribute) is { Length: > 0 } token)
                    {
                        operands.Add(token);
                    }
                }
            }

            return operands;
        }

        /// <summary>
        /// A member row whose light level is outside the legal percentage range: no dimmer can act on it, and the
        /// vendor dialog silently zeroes it on commit.
        /// <para>SUBJECT: every <c>scene_dimmer</c> member row carrying a <c>dimming_value</c>. BOUNDS: both
        /// declared on the entry and both INCLUSIVE — 0 and 100 are legal. EXCLUSION: a row carrying no value, or
        /// one that does not parse. Reading a missing attribute as 0 would report every relay row in the corpus,
        /// and "unset" is a different state that other rows own.</para>
        /// </summary>
        /// <param name="catalog">The catalogue the entry, and both declared bounds, are declared in.</param>
        private static ProjectInspection DimmingOutOfRange(ProblemCatalog catalog)
        {
            double minimum = Threshold(catalog, "scene-dimming-out-of-range", "DimmingMinimum");
            double maximum = Threshold(catalog, "scene-dimming-out-of-range", "DimmingMaximum");
            return inspection =>
            {
                foreach (ProjectElement member in inspection.Analyses.WithTag("scene_dimmer"))
                {
                    if (Milliseconds(member.GetAttribute(DimmingValueAttribute)) is not { } level)
                    {
                        continue;
                    }

                    if (level < minimum || level > maximum)
                    {
                        inspection.Report(member, Arguments(
                            ("member", Name(member)), ("value", level),
                            ("minimum", (int)minimum), ("maximum", (int)maximum)));
                    }
                }
            };
        }

        private static long? Milliseconds(string? raw) =>
            long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : null;


    }
}
