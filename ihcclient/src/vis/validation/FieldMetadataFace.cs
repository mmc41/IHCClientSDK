#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// THE FIELD-METADATA FACE — what a GUI needs to know about a field, answered from the SAME rule definitions
    /// the whole-project run executes. Two engine faces read one rule set; a rule declares which of them it
    /// answers to, and both of the members here honour that declaration.
    /// <para>
    /// It is an extension on <see cref="RuleSet"/> rather than a port of its own. A single-method interface behind
    /// another single-method interface, differing by one parameter, is a pass-through that adds a name and no
    /// abstraction — and its natural second member, "the rules behind this metadata", has no consumer, because a
    /// host reaches everything through the application service.
    /// </para>
    /// <para>
    /// This is the half that closes a live duplication. Today the product-dialog composer derives a field's
    /// numeric bounds and then throws them away, the commit check never reads them, and the GUI carries its own
    /// hard-coded clamps as a second copy that can disagree. One constraint answering both "is this acceptable?"
    /// and "what would be acceptable?" is what makes a bound impossible to enforce in one place and advertise
    /// differently in another.
    /// </para>
    /// </summary>
    public static class FieldMetadataFace
    {
        extension(RuleSet rules)
        {
            /// <summary>
            /// The constraints on one field, merged into the shape a dialog binds to. Where two rules constrain
            /// the same field, the MOST RESTRICTIVE answer wins — a dialog that advertised the looser of two
            /// bounds would invite a value the commit path then refuses.
            /// </summary>
            /// <param name="target">The (tag, attribute) pair the field edits.</param>
            public FieldConstraintMetadata DescribeField(RuleTarget target)
            {
                ArgumentNullException.ThrowIfNull(rules);
                FieldConstraintMetadata merged = FieldConstraintMetadata.Unconstrained;
                foreach (RuleDefinition rule in rules.ForTarget(target).Where(DeclaresThisFace))
                {
                    if (rule.Constraints is not { } sequence)
                    {
                        // A traversal has nothing a dialog could bind to. That is not an omission: it is why a
                        // rule declares exactly one body kind, and why the multi-face claim stays honest.
                        continue;
                    }

                    foreach (IValueConstraint constraint in sequence.Ordered)
                    {
                        merged = Tighten(merged, constraint.Describe());
                    }
                }

                return merged;
            }

            /// <summary>
            /// The rules a dialog field is subject to, for a caller that needs the identities rather than the
            /// merged shape — a tooltip naming which rule refused, or a test proving a field is covered.
            /// </summary>
            /// <param name="target">The (tag, attribute) pair the field edits.</param>
            public EquatableArray<ProblemCode> ConstraintsOn(RuleTarget target)
            {
                ArgumentNullException.ThrowIfNull(rules);
                return rules.ForTarget(target)
                    .Where(DeclaresThisFace)
                    .Where(r => r.Constraints is not null)
                    .SelectMany(r => r.Constraints!.Ordered.Select(c => c.Code))
                    .ToImmutableArray();
            }
        }

        /// <summary>
        /// Whether a rule answers to THIS face. Target alone is not enough: a rule may constrain a field for the
        /// whole-project run without offering that constraint to a dialog, and a declaration nothing reads is a
        /// declaration that means nothing. Registration cannot enforce it — a constraint serving one face is
        /// legal there — so both members of this face filter on it.
        /// </summary>
        private static bool DeclaresThisFace(RuleDefinition rule) =>
            (rule.Entry.Faces & RuleFaces.DialogMetadata) != 0;

        /// <summary>
        /// The merge, exposed: two constraints on one field combine to the STRICTER answer on every axis. Public
        /// because it is the rule a caller composing metadata from more than one source has to follow, and because
        /// a merge that only a private path could exercise could not be tested for the axis it forgets.
        /// </summary>
        /// <param name="into">The metadata accumulated so far.</param>
        /// <param name="from">The metadata to merge in.</param>
        public static FieldConstraintMetadata Stricter(FieldConstraintMetadata into, FieldConstraintMetadata from) =>
            Tighten(into, from);

        private static FieldConstraintMetadata Tighten(FieldConstraintMetadata into, FieldConstraintMetadata from) =>
            new(
                into.Required || from.Required,
                Higher(into.Minimum, from.Minimum),
                Lower(into.Maximum, from.Maximum),
                Higher(into.MinimumLength, from.MinimumLength),
                Lower(into.MaximumLength, from.MaximumLength),
                into.WhitespaceAllowed && from.WhitespaceAllowed,
                Narrow(into.AllowedValues, from.AllowedValues))
            {
                // The stricter blank policy wins, like every other axis here: a dialog advertising the looser of
                // two policies would invite a value the commit path then refuses. WhitespaceIsBlank is the
                // stricter of the two, and EmptyOnly is the identity — which is why Unconstrained carries it.
                Blank = into.Blank == BlankPolicy.WhitespaceIsBlank || from.Blank == BlankPolicy.WhitespaceIsBlank
                    ? BlankPolicy.WhitespaceIsBlank
                    : BlankPolicy.EmptyOnly,
            };

        private static double? Higher(double? a, double? b) =>
            a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

        private static double? Lower(double? a, double? b) =>
            a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

        private static int? Higher(int? a, int? b) =>
            a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

        private static int? Lower(int? a, int? b) =>
            a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

        private static EquatableArray<string> Narrow(EquatableArray<string> a, EquatableArray<string> b)
        {
            if (a.IsEmpty)
            {
                return b;
            }

            if (b.IsEmpty)
            {
                return a;
            }

            HashSet<string> allowed = new(b, StringComparer.Ordinal);
            return a.Where(allowed.Contains).ToImmutableArray();
        }
    }
}
