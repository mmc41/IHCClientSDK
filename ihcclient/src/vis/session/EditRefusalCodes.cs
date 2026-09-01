using Ihc.Vis.Problems;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The identity of every refusal the session layer can give — the <c>edit.*</c> family.
    /// <para>
    /// A refusal has always carried a Danish sentence and nothing a caller could act on programmatically: two
    /// paths answering the same question could only be compared by comparing prose, a host could not group or
    /// count refusals, and a sentence edit was indistinguishable from a behaviour change. A code fixes all three
    /// without touching a single word of what the user reads.
    /// </para>
    /// <para>
    /// The codes live HERE, beside the sentences in <see cref="EditRefusals"/>, rather than with the catalogue.
    /// The session layer may know about the problem contract and must not know about the validation engine, so
    /// this is the side of the boundary they can sit on — and the catalogue's entries are built FROM these
    /// members, so the identity is declared once and the two cannot drift.
    /// </para>
    /// <para>
    /// Session refusals are NOT forced onto catalogue rows. A precondition on an edit and a finding about a file
    /// are different questions: many refusals here have no row, and where one DOES encode the same constraint a
    /// row states, the entry records the cross-reference rather than duplicating the predicate.
    /// </para>
    /// </summary>
    public static class EditRefusalCodes
    {
        // ── the shared guards every command routes through ──────────────────────────────────────────

        /// <summary>The element the command targets is not in the project any more.</summary>
        public static ProblemCode TargetMissing { get; } = new("edit.target-missing");

        /// <summary>The target exists but is not the kind of thing this command edits.</summary>
        public static ProblemCode TargetWrongKind { get; } = new("edit.target-wrong-kind");

        /// <summary>The target lies at or inside a locked function block.</summary>
        public static ProblemCode TargetLocked { get; } = new("edit.target-locked");

        /// <summary>There is no open project to edit.</summary>
        public static ProblemCode NoProjectOpen { get; } = new("edit.no-project-open");

        /// <summary>The edit was prepared against an older version than the one it is being applied to.</summary>
        public static ProblemCode StaleBaseVersion { get; } = new("edit.stale-base-version");

        // ── links and scenes ────────────────────────────────────────────────────────────────────────

        /// <summary>The two terminals cannot be linked in that direction.</summary>
        public static ProblemCode LinkDirection { get; } = new("edit.link-direction");

        /// <summary>An endpoint of the scene is not in the project any more.</summary>
        public static ProblemCode SceneEndpointMissing { get; } = new("edit.scene-endpoint-missing");

        /// <summary>The scene container holds members of a different value kind.</summary>
        public static ProblemCode SceneMemberKind { get; } = new("edit.scene-member-kind");

        // ── variables and enumerator types ──────────────────────────────────────────────────────────

        /// <summary>The section named is not a variable section of a function block.</summary>
        public static ProblemCode SectionNotVariables { get; } = new("edit.section-not-variables");

        /// <summary>The section cannot hold an enumerated variable.</summary>
        public static ProblemCode SectionRejectsEnum { get; } = new("edit.section-rejects-enum");

        /// <summary>The variable was not added.</summary>
        public static ProblemCode VariableNotAdded { get; } = new("edit.variable-not-added");

        /// <summary>The project has no enumerator type of that name.</summary>
        public static ProblemCode EnumTypeMissing { get; } = new("edit.enum-type-missing");

        /// <summary>The enumerator type is built in and cannot be edited.</summary>
        public static ProblemCode EnumTypeReadOnly { get; } = new("edit.enum-type-readonly");

        /// <summary>The enumerator type is still used by resources and cannot be deleted.</summary>
        public static ProblemCode EnumTypeInUse { get; } = new("edit.enum-type-in-use");

        /// <summary>The enumerator type has no value at that position.</summary>
        public static ProblemCode EnumValueMissing { get; } = new("edit.enum-value-missing");

        // ── terminals ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The terminal is not in the project any more.</summary>
        public static ProblemCode TerminalMissing { get; } = new("edit.terminal-missing");

        /// <summary>
        /// The terminal number is outside its data line's range. Cross-reference: the catalogue's
        /// <c>dataline-address-range</c> row states the same constraint about a file that already carries the
        /// address; this refuses one being authored.
        /// </summary>
        public static ProblemCode TerminalAddressRange { get; } = new("edit.terminal-address-range");

        // ── product dialogs ─────────────────────────────────────────────────────────────────────────

        /// <summary>A submitted field points at an element that no longer exists.</summary>
        public static ProblemCode FieldTargetMissing { get; } = new("edit.field-target-missing");

        /// <summary>A submitted field points at an element outside the product.</summary>
        public static ProblemCode FieldOutsideProduct { get; } = new("edit.field-outside-product");

        /// <summary>The product's dialog has no such field.</summary>
        public static ProblemCode FieldNotOffered { get; } = new("edit.field-not-offered");

        /// <summary>The field cannot be edited.</summary>
        public static ProblemCode FieldReadOnly { get; } = new("edit.field-read-only");

        /// <summary>The submitted value does not satisfy the field's own rule.</summary>
        public static ProblemCode FieldValueRule { get; } = new("edit.field-value-rule");

        /// <summary>
        /// The submitted telephone number is not one the modem can dial.
        /// <para>Its own code rather than <see cref="FieldValueRule"/>: the generic entry's template names the
        /// FIELD and has no slot an offending value could bind to, so a specific guidance raised under it would
        /// be a sentence its own catalogue entry does not govern.</para>
        /// </summary>
        public static ProblemCode FieldPhonenumberMalformed { get; } = new("edit.field-phonenumber-malformed");

        /// <summary>
        /// The submitted value is outside the bounds the field's catalog element declares — BOTH of them.
        /// <para>
        /// THREE CODES, one per reachable bound shape (D05). A field constrained on one side only has no value
        /// for the other slot, so a single row declaring both could never be bound at a one-sided field, and the
        /// site authored a sentence of its own instead — leaving the catalogue describing words no user saw.
        /// Splitting by shape gives each row one template whose declared slots ALWAYS bind. Follows the
        /// capacity-input-modules / capacity-output-modules precedent.
        /// </para>
        /// </summary>
        public static ProblemCode FieldOutOfRange { get; } = new("edit.field-out-of-range");

        /// <summary>The submitted value is below the only bound the field declares.</summary>
        public static ProblemCode FieldBelowMinimum { get; } = new("edit.field-below-minimum");

        /// <summary>The submitted value is above the only bound the field declares.</summary>
        public static ProblemCode FieldAboveMaximum { get; } = new("edit.field-above-maximum");

        // ── programs ────────────────────────────────────────────────────────────────────────────────

        /// <summary>Not a valid case branch on a command group.</summary>
        public static ProblemCode CaseBranchInvalid { get; } = new("edit.case-branch-invalid");

        /// <summary>The target row is not a logging row.</summary>
        public static ProblemCode NotALogRow { get; } = new("edit.not-a-log-row");

        /// <summary>The target is not a command group.</summary>
        public static ProblemCode NotACommandGroup { get; } = new("edit.not-a-command-group");

        // ── structure ───────────────────────────────────────────────────────────────────────────────

        /// <summary>The move is not allowed.</summary>
        public static ProblemCode MoveNotAllowed { get; } = new("edit.move-not-allowed");

        /// <summary>The container cannot hold this node.</summary>
        public static ProblemCode ContainerRejectsNode { get; } = new("edit.container-rejects-node");

        // ── why a delete is refused: one code per rule (D5) ─────────────────────────────────────────
        //
        // These were one code, `edit.deletion-refused`, whose entry declared the fixed sentence "Dette element kan
        // ikke slettes." — which no user ever saw, because both raise sites forwarded the engine's own reason
        // instead. A catalogue whose entry states a sentence the product never shows is a catalogue that has
        // stopped being the truth about its own row, so the three reasons became three rows, each with the
        // sentence it actually renders. The old id is RETIRED and stays reserved.

        /// <summary>A product's catalog-declared pin: the catalog owns it, so it cannot be deleted on its own.</summary>
        public static ProblemCode DeletionRefusedCatalogPin { get; } = new("edit.deletion-refused-catalog-pin");

        /// <summary>A node inside a locked function block: the library owns it until the block is unlocked.</summary>
        public static ProblemCode DeletionRefusedLockedBlock { get; } = new("edit.deletion-refused-locked-block");

        /// <summary>A node that is project STRUCTURE rather than content — there is nothing here to remove.</summary>
        public static ProblemCode DeletionRefusedStructural { get; } = new("edit.deletion-refused-structural");

        // ── values a dialog submits ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// A field that must carry a value is blank. ONE code for the condition the app answered three different
        /// ways (T045): the decision is <c>RequiredFieldConstraint</c>'s and the sentence is this entry's, so a
        /// dialog, a prompt and a command cannot disagree about a name of three spaces.
        /// </summary>
        public static ProblemCode ValueRequired { get; } = new("edit.value-required");

        /// <summary>
        /// The case criterion is not a state of the switch's enumerator type. The GUI used to re-derive this
        /// sentence — and the state list behind it — from the same data the factory reads (T045).
        /// </summary>
        public static ProblemCode CaseValueNotAState { get; } = new("edit.case-value-not-a-state");

        // ── catalog resolution: the command cannot be MINTED at all ─────────────────────────────────

        /// <summary>The catalog carries no library function block with that master type.</summary>
        public static ProblemCode LibraryBlockMissing { get; } = new("edit.library-block-missing");

        /// <summary>
        /// The catalog carries no product with that identifier — or carries it more than once and the display name
        /// does not say which (D22).
        /// </summary>
        public static ProblemCode CatalogProductMissing { get; } = new("edit.catalog-product-missing");

        /// <summary>
        /// The project may hold at most one modem and already holds one (US-013). Cross-reference: the catalogue's
        /// <c>capacity-modem-multiple</c> row states the same constraint about a file that already carries two;
        /// this refuses the second one being authored.
        /// </summary>
        public static ProblemCode ModemLimit { get; } = new("edit.modem-limit");

        // ── the two outcomes that are not preconditions ─────────────────────────────────────────────

        /// <summary>A deep guard inside execution refused, after the verdict allowed.</summary>
        public static ProblemCode DeepGuard { get; } = new("edit.deep-guard");

        /// <summary>Every code in this family, for the governance check that each one has a catalogue entry.</summary>
        public static Ihc.Vis.Model.EquatableArray<ProblemCode> All { get; } =
            System.Collections.Immutable.ImmutableArray.Create(
                TargetMissing, TargetWrongKind, TargetLocked, NoProjectOpen, StaleBaseVersion,
                LinkDirection, SceneEndpointMissing, SceneMemberKind,
                SectionNotVariables, SectionRejectsEnum, VariableNotAdded,
                EnumTypeMissing, EnumTypeReadOnly, EnumTypeInUse, EnumValueMissing,
                TerminalMissing, TerminalAddressRange,
                FieldTargetMissing, FieldOutsideProduct, FieldNotOffered, FieldReadOnly, FieldValueRule,
                FieldPhonenumberMalformed, FieldOutOfRange, FieldBelowMinimum, FieldAboveMaximum,
                CaseBranchInvalid, NotALogRow, NotACommandGroup,
                MoveNotAllowed, ContainerRejectsNode,
                DeletionRefusedCatalogPin, DeletionRefusedLockedBlock, DeletionRefusedStructural,
                ValueRequired, CaseValueNotAState,
                LibraryBlockMissing, CatalogProductMissing, ModemLimit,
                DeepGuard);
    }
}
