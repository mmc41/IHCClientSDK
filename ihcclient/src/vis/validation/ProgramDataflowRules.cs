#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

using static Ihc.Vis.Validation.RuleAuthoring;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The remaining LOGIC rows, all of them predicates over <see cref="IProgramUsageAnalysis"/>: an output
    /// nothing drives, a flag that latches, a counter that never returns, a program that retriggers itself, and a
    /// block whose call path reaches itself.
    ///
    /// <para><b>A SUB-PROGRAM IS NOT A PROGRAM for the "which program" rows.</b> Its trigger is its parent's —
    /// the format gives it no <c>events</c> container at all — so a write attributed to the nearest enclosing row
    /// instead of the top-level program reasons about a unit that never starts on its own.</para>
    ///
    /// <para><b>Two rows need to know what a command DOES</b>, and both get it without a token table:
    /// <c>logic-counter-never-reset</c> reuses the model's self-modifying test (an increment is
    /// <c>%P = %P + …</c>, a reset is a plain assignment), and <c>logic-flag-never-cleared</c> keys on the one
    /// bool-command token that only ever sets.</para>
    /// </summary>
    public static class ProgramDataflowRules
    {
        /// <summary>
        /// The bool command that can only SET: <c>%P = ON</c>. Its siblings can all clear — <c>%P = OFF</c>
        /// obviously, <c>Kip %P</c> half the time, and the two-operand assigns whenever their source is off — so a
        /// flag written by anything else is not "cleared by none".
        /// </summary>
        private const string SetOnToken = "_0xa";

        /// <summary>
        /// The two variable kinds whose self-write is the IDIOM rather than the fault, by element tag.
        /// <para>
        /// BY TAG, NOT BY NAME OR METHOD. Both kinds exist to be re-armed and stepped by the very logic they
        /// start — a delay and a pulse count — and the format names them, so the distinction needs neither a
        /// command table nor a reading of the author's Danish. See <see cref="SelfTrigger"/> for why the row's
        /// consequence is false of them.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> RearmingKindTags =
            ["resource_timer", "resource_counter"];

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-output-never-assigned", OutputNeverAssigned),
                Rule(catalog, "logic-flag-never-cleared", FlagNeverCleared),
                Rule(catalog, "logic-counter-never-reset", CounterNeverReset),
                Rule(catalog, "logic-self-trigger", SelfTrigger),
                Rule(catalog, "logic-block-recursive", BlockRecursive));
        }

        /// <summary>
        /// A block whose program path reaches ITSELF: the recursion runs in the simulator and does nothing on
        /// the controller.
        /// <para>
        /// THE GRAPH IS BUILT FROM THE RUN'S EXISTING ANALYSES and nothing else — <c>Usage</c> supplies every
        /// trigger and every write, <c>Topology</c> says which block a program sits in. No second traversal.
        /// </para>
        /// <para>
        /// EACH BLOCK IS CONTRACTED TO ONE NODE BEFORE THE SEARCH, and that is what makes the row's subject
        /// exact. A31 is about a recursive CALL: the path has to leave the block and come back. Two of a block's
        /// own programs signalling each other over its internal settings never leave it — the vendor's shipped
        /// library blocks are built that way — and contracting them to one node means they generate no edge at
        /// all rather than a self-loop. A program outside every block is its own node.
        /// </para>
        /// <para>
        /// The same contraction excludes what <c>logic-self-trigger</c> already reports: a single program
        /// triggered by a variable it assigns is one node writing to itself. No case analysis is needed for
        /// either exclusion — both are the one rule that an edge must join two DIFFERENT nodes.
        /// </para>
        /// <para>
        /// Every block on a cycle is reported, not just one: each is separately the place a reader could break
        /// the loop, and naming only the "closing" block would depend on traversal order.
        /// </para>
        /// </summary>
        private static void BlockRecursive(IProjectInspection inspection)
        {
            (ITopologyAnalysis topology,
             Dictionary<ProjectElement, HashSet<ProjectElement>> triggers,
             Dictionary<ProjectElement, HashSet<ProjectElement>> writes) = BuildDataflow(inspection);

            ProjectElement Node(ProjectElement program) =>
                topology.NearestAncestorOrSelf(program, "functionblock") ?? program;

            // INVERTED FIRST, because the edge test is a set intersection: pairing every writer with every
            // triggered program asks it once per PAIR, where keying the triggered nodes by the variable that
            // triggers them asks it once per WRITE. The contraction is also resolved once per program here
            // rather than once per pair.
            var triggeredBy = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            foreach ((ProjectElement triggered, HashSet<ProjectElement> by) in triggers)
            {
                ProjectElement to = Node(triggered);
                foreach (ProjectElement variable in by)
                {
                    if (!triggeredBy.TryGetValue(variable, out HashSet<ProjectElement>? reached))
                    {
                        triggeredBy[variable] = reached = new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);
                    }

                    reached.Add(to);
                }
            }

            var edges = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            foreach ((ProjectElement writer, HashSet<ProjectElement> written) in writes)
            {
                ProjectElement from = Node(writer);
                foreach (ProjectElement variable in written)
                {
                    if (!triggeredBy.TryGetValue(variable, out HashSet<ProjectElement>? targets))
                    {
                        continue;
                    }

                    foreach (ProjectElement to in targets)
                    {
                        if (ReferenceEquals(from, to))
                        {
                            continue;
                        }

                        if (!edges.TryGetValue(from, out HashSet<ProjectElement>? reached))
                        {
                            edges[from] = reached = new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);
                        }

                        reached.Add(to);
                    }
                }
            }

            foreach (ProjectElement node in NodesOnACycle(edges))
            {
                if (node.Tag == "functionblock")
                {
                    inspection.Report(node, Arguments(("name", Name(node))));
                }
            }
        }

        /// <summary>
        /// Every node that lies on a cycle of the call graph, by depth-first search with an explicit stack.
        /// <para>A node is on a cycle when the search reaches it again while it is still on the current path;
        /// every node currently on that path is then on the cycle too.</para>
        /// <para>
        /// THE STACK IS EXPLICIT BECAUSE THE DEPTH IS THE PROJECT'S, not this engine's. The search descends once
        /// per node on the path, so a long enough chain of blocks calling blocks would recurse as deep as the
        /// file is long — and a blown call stack is the one failure a caller cannot catch, in a component whose
        /// whole contract is to report on a file rather than fall over on one. Each frame carries the node and
        /// its own position in its child list, which is exactly what the recursion kept for it.
        /// </para>
        /// </summary>
        /// <param name="edges">The contracted call graph — a node is a function block, or a program outside one.</param>
        private static HashSet<ProjectElement> NodesOnACycle(
            Dictionary<ProjectElement, HashSet<ProjectElement>> edges)
        {
            var onCycle = new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);
            var settled = new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);

            // The path, and each node's DEPTH on it. The depth is what the cycle mark needs — everything from the
            // revisited node's depth to the top of the path is on the cycle — so one map replaces a membership
            // set plus a linear search for the position that set could not report.
            var path = new List<ProjectElement>();
            var depth = new Dictionary<ProjectElement, int>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<(ProjectElement Node, IEnumerator<ProjectElement> Children)>();

            void Descend(ProjectElement node)
            {
                depth[node] = path.Count;
                path.Add(node);
                stack.Push((node, edges.TryGetValue(node, out HashSet<ProjectElement>? next)
                    ? ((IEnumerable<ProjectElement>)next).GetEnumerator()
                    : Enumerable.Empty<ProjectElement>().GetEnumerator()));
            }

            foreach (ProjectElement root in edges.Keys)
            {
                if (settled.Contains(root))
                {
                    continue;
                }

                Descend(root);
                while (stack.Count > 0)
                {
                    (ProjectElement node, IEnumerator<ProjectElement> children) = stack.Peek();
                    if (children.MoveNext())
                    {
                        ProjectElement child = children.Current;
                        if (depth.TryGetValue(child, out int from))
                        {
                            // Everything from the child's position to here is a cycle.
                            for (int i = from; i < path.Count; i++)
                            {
                                onCycle.Add(path[i]);
                            }
                        }
                        else if (!settled.Contains(child))
                        {
                            Descend(child);
                        }

                        continue;
                    }

                    // The node's children are exhausted, which is where the recursion returned: it leaves the
                    // path and can never be on a cycle discovered later.
                    children.Dispose();
                    stack.Pop();
                    depth.Remove(node);
                    path.RemoveAt(path.Count - 1);
                    settled.Add(node);
                }
            }

            return onCycle;
        }

        /// <summary>
        /// An output pin wired to something but assigned by no program: the physical output can never change state.
        /// <para>THE LINK IS THE POINT: an unlinked output is <c>link-fb-output-unused</c>'s finding — nothing
        /// consumes it. This row is the other half: something DOES consume it, and nothing produces it.</para>
        /// </summary>
        private static void OutputNeverAssigned(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach (ProjectElement pin in Pins(inspection.Analyses, "outputs", "resource_output"))
            {
                if (usage.IsLinked(pin) && !usage.IsWritten(pin))
                {
                    inspection.Report(pin, Arguments(("variable", Name(pin))));
                }
            }
        }

        /// <summary>
        /// A flag some program sets and no program clears: it latches on and the logic never returns.
        /// <para>PREDICATE: every write to the flag is <c>%P = ON</c>. Measured over the corpus, a flag is only
        /// ever written by <c>%P = ON</c> or <c>%P = OFF</c>, so "cleared by none" is decidable from the commands
        /// themselves without asking what value the flag holds.</para>
        /// </summary>
        private static void FlagNeverCleared(IProjectInspection inspection)
        {
            foreach ((ProjectElement variable, ImmutableArray<VariableUsage> writes) in
                Written(inspection, "resource_flag"))
            {
                if (writes.All(w => w.Row.GetAttribute("method") == SetOnToken))
                {
                    inspection.Report(variable, Arguments(("variable", Name(variable))));
                }
            }
        }

        /// <summary>
        /// A counter that only ever steps and is never assigned: the count grows without bound.
        /// <para>
        /// NO TOKEN TABLE NEEDED: an increment is a self-modifying command (<c>%P = %P + 1</c>,
        /// <c>%P = %P + %S</c>) and a reset is a plain assignment (<c>%P = 0</c>, <c>%P = Initialværdi</c>,
        /// <c>%P = %S</c>), so the model's own self-modifying test answers it — the same fact T057 read off the
        /// row's stored template. A decrement-only counter is the same fault and is reported too.
        /// </para>
        /// </summary>
        private static void CounterNeverReset(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach ((ProjectElement variable, ImmutableArray<VariableUsage> writes) in
                Written(inspection, "resource_counter"))
            {
                if (writes.All(w => usage.IsSelfModifying(w.Row)))
                {
                    inspection.Report(variable, Arguments(("variable", Name(variable))));
                }
            }
        }

        /// <summary>
        /// A program triggered by a variable it also assigns: it can retrigger itself.
        /// <para>ATTRIBUTED TO THE TOP-LEVEL PROGRAM, because a sub-program assigning its parent's trigger is the
        /// same loop — the parent is what starts again.</para>
        /// <para>
        /// EXCLUDED BY KIND: <see cref="RearmingKindTags"/>. A timer re-armed by the program it woke is a delay and
        /// a counter stepped by the program it counts for is a tally — both are how the two kinds are meant to be
        /// used, and neither oscillates, because a timer must elapse again and a counter's step is not an edge the
        /// count re-fires. Reporting them stated an oscillation that could not happen. A flag, an output or an
        /// ordinary variable feeding itself back is still the loop this row exists for.
        /// </para>
        /// </summary>
        private static void SelfTrigger(IProjectInspection inspection)
        {
            (_,
             Dictionary<ProjectElement, HashSet<ProjectElement>> triggers,
             Dictionary<ProjectElement, HashSet<ProjectElement>> writes) = BuildDataflow(inspection);

            foreach ((ProjectElement program, HashSet<ProjectElement> triggered) in triggers)
            {
                if (!writes.TryGetValue(program, out HashSet<ProjectElement>? written))
                {
                    continue;
                }

                foreach (ProjectElement variable in triggered.Where(written.Contains))
                {
                    if (RearmingKindTags.Contains(variable.Tag))
                    {
                        continue;
                    }

                    inspection.Report(program, Arguments(
                        ("program", Name(program)), ("variable", Name(variable))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// The document's program dataflow: for each program, the variables that trigger it and the variables it
        /// writes, keyed by reference so two identically-named elements stay apart.
        /// </summary>
        /// <remarks>
        /// The two rows that reason about loops need the same two maps, and building them at each rule's head
        /// twice was two chances for one of them to key, seed or contract the edges differently from the other.
        /// The topology comes back with them because a caller that resolves a program to its enclosing block
        /// needs the same analysis the collection walked.
        /// </remarks>
        private static (ITopologyAnalysis Topology,
                        Dictionary<ProjectElement, HashSet<ProjectElement>> Triggers,
                        Dictionary<ProjectElement, HashSet<ProjectElement>> Writes)
            BuildDataflow(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;

            var triggers = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            var writes = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            Collect(topology, usage, triggers, writes);

            return (topology, triggers, writes);
        }

        /// <summary>Groups the model's triggers and writes by TOP-LEVEL program.</summary>
        private static void Collect(
            ITopologyAnalysis topology,
            IProgramUsageAnalysis usage,
            Dictionary<ProjectElement, HashSet<ProjectElement>> triggers,
            Dictionary<ProjectElement, HashSet<ProjectElement>> writes)
        {
            foreach (VariableUsage item in usage.Usages)
            {
                // A sub-program's trigger is its parent's, so the top-level program is the unit this reasons about.
                if (item.Kind == VariableUsageKind.Read
                    || topology.NearestAncestorOrSelf(item.Program, "program_simple") is not { } program)
                {
                    continue;
                }

                Dictionary<ProjectElement, HashSet<ProjectElement>> target =
                    item.Kind == VariableUsageKind.Trigger ? triggers : writes;
                if (!target.TryGetValue(program, out HashSet<ProjectElement>? variables))
                {
                    target[program] = variables =
                        new HashSet<ProjectElement>(ReferenceEqualityComparer.Instance);
                }

                variables.Add(item.Variable);
            }
        }

        /// <summary>The pins of one container kind, across every block.</summary>
        private static IEnumerable<ProjectElement> Pins(IProjectAnalyses analyses, string container, string tag) =>
            analyses.WithTag("functionblock")
                .Select(block => block.FindChild(container))
                .OfType<ProjectElement>()
                .SelectMany(section => section.Children.Where(c => c.Tag == tag));

        /// <summary>
        /// Every variable of the given tag that IS written, with its writes — the population the two
        /// "written only one way" rows are about.
        /// </summary>
        private static IEnumerable<(ProjectElement Variable, ImmutableArray<VariableUsage> Writes)> Written(
            IProjectInspection inspection, string tag)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach (ProjectElement variable in inspection.Analyses.WithTag(tag))
            {
                ImmutableArray<VariableUsage> writes =
                [
                    .. usage.Usages.Where(u => u.Kind == VariableUsageKind.Write
                        && ReferenceEquals(u.Variable, variable)),
                ];
                if (writes.Length > 0)
                {
                    yield return (variable, writes);
                }
            }
        }
    }
}
