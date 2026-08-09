#nullable enable
using System;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Session;

namespace Ihc.Vis
{
    /// <summary>
    /// The interactive document port (crudarch D01, proposal §3.1): ONE open project with command execution,
    /// labelled undo/redo history, dirty/version tracking and change events — obtained from
    /// <see cref="ProjectAppService.OpenDocument"/> and implemented directly by the session layer (no wrapper).
    /// This is the door for INTERACTIVE frontends: a GUI holds one document per open file and drives every
    /// edit through it. The stateless <see cref="ProjectAppService.Apply(Project, Session.ProjectCommand)"/> /
    /// <see cref="ProjectAppService.CanApply"/> / <see cref="ProjectAppService.Preview"/> facade remains the
    /// door for ONE-SHOT callers (console tools, tests, non-interactive automation) that hold no document
    /// lifecycle.
    /// </summary>
    /// <remarks>
    /// <b>Threading contract (crudarch D04):</b> members are lock-serialized, so any thread may READ —
    /// <see cref="Current"/> and the state properties, e.g. a background backup capturing the immutable
    /// snapshot — while another thread edits. <see cref="Changed"/>/<see cref="StateChanged"/> are raised
    /// synchronously on the thread that performed the state change, never marshalled or deferred; an
    /// interactive caller must therefore issue ALL mutations (<see cref="Apply(Session.ProjectCommand, int?)"/>,
    /// <see cref="Undo"/>/<see cref="Redo"/>, <see cref="Open"/>/<see cref="MarkSaved"/>/<see cref="Close"/>)
    /// from one thread — the UI thread in a GUI — for handler ordering to stay meaningful.
    /// </remarks>
    public interface IProjectDocument
    {
        /// <summary>The current project snapshot, or null when none is open.</summary>
        Project? Current { get; }

        /// <summary>Bumps on apply/undo/redo/open/close (never on <see cref="MarkSaved"/>); base-version
        /// guards compare against it to refuse a stale dialog commit.</summary>
        int Version { get; }

        /// <summary>Whether the current snapshot differs from the last written save point.</summary>
        bool IsDirty { get; }

        /// <summary>Whether there is an edit to undo.</summary>
        bool CanUndo { get; }

        /// <summary>Whether there is an undone edit to redo.</summary>
        bool CanRedo { get; }

        /// <summary>The label of the edit that <see cref="Undo"/> would reverse, or null.</summary>
        string? UndoLabel { get; }

        /// <summary>The label of the edit that <see cref="Redo"/> would re-apply, or null.</summary>
        string? RedoLabel { get; }

        /// <summary>Applies a command through the full pipeline (evaluate → execute → commit + history).</summary>
        EditOutcome Apply(ProjectCommand command, int? baseVersion = null);

        /// <summary>Applies a value-producing command, surfacing the produced value on a committed outcome.</summary>
        EditOutcome<T> Apply<T>(ProjectCommand<T> command, int? baseVersion = null);

        /// <summary>The command's legality verdict against the current snapshot (cheap — reuses the
        /// per-commit index, no edit), for drag-over probes and menu gates.</summary>
        EditVerdict CanApply(ProjectCommand command);

        /// <summary>Whether <paramref name="dragged"/> and <paramref name="target"/> are distinct same-parent,
        /// same-tag siblings — the reorder drag-over probe (US-055), answered against the per-commit index so
        /// the pointer path pays no full-tree walk (proposal §3.1 review F5). Same rule as the gateway's
        /// <c>ProjectCommands.CanReorderNode</c> Project-walking query; false when no project is open.</summary>
        bool CanReorderNode(ElementId dragged, ElementId target);

        /// <summary>Whether <paramref name="id"/> can move <paramref name="delta"/> positions among its same-tag
        /// siblings (US-055) — the index-backed MENU-gate peer of <see cref="CanReorderNode"/>, so the Move up/down
        /// gates re-evaluated on every selection change pay dictionary lookups instead of full-tree walks and the
        /// caller mints no command until Execute (review F02). Answers the gateway's own boundary rule plus the
        /// reorder command's verdict, so it cannot disagree with the <see cref="Apply(ProjectCommand,int?)"/> it
        /// guards; false when no project is open.</summary>
        bool CanReorder(ElementId id, int delta);

        /// <summary>The typed preview of a command applied now, without committing.</summary>
        PreviewOutcome Preview(ProjectCommand command);

        /// <summary>Reverses the most recent edit. A committed outcome carries the undo change set
        /// (<c>Changes.Origin</c> = "undo"), so a projector can reconcile in place instead of rebuilding.</summary>
        EditOutcome Undo();

        /// <summary>Re-applies the most recently undone edit. A committed outcome carries the redo change
        /// set (<c>Changes.Origin</c> = "redo").</summary>
        EditOutcome Redo();

        /// <summary>Discards the most recent committed edit as if it never happened — the cancel arm of an
        /// apply → dialog → cancel gesture (e.g. a cancelled product insert). Unlike <see cref="Undo"/> it
        /// restores the previous snapshot VERBATIM — including <c>last_unique_id</c>, because a cancelled
        /// gesture burns no ids (vendor-measured, uxparity S-12) while a real undo keeps the raised allocator
        /// (alignment F-10) — and it pushes nothing onto the redo stack: a gesture that never completed is not
        /// redoable. A committed outcome carries the change set with <c>Changes.Origin</c> = "rollback".</summary>
        EditOutcome Rollback();

        /// <summary>Opens a project as the current snapshot, resetting history and version. When
        /// <paramref name="startClean"/> the opened snapshot is also the save point (dirty = false).</summary>
        void Open(Project project, bool startClean = true);

        /// <summary>Records the snapshot that was written to disk as the save point. Pass the exact saved
        /// snapshot, not <see cref="Current"/>, so an edit that landed during the save stays dirty.</summary>
        void MarkSaved(Project savedSnapshot);

        /// <summary>Closes the current project, clearing all document state.</summary>
        void Close();

        /// <summary>Raised after apply/undo/redo with the structural change set (on the mutating thread —
        /// see the threading remarks).</summary>
        event EventHandler<ProjectChangedEventArgs>? Changed;

        /// <summary>Raised on the non-edit transitions (open/mark-saved/close), on the mutating thread —
        /// see the threading remarks.</summary>
        event EventHandler? StateChanged;
    }
}
