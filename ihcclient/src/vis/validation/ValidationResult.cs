#nullable enable
using System.Collections.Immutable;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The outcome of validating a project against the pre-serialize checklist (id uniqueness, IDREF
    /// resolution, reciprocal link/scene bijection, function-block child sequence, Latin-1
    /// encodability, ...). See plan §7 <c>VisValidator</c>.
    /// </summary>
    public sealed record ValidationResult(bool IsValid, ImmutableArray<string> Errors)
    {
        /// <summary>A clean result with no errors.</summary>
        public static ValidationResult Success { get; } = new(true, ImmutableArray<string>.Empty);
    }
}
