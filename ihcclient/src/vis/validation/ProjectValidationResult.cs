#nullable enable
using System;
using System.Collections.Immutable;

namespace Ihc.Projects
{
    /// <summary>
    /// The outcome of validating a project against the pre-serialize checklist (id uniqueness, IDREF
    /// resolution, reciprocal link/scene bijection, function-block child sequence, Latin-1
    /// encodability, ...).
    /// </summary>
    public sealed record ProjectValidationResult(bool IsValid, ImmutableArray<string> Errors)
    {
        /// <summary>A clean result with no errors.</summary>
        public static ProjectValidationResult Success { get; } = new(true, ImmutableArray<string>.Empty);

        /// <summary>Structural (value) equality, comparing the <see cref="Errors"/> contents by value.</summary>
        public bool Equals(ProjectValidationResult? other) =>
            other is not null
            && IsValid == other.IsValid
            && ImmutableArrayValue.Equal(Errors, other.Errors);

        public override int GetHashCode() =>
            HashCode.Combine(IsValid, ImmutableArrayValue.Hash(Errors));

        public override string ToString() =>
            $"ProjectValidationResult(IsValid={IsValid}, Errors=string[{(Errors.IsDefaultOrEmpty ? 0 : Errors.Length)}])";
    }
}
