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

    /// <summary>The result of a command legality check (proposal §3.4): allowed, or refused with a reason.</summary>
    public readonly record struct EditVerdict(bool Ok, string? Reason)
    {
        /// <summary>The command is allowed.</summary>
        public static EditVerdict Allow => new(true, null);

        /// <summary>The command is refused for the stated reason.</summary>
        public static EditVerdict Refuse(string reason) => new(false, reason);
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
    /// (for the undo/status text), an optional refusal/failure reason, and the change set when it committed.</summary>
    public record EditOutcome(EditStatus Status, string Label, string? Reason, ProjectChangeSet? Changes);

    /// <summary>An <see cref="EditOutcome"/> that also carries a produced value (e.g. a new element's id). Derives
    /// from <see cref="EditOutcome"/> so one GUI outcome→status/dialog mapping serves both shapes.</summary>
    public sealed record EditOutcome<T>(
        EditStatus Status, string Label, string? Reason, ProjectChangeSet? Changes, T? Value)
        : EditOutcome(Status, Label, Reason, Changes);

    /// <summary>The read-only context a command's legality check runs against: the pre-edit project and its
    /// <see cref="ProjectIndex"/>. Internal — only the session builds and passes it.</summary>
    internal readonly record struct EditContext(Project Project, ProjectIndex Index)
    {
        /// <summary>Allow when <paramref name="id"/> still resolves in the pre-edit index, else Refuse naming the
        /// <paramref name="noun"/> — the single "does the target still exist?" legality guard the command Evaluate
        /// checks route through, preserving each command's per-noun refusal message (review theme 2).</summary>
        public EditVerdict RequireExists(ElementId id, string noun) =>
            Index.FindById(id) is not null ? EditVerdict.Allow : EditVerdict.Refuse($"The {noun} no longer exists.");
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
