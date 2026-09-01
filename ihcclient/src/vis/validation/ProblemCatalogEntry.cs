using System;

using Ihc.Vis.Model;
using Ihc.Vis.Problems;

namespace Ihc.Vis.Validation
{
    /// <summary>
    /// ONE entry of the catalogue — the single source of a code's identity, classification, Danish text,
    /// thresholds and rule wiring. A compiled DECLARATION, not a parsed row: the code is the truth.
    /// <para>
    /// This type carries what a separate rule descriptor would duplicate. Five facts — <see cref="Kind"/>,
    /// <see cref="Category"/>, <see cref="Disposition"/>, <see cref="Shape"/> and
    /// <see cref="RequiresControllerLimits"/> — would otherwise be declared in two places with nothing checking
    /// the copies for agreement. Folding them onto the entry makes "a fact appears once" true BY CONSTRUCTION,
    /// so the check is unnecessary rather than merely unwritten.
    /// </para>
    /// <para>
    /// The predicate a row is implemented from — the condition, the subject it walks, the exclusions — is
    /// authored as the DOC-COMMENT on each declaration, beside the row and in the same diff. Only
    /// <see cref="Thresholds"/> stays data, because only thresholds are read by code.
    /// </para>
    /// </summary>
    /// <param name="Code">The row's identity. Bare kebab-case for a catalogue row; dotted for an operation family.</param>
    /// <param name="Section">Which of the three sections this entry is in.</param>
    /// <param name="Category">
    /// The eight-category classification, or NULL for an <see cref="ProblemCatalogSection.OperationOutcomes"/>
    /// entry, where the axis does not apply. Classification only: per-rule severity override is the strictness
    /// lever, so there is no per-category configuration surface. Nullability is confined to the entry —
    /// a finding's category stays non-null, because a refusal produces no finding.
    /// </param>
    /// <param name="Disposition">
    /// What this row costs: a finding at one of the declared severities, a refusal, or neither.
    /// </param>
    /// <param name="Kind">Which rule kind realises this row.</param>
    /// <param name="Faces">
    /// Which faces consume it. <see cref="RuleFaces.None"/> is correct for an operation outcome, which is realised
    /// at a throw site rather than by an executor, and is rejected for a registered rule.
    /// </param>
    /// <param name="Target">What it is about, as a (tag, attribute) pair.</param>
    /// <param name="Shape">How many findings one violation produces.</param>
    /// <param name="Slots">
    /// The declared argument slots, in template order. This code's generated factory takes them as real
    /// parameters, so a wrong argument count or type is a COMPILE error at the call site — which is the whole
    /// arity-and-type gate, at no cost.
    /// </param>
    /// <param name="MessageTemplate">
    /// The user-facing DANISH template — a short fixed label, with <c>{slot}</c> placeholders where a datum
    /// belongs. Empty only for a row whose own build-out task authors it; never invented in bulk.
    /// </param>
    /// <param name="Status">Active, retired, or ruled out.</param>
    public sealed record ProblemCatalogEntry(
        ProblemCode Code,
        ProblemCatalogSection Section,
        ValidationCategory? Category,
        CatalogDisposition Disposition,
        RuleKind Kind,
        RuleFaces Faces,
        RuleTarget Target,
        FindingShape Shape,
        EquatableArray<ProblemArgumentSlot> Slots,
        string MessageTemplate,
        ProblemCodeStatus Status = ProblemCodeStatus.Active)
    {
        /// <summary>
        /// The ENGLISH engine diagnostic beside the Danish label — the sentence a migrated rule emits today, or
        /// the pre-existing English text for a catalog-definition row whose hand-author needs it.
        /// </summary>
        public string? Diagnostic { get; init; }

        /// <summary>The readiness mark — metadata, never a gate.</summary>
        public EvidenceMark Evidence { get; init; }

        /// <summary>Every number this row's predicate needs, declared rather than written inline.</summary>
        public EquatableArray<DeclaredThreshold> Thresholds { get; init; }

        /// <summary>
        /// The operations this row REFUSES, by their <see cref="OperationCodes"/> heads — empty for the rows that
        /// refuse nothing, which is most of them.
        /// <para>
        /// This is a fact about the row that is independent of <see cref="Disposition"/>, and the two must not be
        /// conflated. A <see cref="CatalogDisposition.Refusal"/> row refuses and reports nothing; a
        /// <see cref="CatalogDisposition.Error"/> row that ALSO refuses — an undeclared attribute stops a save and
        /// is a finding at validate — is what lets a host tell a fatal error from an ordinary one without asking
        /// the catalogue at all: <see cref="ValidationFinding.RefusedOperations"/> carries it there.
        /// </para>
        /// <para>
        /// The vocabulary is the HEAD SET, and the catalogue's §4 is now RENDERED from it — one published word per
        /// head, none shared, the whole six expressible. It was not always: the column was hand-typed with four
        /// file-lifecycle labels, could not say <c>edit.open</c> at all, and published "Import" for a controller
        /// download. Those gaps are what made this a declared field rather than a reading of the prose, and
        /// specifying it over that column would have rebuilt every one of them.
        /// </para>
        /// </summary>
        public EquatableArray<ProblemCode> RefusedOperations { get; init; }

        /// <summary>
        /// Whether this row needs a target controller's capability limits, which the project file cannot supply.
        /// A rule that needs them is absent from the default project-only profile — it does not run and does not
        /// report, rather than running against a guess.
        /// <para>
        /// A bool rather than a context-kind vocabulary: exactly one such context exists and three rows use it,
        /// so an enum with one member, an availability set and an interface to read it would be six mechanisms
        /// for one question.
        /// </para>
        /// </summary>
        public bool RequiresControllerLimits { get; init; }

        /// <summary>
        /// Whether the row can only be decided against the LIBRARY a placed block claims — the second declared
        /// context, ruled in by D27. A rule that needs it is absent from a profile that carries no
        /// <see cref="ILibraryBlockSource"/>: it does not run and does not report, rather than guessing what a
        /// library default was.
        /// <para>
        /// The bool-per-context shape is kept deliberately (see the note above): two independent contexts are two
        /// bools, and neither the entry nor the profile has to know about a vocabulary of context kinds. The moment
        /// a third arrives, that judgement is worth revisiting; two is not a vocabulary.
        /// </para>
        /// </summary>
        public bool RequiresLibrary { get; init; }

        /// <summary>
        /// The controller firmware release that fixed this row's defect, for a row whose condition is a firmware or
        /// shipped-block errata — null for every other row, which is nearly all of them.
        /// <para>
        /// <b>This is a NARROWING context, and that is why it is not a third bool.</b> The two flags above are
        /// ENABLING: absent context means the rule does not run and does not report, because counting against a
        /// limit nobody supplied would be a guess. Here the condition — this project uses the affected feature — is
        /// a property of the file and is decided without any context at all. A declared target can only WITHHOLD a
        /// finding it has already left behind, never create one. So the row reports by default, which is the whole
        /// point: a guardrail that stayed silent until a controller was connected would be silent exactly while the
        /// project is being designed.
        /// </para>
        /// <para>
        /// It follows that this must NOT reach <see cref="ValidationProfile.CanEvaluate"/>. A row narrowed away by
        /// a target WAS evaluable; publishing it in the export's could-not-evaluate list would tell the reader the
        /// caller withheld context this row never needed.
        /// </para>
        /// </summary>
        public DeclaredFirmwareBound? FirmwareBound { get; init; }

        /// <summary>
        /// The finding severity this row reports as, or null when it refuses instead of reporting. Derived from
        /// <see cref="Disposition"/>, so the two axes cannot disagree.
        /// </summary>
        public ValidationSeverity? Severity => Disposition switch
        {
            CatalogDisposition.Error => ValidationSeverity.Error,
            CatalogDisposition.Warning => ValidationSeverity.Warning,
            CatalogDisposition.Info => ValidationSeverity.Info,
            _ => null,
        };

        /// <summary>
        /// Binds a problem's declared arguments into this row's Danish template — the ONLY text assembly the
        /// design permits, and it assembles a template with DATA, never one sentence with another.
        /// <para>
        /// It lives here, on the entry that owns the template, rather than in a rendering service: the three
        /// composition cases need no code at all, being property reads on two records, so a renderer that
        /// re-implemented them would be a second presentation path beside the shell's.
        /// </para>
        /// <para>
        /// A slot the problem does not supply is left as its own <c>{name}</c> placeholder rather than blanked:
        /// a visible gap is a defect a reader reports, where a silent blank reads as intended text.
        /// </para>
        /// </summary>
        /// <param name="problem">The problem whose arguments fill this row's slots.</param>
        public string BindTemplate(Problem problem)
        {
            ArgumentNullException.ThrowIfNull(problem);
            return Bind(MessageTemplate, problem);
        }

        /// <summary>
        /// Binds the same arguments into this row's ENGLISH <see cref="Diagnostic"/>, which declares the same
        /// slots as the Danish template and had been left unbound — so the one text written for the person
        /// debugging was the one text that reached the log with <c>{attribute}</c> still spelled out.
        /// <para>
        /// Null in, null out: a row that declares no diagnostic gains nothing to bind.
        /// </para>
        /// </summary>
        /// <param name="problem">The problem whose arguments fill this row's slots.</param>
        public string? BindDiagnostic(Problem problem)
        {
            ArgumentNullException.ThrowIfNull(problem);
            return Diagnostic is { } diagnostic ? Bind(diagnostic, problem) : null;
        }

        /// <summary>
        /// The one substitution, shared so this row's two texts cannot bind by different rules — and shared with
        /// the refusing sites below the engine, which bind the same sentence through
        /// <see cref="RefusalIdentity.Binding"/> because they may not read this catalogue.
        /// </summary>
        private static string Bind(string template, Problem problem) =>
            ProblemTemplate.Bind(template, problem.Arguments);
    }
}
