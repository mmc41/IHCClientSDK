
using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// How a blank value is decided. Two named partitions, because the three ad-hoc blank gates this replaces
    /// disagreed about exactly the middle case, and both spellings ship in the codebase today.
    /// </summary>
    public enum BlankPolicy
    {
        /// <summary>Only null or empty is blank. Whitespace is content.</summary>
        EmptyOnly,

        /// <summary>
        /// Null, empty or whitespace-only is blank. THE DEFAULT for a user-authored name: a name of three spaces
        /// is not a name, and treating it as one is how a project ends up with invisible labels.
        /// </summary>
        WhitespaceIsBlank,
    }

    /// <summary>
    /// The required-field vocabulary: ONE declarative rule kind replacing three inconsistent blank gates, read by
    /// the dialog metadata and the commit verdict alike.
    /// <para>
    /// This is the cheaper and more common half of the constraint pattern, and it exists to show the shape
    /// generalises: numeric bounds could have been a one-off, while required-ness appears on dozens of rows. Both
    /// reach their faces through the same <see cref="IValueConstraint"/>, which is what makes it a vocabulary
    /// rather than two special cases.
    /// </para>
    /// <para>
    /// WHAT IT REPLACES, and why the middle case matters. Three gates exist in the app today and each answers
    /// differently: one trims before testing, two test emptiness alone, and only one of the three tells the user
    /// anything at all. A shared declaration means the same question gets the same answer everywhere, and the
    /// answer is stated rather than implied by whichever helper the site happened to reach for.
    /// </para>
    /// <para>
    /// A required field's refusal carries the rule's OWN Danish message, so a host does not author a second
    /// sentence for the same condition.
    /// </para>
    /// </summary>
    public sealed class RequiredFieldConstraint : IValueConstraint
    {
        private RequiredFieldConstraint(ProblemCode code, BlankPolicy policy)
        {
            Code = code;
            Policy = policy;
        }

        /// <inheritdoc/>
        public ProblemCode Code { get; }

        /// <summary>Which values count as blank for this field.</summary>
        public BlankPolicy Policy { get; }

        /// <summary>
        /// Builds the constraint for one rule with the given blank policy.
        /// </summary>
        /// <param name="code">The rule this constraint realises, whose Danish message the refusal carries.</param>
        /// <param name="policy">
        /// Which values count as blank. <see cref="BlankPolicy.WhitespaceIsBlank"/> for anything a person reads;
        /// <see cref="BlankPolicy.EmptyOnly"/> where a leading or trailing space is content the file must keep.
        /// </param>
        public static RequiredFieldConstraint For(ProblemCode code, BlankPolicy policy = BlankPolicy.WhitespaceIsBlank) =>
            new(code, policy);

        /// <inheritdoc/>
        public ValueConstraintVerdict Check(string? rawValue) =>
            IsBlank(rawValue)
                ? ValueConstraintVerdict.Failed(EquatableArray<ProblemArgument>.Empty)
                : ValueConstraintVerdict.Ok;

        /// <summary>
        /// Reports <see cref="FieldConstraintMetadata.Required"/>, so a dialog marks the field and refuses an
        /// empty commit using the rule's own message rather than a second sentence authored for the same
        /// condition. The whitespace policy travels with it, so a dialog that trims and a validator that does not
        /// cannot disagree about a field of three spaces.
        /// </summary>
        public FieldConstraintMetadata Describe() =>
            FieldConstraintMetadata.Unconstrained with
            {
                Required = true,

                // The policy on its OWN member (D4), never folded into WhitespaceAllowed: that flag means "no
                // whitespace character anywhere", so setting it from the policy advertised a required NAME as
                // rejecting "Stue loft" — and the merge is a conjunction, so one such constraint decided the
                // field for every other rule on it.
                Blank = Policy,
                MinimumLength = 1,
            };

        private bool IsBlank(string? value) => Policy == BlankPolicy.EmptyOnly
            ? string.IsNullOrEmpty(value)
            : string.IsNullOrWhiteSpace(value);
    }
}
