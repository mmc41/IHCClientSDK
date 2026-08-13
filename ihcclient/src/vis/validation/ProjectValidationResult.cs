#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// The outcome of validating a project against the pre-serialize checklist (id uniqueness, IDREF
    /// resolution, reciprocal link/scene bijection, function-block child sequence, Latin-1
    /// encodability, ...). <see cref="Errors"/> keeps the flat message list; <see cref="Findings"/> carries
    /// the structured form (severity, rule id, locator) a GUI filters and navigates by. <see cref="IsValid"/>
    /// means no <see cref="ValidationSeverity.Error"/> findings — warnings alone leave a project valid.
    /// </summary>
    public sealed record ProjectValidationResult(bool IsValid, EquatableArray<string> Errors)
    {
        /// <summary>A clean result with no errors.</summary>
        public static ProjectValidationResult Success { get; } = new(true, []);

        /// <summary>
        /// Builds a result from a finding list: <see cref="IsValid"/> is true when no finding is an
        /// <see cref="ValidationSeverity.Error"/>, and <see cref="Errors"/> collects those error messages. An empty
        /// finding list returns <see cref="Success"/>. The shared shaping every validation entry point uses.
        /// </summary>
        public static ProjectValidationResult FromFindings(EquatableArray<ProjectValidationFinding> findings)
        {
            if (findings.IsEmpty)
            {
                return Success;
            }
            ImmutableArray<string> errors = findings
                .Where(f => f.Severity == ValidationSeverity.Error)
                .Select(f => f.Message)
                .ToImmutableArray();
            return new ProjectValidationResult(errors.IsEmpty, errors) { Findings = findings };
        }

        /// <summary>Every finding (errors and warnings), in document-scan order.</summary>
        public EquatableArray<ProjectValidationFinding> Findings { get; init; } = [];

        /// <summary>The warning messages (vendor-tolerated deviations; never block a save/upload).</summary>
        /// <remarks>Computed, so it has no backing field and carries no equality significance of its own — the
        /// <see cref="Findings"/> it derives from is the stored member equality compares.</remarks>
        public ImmutableArray<string> Warnings =>
            [.. Findings.Where(f => f.Severity == ValidationSeverity.Warning).Select(f => f.Message)];

        // Equality and hashing are the record's, over both EquatableArray members. Findings still participates,
        // which is what keeps a warnings-only result (valid, no errors, but carrying warning findings) distinct
        // from Success — a change in warnings is never hidden by equality-based diffing.

        public override string ToString() =>
            $"ProjectValidationResult(IsValid={IsValid}, Errors=string[{Errors.Length}], " +
            $"Findings=ProjectValidationFinding[{Findings.Length}])";
    }
}
