#nullable enable
using System;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>The terminal state of an <see cref="EditOutcome"/> (proposal §3.3): a command either committed a
    /// change, made no change, was refused by a legality check, or failed with an engine/other error.</summary>
    public enum EditStatus
    {
        /// <summary>The command changed the project and the change was pushed onto the undo history.</summary>
        Committed,

        /// <summary>The command produced an identical project — nothing committed, history untouched.</summary>
        NoChange,

        /// <summary>A legality check (or a stale base-version guard) refused the command; nothing changed.</summary>
        Refused,

        /// <summary>The command threw an engine/other error; nothing changed, the message is preserved.</summary>
        Failed,
    }

    /// <summary>
    /// The result of a command legality check: allowed, or refused with a reason and an identity.
    /// <para>
    /// The <see cref="Code"/> is what a caller can act on. The sentence has always been the whole of a refusal,
    /// which meant two paths answering the same question could only be compared by comparing prose, a host could
    /// not group or count refusals, and a wording edit was indistinguishable from a behaviour change. The code
    /// fixes all three without changing a word of what the user reads.
    /// </para>
    /// </summary>
    /// <param name="Ok">Whether the command may be applied.</param>
    /// <param name="Reason">The Danish sentence a frontend shows, or null when allowed.</param>
    /// <param name="Code">The refusal's identity, or the default when allowed.</param>
    public readonly record struct EditVerdict(bool Ok, string? Reason, Problems.ProblemCode Code = default)
    {
        /// <summary>The command is allowed.</summary>
        public static EditVerdict Allow => new(true, null);

        /// <summary>The command is refused, with an identity and the sentence the user reads.</summary>
        /// <param name="code">Which refusal this is.</param>
        /// <param name="reason">The Danish sentence.</param>
        public static EditVerdict Refuse(Problems.ProblemCode code, string reason) => new(false, reason, code);

        /// <summary>
        /// Refuses with a sentence and NO identity — for a host that has not adopted a code family yet.
        /// <para>
        /// It exists because a host builds verdicts of its own for its own availability gates, and a host's codes
        /// are a host's business: requiring one here would close the vocabulary from the SDK side, which is
        /// precisely what the reserved-family design refuses to do. What it is NOT for is an SDK refusal — every
        /// one of those names its code, and a source scan pins that they do, so this overload cannot quietly
        /// become the easy path back to anonymous refusals inside the engine.
        /// </para>
        /// </summary>
        /// <param name="reason">The Danish sentence.</param>
        public static EditVerdict Refuse(string reason) => new(false, reason);

        /// <summary>Chains a second legality check after this one, keeping the FIRST failure (short-circuits on a
        /// refusal) — lets a command Evaluate compose "target exists / has the right tag" with "target is not locked"
        /// (T003) in one expression.</summary>
        public EditVerdict And(EditVerdict next) => Ok ? next : this;
    }

    /// <summary>
    /// The refusal sentences a caller may forward rather than re-author. They sit on the session CONTRACT, next to
    /// the <see cref="EditVerdict"/> that carries them, because a frontend answering the same condition needs the
    /// same sentence without reaching the concrete command runner that happens to raise it first.
    /// <para>The <c>Refusal</c> suffix on each name is load-bearing, not decoration: the refusal-language test scans
    /// SDK source for named constants matching it, so a rename that drops the suffix would hide the sentence from
    /// the check that pins it as Danish.</para>
    /// </summary>
    public static class EditRefusals
    {
        /// <summary>
        /// The ONE "nothing is open" refusal. Public so the app layer forwards this exact sentence instead of
        /// authoring its own: a frontend answers the same question when it holds no document at all, where there
        /// is no session to ask, and two separately-worded sentences for one condition is duplication.
        /// </summary>
        public const string NoProjectOpenRefusal = "Der er ikke åbnet et projekt.";

        /// <summary>
        /// The optimistic-concurrency refusal: the edit was prepared against an older version than the one it is
        /// being applied to. Named rather than inlined for the same reason as the sentence above — it is one
        /// condition and must read as one sentence wherever it is answered.
        /// </summary>
        public const string StaleBaseVersionRefusal = "Projektet er ændret, siden denne redigering blev forberedt.";
    }

    /// <summary>The undo-history retention policy (proposal §3.3/D1): a <see cref="Cap"/> of null means unbounded
    /// (memory only), any int is a hard entry cap.</summary>
    public readonly record struct HistoryPolicy(int? Cap)
    {
        /// <summary>A policy that keeps at most <paramref name="cap"/> history entries.</summary>
        public static HistoryPolicy Bounded(int cap) => new(cap);

        /// <summary>A policy with no configured cap — undo depth is bounded only by process memory.</summary>
        public static HistoryPolicy Unlimited => new((int?)null);
    }

    /// <summary>The result of applying a command (proposal §3.3): its terminal <see cref="Status"/>, a human label
    /// (for the undo/status text), an optional refusal/failure reason, the change set when it committed, and the
    /// refusal's IDENTITY when it refused.
    /// <para>
    /// <see cref="Code"/> is what makes agreement between the gate and this door checkable: a refused apply carries
    /// the same code <see cref="ProjectDocumentSession.CanApply"/> returned, so a caller can compare, group and
    /// count refusals instead of comparing two Danish sentences that happen to match today. It is the default for
    /// every non-refusing outcome — a committed edit has no refusal to identify.
    /// </para>
    /// <para>
    /// LAST and defaulted deliberately: every existing construction site keeps compiling, so the sites that gained
    /// a code are exactly the refusing ones and a reviewer can see that in the diff.
    /// </para>
    /// </summary>
    public record EditOutcome(
        EditStatus Status, string Label, string? Reason, ProjectChangeSet? Changes,
        Problems.ProblemCode Code = default);

    /// <summary>An <see cref="EditOutcome"/> that also carries a produced value (e.g. a new element's id). Derives
    /// from <see cref="EditOutcome"/> so one GUI outcome→status/dialog mapping serves both shapes.</summary>
    public sealed record EditOutcome<T>(
        EditStatus Status, string Label, string? Reason, ProjectChangeSet? Changes, T? Value,
        Problems.ProblemCode Code = default)
        : EditOutcome(Status, Label, Reason, Changes, Code);

    /// <summary>The terminal state of a <see cref="PreviewOutcome"/> — the non-committing mirror of
    /// <see cref="EditStatus"/> (M8/D05): previewing a command against the current project shows it either WOULD
    /// change it, would make no change, was refused by a legality check, or FAULTED with an unexpected engine
    /// error.</summary>
    public enum PreviewStatus
    {
        /// <summary>The command would change the project; <see cref="PreviewOutcome.Changes"/> carries the delta.</summary>
        WouldChange,

        /// <summary>The command would produce an identical project — nothing to commit.</summary>
        NoChange,

        /// <summary>A legality check (or a deep engine guard) refused the command; the reason is preserved.</summary>
        Refused,

        /// <summary>The command's Execute threw an unexpected engine error; the message is preserved.</summary>
        Faulted,
    }

    /// <summary>The result of previewing a command without committing (M8/D05) — the mirror of
    /// <see cref="EditOutcome"/>: its terminal <see cref="Status"/>, the <see cref="Changes"/> it would commit (only
    /// when <see cref="PreviewStatus.WouldChange"/>), and a <see cref="Reason"/> for a refuse/fault. Distinguishing a
    /// legitimate refuse/no-change from an unexpected engine fault lets a caller surface a genuine bug instead of
    /// swallowing it as "nothing to preview" (the bare-catch it replaces conflated all three as null).</summary>
    public sealed record PreviewOutcome(
        PreviewStatus Status, ProjectChangeSet? Changes, string? Reason, Problems.ProblemCode Code = default)
    {
        /// <summary>The command would commit <paramref name="changes"/>.</summary>
        public static PreviewOutcome WouldChange(ProjectChangeSet changes) => new(PreviewStatus.WouldChange, changes, null);

        /// <summary>The command would make no change.</summary>
        public static PreviewOutcome NoChange { get; } = new(PreviewStatus.NoChange, null, null);

        /// <summary>The command was refused for the stated reason, with an identity when one is known.</summary>
        /// <param name="reason">The Danish sentence.</param>
        /// <param name="code">The refusal's identity, or the default when the refusing path has none.</param>
        public static PreviewOutcome Refused(string? reason, Problems.ProblemCode code = default) =>
            new(PreviewStatus.Refused, null, reason, code);

        /// <summary>The preview was refused, carrying the refusal identity through unchanged.</summary>
        /// <param name="verdict">The verdict that refused it.</param>
        public static PreviewOutcome Refused(EditVerdict verdict) => Refused(verdict.Reason, verdict.Code);

        /// <summary>The command faulted with an unexpected engine error.</summary>
        public static PreviewOutcome Faulted(string? reason) => new(PreviewStatus.Faulted, null, reason);
    }

    /// <summary>The read-only context a command's legality check runs against: the pre-edit project and its
    /// <see cref="ProjectIndex"/>. Internal — only the session builds and passes it.</summary>
    internal readonly record struct EditContext(Project Project, ProjectIndex Index)
    {
        /// <summary>Allow when <paramref name="id"/> still resolves in the pre-edit index, else Refuse naming the
        /// <paramref name="noun"/> — the single "does the target still exist?" legality guard the command Evaluate
        /// checks route through, preserving each command's per-noun refusal message (review theme 2).
        /// <para><paramref name="noun"/> is DANISH and in its definite form, because it is spliced into a Danish
        /// sentence the GUI forwards to the installer verbatim (FR-2.6 / D13). An English noun here breaks nothing
        /// mechanically — it just puts half-Danish text in front of a user — so the nouns are named at the call
        /// sites and the whole channel is asserted by <c>RefusalLanguageTests</c>.</para></summary>
        public EditVerdict RequireExists(ElementId id, string noun) =>
            Index.FindById(id) is not null
                ? EditVerdict.Allow
                : EditVerdict.Refuse(EditRefusalCodes.TargetMissing, $"{noun} findes ikke længere.");

        /// <summary>Allow when <paramref name="id"/> resolves to an element whose tag is one of
        /// <paramref name="tags"/>, else Refuse naming the expected <paramref name="noun"/> — the tag-aware peer of
        /// <see cref="RequireExists"/> (review theme 2 / M5): the single "does the target exist AND carry the right
        /// tag?" legality guard the command Evaluate checks route through, replacing the hand-inlined
        /// <c>?.Tag == "…" ? Allow : Refuse</c> copies.</summary>
        public EditVerdict RequireTag(ElementId id, string noun, params string[] tags) =>
            Index.FindById(id) is { } element && System.Array.IndexOf(tags, element.Tag) >= 0
                ? EditVerdict.Allow
                : EditVerdict.Refuse(EditRefusalCodes.TargetWrongKind, $"Målet er ikke {noun}.");

        /// <summary>Allow unless <paramref name="id"/> lies at/within a locked function block's subtree, in which case
        /// Refuse — the session half of the central locked-ancestor authorization (T003): a structural
        /// insert/reorder/move/copy targeting a locked block is refused cleanly (a verdict, so <c>CanApply</c>/Preview
        /// agree with Apply) exactly where a direct engine call throws. <paramref name="inclusive"/> per
        /// <see cref="Ihc.Vis.Editing.ProjectEditor.IsWithinLockedBlock"/> (an insert/move/copy TARGET counts itself; a
        /// reorder does not, so the block may still be reordered among its siblings).</summary>
        public EditVerdict RequireUnlockedTarget(ElementId id, bool inclusive) =>
            Index.IsWithinLockedBlock(id, inclusive)
                ? EditVerdict.Refuse(EditRefusalCodes.TargetLocked, Ihc.Vis.Editing.ProjectEditor.LockedBlockEditRefusal)
                : EditVerdict.Allow;

        /// <summary>The composition the authoring commands actually want: <see cref="RequireTag"/> on
        /// <paramref name="id"/> followed by <see cref="RequireUnlockedTarget"/> on the SAME id, inclusive — "this
        /// target exists, carries the right tag, and is not inside a locked block". Spelled out once here rather than
        /// re-paired in every <c>Evaluate</c>, so a change to what an authoring command must check is one edit and the
        /// two halves can never be wired to different ids. Commands whose first half is not a tag test (a container
        /// predicate, an eligibility rule) keep composing the two guards themselves.</summary>
        public EditVerdict RequireUnlockedTag(ElementId id, string noun, params string[] tags) =>
            RequireTag(id, noun, tags).And(RequireUnlockedTarget(id, inclusive: true));
    }

    /// <summary>Thrown by a deep engine guard that can only refuse a command once inside its Execute (proposal
    /// §3.4). The session maps it to <see cref="EditStatus.Refused"/>; every other exception is a failure.</summary>
    public sealed class EditRefusedException : Exception
    {
        /// <summary>Creates the exception with the refusal reason.</summary>
        public EditRefusedException(string message) : base(message) { }
    }

    /// <summary>Carries the <see cref="ProjectChangeSet"/> for a document-session change notification.</summary>
    public sealed class ProjectChangedEventArgs(ProjectChangeSet changes) : EventArgs
    {
        /// <summary>The structural delta the change produced.</summary>
        public ProjectChangeSet Changes { get; } = changes;
    }
}
