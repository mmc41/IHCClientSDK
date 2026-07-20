#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Ihc.Vis.Programs
{
    /// <summary>The four program-method categories a <see cref="ProgramMethod"/> belongs to (US-028/029/031/032).</summary>
    public enum ProgramMethodCategory
    {
        Event,
        Command,
        Condition,
        Arithmetic,
    }

    /// <summary>
    /// One vendor program method (US-028/029/032): the <c>_0x</c> <see cref="Token"/> persisted on the event/action/
    /// condition row, the vendor <see cref="NameTemplate"/> and <see cref="Note"/> stored verbatim (the <c>%P</c>/
    /// <c>%S</c> placeholders stay live so the row re-renders when its operands are renamed), and the semantics the
    /// GUI can no longer infer from a token: <see cref="OperandCount"/> (1 for event/command/condition, 2 for
    /// arithmetic's <c>%P … %S</c>) and <see cref="OperatorSymbol"/> (<c>+</c>/<c>-</c> for arithmetic, else null;
    /// ASCII hyphen-minus, not U+2212 — the .vis format is ISO-8859-1 and cannot encode a MINUS SIGN).
    /// The same token can appear under more than one category (e.g. <c>_0xa</c> is Event, Command and Condition), so
    /// a method is identified by the <c>(Category, Token)</c> pair, never the token alone.
    /// </summary>
    /// <remarks>Intentional test-only seam (D02): <see cref="OperandCount"/> is currently asserted only by the
    /// ProgramMethodCatalog tests (1 for event/command/condition, 2 for arithmetic); it is kept as the method-arity
    /// fact a future GUI operand picker would consult. (<see cref="Category"/> is NOT a test-only member — the
    /// OpenVisual program menu keys its verbs by the <c>(Category, Token)</c> pair, so it is production-used.)</remarks>
    public sealed record ProgramMethod(
        ProgramMethodCategory Category,
        string Token,
        string NameTemplate,
        string Note,
        int OperandCount,
        string? OperatorSymbol);

    /// <summary>
    /// The SDK-owned catalog of the program methods OpenVisual authors (US-028/029/031/032): the single source of the
    /// vendor tokens, persisted name/note templates and method semantics, promoted out of the GUI's four hand-kept
    /// tables. The primary surface is the four per-category lists (the GUI iterates by category, matching its menus);
    /// the values are the byte-fidelity vendor literals from the recorded oracles. Only the tokens the app uses are
    /// promoted (YAGNI); the full vendor <c>Data\mNN.def</c> vocabulary stays out of scope.
    /// </summary>
    public static class ProgramMethodCatalog
    {
        /// <summary>The event triggers a variable can raise on a program's Events node (US-028).</summary>
        public static readonly ImmutableArray<ProgramMethod> Events = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Event, "_0xa", "%P -> ON", "Start program when %P changes to ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x96", "%P changes state", "Start program when %P changes state", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x9b", "%P is assigned", "Start program when %P is assigned", 1, null));

        /// <summary>The single-operand commands a variable can be driven by on a program's Commands node (US-028).</summary>
        public static readonly ImmutableArray<ProgramMethod> Commands = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Command, "_0xa", "%P = ON", "Sets %P to ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x14", "%P = OFF", "Sets %P to OFF", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x23", "Toggle %P", "Sets %P to the opposite value", 1, null));

        /// <summary>The conditions a variable can be tested by on a sub-program's Conditions node, incl. the NOT
        /// variant (US-029).</summary>
        public static readonly ImmutableArray<ProgramMethod> Conditions = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xa", "%P = ON", "Condition that %P is ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x14", "%P = OFF", "Condition that %P is OFF", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x28", "%P <> ON", "Condition that %P is not ON", 1, null));

        /// <summary>The binary arithmetic operations on a numeric register (US-032): add and subtract. Multiply/divide
        /// have no attested vendor token, so are not offered. The <c>%S</c> is the second operand.</summary>
        public static readonly ImmutableArray<ProgramMethod> Arithmetic = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x5a", "%P = %P + %S", "Adds %S to %P", 2, "+"),
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x64", "%P = %P - %S", "Subtracts %S from %P", 2, "-"));

        /// <summary>The variable types a <c>program_case</c> may switch on (US-031): counter, enumerator, weekday,
        /// integer, or date. The single public source of truth for case-switch eligibility — the session's AddCase
        /// guard and the OpenVisual case menu both test membership against this set, so neither keeps its own copy.</summary>
        public static readonly FrozenSet<string> EligibleCaseVariableTags = new[]
        {
            "resource_counter", "resource_enum", "resource_weekday", "resource_integer", "resource_date",
        }.ToFrozenSet(StringComparer.Ordinal);
    }
}
