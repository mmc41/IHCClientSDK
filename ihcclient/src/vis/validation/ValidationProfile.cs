#nullable enable
using System;
using System.Linq;

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
    /// <item><description>
    /// APPLICABILITY — a firmware-errata rule runs with NO context and is withheld only when <see cref="Firmware"/>
    /// names a target that is already past the release which fixed its defect. The reverse direction from
    /// evaluability, for the reason spelled out on <see cref="Firmware"/>: its condition is in the file, so the
    /// safe answer is the one a caller who knows nothing gets.
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
    /// A context is a nullable field beside its per-entry declaration, never a context vocabulary: an enum of
    /// kinds, an availability set and an interface to read it would be several mechanisms for one question, and the
    /// properties that matter survive without them — the requirement is declared, it is evaluability or
    /// applicability rather than strictness, there is no implicit fallback, and no rule body has to handle absence.
    /// </para>
    /// <para>
    /// The three are NOT interchangeable, and the entry says which it means by which field it sets. Controller
    /// limits and a library are ENABLING and declared as bools; a firmware bound is NARROWING and declared as the
    /// bound itself, because there is no third bool to set — the row needs no permission to run.
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

        /// <summary>
        /// The TARGET CONTROLLER'S firmware, when the caller knows it — the third declared context, and the one
        /// that behaves oppositely to the two above.
        /// <para>
        /// <b>Narrowing, not enabling.</b> The two contexts above are enabling: absent, and the rules needing them
        /// do not run. This one is absent by default and the rules that read it RUN ANYWAY, because their condition
        /// — the project uses a feature an affected firmware mishandles — is decided from the file. Supplying a
        /// target can only withhold a finding whose fix that target is past. It can never add one, which is what
        /// keeps the default answer the safe one and bounds what a wrong target can cost.
        /// </para>
        /// <para>
        /// The consequence is deliberate and worth naming: with a target declared, the same project reports
        /// differently on two workstations — the thing the evaluability axis above refuses to allow. It is
        /// tolerable here only because the direction is fixed. Undeclared is the strict reading, so a caller who
        /// knows nothing is never told less than a caller who knows something.
        /// </para>
        /// </summary>
        public ControllerFirmwareVersion? Firmware { get; init; }

        /// <summary>Whether this profile selects that rule — audience, evaluability and applicability together.</summary>
        /// <param name="entry">The catalogue entry to test.</param>
        public bool Includes(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return CanEvaluate(entry)
                && AppliesTo(entry)
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

        /// <summary>
        /// The APPLICABILITY half: whether this row's condition still costs anything on the declared target.
        /// <para>
        /// Three of the four answers are yes, and that asymmetry is the design. A row declaring no firmware bound
        /// is unaffected; a row whose defect no release is known to fix is unaffected however new the target; a
        /// caller with no target in mind gets the strict reading. Only a KNOWN target at or past a KNOWN fix
        /// withholds — the one case where reporting would be telling a user about a defect their controller does
        /// not have.
        /// </para>
        /// <para>
        /// Deliberately separate from <see cref="CanEvaluate"/> rather than folded into it, and the separation is
        /// load-bearing rather than tidiness: the findings export publishes <see cref="CanEvaluate"/>'s negatives
        /// as rules it could not run for want of context. A row narrowed away here was evaluated and found not to
        /// apply, which is the opposite statement.
        /// </para>
        /// <para>
        /// Private, unlike <see cref="CanEvaluate"/>: that half has an in-assembly reader in the export, this one
        /// has none, and <see cref="Includes"/> — which null-checks for it — is the door.
        /// </para>
        /// </summary>
        /// <param name="entry">The catalogue entry to test.</param>
        private bool AppliesTo(ProblemCatalogEntry entry) =>
            entry.FirmwareBound?.FixedIn is not { } fixedIn
                || Firmware is not { } target
                || target < fixedIn;

        /// <summary>
        /// The severity this rule takes here: its disposition, unless an override names it.
        /// <para>
        /// The lever raises strictness and may lower it, but it stops at the rows that REFUSE an operation. Their
        /// consequence is not a judgement a profile gets to soften — the save does not happen, the file does not
        /// open — so a demotion below <see cref="ValidationSeverity.Error"/> would produce a finding whose Danish
        /// sentence says the operation was refused while its severity files it as advice. It THROWS rather than
        /// flooring silently, because a profile that cannot mean what it says is a caller's mistake, and quietly
        /// ignoring one half of an override is how a strictness setting comes to be believed and not applied.
        /// </para>
        /// </summary>
        /// <param name="entry">The catalogue entry whose severity is wanted.</param>
        /// <exception cref="InvalidOperationException">
        /// An override demotes a row declaring <see cref="ProblemCatalogEntry.RefusedOperations"/> below
        /// <see cref="ValidationSeverity.Error"/>.
        /// </exception>
        public ValidationSeverity SeverityFor(ProblemCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            foreach (SeverityOverride @override in Overrides)
            {
                if (@override.Code == entry.Code)
                {
                    if (@override.Severity != ValidationSeverity.Error && !entry.RefusedOperations.IsEmpty)
                    {
                        throw new InvalidOperationException(
                            $"Profile '{Name}' overrides '{entry.Code.Value}' to {@override.Severity}, but that row "
                            + $"refuses {string.Join(", ", entry.RefusedOperations.Select(op => op.Value))}. A row "
                            + "that stops an operation reports as Error under every profile; the per-rule override "
                            + "may make a rule stricter, never advisory.");
                    }

                    return @override.Severity;
                }
            }

            return entry.Severity ?? ValidationSeverity.Error;
        }
    }
}
