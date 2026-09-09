using System;
using System.Collections.Immutable;
using System.Linq;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Schema;

namespace Ihc.Vis.Programs
{
    /// <summary>
    /// The compatibility rules behind <c>ProjectAppService.GetProgramOperations</c>, kept in the engine beside the
    /// method tables they read rather than in the facade: a copy in the facade is how the offered set and the
    /// accepted set drift apart.
    /// <para>
    /// Two of these rules are SHARED with the session's command guards, which call them here rather than keeping
    /// their own copies -- <see cref="KindOfContainer"/> (which container takes which kind of operation) and
    /// <see cref="IsEligibleCaseSwitch"/>. The rest are offer-side filters: they decide what is worth OFFERING,
    /// while what is legal to APPLY stays the commands' own verdict.
    /// </para>
    /// </summary>
    internal static class ProgramAuthoring
    {
        /// <summary>
        /// The container tags a program operation can be authored into, and the kind each one takes. This is the
        /// SINGLE place that mapping lives: the session's own command-container guard reads it too, so a container
        /// the engine accepts commands into is exactly one the query offers commands for.
        /// <para>
        /// A case VALUE branch (<c>case_action</c>) is a commands container as much as a program's <c>actions</c> is
        /// -- that is where the commands of one case land -- which is why it is not simply the three obvious tags.
        /// </para>
        /// </summary>
        internal static ProgramOperationKind? KindOfContainer(string tag) => tag switch
        {
            "events" => ProgramOperationKind.Event,
            "actions" or "case_action" => ProgramOperationKind.Command,
            "conditions" => ProgramOperationKind.Condition,
            _ => null,
        };

        /// <summary>
        /// Every operation <paramref name="variable"/> may be given inside <paramref name="container"/>, each
        /// carrying the operands it may be completed with. An empty result is the answer for "nothing may be
        /// authored here" -- a container that holds no program operations, or a pin family with no operator of that
        /// kind -- never an error, because a frontend asks this on every selection change.
        /// </summary>
        internal static ImmutableArray<ProgramOperationOffer> OffersFor(
            Project project, ProjectElement container, ProjectElement variable)
        {
            if (KindOfContainer(container.Tag) is not { } kind)
                return ImmutableArray<ProgramOperationOffer>.Empty;

            ProgramPinType pinType = ProgramMethodCatalog.ClassifyPin(variable.Tag);
            ImmutableArray<ProgramMethod> methods = kind switch
            {
                ProgramOperationKind.Event => ProgramMethodCatalog.EventsFor(pinType),
                ProgramOperationKind.Command => ProgramMethodCatalog.CommandsFor(pinType),
                ProgramOperationKind.Condition => ProgramMethodCatalog.ConditionsFor(pinType),
                _ => ImmutableArray<ProgramMethod>.Empty,
            };

            // Every candidate operand comes from the function block the container belongs to: a program acts on the
            // block it lives in, so a pin from another block is not addressable from here. Taken on FIRST USE, not
            // up front: a family whose operators are all unary -- a timer's two event triggers, say -- never asks,
            // and the walk is a parent chain plus every variable of the block.
            ImmutableArray<ProjectElement>? blockVariables = null;
            ImmutableArray<ProjectElement> Siblings() =>
                blockVariables ??= VariablesOfOwningBlock(project, container);

            ProgramPinFamily family = FamilyOf(pinType);

            var offers = ImmutableArray.CreateBuilder<ProgramOperationOffer>();
            foreach (ProgramMethod method in methods)
            {
                // A command that drives an output to its opposite is meaningless on a variable that has no output
                // to drive, so it is withheld rather than offered and then refused.
                if (kind == ProgramOperationKind.Command
                    && ProgramMethodCatalog.BoolOutputOnlyCommandTokens.Contains(method.Token)
                    && !variable.IsOutputPin)
                {
                    continue;
                }

                EquatableArray<ProgramOperandOffer> operands = method.OperandCount > 1
                    ? SameFamilyOperands(project, Siblings(), pinType, variable, method)
                    : EquatableArray<ProgramOperandOffer>.Empty;

                // A two-operand operation with nothing to complete it is not authorable, so it is not offered.
                // Dropping it here is what stops a caller from binding an operand of its own choosing.
                if (method.OperandCount > 1 && operands.Length == 0)
                    continue;

                offers.Add(new ProgramOperationOffer(
                    kind, family, method.Token, method.NameTemplate, method.Note,
                    method.OperandCount > 1, method.OperatorSymbol, operands));
            }

            if (kind == ProgramOperationKind.Command)
            {
                // A case switches on the armed variable, so its eligibility is the variable's type, tested against
                // the very set the AddCase guard tests.
                if (IsEligibleCaseSwitch(variable))
                {
                    offers.Add(new ProgramOperationOffer(
                        ProgramOperationKind.Case, family, string.Empty,
                        ProgramMethodCatalog.CaseNameTemplate, ProgramMethodCatalog.CaseNote, NeedsOperand: false, null,
                        EquatableArray<ProgramOperandOffer>.Empty));
                }

                // Asked only of a numeric target, so the sibling walk stays unmade for every other armed variable.
                if (ProgramMethodCatalog.NumericVariableTags.Contains(variable.Tag))
                    offers.AddRange(ArithmeticOffers(project, Siblings(), variable, family));
            }

            return offers.ToImmutable();
        }

