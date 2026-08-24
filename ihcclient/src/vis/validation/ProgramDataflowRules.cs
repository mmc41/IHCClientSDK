#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The six remaining LOGIC rows, all of them predicates over <see cref="IProgramUsageAnalysis"/>: an output
    /// nothing drives, a flag that latches, a counter that never returns, a timer nothing starts, a program that
    /// retriggers itself, and two programs fighting over one value.
    ///
    /// <para><b>A SUB-PROGRAM IS NOT A PROGRAM for the two "which program" rows.</b> Its trigger is its parent's —
    /// the format gives it no <c>events</c> container at all — and two branches of one program are mutually
    /// exclusive rather than contending. Attributing writes to the nearest enclosing row instead of the top-level
    /// program is what made a first attempt report 17 contentions on <c>Project1</c>, a project with two blocks:
    /// every sub-program's trigger set was empty, and empty sets are trivially disjoint.</para>
    ///
    /// <para><b>And "unrelated triggers" is a DATAFLOW question, not a set difference.</b> A library block's
    /// standard shape is one program setting an output ON and another setting it OFF, each triggered by its own
    /// pulse flag — and both pulse flags are written by programs triggered by the SAME button. Comparing trigger
    /// variables directly reports that shape on every library block (24 on <c>project3</c>, 9 on <c>Project1</c>);
    /// comparing the transitive ANCESTOR sets — who writes my trigger, and what triggers them — reports 4 and 2,
    /// and those are the real ones: a timer driven from two sources, a blocking flag, a clock output.</para>
    ///
    /// <para><b>Two rows need to know what a command DOES</b>, and both get it without a token table:
    /// <c>logic-counter-never-reset</c> reuses the model's self-modifying test (an increment is
    /// <c>%P = %P + …</c>, a reset is a plain assignment), and <c>logic-flag-never-cleared</c> keys on the one
    /// bool-command token that only ever sets. The timer row is the exception: its three ACTIVATION tokens come
    /// from <see cref="Programs.ProgramMethodCatalog.TimerCommands"/>, cited rather than guessed.</para>
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
        /// The three commands that START a timer, from <see cref="Programs.ProgramMethodCatalog.TimerCommands"/>:
        /// activate count-down with initial value, activate count-up, and bare activate count-down. Stopping
        /// (<c>_0xdc</c>) and assigning (<c>_0xa</c>, <c>_0x19</c>, <c>_0x1e</c>) are not starting.
        /// </summary>
        private static readonly ImmutableHashSet<string> TimerStartTokens = ["_0xbe", "_0xc8", "_0xd2"];

        /// <summary>The six rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-output-never-assigned", OutputNeverAssigned),
                Rule(catalog, "logic-flag-never-cleared", FlagNeverCleared),
                Rule(catalog, "logic-counter-never-reset", CounterNeverReset),
                Rule(catalog, "logic-timer-unused", TimerNeverStarted),
                Rule(catalog, "logic-self-trigger", SelfTrigger),
                Rule(catalog, "logic-contending-writers", ContendingWriters));
        }

        private static RuleDefinition Rule(ProblemCatalog catalog, string code, ProjectInspection body) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Inspect(body).Build()
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

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
        /// A declared timer no program ever starts: the timer never runs.
        /// <para>STARTING IS NOT ASSIGNING: the three activation commands start a timer; setting it to zero or to
        /// its initial value does not. Which is why this row is not just "never written".</para>
        /// </summary>
        private static void TimerNeverStarted(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach (ProjectElement timer in Variables(inspection.Analyses, "resource_timer"))
            {
                bool started = usage.Usages.Any(u => u.Kind == VariableUsageKind.Write
                    && ReferenceEquals(u.Variable, timer)
                    && TimerStartTokens.Contains(u.Row.GetAttribute("method") ?? string.Empty));
                if (!started)
                {
                    inspection.Report(timer, Arguments(("variable", Name(timer))));
                }
            }
        }

        /// <summary>
        /// A program triggered by a variable it also assigns: it can retrigger itself.
        /// <para>ATTRIBUTED TO THE TOP-LEVEL PROGRAM, because a sub-program assigning its parent's trigger is the
        /// same loop — the parent is what starts again.</para>
        /// </summary>
        private static void SelfTrigger(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;

            var triggers = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            var writes = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            Collect(topology, usage, triggers, writes);

            foreach ((ProjectElement program, HashSet<ProjectElement> triggered) in triggers)
            {
                if (!writes.TryGetValue(program, out HashSet<ProjectElement>? written))
                {
                    continue;
                }

                foreach (ProjectElement variable in triggered.Where(written.Contains))
                {
                    inspection.Report(program, Arguments(
                        ("program", Name(program)), ("variable", Name(variable))));
                }
            }
        }

        /// <summary>
        /// Two programs assigning one variable from unrelated triggers: which value survives depends on event order.
        /// <para>
        /// UNRELATED IS MEASURED THROUGH THE DATAFLOW, not by comparing trigger variables: a library block's
        /// standard shape is one program setting an output ON and another setting it OFF, each triggered by its own
        /// pulse flag, and both pulse flags written by programs triggered by the SAME button. Those two triggers
        /// share an ANCESTOR, so the shape is related and not reported. Comparing the trigger variables directly
        /// reports it on every library block — 24 findings on <c>project3</c> against 4 for this reading.
        /// </para>
        /// <para>
        /// TWO FURTHER REQUIREMENTS, both from the row's stated consequence: each program must HAVE a trigger (a
        /// program that never starts cannot contend, and its own row already reports it), and the commands must
        /// DIFFER — two programs both setting the same output to ON do not depend on event order.
        /// </para>
        /// </summary>
        private static void ContendingWriters(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;

            var triggers = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            var writes = new Dictionary<ProjectElement, HashSet<ProjectElement>>(ReferenceEqualityComparer.Instance);
            Collect(topology, usage, triggers, writes);

            // variable -> the top-level programs that assign it, and the commands they use
            var writers = new Dictionary<ProjectElement, List<ProjectElement>>(ReferenceEqualityComparer.Instance);
            var commands = new Dictionary<ProjectElement, HashSet<string>>(ReferenceEqualityComparer.Instance);
            foreach (VariableUsage write in usage.Usages.Where(u => u.Kind == VariableUsageKind.Write))
            {
                if (TopLevel(topology, write.Program) is not { } program)
                {
                    continue;
                }

                if (!writers.TryGetValue(write.Variable, out List<ProjectElement>? programs))
                {
                    writers[write.Variable] = programs = [];
                }

                if (!programs.Any(p => ReferenceEquals(p, program)))
                {
                    programs.Add(program);
                }

                if (!commands.TryGetValue(write.Variable, out HashSet<string>? used))
                {
                    commands[write.Variable] = used = new HashSet<string>(StringComparer.Ordinal);
                }

                used.Add(write.Row.GetAttribute("method") ?? string.Empty);
            }

            foreach ((ProjectElement variable, List<ProjectElement> programs) in writers)
            {
                if (programs.Count < 2 || commands[variable].Count < 2)
                {
                    continue;
                }

                List<HashSet<ProjectElement>> ancestors =
                    [.. programs.Select(p => TriggerAncestors(p, triggers, writers))];
                if (ancestors.Any(a => a.Count == 0))
                {
                    continue;   // a program that never starts cannot contend
                }

                bool unrelated = ancestors
                    .SelectMany((a, i) => ancestors.Skip(i + 1).Select(b => (a, b)))
                    .All(pair => !pair.a.Overlaps(pair.b));
                if (unrelated)
                {
                    inspection.Report(variable, Arguments(
                        ("variable", Name(variable)), ("writers", programs.Count)));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// Every variable a program's trigger transitively depends on: the trigger itself, then whoever writes it,
        /// then what triggers THEM, and so on. Two programs are related when these sets meet.
        /// </summary>
        private static HashSet<ProjectElement> TriggerAncestors(
            ProjectElement program,
            Dictionary<ProjectElement, HashSet<ProjectElement>> triggers,
            Dictionary<ProjectElement, List<ProjectElement>> writers)
        {
            HashSet<ProjectElement> seen = new(ReferenceEqualityComparer.Instance);
            Stack<ProjectElement> pending = new();
            if (triggers.TryGetValue(program, out HashSet<ProjectElement>? own))
            {
                foreach (ProjectElement variable in own)
                {
                    pending.Push(variable);
                }
            }

            while (pending.Count > 0)
            {
                ProjectElement variable = pending.Pop();
                if (!seen.Add(variable) || !writers.TryGetValue(variable, out List<ProjectElement>? upstream))
                {
                    continue;
                }

                foreach (ProjectElement earlier in upstream)
                {
                    if (!triggers.TryGetValue(earlier, out HashSet<ProjectElement>? theirs))
                    {
                        continue;
                    }

                    foreach (ProjectElement candidate in theirs.Where(c => !seen.Contains(c)))
                    {
                        pending.Push(candidate);
                    }
                }
            }

            return seen;
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
                if (item.Kind == VariableUsageKind.Read || TopLevel(topology, item.Program) is not { } program)
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

        /// <summary>
        /// The triggered program a row ultimately belongs to. A sub-program's trigger is its parent's, so the
        /// parent is the unit both "which program" rows reason about.
        /// </summary>
        private static ProjectElement? TopLevel(ITopologyAnalysis topology, ProjectElement program) =>
            topology.NearestAncestorOrSelf(program, "program_simple");

        /// <summary>Every variable of the given tag the project declares, wherever it sits.</summary>
        private static IEnumerable<ProjectElement> Variables(IProjectAnalyses analyses, string tag) =>
            analyses.WithTag(tag);

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
            foreach (ProjectElement variable in Variables(inspection.Analyses, tag))
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

        private static string Name(ProjectElement element) =>
            element.GetAttribute("name") is { Length: > 0 } name ? name : element.Tag;

        private static EquatableArray<ProblemArgument> Arguments(params (string Name, object Value)[] bindings) =>
            [.. bindings.Select(b => new ProblemArgument(b.Name, b.Value))];
    }
}
