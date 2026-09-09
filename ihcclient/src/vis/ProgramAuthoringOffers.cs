using System.Linq;
using Ihc.Vis.Model;

namespace Ihc.Vis
{
    /// <summary>
    /// What an offered program operation does to the armed variable. It names the CONTAINER the operation belongs
    /// in, which is the only structural fact a caller needs to place it: an event goes on a program's events
    /// container, a command and a case on its commands container, a condition on a sub-program's conditions group.
    /// </summary>
    public enum ProgramOperationKind
    {
        /// <summary>A trigger on the program's events container.</summary>
        Event,

        /// <summary>A command on the program's commands container.</summary>
        Command,

        /// <summary>A predicate on a sub-program's conditions group.</summary>
        Condition,

        /// <summary>A case structure keyed on the armed variable, on the commands container.</summary>
        Case,

        /// <summary>An arithmetic assignment or counter step on the armed numeric register, on the commands
        /// container. Separate from <see cref="Command"/> because its opcode is a property of the (operator,
        /// target type, operand type) cell rather than of the method alone.</summary>
        Arithmetic,
    }

    /// <summary>
    /// The operator family an armed variable belongs to — which operations it can be given at all, and which of
    /// several meanings a shared vendor token carries. A timer's <c>_0xa</c> sets a timer to zero where a bool's
    /// sets a pin ON, so a caller that renders or groups by operation keys on the family as well as the token.
    /// </summary>
    public enum ProgramPinFamily
    {
        /// <summary>An ON/OFF variable: a pin, a flag, or any type with no family of its own.</summary>
        Bool,

        /// <summary>A continuous-value sensor or register — temperature, humidity, light level, decimal.</summary>
        Analog,

        /// <summary>A weekday variable.</summary>
        Weekday,

        /// <summary>A timer, which counts and has its own triggers, commands and comparisons.</summary>
        Timer,

        /// <summary>An enumerator, whose operands must share its enum type.</summary>
        Enum,

        /// <summary>
        /// A numeric register (integer or counter): it has a VALUE rather than an ON/OFF state, so it offers no
        /// boolean command, and its authoring surface is arithmetic.
        /// </summary>
        Numeric,
    }

    /// <summary>
    /// One operand a two-operand operation may be completed with: the variable to pick, the name a caller would
    /// show for it, and the vendor method and note THIS pairing is authored with.
    /// <para>
    /// The method travels with the operand rather than with the operation because an arithmetic opcode depends on
    /// the pair: <c>counter + integer</c> and <c>counter + decimal</c> are different tokens for the same operator.
    /// For an ordinary two-operand row every operand repeats the operation's own method, which is what makes one
    /// shape serve both.
    /// </para>
    /// </summary>
    /// <param name="VariableId">The variable to author as the second operand.</param>
    /// <param name="Name">The operand's name, as the project records it.</param>
    /// <param name="Method">The vendor method token for this pairing.</param>
    /// <param name="Note">The persisted note for this pairing.</param>
    public sealed record ProgramOperandOffer(ElementId VariableId, string Name, string Method, string Note);

    /// <summary>
    /// One operation the SDK offers for an armed variable inside a container — the answer to "what may I author
    /// here", in terms a caller can act on without knowing how the engine decided.
    /// <para>
    /// <see cref="Method"/>, <see cref="NameTemplate"/> and <see cref="Note"/> are the persisted vendor values the
    /// matching command takes, so an offer feeds a command directly. The templates keep their live <c>%P</c>/
    /// <c>%S</c>/<c>%LT</c> placeholders: they are .vis payload that re-renders when an operand is renamed, not
    /// display text to be substituted here.
    /// </para>
    /// </summary>
    /// <param name="Kind">Which container the operation belongs in.</param>
    /// <param name="Family">
    /// The armed variable's operator family. It is part of an operation's identity, not decoration: the same token
    /// means different things in different families, so a caller that renders or groups by operation keys on the
    /// family too.
    /// </param>
    /// <param name="Method">The vendor method token the matching command takes, or empty for a case.</param>
    /// <param name="NameTemplate">The persisted name template of the authored row.</param>
    /// <param name="Note">The persisted note of the authored row.</param>
    /// <param name="NeedsOperand">
    /// Whether authoring this operation requires a second operand the caller must still choose from
    /// <paramref name="Operands"/>.
    /// </param>
    /// <param name="OperatorSymbol">The arithmetic or counter-step operator, where the operation has one.</param>
    /// <param name="Operands">
    /// The operands this operation may be completed with, empty for a unary one. A two-operand operation with no
    /// eligible operand is not offered at all, so a non-empty list here is part of what being offered means.
    /// </param>
    public sealed record ProgramOperationOffer(
        ProgramOperationKind Kind,
        ProgramPinFamily Family,
        string Method,
        string NameTemplate,
        string Note,
        bool NeedsOperand,
        string? OperatorSymbol,
        EquatableArray<ProgramOperandOffer> Operands)
    {
        /// <summary>
        /// A stable identifier for this offer, unique within one query's result: the key a caller may store, compare
        /// or address a menu entry by. Derived rather than carried, so it cannot disagree with the two members it is
        /// made of — and kind-scoped because the same vendor token appears under more than one kind.
        /// </summary>
        public string Id => Method.Length == 0 ? KindNames[(int)Kind] : string.Concat(KindNames[(int)Kind], ":", Method);

        // Formatted once per kind rather than per offer: a GUI asks for these on every selection change while a
        // variable is armed, and the names are as fixed as the enum is.
        private static readonly string[] KindNames =
            [.. System.Enum.GetValues<ProgramOperationKind>().Select(kind => kind.ToString().ToLowerInvariant())];
    }
}