        /// <summary>
        /// The arithmetic an armed numeric register may be given (US-032): one offer per operator that has an
        /// authorable cell for at least one operand, plus the one-operand steps a counter target adds.
        /// <para>
        /// The opcode of each cell comes from the SDK's own grid, so a dead cell is never offered and a caller never
        /// derives a token from an operator symbol and two type tags.
        /// </para>
        /// </summary>
        private static ImmutableArray<ProgramOperationOffer> ArithmeticOffers(
            Project project, ImmutableArray<ProjectElement> siblings, ProjectElement target, ProgramPinFamily family)
        {
            if (!ProgramMethodCatalog.NumericVariableTags.Contains(target.Tag))
                return ImmutableArray<ProgramOperationOffer>.Empty;

            var offers = ImmutableArray.CreateBuilder<ProgramOperationOffer>();
            foreach (ProgramMethod op in ProgramMethodCatalog.Arithmetic)
            {
                string symbol = op.OperatorSymbol!;
                var operands = ImmutableArray.CreateBuilder<ProgramOperandOffer>();
                foreach (ProjectElement candidate in siblings)
                {
                    if (!ProgramMethodCatalog.NumericVariableTags.Contains(candidate.Tag))
                        continue;
                    if (ProgramMethodCatalog.ArithmeticToken(symbol, target.Tag, candidate.Tag) is not { } token)
                        continue;
                    operands.Add(new ProgramOperandOffer(
                        candidate.Id!.Value, NameOf(project, candidate), token,
                        ProgramMethodCatalog.ArithmeticNote(symbol, target.Tag, candidate.Tag)!));
                }

                if (operands.Count > 0)
                {
                    offers.Add(new ProgramOperationOffer(
                        ProgramOperationKind.Arithmetic, family, op.Token, op.NameTemplate, op.Note,
                        op.OperandCount > 1, symbol, operands.ToImmutable()));
                }
            }

            if (target.Tag == CounterTag)
            {
                foreach (ProgramMethod step in ProgramMethodCatalog.CounterSteps)
                {
                    offers.Add(new ProgramOperationOffer(
                        ProgramOperationKind.Arithmetic, family, step.Token, step.NameTemplate, step.Note,
                        step.OperandCount > 1, step.OperatorSymbol, EquatableArray<ProgramOperandOffer>.Empty));
                }
            }

            return offers.ToImmutable();
        }

