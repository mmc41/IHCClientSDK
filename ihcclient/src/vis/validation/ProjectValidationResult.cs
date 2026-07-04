#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Ihc.Projects
{
    /// <summary>
    /// The outcome of validating a project against the pre-serialize checklist (id uniqueness, IDREF
    /// resolution, reciprocal link/scene bijection, function-block child sequence, Latin-1
    /// encodability, ...). <see cref="Errors"/> keeps the flat message list; <see cref="Findings"/> carries
    /// the structured form (severity, rule id, locator) a GUI filters and navigates by. <see cref="IsValid"/>
    /// means no <see cref="ValidationSeverity.Error"/> findings — warnings alone leave a project valid.
    /// </summary>
    public sealed record ProjectValidationResult(bool IsValid, ImmutableArray<string> Errors)
    {
        /// <summary>A clean result with no errors.</summary>
        public static ProjectValidationResult Success { get; } = new(true, ImmutableArray<string>.Empty);

        /// <summary>Every finding (errors and warnings), in document-scan order.</summary>
        public ImmutableArray<ProjectValidationFinding> Findings { get; init; } =
            ImmutableArray<ProjectValidationFinding>.Empty;

        /// <summary>The warning messages (vendor-tolerated deviations; never block a save/upload).</summary>
        public ImmutableArray<string> Warnings =>
            Findings.IsDefaultOrEmpty
                ? ImmutableArray<string>.Empty
                : Findings.Where(f => f.Severity == ValidationSeverity.Warning).Select(f => f.Message).ToImmutableArray();

        /// <summary>
        /// Structural (value) equality over the full outcome — <see cref="IsValid"/>, the <see cref="Errors"/>
        /// list and the <see cref="Findings"/> list — each compared by content. Including <see cref="Findings"/>
        /// keeps a warnings-only result (valid, no errors, but carrying warning findings) distinct from
        /// <see cref="Success"/>, so a change in warnings is never hidden by equality-based diffing.
        /// </summary>
        public bool Equals(ProjectValidationResult? other) =>
            other is not null
            && IsValid == other.IsValid
            && ImmutableArrayValue.Equal(Errors, other.Errors)
            && ImmutableArrayValue.Equal(Findings, other.Findings);

        public override int GetHashCode() =>
            HashCode.Combine(IsValid, ImmutableArrayValue.Hash(Errors), ImmutableArrayValue.Hash(Findings));

        public override string ToString() =>
            $"ProjectValidationResult(IsValid={IsValid}, Errors=string[{(Errors.IsDefaultOrEmpty ? 0 : Errors.Length)}], " +
            $"Findings=ProjectValidationFinding[{(Findings.IsDefaultOrEmpty ? 0 : Findings.Length)}])";
    }
}
