#nullable enable
namespace Ihc.Vis.Validation
{
    /// <summary>
    /// What KIND of thing a catalogue row is. The kind decides which executors can consume it, so it is declared
    /// once on the entry rather than restated at each rule site.
    /// </summary>
    public enum RuleKind
    {
        /// <summary>
        /// About the INSTALLATION the project describes: wiring, scenes, addressing, naming, capacity. The large
        /// majority.
        /// </summary>
        UserContentRule,

        /// <summary>
        /// A PRECONDITION on an edit: may this command be applied to this target now. Feeds NO engine face —
        /// there is no project-wide question here, and the question it does answer belongs to the session: an
        /// entry of this kind is realised as a coded refusal at the command that raises it
        /// (<c>ProjectDocumentSession.CanApply</c> / <c>Apply</c>), never by an executor. That is why such an
        /// entry declares <see cref="RuleFaces.None"/> and why a registered RULE declaring it is rejected.
        /// </summary>
        EditPrecondition,

        /// <summary>
        /// A guard on schema conformance: undeclared attribute, missing <c>#REQUIRED</c>, non-Latin-1 text. Feeds
        /// the whole-project executor, and is also what a save refuses on.
        /// </summary>
        SchemaSerializationGuard,

        /// <summary>
        /// The DISPOSITION of a whole operation — open, save, import, download, upload. Produces a coded refusal
        /// and never a finding, which is exactly <see cref="CatalogDisposition.Refusal"/> and nothing else.
        /// </summary>
        OperationOutcome,

        /// <summary>
        /// A fault in the TOOL: a rule that threw, an edit that broke, an operation that faulted. It is none of
        /// the kinds above — it is not a rule, and it is not the outcome of an operation the user asked for.
        /// <para>
        /// The kind and the shape are a BICONDITIONAL, checked in both directions: an entry of this kind declares
        /// no category, no faces, no refused operations and
        /// <see cref="CatalogDisposition.NotApplicable"/>; and an entry declaring exactly that must be of this
        /// kind. The forward direction stops a fault row from making claims about a project it could not examine;
        /// the reverse stops the catalogue growing a second, unlabelled shape that means the same thing.
        /// </para>
        /// </summary>
        InternalFault,
    }

    /// <summary>
    /// Which faces consume a rule. Declared on the entry and checked at registration, so "one definition, N faces"
    /// is a property the catalogue can state rather than a claim in a document.
    /// <para>
    /// There is no serialization member. A save-path guard raises a REFUSAL, not a finding, so no registered rule
    /// ever produces on such a face.
    /// </para>
    /// </summary>
    [System.Flags]
    public enum RuleFaces
    {
        /// <summary>
        /// Consumed by no face. Correct for an <see cref="RuleKind.OperationOutcome"/> entry, which is realised as
        /// a coded refusal at a throw site rather than by an executor; a registered RULE declaring it is rejected.
        /// </summary>
        None = 0,

        /// <summary>The collect-all whole-project executor.</summary>
        WholeProject = 1,

        /// <summary>The dialog-metadata read.</summary>
        DialogMetadata = 2,
    }

    /// <summary>
    /// What a rule is ABOUT, named directly as a (tag, attribute) pair — the freedom a hand-built engine was
    /// chosen for. There is no library object model to adapt to, so a rule says <c>("product", "address")</c> and
    /// means it, rather than expressing the same thing as a property selector on a generated type this SDK does
    /// not have: its rule set comes from DTD metadata at runtime.
    /// </summary>
    /// <param name="Tag">
    /// The <c>.vis</c> element tag. Null means one of two things, decided by <paramref name="Attribute"/>:
    /// with an attribute it is the WILDCARD — "this attribute, on whatever element the rule reports" — and
    /// without one it is the project as a whole (<see cref="IsWholeProject"/>).
    /// </param>
    /// <param name="Attribute">The attribute on that tag, or null for a rule about the element itself.</param>
    public readonly record struct RuleTarget(string? Tag, string? Attribute)
    {
        /// <summary>Whether this target names an attribute, as opposed to an element or the whole project.</summary>
        public bool IsAttributeTarget => Attribute is not null;

        /// <summary>Whether this target names nothing — the rule is about the project as a whole.</summary>
        public bool IsWholeProject => Tag is null && Attribute is null;
    }

    /// <summary>
    /// How many findings ONE violation produces.
    /// <para>
    /// The test is what the user must DO to clear the finding. One repair clears everything →
    /// <see cref="OneFinding"/>. Each occurrence needs its own → <see cref="OnePerOccurrence"/>. One repair, but
    /// the user must SEE every site to make it → <see cref="PrimaryWithRelated"/>.
    /// </para>
    /// </summary>
    public enum FindingShape
    {
        /// <summary>One finding for the whole project, however many times the condition holds.</summary>
        OneFinding,

        /// <summary>One finding per occurrence, each independently repairable. The default for content rows.</summary>
        OnePerOccurrence,

        /// <summary>
        /// One finding with a primary location plus RELATED locations. The duplicate-id group is the motivating
        /// case, and it is a live improvement: today the engine reports N separate findings for one collision,
        /// telling the user N times that two things collide.
        /// </summary>
        PrimaryWithRelated,
    }
}
