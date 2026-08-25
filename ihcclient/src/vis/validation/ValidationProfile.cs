#nullable enable
using System;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// WHO the findings are for. Two named values rather than a configuration surface: the eight categories are
    /// classification-only, so there is no per-category enable/disable to build.
    /// </summary>
    public enum ProfileAudience
    {
        /// <summary>The pre-serialize structural checklist alone — what <c>Validate</c> means today.</summary>
        Structural,

        /// <summary>
        /// The full categorized verification — the structural checklist plus the advisory documentation findings
        /// that feed the report appendix. What <c>ValidateCategorized</c> means today.
        /// </summary>
        Categorized,
    }

    /// <summary>What happens when a RULE ITSELF throws.</summary>
    public enum RuleFailurePolicy
    {
        /// <summary>
        /// THE DEFAULT. The failing rule contributes one <c>internal.unexpected</c> finding carrying the exception
        /// as its English diagnostic, and the pass CONTINUES. A broken rule costs its own result, not the run —
        /// the alternative hands the user a clean bill of health produced by a crash.
        /// </summary>
        ReportAndContinue,

        /// <summary>
        /// Rethrow, aborting the pass. For test and diagnostic runs, where a swallowed bug is worse than a lost
        /// pass. Selectable here rather than fixed in the executor precisely so a rule-throws test can choose it.
        /// </summary>
        Rethrow,
    }

    /// <summary>One rule's severity, overridden for one profile.</summary>
    /// <param name="Code">The rule.</param>
    /// <param name="Severity">The severity it takes under this profile.</param>
    public readonly record struct SeverityOverride(ProblemCode Code, ValidationSeverity Severity);

    /// <summary>
    /// WHICH RULES RUN. A profile is a selection on two axes:
    /// <list type="number">
    /// <item><description>
    /// AUDIENCE — structural versus categorized, the distinction the two existing entry points already draw.
    /// </description></item>
    /// <item><description>
    /// EVALUABILITY — a rule needing controller limits is NOT IN the profile unless <see cref="Controller"/> was
    /// supplied. It does not run and does not report, rather than running and guessing: a verdict that depends on
    /// the machine is not a property of the project file, and reporting one would make the same project valid on
    /// one workstation and invalid on another.
    /// </description></item>
    /// </list>
    /// <para>
    /// A profile is NOT a blocking policy. That distinction is load-bearing — a profile changes what is LOOKED
    /// FOR, blocking changes what is TOLERATED — and conflating them would let a stricter save gate silently run
    /// rules the user never saw findings from. But blocking needs no type: every gate in the product blocks on
    /// errors and tolerates warnings, which is one read over one pass. A threshold type would have had exactly one
    /// legal value, and its only other lever is the per-rule override below.
    /// </para>
    /// <para>
    /// The context axis is a nullable field plus a per-entry bool, not a context vocabulary: exactly one such
    /// context exists and three rows use it, so an enum with one member, an availability set and an interface to
    /// read it would be six mechanisms for one question. Every property that matters survives — the requirement is
    /// declared, it is evaluability rather than strictness, there is no implicit fallback, and no rule body has to
    /// handle absence.
    /// </para>
    /// </summary>
    /// <param name="Name">The profile's name, for diagnostics.</param>
    /// <param name="Audience">Structural or categorized.</param>
    /// <param name="Controller">The target controller's limits, or null — the evaluability axis.</param>
    /// <param name="Overrides">Per-rule severity overrides, the one strictness lever.</param>
    public sealed record ValidationProfile(
        string Name,
        ProfileAudience Audience,
        ControllerCapabilityLimits? Controller,
        EquatableArray<SeverityOverride> Overrides)
    {
        /// <summary>The default: structural rules, project file only, no overrides.</summary>
        public static ValidationProfile ProjectOnly { get; } =
            new(nameof(ProjectOnly), ProfileAudience.Structural, null, EquatableArray<SeverityOverride>.Empty);

        /// <summary>Structural plus documentation, project file only.</summary>
        public static ValidationProfile Categorized { get; } =
            new(nameof(Categorized), ProfileAudience.Categorized, null, EquatableArray<SeverityOverride>.Empty);

        /// <summary>What a run does when a rule throws. <see cref="RuleFailurePolicy.ReportAndContinue"/> by default.</summary>
        public RuleFailurePolicy FailurePolicy { get; init; }

        /// <summary>
        /// The library a placed block's claimed identity can be looked up in, when the caller has one (D27). Null
        /// on the two default profiles: a caller with no catalog validates without the rows that need one, rather
        /// than against a guessed library default. <c>ProjectAppService</c> supplies it, because it already holds
        /// a catalog.
        /// </summary>
        public ILibraryBlockSource? Library { get; init; }

        /// <summary>Whether this profile selects that rule — audience and evaluability together.</summary>
        /// <param name="entry">The catalogue entry to test.</param>
        public bool Includes(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return CanEvaluate(entry)
                && (Audience == ProfileAudience.Categorized
                    || entry.Category != ValidationCategory.Documentation);
        }

        /// <summary>
        /// The EVALUABILITY half of <see cref="Includes"/> on its own: whether this profile was given what the
        /// rule needs to run at all.
        /// <para>
        /// Separate from the audience half because one consumer needs exactly this half and not the other. A
        /// findings export states which rules could not be evaluated, and a profile that legitimately omits a
        /// whole category for its AUDIENCE has not failed to evaluate anything — listing those would turn a
        /// deliberate scope into an apology. Stating the predicate once is what keeps a third capability flag
        /// from reaching one reader and not the other.
        /// </para>
        /// <para>
        /// Internal: <see cref="Includes"/> remains the public question. This half has one in-assembly reader,
        /// and sharing a predicate is not a reason to widen the SDK's surface.
        /// </para>
        /// </summary>
        /// <param name="entry">The catalogue entry to test.</param>
        internal bool CanEvaluate(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return (!entry.RequiresControllerLimits || Controller is not null)
                && (!entry.RequiresLibrary || Library is not null);
        }

        /// <summary>The severity this rule takes here: its disposition, unless an override names it.</summary>
        /// <param name="entry">The catalogue entry whose severity is wanted.</param>
        public ValidationSeverity SeverityFor(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            foreach (SeverityOverride @override in Overrides)
            {
                if (@override.Code == entry.Code)
                {
                    return @override.Severity;
                }
            }

            return entry.Severity ?? ValidationSeverity.Error;
        }
    }
}
