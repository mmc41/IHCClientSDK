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
    /// The program-authoring pin-type families the operator popup keys on (US-028, PG-1b): the dragged pin's type
    /// decides which operators the Events/Commands/Conditions popup offers, independent of the container category. A
    /// pin's SDK tag maps to exactly one family via <see cref="ProgramMethodCatalog.ClassifyPin"/>. <see cref="Bool"/>
    /// is the default (its lists also serve enum/numeric/date pins until those are typed separately — out of PG-1b
    /// scope); a numeric pin additionally reaches the arithmetic submenu via
    /// <see cref="ProgramMethodCatalog.NumericVariableTags"/>, which is orthogonal to this family.
    /// </summary>
    public enum ProgramPinType
    {
        Bool,
        Analog,
        Weekday,
        Timer,

        /// <summary>
        /// A numeric register (integer / counter): it has a VALUE, not an ON/OFF state, so it offers none of the
        /// boolean commands (uxparity2 F5 — these pins used to fall to <see cref="Bool"/>'s default list and were
        /// offered <c>set to ON</c>/<c>OFF</c>). Its authoring surface is the arithmetic submenu, which is reached
        /// through <see cref="ProgramMethodCatalog.NumericVariableTags"/> and is orthogonal to this family.
        /// </summary>
        Numeric,
    }

    /// <summary>
    /// One vendor program method (US-028/029/032): the <c>_0x</c> <see cref="Token"/> persisted on the event/action/
    /// condition row, the vendor <see cref="NameTemplate"/> and <see cref="Note"/> stored verbatim (the <c>%P</c>/
    /// <c>%S</c> placeholders stay live so the row re-renders when its operands are renamed), and the semantics the
    /// GUI can no longer infer from a token: <see cref="OperandCount"/> (1 for event/command/condition, 2 for
    /// arithmetic's <c>%P … %S</c>) and <see cref="OperatorSymbol"/> (<c>+</c>/<c>-</c> for arithmetic, else null;
    /// ASCII hyphen-minus, not U+2212 — the .vis format is ISO-8859-1 and cannot encode a MINUS SIGN).
    /// The same token can appear under more than one category (e.g. <c>_0xa</c> is Event, Command and Condition), so
    /// within one pin-type family a method is identified by the <c>(Category, Token)</c> pair, never the token alone.
    /// <b>Across pin-type families the same <c>(Category, Token)</c> also differs</b> (e.g. <c>(Command,_0xa)</c> is
    /// <c>%P = ON</c> for a Bool pin but <c>%P = 0</c> for a Timer pin — review E2), so the identity over the COMBINED
    /// catalog is <c>(PinType, Category, Token)</c>; a consumer keys on <c>(Category, Token)</c> only after it has
    /// picked one pin-type family via <see cref="ProgramMethodCatalog.EventsFor"/>/<c>CommandsFor</c>/<c>ConditionsFor</c>.
    /// </summary>
    /// <remarks>Intentional test-only seam (D02): <see cref="OperandCount"/> is currently asserted only by the
    /// ProgramMethodCatalog tests (1 for event/command/condition, 2 for arithmetic); it is kept as the method-arity
    /// fact a future GUI operand picker would consult. (<see cref="Category"/> is NOT a test-only member — the
    /// OpenVisual program menu keys its verbs by the full <c>(PinType, Category, Token)</c> triple, so it is
    /// production-used.)</remarks>
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
        /// <summary>The event triggers a bool variable can raise on a program's Events node (US-028): the two direct
        /// transitions <c>-&gt; ON</c>/<c>-&gt; OFF</c>, the two-operand <c>-&gt; %S</c> / <c>NOT -&gt; %S</c>
        /// (the target value is a second pin the author picks, T008), and the state-change/assignment triggers.</summary>
        public static readonly ImmutableArray<ProgramMethod> Events = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Event, "_0xa", "%P -> ON", "Start program when %P changes to ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x14", "%P -> OFF", "Start program when %P changes to OFF", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x1e", "%P -> %S", "Start program when %P changes to %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x28", "%P NOT -> %S", "Start program when %P changes to a value other than %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x96", "%P changes state", "Start program when %P changes state", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x9b", "%P is assigned", "Start program when %P is assigned", 1, null));

        /// <summary>The commands a bool variable can be driven by on a program's Commands node (US-028): the direct
        /// <c>= ON</c>/<c>= OFF</c>, the two-operand assign <c>= %S</c> and <c>= NOT</c> (<c>%P &lt;&gt; %S</c>, the
        /// second pin picked by the author, T008), and <c>Toggle</c> (bool-output only, PG-1c).</summary>
        public static readonly ImmutableArray<ProgramMethod> Commands = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Command, "_0xa", "%P = ON", "Sets %P to ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x14", "%P = OFF", "Sets %P to OFF", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x1e", "%P = %S", "Sets %P to %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x28", "%P <> %S", "Sets %P to differ from %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x23", "Toggle %P", "Sets %P to the opposite value", 1, null));

        /// <summary>The <see cref="Commands"/> method tokens offered ONLY when the armed variable is a bool-OUTPUT
        /// pin (PG-1c/US-028): Toggle (<c>_0x23</c>) sets an output to its opposite value, which is meaningless for an
        /// input or a non-bool variable. The GUI filters the <see cref="Commands"/> list against this set using the
        /// armed pin's output-ness (a <c>resource_output</c> / <c>dataline_output</c> / <c>airlink_relay</c> pin), so
        /// the "which commands need a bool output" fact stays SDK-owned rather than a hard-coded token in the app.</summary>
        public static readonly FrozenSet<string> BoolOutputOnlyCommandTokens =
            new[] { "_0x23" }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>The conditions a variable can be tested by on a sub-program's Conditions node, incl. the NOT
        /// variant (US-029).</summary>
        public static readonly ImmutableArray<ProgramMethod> Conditions = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xa", "%P = ON", "Condition that %P is ON", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x14", "%P = OFF", "Condition that %P is OFF", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x1e", "%P = %S", "Condition that %P equals %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x28", "%P <> %S", "Condition that %P differs from %S", 2, null));

        /// <summary>The four binary arithmetic operators on a numeric register (US-032, F-108): add / subtract /
        /// divide / multiply. Each entry's <see cref="ProgramMethod.Token"/> is the <b>generic-column</b> opcode; the
        /// actual opcode for a concrete <c>(target, operand)</c> pair — and whether that cell is authorable at all —
        /// comes from <see cref="ArithmeticToken"/> (a dead cell returns null and is never offered). The
        /// <see cref="ProgramMethod.NameTemplate"/> is token-independent (only the token varies by pair).</summary>
        public static readonly ImmutableArray<ProgramMethod> Arithmetic = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x5a", "%P = %P + %S", "Adds %S to %P", 2, "+"),
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x64", "%P = %P - %S", "Subtracts %S from %P", 2, "-"),
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x6e", "%P = %P / %S", "Divides %P by %S", 2, "/"),
            new ProgramMethod(ProgramMethodCategory.Arithmetic, "_0x78", "%P = %P * %S", "Multiplies %P by %S", 2, "*"));

        /// <summary>The 1-op counter increment / decrement commands (<c>%P = %P + 1</c> / <c>%P = %P - 1</c>, tokens
        /// <c>_0x54</c>/<c>_0x57</c>) offered on a counter target instead of a second operand (US-032, e2).</summary>
        public static readonly ImmutableArray<ProgramMethod> CounterSteps = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Command, "_0x54", "%P = %P + 1", "Increments %P by 1", 1, "+"),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x57", "%P = %P - 1", "Decrements %P by 1", 1, "-"));

        private static bool IsFloatTag(string tag) => tag == "resource_floating_point";
        private static bool IsIntTag(string tag) => tag == "resource_integer";

        /// <summary>
        /// The arithmetic opcode for <c>%P &lt;op&gt; %S</c> with target type <paramref name="targetTag"/> and operand
        /// type <paramref name="operandTag"/>, or <c>null</c> when the cell is NOT authorable — the F-108 grid and the
        /// F-109 commit-legality matrix in one place (US-032, D22). A null cell is a dead vendor popup entry: OpenVisual
        /// never offers it and never invents a token. The token is the generic-column opcode for a same-class pair, or
        /// that opcode <c>+ 0x5</c> for a MIXED pair (exactly one operand is a floating-point). Legality by operator:
        /// <c>+</c> every pair except float+float; <c>-</c> only a float target with a float/int operand; <c>/</c> only
        /// an int target with an int/float operand (never a float target — F-107); <c>*</c> only a float/int target
        /// with at least one float operand (never int×int, never a counter). Counter targets get <c>+</c> (and the 1-op
        /// <see cref="CounterSteps"/>), never <c>-</c>/<c>*</c>/<c>/</c>.
        /// </summary>
        public static string? ArithmeticToken(string operatorSymbol, string targetTag, string operandTag)
        {
            bool tFloat = IsFloatTag(targetTag), oFloat = IsFloatTag(operandTag);
            bool tInt = IsIntTag(targetTag), oInt = IsIntTag(operandTag);
            bool authorable = operatorSymbol switch
            {
                "+" => !(tFloat && oFloat),                                          // all pairs except float+float
                "-" => tFloat && (oFloat || oInt),                                   // float target, float/int operand
                "/" => tInt && (oInt || oFloat),                                     // int target, int/float operand
                "*" => (tFloat || tInt) && (oFloat || oInt) && (tFloat || oFloat),   // float/int target, ≥1 float
                _ => false,
            };
            if (!authorable)
            {
                return null;
            }
            string generic = operatorSymbol switch { "+" => "_0x5a", "-" => "_0x64", "/" => "_0x6e", _ => "_0x78" };
            bool mixed = tFloat ^ oFloat;   // exactly one floating-point operand → the +0x5 mixed-conversion column
            return mixed ? MixedToken(generic) : generic;
        }

        // The mixed-column opcode is the generic-column opcode + 0x5 (F-108: `_0x5a`→`_0x5f`, `_0x64`→`_0x69`, …).
        private static string MixedToken(string genericToken) =>
            "_0x" + (Convert.ToInt32(genericToken.AsSpan(3).ToString(), 16) + 0x5).ToString("x", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The analog (continuous-value sensor/register) event triggers (US-028/PG-1b): the two %P-only
        /// triggers <c>is changed</c> (<c>_0x96</c>) / <c>is written</c> (<c>_0x9b</c>). Reuses the bool
        /// <see cref="Events"/> definitions for those tokens (same token, same template); an analog pin offers NO
        /// commands or conditions of its own (a numeric analog still reaches the arithmetic submenu separately).</summary>
        public static readonly ImmutableArray<ProgramMethod> AnalogEvents =
            Events.RemoveAll(m => m.Token is not ("_0x96" or "_0x9b"));

        /// <summary>The weekday event triggers (US-028/PG-1b): the vendor <c>System weekday -&gt; %P</c>
        /// (<c>_0x5</c>) plus the two shared <c>is changed</c>/<c>is written</c> triggers.</summary>
        public static readonly ImmutableArray<ProgramMethod> WeekdayEvents = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Event, "_0x5", "System ugedag -> %P", "Start program on the system weekday", 1, null))
            .AddRange(AnalogEvents);

        /// <summary>The full vendor set of nine timer command operators (US-028, D21/D22, e2/progmode3 oracles):
        /// reset <c>= 0</c> (<c>_0xa</c>), <c>= initial value</c> (<c>_0x19</c>), assign <c>= &lt;pin&gt;</c>
        /// (<c>_0x1e</c>), the two-operand generic add/subtract <c>= Timer +</c> (<c>_0x5a</c>) / <c>= Timer -</c>
        /// (<c>_0x64</c>), <c>Activate count-down … with initial value</c> (<c>_0xbe</c>), <c>Activate count-up</c>
        /// (<c>_0xc8</c>), bare <c>Activate count-down</c> (<c>_0xd2</c>), and <c>Stop counting</c> (<c>_0xdc</c>).
        /// Templates are the byte-fidelity vendor literals from <c>project4-PrgTokens.vis</c>; the two-operand entries
        /// use T008's second-pin picker (no copy of the vendor's F-096 Timertid-existence precondition, D21). Distinct
        /// from the bool <see cref="Commands"/> even where a token is shared (a timer's <c>_0xa</c> is <c>%P = 0</c>).</summary>
        public static readonly ImmutableArray<ProgramMethod> TimerCommands = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Command, "_0xa", "%P = 0", "Sets %P to 0", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x19", "%P = Initialværdi", "Sets %P to its initial value", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x1e", "%P = %S", "Assigns %S to %P", 2, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x5a", "%P = %P + %S", "Adds %S to %P", 2, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0x64", "%P = %P - %S", "Subtracts %S from %P", 2, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0xbe", "Aktiver nedtælling på %P med initial værdi",
                "Activates a count-down on %P starting from its initial value", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0xc8", "Aktiver optælling på %P", "Activates a count-up on %P", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0xd2", "Aktiver nedtælling på %P", "Activates a count-down on %P", 1, null),
            new ProgramMethod(ProgramMethodCategory.Command, "_0xdc", "Stands tælling på %P", "Stops counting on %P", 1, null));

        /// <summary>The timer event triggers (US-028, D22/progmode3): <c>-&gt; 0</c> (<c>_0xa</c>) and
        /// <c>is written</c> (<c>_0x9b</c>). The vendor's third popup entry (a two-operand <c>Timer -&gt;</c>) is a
        /// dead item (F-106) and is never modelled.</summary>
        public static readonly ImmutableArray<ProgramMethod> TimerEvents = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Event, "_0xa", "%P -> 0", "Start program when %P reaches 0", 1, null),
            new ProgramMethod(ProgramMethodCategory.Event, "_0x9b", "%P bliver tilskrevet", "Start program when %P is written", 1, null));

        /// <summary>The timer condition predicates (US-028/US-031, D22/progmode3): <c>= 0</c> (<c>_0xa</c>), the
        /// two-operand comparisons <c>&gt;</c> (<c>_0x32</c>) / <c>&gt;=</c> (<c>_0x46</c>) / <c>&lt;=</c>
        /// (<c>_0x50</c>), and the count-state predicates <c>counting up</c> (<c>_0xc8</c>) / <c>counting down</c>
        /// (<c>_0xd2</c>) / <c>stopped</c> (<c>_0xdc</c>). The vendor's <c>&lt;</c> is dead (no less-than token; authors
        /// swap operands into <c>&gt;</c>, F-106) and is never modelled. Note the count-state predicates REUSE the
        /// command opcodes <c>_0xc8</c>/<c>_0xd2</c>/<c>_0xdc</c> but carry the condition-family template — method
        /// semantics are (code, family)-scoped (F-105), which the (Category, Token) identity already models.</summary>
        public static readonly ImmutableArray<ProgramMethod> TimerConditions = ImmutableArray.Create(
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xa", "%P = 0", "Condition that %P is 0", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x32", "%P > %S", "Condition that %P is greater than %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x46", "%P >= %S", "Condition that %P is at least %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0x50", "%P <= %S", "Condition that %P is at most %S", 2, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xc8", "%P tæller op", "Condition that %P is counting up", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xd2", "%P tæller ned", "Condition that %P is counting down", 1, null),
            new ProgramMethod(ProgramMethodCategory.Condition, "_0xdc", "%P stoppet", "Condition that %P has stopped counting", 1, null));

        /// <summary>The event operators a pin of <paramref name="type"/> offers on an Events group (US-028/PG-1b).</summary>
        public static ImmutableArray<ProgramMethod> EventsFor(ProgramPinType type) => type switch
        {
            ProgramPinType.Analog => AnalogEvents,
            ProgramPinType.Weekday => WeekdayEvents,
            ProgramPinType.Timer => TimerEvents,
            _ => Events,
        };

        /// <summary>The command operators a pin of <paramref name="type"/> offers on a Commands group (US-028/PG-1b).
        /// Analog/weekday pins have none of their own (a numeric analog still reaches arithmetic separately).</summary>
        public static ImmutableArray<ProgramMethod> CommandsFor(ProgramPinType type) => type switch
        {
            ProgramPinType.Timer => TimerCommands,
            // F5: a numeric register offers NO boolean command. It is deliberately empty rather than populated with
            // an assignment set: the reference application's menu shows `Tal = 0` / `Tal =`, but no recorded oracle
            // contains a numeric-target action, so the opcodes are unmeasured — and this catalog's standing rule is
            // that a method is never offered without its vendor token. Adding them needs a token capture first.
            ProgramPinType.Numeric or ProgramPinType.Analog or ProgramPinType.Weekday => ImmutableArray<ProgramMethod>.Empty,
            _ => Commands,
        };

        /// <summary>The condition operators a pin of <paramref name="type"/> offers on a Conditions group
        /// (US-028/PG-1b). Only bool (and, by default, enum/numeric) pins have condition operators here; the timer
        /// condition comparisons land in T039.</summary>
        /// <remarks><see cref="ProgramPinType.Numeric"/> keeps the <see cref="Conditions"/> list it had while
        /// integer/counter classified as <see cref="ProgramPinType.Bool"/>: F5 is about COMMANDS, and no measurement
        /// of a numeric register's condition set exists yet, so splitting it here would be an unevidenced change
        /// smuggled in beside an evidenced one. Same for <c>EventsFor</c>, which falls to the default list.</remarks>
        public static ImmutableArray<ProgramMethod> ConditionsFor(ProgramPinType type) => type switch
        {
            ProgramPinType.Bool or ProgramPinType.Numeric => Conditions,
            ProgramPinType.Timer => TimerConditions,
            _ => ImmutableArray<ProgramMethod>.Empty,
        };

        /// <summary>The continuous-value sensor/register pin tags that classify as <see cref="ProgramPinType.Analog"/>
        /// (US-028/PG-1b) — temperature, humidity, light level and the generic floating-point register.</summary>
        private static readonly FrozenSet<string> AnalogPinTags = new[]
        {
            "resource_temperature", "resource_humidity_level", "resource_light_level", "resource_floating_point",
        }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>Maps a pin's SDK tag to its <see cref="ProgramPinType"/> operator family (US-028/PG-1b). A tag
        /// outside the timer/weekday/analog sets is <see cref="ProgramPinType.Bool"/> (the default list), so a
        /// timer/analog/weekday pin no longer inherits the bool operators while enum/numeric/date pins still do.</summary>
        public static ProgramPinType ClassifyPin(string tag) => tag switch
        {
            "resource_timer" => ProgramPinType.Timer,
            "resource_weekday" => ProgramPinType.Weekday,
            _ when AnalogPinTags.Contains(tag) => ProgramPinType.Analog,
            // F5: an integer/counter register is NOT a bool. (resource_floating_point is already Analog, above.)
            "resource_integer" or "resource_counter" => ProgramPinType.Numeric,
            _ => ProgramPinType.Bool,
        };

        /// <summary>The variable types a <c>program_case</c> may switch on (US-031): counter, enumerator, weekday,
        /// integer, or date. The single public source of truth for case-switch eligibility — the session's AddCase
        /// guard and the OpenVisual case menu both test membership against this set, so neither keeps its own copy.</summary>
        public static readonly FrozenSet<string> EligibleCaseVariableTags = new[]
        {
            "resource_counter", "resource_enum", "resource_weekday", "resource_integer", "resource_date",
        }.ToFrozenSet(StringComparer.Ordinal);

        /// <summary>The numeric variable types that can be an arithmetic target register or operand (US-032, sliver #3
        /// relocated from the app): decimal, integer, counter. The single public source of truth for arithmetic
        /// eligibility — the OpenVisual arithmetic menu tests membership against this set.</summary>
        public static readonly FrozenSet<string> NumericVariableTags = new[]
        {
            "resource_floating_point", "resource_integer", "resource_counter",
        }.ToFrozenSet(StringComparer.Ordinal);
    }
}
