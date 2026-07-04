#nullable enable
namespace Ihc.Projects
{
    /// <summary>How severe a validation finding is for persisting the project.</summary>
    public enum ValidationSeverity
    {
        /// <summary>A state the serializer, IHC Visual or the controller is known (or spec-required) to reject.</summary>
        Error,

        /// <summary>A deviation vendor tooling tolerates — advisory for a GUI, never blocks a save/upload.</summary>
        Warning,
    }

    /// <summary>
    /// One structured validation finding: its <see cref="Severity"/>, a stable kebab-case <see cref="RuleId"/>
    /// (for filtering/suppression), a <see cref="Locator"/> a GUI can navigate to (the element's <c>_0x</c> id
    /// token when it has one, else its tag), and the human-readable <see cref="Message"/>.
    /// </summary>
    public sealed record ProjectValidationFinding(
        ValidationSeverity Severity,
        string RuleId,
        string? Locator,
        string Message)
    {
        public override string ToString() =>
            $"[{Severity}] {RuleId}{(Locator is null ? string.Empty : " @" + Locator)}: {Message}";
    }
}
