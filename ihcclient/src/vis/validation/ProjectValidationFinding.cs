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

    /// <summary>
    /// Which check family a finding belongs to — the catalogue's eight categories, classifying WHAT PART of the
    /// project a condition is about.
    /// <para>
    /// The three-letter codes the catalogue prints (<c>INT</c>, <c>WIR</c>, …) are DATA on the side, reachable
    /// through <see cref="CategoryExtensions"/>; they are never member names, so a reader of the code meets a
    /// word rather than an abbreviation.
    /// </para>
    /// <para>
    /// CLASSIFICATION ONLY. There is deliberately no per-category configuration surface — no threshold, no
    /// profile expressed per category. Taxonomies exist for aggregation, building a lever nothing asks for is
    /// the cost this project declines, and per-RULE severity override already supplies the strictness lever.
    /// </para>
    /// </summary>
    public enum ValidationCategory
    {
        /// <summary>INT — container, encoding, XML/DTD, ids, IDREFs, schema conformance, root invariants, and the open/save/import/export operations.</summary>
        FileIntegrity,

        /// <summary>WIR — follow-links between products and function blocks.</summary>
        Wiring,

        /// <summary>LOG — function-block shape, programs, variables, flags, timers, enums.</summary>
        Logic,

        /// <summary>SCN — scene resources and their member rows.</summary>
        Scenes,

        /// <summary>ADR — data-line addresses, wireless binding, dimmer channels, meters, modem.</summary>
        Addressing,

        /// <summary>DEV — dimmer, shutter, backup, initial-value and accessibility settings.</summary>
        DeviceSettings,

        /// <summary>DOC — names, identification codes, cable data, placement, report completeness.</summary>
        Documentation,

        /// <summary>PRJ — localities, orphan blocks, housekeeping, controller fit.</summary>
        ProjectStructure,
    }

    /// <summary>
    /// One structured validation finding: its <see cref="Severity"/>, a stable kebab-case <see cref="RuleId"/>
    /// (for filtering and grouping — NOT for suppression, which is foreclosed: there is no rule-level disable
    /// and no per-element accepted-store, because a silenced finding is invisible to the next reader and nothing
    /// here can record who accepted what), a <see cref="Locator"/> a GUI can navigate to (the element's <c>_0x</c> id
    /// token when it has one, else its tag), and the human-readable <see cref="Message"/>. The
    /// <see cref="Category"/> tells the check families apart, and it defaults to
    /// <see cref="ValidationCategory.FileIntegrity"/> — the category of the definition-file checks that are the
    /// remaining construction sites for this type.
    /// </summary>
    public sealed record ProjectValidationFinding(
        ValidationSeverity Severity,
        string RuleId,
        string? Locator,
        string Message)
    {
        /// <summary>The check family this finding belongs to; <see cref="ValidationCategory.FileIntegrity"/> by default.</summary>
        public ValidationCategory Category { get; init; } = ValidationCategory.FileIntegrity;

        public override string ToString() =>
            $"[{Severity}] {RuleId}{(Locator is null ? string.Empty : " @" + Locator)}: {Message}";
    }
}
