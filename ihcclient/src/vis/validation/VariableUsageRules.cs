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
    /// The VARIABLE-USAGE rows, as predicates over <see cref="IProgramUsageAnalysis"/> — none of them walks
    /// the raw tree, which is the point of having the shared model at all.
    ///
    /// <para><b>THE BOUNDARY THAT MAKES THESE ROWS USABLE: a block's PINS are its interface, its
    /// <c>settings</c>/<c>internalsettings</c> are its state.</b> An input pin's producer and an output pin's
    /// consumer live OUTSIDE the block, so "no program reads this input" is the ordinary state of every fed pin —
    /// measured, 28 of project3's 29 read-only candidates and 19 of its 19 write-only ones were pins, and the
    /// wiring set already owns them (<c>link-fb-input-unfed</c>, <c>link-fb-output-unused</c>). Scoping these rows
    /// to the two state containers is what leaves only genuinely dead declarations behind.</para>
    ///
    /// <para><b>And a SETTING is configured from the dialog, never assigned by a program</b>, so
    /// <c>logic-variable-read-only</c> is scoped to <c>internalsettings</c> alone: reporting a settings variable
    /// for "the logic always sees its initial value" would report the whole point of a setting. It stays in scope
    /// for the write-only row, where a dialog-set value nothing reads really is dead.</para>
    /// </summary>
    public static class VariableUsageRules
    {
        /// <summary>The two containers holding a block's OWN state, as opposed to its interface pins.</summary>
        private static readonly ImmutableHashSet<string> StateContainers = ["settings", "internalsettings"];

        /// <summary>The container holding the variables a program is expected to assign.</summary>
        private const string InternalContainer = "internalsettings";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-variable-write-only", WriteOnly),
                Rule(catalog, "logic-variable-read-only", ReadOnly),
                Rule(catalog, "logic-case-value-foreign", CaseValueForeign),
                Rule(catalog, "logic-holiday-schedule-firmware", HolidayScheduleFirmware));
        }

        /// <summary>
        /// The v3 holiday schedule, which the vendor states did not work at all below controller firmware
        /// 3.3.21.
        /// <para>THIS MODULE OWNS IT because the subject is a variable — <c>resource_holiday</c> is a declared
        /// resource element, and the <c>logic-variable-*</c> family lives here. The row is a firmware erratum
        /// rather than a usage judgement, but D18 organises by SUBJECT, not by why a row exists.</para>
        /// <para>PRESENCE IS THE WHOLE PREDICATE, and the narrowing is not this rule's business: the profile
        /// withholds the finding when a declared target is at or past the fix, which keeps the predicate a
        /// statement about the FILE and the version comparison in one place.</para>
        /// <para>ONE FINDING: the reader's decision is a firmware upgrade for the installation, and four
        /// holiday resources do not make four of those.</para>
        /// </summary>
        private static void HolidayScheduleFirmware(IProjectInspection inspection)
        {
            if (inspection.Analyses.WithTag("resource_holiday").Any())
            {
                inspection.Report(null, default);
            }
        }

        /// <summary>
        /// A state variable programs assign and nothing ever reads: the value is computed and thrown away.
        /// <para>A LINK COUNTS AS A READER, which is what keeps an output-shaped state variable quiet, and the
        /// row's own disagreement column covers the rest (a value read externally by the controller API or the
        /// app).</para>
        /// </summary>
        private static void WriteOnly(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach ((ProjectElement variable, ProjectElement block, string _) in StateVariables(inspection))
            {
                if (usage.IsWritten(variable) && !usage.IsRead(variable) && !usage.IsTriggeredOn(variable)
                    && !usage.IsLinked(variable))
                {
                    inspection.Report(variable, Arguments(
                        ("variable", Name(variable)), ("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// An INTERNAL variable programs read and never assign: the logic always sees its initial value.
        /// <para>SCOPED TO <c>internalsettings</c>: a <c>settings</c> variable is configured from the product or
        /// block dialog and is SUPPOSED to keep its configured value, so reporting one here would report the whole
        /// point of a setting. Measured — project3's read-only candidates are 28 pins and 7 settings, and not one
        /// internal variable.</para>
        /// </summary>
        private static void ReadOnly(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach ((ProjectElement variable, ProjectElement block, string container) in StateVariables(inspection))
            {
                if (container == InternalContainer
                    && (usage.IsRead(variable) || usage.IsTriggeredOn(variable))
                    && !usage.IsWritten(variable)
                    && !usage.IsLinked(variable))
                {
                    inspection.Report(variable, Arguments(
                        ("variable", Name(variable)), ("block", Name(block))));
                }
            }
        }

        /// <summary>
        /// A case branch testing a value that is not one of its switch variable's enum values: the branch can never
        /// be taken.
        /// <para>
        /// THE CHAIN, measured: a branch's <c>value</c> names an inline operand element, and THAT element's
        /// <c>inivalue</c> is the value actually tested. The switch variable's <c>typedef</c> names its enum
        /// definition, whose <c>enum_value</c> children are the legal set. A predicate comparing the branch's
        /// <c>value</c> directly against the definition's values would find no match anywhere and report every
        /// branch in the corpus.
        /// </para>
        /// <para>SKIPPED: a switch that is not enum-typed at all (an integer switch stores a literal), and a branch
        /// whose operand or switch cannot be resolved — a broken reference is <c>idref-dangling</c>'s finding.</para>
        /// </summary>
        private static void CaseValueForeign(IProjectInspection inspection)
        {
            ITopologyAnalysis topology = inspection.Analyses.Topology;
            foreach (CaseTest test in inspection.Analyses.Usage.CaseTests)
            {
                if (test.Switch is not { } switchVariable
                    || test.ValueToken is not { Length: > 0 } value
                    || topology.ByToken(switchVariable.GetAttribute("typedef")) is not { } definition)
                {
                    continue;
                }

                bool declared = definition.Children
                    .Any(c => c.Tag == "enum_value" && c.GetAttribute("id") == value);
                if (!declared)
                {
                    inspection.Report(test.Branch, Arguments(
                        ("program", Name(test.Branch)), ("enum", Name(definition))));
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        /// <summary>
        /// Every variable a block declares as its own STATE, with its block and the container it sits in. The two
        /// pin containers are deliberately absent: see the type's own summary for what including them costs.
        /// </summary>
        private static IEnumerable<(ProjectElement Variable, ProjectElement Block, string Container)> StateVariables(
            IProjectInspection inspection)
        {
            foreach (ProjectElement block in inspection.Analyses.WithTag("functionblock"))
            {
                foreach (string container in StateContainers)
                {
                    if (block.FindChild(container) is not { } section)
                    {
                        continue;
                    }

                    foreach (ProjectElement variable in section.Children
                        .Where(c => c.Tag.StartsWith("resource_", StringComparison.Ordinal)))
                    {
                        yield return (variable, block, container);
                    }
                }
            }
        }
    }
}
