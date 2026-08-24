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
    /// The five VARIABLE-USAGE rows, as predicates over <see cref="IProgramUsageAnalysis"/> — none of them walks
    /// the raw tree, which is the point of having the shared model at all.
    ///
    /// <para><b>THE BOUNDARY THAT MAKES THESE ROWS USABLE: a block's PINS are its interface, its
    /// <c>settings</c>/<c>internalsettings</c> are its state.</b> An input pin's producer and an output pin's
    /// consumer live OUTSIDE the block, so "no program reads this input" is the ordinary state of every fed pin —
    /// measured, 28 of project3's 29 read-only candidates and 19 of its 19 write-only ones were pins, and the
    /// wiring set already owns them (<c>link-fb-input-unfed</c>, <c>link-fb-output-unused</c>). Scoping these three
    /// rows to the two state containers takes project3 from 64 findings to 9, every one of them a genuinely dead
    /// declaration.</para>
    ///
    /// <para><b>And a SETTING is configured from the dialog, never assigned by a program</b>, so
    /// <c>logic-variable-read-only</c> is scoped to <c>internalsettings</c> alone: reporting a settings variable
    /// for "the logic always sees its initial value" would report the whole point of a setting. It stays in scope
    /// for the unused and write-only rows, where a dialog-set value nothing reads really is dead.</para>
    /// </summary>
    public static class VariableUsageRules
    {
        /// <summary>The two containers holding a block's OWN state, as opposed to its interface pins.</summary>
        private static readonly ImmutableHashSet<string> StateContainers = ["settings", "internalsettings"];

        /// <summary>The container holding the variables a program is expected to assign.</summary>
        private const string InternalContainer = "internalsettings";

        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-variable-unused", Unused),
                Rule(catalog, "logic-variable-write-only", WriteOnly),
                Rule(catalog, "logic-variable-read-only", ReadOnly),
                Rule(catalog, "enum-value-unused", EnumValueUnused),
                Rule(catalog, "logic-case-value-foreign", CaseValueForeign));
        }

        private static RuleDefinition Rule(ProblemCatalog catalog, string code, ProjectInspection body) =>
            catalog.TryGet(new ProblemCode(code), out ProblemCatalogEntry entry)
                ? new RuleBuilder(entry).Inspect(body).Build()
                : throw new RuleRegistrationException(new ProblemCode(code), RuleRegistrationFault.NoCatalogueEntry);

        /// <summary>
        /// A declared state variable no program touches and no link reaches: a dead declaration, noise in the block
        /// and in the reports.
        /// <para>REPORTED ONCE PER VARIABLE, never once per program — the catalogue's deliberate-non-findings
        /// section says so in as many words ("a block with more variables than its programs read … reported once,
        /// as <c>logic-variable-unused</c>").</para>
        /// </summary>
        private static void Unused(IProjectInspection inspection)
        {
            IProgramUsageAnalysis usage = inspection.Analyses.Usage;
            foreach ((ProjectElement variable, ProjectElement block, string _) in StateVariables(inspection))
            {
                if (!usage.IsRead(variable) && !usage.IsWritten(variable) && !usage.IsTriggeredOn(variable)
                    && !usage.IsLinked(variable))
                {
                    inspection.Report(variable, Arguments(
                        ("variable", Name(variable)), ("block", Name(block))));
                }
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
        /// A declared enum value nothing ever tests or assigns: a state the logic never uses.
        /// <para>
        /// THE ONE REFERENCE FORM is <c>inivalue</c>, measured at 598 occurrences and no other attribute anywhere,
        /// and it covers both halves of "tested or assigned": a variable's initial value and a case branch's inline
        /// operand are stored the same way.
        /// </para>
        /// <para>
        /// FIRING ON EVERY VALUE OF A USER-AUTHORED TYPE IS CORRECT, and the error fixture's own record measured
        /// why (M-14): IHC Visual cannot bind a user-created enumerator type to a variable at all, so its values
        /// can never be referenced. EXCLUDED: the format's own <c>typeid</c> system tables, which are read-only
        /// furniture — reporting their 11 unreferenced values in every project, the empty one included, would
        /// drown the row.
        /// </para>
        /// </summary>
        private static void EnumValueUnused(IProjectInspection inspection)
        {
            IReadOnlySet<string> referenced = inspection.Analyses.Usage.ReferencedValueTokens;
            foreach (ProjectElement definition in inspection.Analyses.WithTag("enum_definition")
                .Where(EnumTypeIdentity.IsAuthored))
            {
                foreach (ProjectElement value in definition.Children.Where(c => c.Tag == "enum_value"))
                {
                    if (value.GetAttribute("id") is { Length: > 0 } id && !referenced.Contains(id))
                    {
                        inspection.Report(value, Arguments(
                            ("value", Name(value)), ("enum", Name(definition))));
                    }
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

        private static string Name(ProjectElement element) =>
            element.GetAttribute("name") is { Length: > 0 } name ? name : element.Tag;

        private static EquatableArray<ProblemArgument> Arguments(params (string Name, object Value)[] bindings) =>
            [.. bindings.Select(b => new ProblemArgument(b.Name, b.Value))];
    }
}
