#nullable enable
using System.Collections.Immutable;
using System.Linq;

using Ihc.Vis.Model;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// BLOCKING, read over the findings of ONE pass.
    /// <para>
    /// A profile and a blocking decision are different things, and keeping them apart is load-bearing: a profile
    /// changes what is LOOKED FOR, blocking changes what is TOLERATED. Conflating them would let a stricter save
    /// gate silently run rules the user never saw findings from, so that a save fails for a reason nothing ever
    /// reported.
    /// </para>
    /// <para>
    /// Blocking needs no type of its own. Every gate in the product blocks on errors and tolerates the advisory
    /// tiers, which is one read over one run — a threshold type would have had exactly one legal value, and the
    /// only other strictness lever is the per-rule severity override on the profile. Where a gate must be
    /// stricter, it selects a profile that PROMOTES the rule it cares about, so the finding the user sees and the
    /// finding that blocks are the same finding.
    /// </para>
    /// <para>
    /// These are extension members on the findings themselves rather than a result record, because a second
    /// result type would duplicate the one the application service already returns.
    /// </para>
    /// </summary>
    public static class ValidationGate
    {
        extension(EquatableArray<ValidationFinding> findings)
        {
            /// <summary>
            /// Whether the project passes: no finding is an Error. Neither advisory tier blocks — a Warning is a
            /// punch-list item only the author can judge, and an Info is not even that.
            /// </summary>
            public bool IsValid => !findings.Any(f => f.Severity == ValidationSeverity.Error);

            /// <summary>The blocking findings, in the run's order.</summary>
            public EquatableArray<ValidationFinding> Errors =>
                findings.Where(f => f.Severity == ValidationSeverity.Error).ToImmutableArray();

            /// <summary>The advisory findings the author is asked to JUDGE, in the run's order.</summary>
            public EquatableArray<ValidationFinding> Warnings =>
                findings.Where(f => f.Severity == ValidationSeverity.Warning).ToImmutableArray();

            /// <summary>
            /// The findings that are merely worth KNOWING, in the run's order — the tier below
            /// <c>Warnings</c>, and like it never blocking.
            /// </summary>
            public EquatableArray<ValidationFinding> Infos =>
                findings.Where(f => f.Severity == ValidationSeverity.Info).ToImmutableArray();
        }
    }
}
