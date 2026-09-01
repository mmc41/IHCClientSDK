using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The outcome of checking one value against one constraint. Carries the ARGUMENTS the message needs — the
    /// offending value, the bound it broke — rather than a formatted sentence, so one verdict can become a dialog
    /// tooltip, a commit refusal and a report finding without any of them re-wording it.
    /// </summary>
    /// <param name="Satisfied">Whether the value is acceptable.</param>
    /// <param name="Arguments">The declared argument bindings for the rule's Danish template. Empty when satisfied.</param>
    public readonly record struct ValueConstraintVerdict(
        bool Satisfied,
        EquatableArray<ProblemArgument> Arguments)
    {
        /// <summary>The satisfied verdict, with no arguments.</summary>
        public static ValueConstraintVerdict Ok => new(true, EquatableArray<ProblemArgument>.Empty);

        /// <summary>A failing verdict carrying the arguments its rule's template needs.</summary>
        /// <param name="arguments">The declared argument bindings, in slot order.</param>
        public static ValueConstraintVerdict Failed(EquatableArray<ProblemArgument> arguments) =>
            new(false, arguments);
    }

    /// <summary>
    /// What a GUI needs to know about a field, derived from the same constraint the validator runs.
    /// <para>
    /// This is what closes a live duplication: the product-dialog composer derives a numeric minimum and maximum
    /// from the placed element and then throws them away — the commit check reads the field's rule and never the
    /// bounds — so an out-of-range value commits, and the GUI carries its own hard-coded clamps as a second copy.
    /// </para>
    /// <para>
    /// WIDER than bounds and required-ness, deliberately. The shipped dialog rule this must replace carries a
    /// minimum length, a whitespace policy and a country-code requirement, and enum membership needs an allowed
    /// set. A four-field record could not hold them, and would have left the GUI's copy in place with nothing to
    /// delete.
    /// </para>
    /// </summary>
    /// <param name="Required">Whether a blank value is refused.</param>
    /// <param name="Minimum">The inclusive numeric lower bound, or null when unbounded below.</param>
    /// <param name="Maximum">The inclusive numeric upper bound, or null when unbounded above.</param>
    /// <param name="MinimumLength">The minimum text length, or null when unbounded.</param>
    /// <param name="MaximumLength">The maximum text length, or null when unbounded.</param>
    /// <param name="WhitespaceForbidden">
    /// Whether whitespace may NOT appear in the value at all — that is how the shipped <c>DialogValueRule</c>
    /// enforces it, rejecting a value containing any whitespace character. It is NOT the blank policy: a name with
    /// a space in it is a perfectly good name. See <see cref="FieldConstraintMetadata.Blank"/>, which is the other
    /// question and had to stop being answered here.
    /// <para>
    /// STATED AS THE BAN rather than the permission so that the struct's own <see langword="default"/> is the
    /// LOOSEST answer, which is the property every other member here already has. This is a record STRUCT, so a
    /// caller writing <c>FieldConstraintMetadata x = default</c> — which a dialog DTO's optional member must,
    /// since a default parameter has to be a compile-time constant and
    /// <see cref="FieldConstraintMetadata.Unconstrained"/> is not one — gets the all-zero value. With the
    /// permission stored, that zero said "whitespace forbidden": a rule nobody declared, applied to exactly the
    /// fields nobody had constrained. Read it through <see cref="FieldConstraintMetadata.WhitespaceAllowed"/>.
    /// </para>
    /// </param>
    /// <param name="AllowedValues">
    /// The closed set of acceptable values, or empty when the field is open. This is the enum-membership case; a
    /// GUI may present it however it likes, but it stays a typing aid rather than a closed picker unless the
    /// product family says otherwise.
    /// </param>
    public readonly record struct FieldConstraintMetadata(
        bool Required,
        double? Minimum,
        double? Maximum,
        int? MinimumLength,
        int? MaximumLength,
        bool WhitespaceForbidden,
        EquatableArray<string> AllowedValues)
    {
        /// <summary>
        /// Whether whitespace may appear in the value at all — the reading half of
        /// <see cref="WhitespaceForbidden"/>, and the one every consumer wants. Stored as the ban so that the
        /// struct's default is unconstrained; read as the permission because that is the question a dialog asks.
        /// </summary>
        public bool WhitespaceAllowed => !WhitespaceForbidden;

        /// <summary>
        /// WHAT COUNTS AS BLANK for a <see cref="Required"/> field: empty only, or whitespace-only too.
        /// <para>
        /// Its own member (D4), because the two facts it was once folded into
        /// <see cref="WhitespaceAllowed"/> beside are not the same question. Overloading that flag told a dialog
        /// that a required name could contain no space at all, and since the merge is a conjunction, one such
        /// constraint decided the field for every other rule on it.
        /// </para>
        /// <para>
        /// <see cref="BlankPolicy.EmptyOnly"/> is the default so that an <see cref="Unconstrained"/> field is the
        /// LOOSEST answer and a merge can only tighten — the property every other member here already has.
        /// </para>
        /// </summary>
        public BlankPolicy Blank { get; init; }

        /// <summary>
        /// A field with no constraint at all — the honest answer for an attribute no rule targets, and byte-for-byte
        /// the struct's own <see langword="default"/>, so an OMITTED constraint and an explicitly unconstrained one
        /// are the same value.
        /// </summary>
        public static FieldConstraintMetadata Unconstrained =>
            new(false, null, null, null, null, false, EquatableArray<string>.Empty);
    }

    /// <summary>
    /// A DECLARATIVE constraint on one value — what makes "one definition, several faces" real rather than
    /// asserted. <see cref="Check"/> (is this value acceptable?) serves the commit verdict and the whole-project
    /// finding; <see cref="Describe"/> (what would be acceptable?) serves the dialog. Because both come off one
    /// object, a bound cannot be enforced in one place and advertised differently in another.
    /// <para>
    /// SCOPE, measured rather than assumed: of the 35 existing rule ids about nine are genuinely declarative with
    /// something for <see cref="Describe"/> to say, and roughly twenty of the eventual rows. The rest are
    /// traversals — graph reachability, dataflow, cross-element counting — and use <see cref="ProjectInspection"/>
    /// instead. So this abstraction serves about one row in five. That is a real population and worth the type;
    /// it is not a general property of the catalogue, and nothing should be designed as though it were.
    /// </para>
    /// </summary>
    public interface IValueConstraint
    {
        /// <summary>The rule this constraint realises.</summary>
        ProblemCode Code { get; }

        /// <summary>Whether a value is acceptable, and the arguments explaining it if not.</summary>
        /// <param name="rawValue">The attribute value as it stands in the file, or null when absent.</param>
        ValueConstraintVerdict Check(string? rawValue);

        /// <summary>The same constraint as data a GUI can bind to.</summary>
        FieldConstraintMetadata Describe();
    }

    /// <summary>
    /// Several ordered ways to fail ONE code, applied in order over one attribute and stopping at the first
    /// failure.
    /// <para>
    /// WHAT IT CAN EXPRESS, stated against what the executor actually does: <c>RunConstraints</c> walks the
    /// sequence, stops at the first unsatisfied constraint, and reports through <c>rule.Entry</c> — the RULE's
    /// entry, never the failing constraint's own <see cref="IValueConstraint.Code"/>. So a sequence is one
    /// finding with one code and one template, whose members differ only in the <c>Arguments</c> they bind: a
    /// value can be reported as malformed by one member and as out of range by another, and the reader gets the
    /// first reason rather than a list of every way it is wrong.
    /// </para>
    /// <para>
    /// WHAT IT CANNOT EXPRESS, and this correction is why the type is still unexercised: a family of SEPARATE
    /// codes. Three rules with three catalogue entries stay three rules — a sequence would collapse them onto
    /// whichever entry the rule was built from, so two of the three codes would never be raised and their
    /// entries would fail the completeness gate. Whether the executor SHOULD instead report under the failing
    /// constraint's own code is a real question and is deliberately not answered here.
    /// </para>
    /// <para>
    /// Authored and reserved: no registered rule needs it yet.
    /// </para>
    /// </summary>
    /// <param name="Ordered">The constraints, most fundamental first. Evaluation stops at the first failure.</param>
    public sealed record ConstraintSequence(EquatableArray<IValueConstraint> Ordered);
}
