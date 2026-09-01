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
    /// The PROGRAM-SHAPE rows: a program that never starts, one that starts and does nothing, a branch that
    /// always goes the same way, a switch with nothing to switch on, two branches testing one value, and a
    /// statement naming no operand at all.
    ///
    /// <para><b>THE GRAMMAR FACT THIS SET TURNS ON:</b> only <c>program_simple</c> has events. Measured over the
    /// corpus, all 746 <c>program_sub</c> elements carry <c>conditions</c> and <c>actions</c> and NO
    /// <c>events</c> container at all, and <c>program_case</c> carries <c>actions</c> plus its <c>case_action</c>
    /// branches. A sub-program is a conditional BRANCH inside a program, not a program with a missing trigger — so
    /// an events rule that walked every <c>program_*</c> element would report 746 of them, in every authentic file.
    /// </para>
    ///
    /// <para><b>Neither events row names the shipped empty default</b>, and between them they say why: a program
    /// with no trigger is reported only when it carries WORK a trigger could have run, and
    /// <c>logic-program-no-actions</c> requires events to be PRESENT, which is the row's own wording ("declares
    /// events but no commands"). A block freshly inserted from the library brings a program with neither, in every
    /// authentic file — reporting it says only that the author has not finished. A block empty ALL THE WAY DOWN is
    /// still <c>logic-block-empty</c>'s. Measured: one program in the whole corpus has events and no commands, in
    /// the error fixture.</para>
    /// </summary>
    public static class ProgramShapeRules
    {
        private const string SimpleProgramTag = "program_simple";

        private const string SubProgramTag = "program_sub";

        private const string CaseProgramTag = "program_case";

        private const string CaseBranchTag = "case_action";

        /// <summary>
        /// The three statement tags, and the ONLY way this module recognises a statement.
        /// <para>
        /// NOT BY ID TYPE CODE AND NOT BY ICON. <c>event_power</c> shares <c>event</c>'s type code <c>c8</c> and
        /// its constant <c>icon="_0xc"</c>, so either shortcut would report every Powerup event in every authentic
        /// file — 7 across the validation corpus alone. The tag is the discriminator the format itself uses.
        /// </para>
        /// </summary>
        private static readonly ImmutableHashSet<string> StatementTags =
            ["event", "condition", "action"];

        /// <summary>The attribute a statement carries to say what it acts on.</summary>
        private const string LinkAttribute = "link1";

        /// <summary>The rules, ready to register against the catalogue.</summary>
        /// <param name="catalog">The catalogue the entries are declared in.</param>
        public static EquatableArray<RuleDefinition> All(ProblemCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            return ImmutableArray.Create(
                Rule(catalog, "logic-program-no-events", NoEvents),
                Rule(catalog, "logic-program-no-actions", NoActions),
                Rule(catalog, "logic-subprogram-no-conditions", NoConditions),
                Rule(catalog, "logic-case-no-branches", NoBranches),
                Rule(catalog, "logic-case-duplicate-value", DuplicateCaseValue),
                Rule(catalog, "logic-statement-unlinked", StatementUnlinked));
        }

        /// <summary>
        /// A program that carries work and no trigger: the commands are written and nothing can ever run them.
        /// <para>SUBJECT: <c>program_simple</c> alone, because it is the only program kind the format gives an
        /// <c>events</c> container to.</para>
        /// <para>EXCLUSION, and it is what the row's value now rests on: a program with NO WORK EITHER. Every block
        /// inserted from the library brings a program with neither trigger nor command, and every witnessed hit of
        /// the untightened row was one of those — a statement that the author has not finished, which they can see.
        /// The finding is about work STRANDED, so the subject is a program that has some.</para>
        /// <para>WORK IS COMMANDS OR A BRANCH: the commands may all sit inside a sub-program, and such a program is
        /// stranded just as completely as one whose commands sit at the top level.</para>
        /// </summary>
        private static void NoEvents(IProjectInspection inspection)
        {
            foreach (ProjectElement program in Programs(inspection.Analyses, SimpleProgramTag))
            {
                if (!Container(program, "events").Any() && CarriesWork(program))
                {
                    inspection.Report(program, Arguments(("program", Name(program))));
                }
            }
        }

        /// <summary>
        /// A program with events and no commands: it starts and does nothing.
        /// <para>EVENTS MUST BE PRESENT — the row says so, and it is also what keeps this row off the shipped empty
        /// default program, which declares none. The two rows therefore never both fire on one program, and neither
        /// fires on that default.</para>
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

        /// <summary>
        /// A statement that references nothing: it does nothing that can be modelled, and IHC Visual terminates
        /// outright when such a program runs. The set's second ERROR, and the only row here whose state no editor
        /// can author.
        /// <para>
        /// WALKED PER BLOCK rather than per program, because the finding's second argument IS the block: taking
        /// the statements out of the block that contains them gives the argument for free and needs no ancestor
        /// lookup. It also scopes the walk to the subject exactly — statements live in programs, and programs live
        /// in blocks.
        /// </para>
        /// <para>
        /// THE ABSENT ATTRIBUTE, not a blank one and not a dangling one. A <c>link1</c> naming a missing id is
        /// <c>idref-dangling</c>'s finding, and the null token was never measured here — see the entry.
        /// </para>
        /// </summary>
        private static void StatementUnlinked(IProjectInspection inspection)
        {
            foreach (ProjectElement block in Blocks(inspection.Analyses))
            {
                string name = Name(block);
                foreach (ProjectElement statement in block.Descendants())
                {
                    if (StatementTags.Contains(statement.Tag)
                        && statement.GetAttribute(LinkAttribute) is null)
                    {
                        inspection.Report(statement, Arguments(("tag", statement.Tag), ("block", name)));
                    }
                }
            }
        }

        // ---- the shared reads ------------------------------------------------------------------------------

        private static IEnumerable<ProjectElement> Programs(IProjectAnalyses analyses, string tag) =>
            analyses.WithTag(tag);

        private static IEnumerable<ProjectElement> Container(ProjectElement program, string container) =>
            program.FindChild(container) is { } section ? section.Children : [];

        /// <summary>Whether the program holds anything a trigger could have run: a command, or a branch holding one.</summary>
        private static bool CarriesWork(ProjectElement program) =>
            Container(program, "actions").Any()
            || program.Children.Any(c => c.Tag is SubProgramTag or CaseProgramTag);

        private static IEnumerable<ProjectElement> Branches(ProjectElement caseProgram) =>
            caseProgram.Descendants().Where(e => e.Tag == CaseBranchTag);
    }
}
