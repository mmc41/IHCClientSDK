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
    /// The five PROGRAM-SHAPE rows: a program that never starts, one that starts and does nothing, a branch that
    /// always goes the same way, a switch with nothing to switch on, and two branches testing one value.
    ///
    /// <para><b>THE GRAMMAR FACT THIS SET TURNS ON:</b> only <c>program_simple</c> has events. Measured over the
    /// corpus, all 746 <c>program_sub</c> elements carry <c>conditions</c> and <c>actions</c> and NO
    /// <c>events</c> container at all, and <c>program_case</c> carries <c>actions</c> plus its <c>case_action</c>
    /// branches. A sub-program is a conditional BRANCH inside a program, not a program with a missing trigger — so
    /// an events rule that walked every <c>program_*</c> element would report 746 of them, in every authentic file.
    /// </para>
    ///
    /// <para><b>And <c>logic-program-no-actions</c> requires events to be PRESENT</b>, which is the row's own
    /// wording ("declares events but no commands") and also what keeps it from re-reporting the empty default
    /// program that <c>logic-program-no-events</c> already names. Measured: one program in the whole corpus has
    /// events and no commands, in the error fixture.</para>
    /// </summary>
    public static class ProgramShapeRules
    {
        private const string SimpleProgramTag = "program_simple";

        private const string SubProgramTag = "program_sub";

        private const string CaseProgramTag = "program_case";

        private const string CaseBranchTag = "case_action";

        /// <summary>The five rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-program-no-events", NoEvents),
                Rule(catalog, "logic-program-no-actions", NoActions),
                Rule(catalog, "logic-subprogram-no-conditions", NoConditions),
                Rule(catalog, "logic-case-no-branches", NoBranches),
                Rule(catalog, "logic-case-duplicate-value", DuplicateCaseValue));
        }

        /// <summary>
        /// A program with no events: it never starts.
        /// <para>SUBJECT: <c>program_simple</c> alone, because it is the only program kind the format gives an
        /// <c>events</c> container to.</para>
        /// <para>MEASURED: 16 across the authentic corpus, every one of them either a freshly inserted block's
        /// default empty program or a hand-built program in the token fixtures. The row's own
        /// reasonable-disagreement column names exactly that case ("program under construction").</para>
        /// </summary>
        private static void NoEvents(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, SimpleProgramTag))
            {
                if (!Container(program, "events").Any())
                {
                    inspection.Report(program, Arguments(("program", Name(program))));
                }
            }
        }

        /// <summary>
        /// A program with events and no commands: it starts and does nothing.
        /// <para>EVENTS MUST BE PRESENT — the row says so, and it is what keeps this row from re-reporting the
        /// empty default program that <c>logic-program-no-events</c> already names. The two rows therefore never
        /// both fire on one program.</para>
        /// </summary>
        private static void NoActions(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, SimpleProgramTag))
            {
                if (Container(program, "events").Any() && !Container(program, "actions").Any())
                {
                    inspection.Report(program, Arguments(("program", Name(program))));
                }
            }
        }

        /// <summary>
        /// A sub-program with no conditions: the conditional branch always takes the same path.
        /// <para>The container is always PRESENT on a sub-program (746 of 746 in the corpus), so this row is about
        /// an EMPTY one — which is what an author leaves behind when a branch is added and never filled in.</para>
        /// </summary>
        private static void NoConditions(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, SubProgramTag))
            {
                if (!Container(program, "conditions").Any())
                {
                    inspection.Report(program, Arguments(("program", Name(program))));
                }
            }
        }

        /// <summary>
        /// A case node with no branches: the switch does nothing.
        /// <para>BRANCHES ARE COUNTED WHEREVER THEY SIT: the corpus stores <c>case_action</c> both as a direct
        /// child of the case node and inside its <c>actions</c> container, so the walk is over the whole subtree
        /// rather than one container.</para>
        /// </summary>
        private static void NoBranches(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, CaseProgramTag))
            {
                if (!Branches(program).Any())
                {
                    inspection.Report(program, Arguments(("program", Name(program))));
                }
            }
        }

        /// <summary>
        /// Two branches of one switch testing the same value: whichever the author meant, one of them never runs.
        /// The set's one ERROR.
        /// <para>
        /// UNWITNESSED BY THE CORPUS, and the catalogue records why: <c>Indsæt ▸ Ny case værdi</c> writes its
        /// branch under the LEFT PANE's caret rather than into the selected case node, and the left pane never
        /// holds one — four routes were driven, including the vendor's own documented gesture. So the state arrives
        /// by hand-editing, which is what the whole-project face is for.
        /// </para>
        /// <para>LOCATION: the second branch, with the first as a related location.</para>
        /// </summary>
        private static void DuplicateCaseValue(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, CaseProgramTag))
            {
                Dictionary<string, ProjectElement> seen = new(StringComparer.Ordinal);
                foreach (ProjectElement branch in Branches(program))
                {
                    if (branch.GetAttribute("value") is not { Length: > 0 } value)
                    {
                        continue;   // a branch testing nothing is not a collision
                    }

                    if (seen.TryGetValue(value, out ProjectElement? first))
                    {
                        inspection.ReportGroup(branch, [first], Arguments(("program", Name(program))));
                    }
                    else
                    {
                        seen[value] = branch;
                    }
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        private static IEnumerable<ProjectElement> Programs(IProjectAnalyses analyses, string tag) =>
            analyses.WithTag(tag);

        private static IEnumerable<ProjectElement> Container(ProjectElement program, string container) =>
            program.FindChild(container) is { } section ? section.Children : [];

        private static IEnumerable<ProjectElement> Branches(ProjectElement caseProgram) =>
            caseProgram.Descendants().Where(e => e.Tag == CaseBranchTag);
    }
}