        /// <summary>
        /// The operands a two-operand row of <paramref name="method"/> may be completed with: the block's other
        /// variables of the armed pin's family, the armed pin itself excluded. An enumerator additionally requires
        /// the SAME enum type on both ends -- two enumerators of different typedefs have no comparable states.
        /// </summary>
        private static EquatableArray<ProgramOperandOffer> SameFamilyOperands(
            Project project, ImmutableArray<ProjectElement> siblings, ProgramPinType pinType,
            ProjectElement armed, ProgramMethod method)
        {
            string? armedEnumType = pinType == ProgramPinType.Enum
                ? new EnumVariableView(project, armed).TypeToken
                : null;

            var operands = ImmutableArray.CreateBuilder<ProgramOperandOffer>();
            foreach (ProjectElement candidate in siblings)
            {
                if (candidate.Id is not { } id || id == armed.Id
                    || ProgramMethodCatalog.ClassifyPin(candidate.Tag) != pinType)
                {
                    continue;
                }

                if (pinType == ProgramPinType.Enum
                    && (armedEnumType is null || new EnumVariableView(project, candidate).TypeToken != armedEnumType))
                {
                    continue;
                }

                operands.Add(new ProgramOperandOffer(id, NameOf(project, candidate), method.Token, method.Note));
            }

            return operands.ToImmutable();
        }

        /// <summary>
        /// The variables of the function block owning <paramref name="container"/>, in document order. Read through
        /// the block's DECLARED variable sections rather than every child it has, so a program node or a nested
        /// product child is never a candidate operand -- the same table every other reader of a block's sections
        /// uses.
        /// </summary>
        private static ImmutableArray<ProjectElement> VariablesOfOwningBlock(Project project, ProjectElement container)
        {
            ProjectElement? block = container;
            while (block is not null && block.Kind is not ElementKind.FunctionBlock)
                block = block.Id is { } id ? project.FindParent(id) : null;

            if (block is null)
                return ImmutableArray<ProjectElement>.Empty;

            var variables = ImmutableArray.CreateBuilder<ProjectElement>();
            foreach (FunctionBlockSectionInfo section in FunctionBlockSections.Descriptions)
                if (block.FindChild(section.Container) is { } holder)
                    foreach (ProjectElement child in holder.Children)
                        if (child.Id is not null && VariableTypeRegistry.IsVariableType(child.Tag))
                            variables.Add(child);

            return variables.ToImmutable();
        }

        private static string NameOf(Project project, ProjectElement element) =>
            project.View(element).Name is { Length: > 0 } name ? name : element.Tag;

        /// <summary>
        /// The published family for an engine pin classification. Total by construction: every
        /// <see cref="ProgramPinType"/> maps, and a value added there without a counterpart here throws rather than
        /// silently reporting some other family -- which is what a stringified name would have done.
        /// </summary>
        internal static ProgramPinFamily FamilyOf(ProgramPinType type) => type switch
        {
            ProgramPinType.Bool => ProgramPinFamily.Bool,
            ProgramPinType.Analog => ProgramPinFamily.Analog,
            ProgramPinType.Weekday => ProgramPinFamily.Weekday,
            ProgramPinType.Timer => ProgramPinFamily.Timer,
            ProgramPinType.Enum => ProgramPinFamily.Enum,
            ProgramPinType.Numeric => ProgramPinFamily.Numeric,
            _ => throw new NotSupportedException($"No published family for pin type '{type}'."),
        };

        private const string CounterTag = "resource_counter";

        /// <summary>Whether a <c>program_case</c> may switch on <paramref name="variable"/> — the predicate the
        /// session's <c>AddCase</c> guard calls, so the offered set and the accepted set cannot disagree.</summary>
        internal static bool IsEligibleCaseSwitch(ProjectElement variable) =>
            ProgramMethodCatalog.EligibleCaseVariableTags.Contains(variable.Tag);

    }
}
