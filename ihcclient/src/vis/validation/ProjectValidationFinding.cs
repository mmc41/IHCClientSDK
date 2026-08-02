#nullable enable
namespace Ihc.Vis.Validation
{
    /// <summary>How severe a validation finding is for persisting the project.</summary>
    public enum ValidationSeverity
    {
        /// <summary>A state the serializer, IHC Visual or the controller is known (or spec-required) to reject.</summary>
        Error,

        /// <summary>A deviation vendor tooling tolerates — advisory for a GUI, never blocks a save/upload.</summary>
        Warning,
    }

    /// <summary>Which check family a validation finding belongs to (R10).</summary>
    public enum ValidationCategory
    {
        /// <summary>The pre-serialize structural checklist — ids, IDREFs, bijections, schema conformance, …</summary>
        Structural,

        /// <summary>Documentation completeness (the US-072 checks) — advisory content gaps, never structural damage.</summary>
        Documentation,
    }

    /// <summary>
    /// One structured validation finding: its <see cref="Severity"/>, a stable kebab-case <see cref="RuleId"/>
    /// (for filtering/suppression), a <see cref="Locator"/> a GUI can navigate to (the element's <c>_0x</c> id
    /// token when it has one, else its tag), and the human-readable <see cref="Message"/>. The
    /// <see cref="Category"/> tells the check families apart; it defaults to
    /// <see cref="ValidationCategory.Structural"/> so every pre-R10 construction site behaves unchanged.
    /// </summary>
    public sealed record ProjectValidationFinding(
        ValidationSeverity Severity,
        string RuleId,
        string? Locator,
        string Message)
    {
        /// <summary>The check family this finding belongs to; <see cref="ValidationCategory.Structural"/> by default.</summary>
        public ValidationCategory Category { get; init; } = ValidationCategory.Structural;

        public override string ToString() =>
            $"[{Severity}] {RuleId}{(Locator is null ? string.Empty : " @" + Locator)}: {Message}";
    }
}
