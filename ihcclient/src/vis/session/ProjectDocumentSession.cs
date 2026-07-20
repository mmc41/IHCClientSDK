#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ihc.Vis.Editing;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The GUI-free document session (proposal §3.3): holds the current <see cref="Project"/>, applies
    /// <see cref="ProjectCommand"/>s through one <see cref="Apply(ProjectCommand, int?)"/> pipeline with labelled
    /// undo/redo, and tracks dirty state against the written save point. No Avalonia, no dialogs — it emits OTel
    /// spans and raises <see cref="Changed"/>/<see cref="StateChanged"/> for the UI to project.
    /// </summary>
    /// <remarks>
    /// <b>Thread-affine (D12):</b> the session captures its owning thread at construction and every member fails
    /// fast on a wrong-thread access; there is no lock. Snapshots are immutable <see cref="Project"/> instances, so
    /// a background save/backup reads a captured snapshot off-thread while the session stays single-threaded.
    /// </remarks>
    public sealed class ProjectDocumentSession
    {
        private readonly int _ownerThreadId = Environment.CurrentManagedThreadId;
        private readonly HistoryPolicy _history;
        private readonly LinkedList<HistoryEntry> _undo = new();   // Last = top of the undo stack
        private readonly Stack<HistoryEntry> _redo = new();

        private Project? _current;
        private Project? _savePoint;
        private ProjectIndex? _index;
        private int _version;

        private readonly record struct HistoryEntry(Project Snapshot, string Label);

        // The transition-origin tags stamped on a ProjectChangeSet, naming which operation produced it (a consumer
        // may branch on these, e.g. undo/redo drive a full rebuild while apply reconciles in place).
        private const string OriginApply = "apply";
        private const string OriginUndo = "undo";
        private const string OriginRedo = "redo";
        private const string OriginPreview = "preview";

        /// <summary>Creates a session with the given history policy. The default is <see cref="HistoryPolicy.Unlimited"/>
        /// (W4-4): undo depth is bounded only by process memory now that a committed snapshot path-copies just the
        /// subtrees it changed (W4-3), so a history entry costs its changed path, not a full tree.</summary>
        public ProjectDocumentSession(HistoryPolicy? history = null) =>
            _history = history ?? HistoryPolicy.Unlimited;

        /// <summary>Raised after <see cref="Apply(ProjectCommand, int?)"/>/<see cref="Undo"/>/<see cref="Redo"/>
        /// with the structural change set, so a projector can reconcile in place.</summary>
        public event EventHandler<ProjectChangedEventArgs>? Changed;

        /// <summary>Raised on the non-edit transitions (<see cref="Open"/>/<see cref="Close"/>/<see cref="MarkSaved"/>)
        /// so the UI can refresh dirty state, the title, and lifecycle without a change set.</summary>
        public event EventHandler? StateChanged;

        /// <summary>The current project snapshot, or null when none is open.</summary>
        public Project? Current { get { VerifyAccess(); return _current; } }

        /// <summary>Bumps on apply/undo/redo/open/close (never on <see cref="MarkSaved"/>); the base-version guard
        /// compares against it to refuse a stale dialog commit.</summary>
        public int Version { get { VerifyAccess(); return _version; } }

        /// <summary>Whether the current snapshot differs from the last written save point — computed by reference,
        /// never stored, so undoing back to the saved snapshot reads clean.</summary>
        public bool IsDirty { get { VerifyAccess(); return !ReferenceEquals(_current, _savePoint); } }

        /// <summary>Whether there is an edit to undo.</summary>
        public bool CanUndo { get { VerifyAccess(); return _undo.Count > 0; } }

        /// <summary>Whether there is an undone edit to redo.</summary>
        public bool CanRedo { get { VerifyAccess(); return _redo.Count > 0; } }

        /// <summary>The label of the edit that <see cref="Undo"/> would reverse, or null.</summary>
        public string? UndoLabel { get { VerifyAccess(); return _undo.Count > 0 ? _undo.Last!.Value.Label : null; } }

        /// <summary>The label of the edit that <see cref="Redo"/> would re-apply, or null.</summary>
        public string? RedoLabel { get { VerifyAccess(); return _redo.Count > 0 ? _redo.Peek().Label : null; } }

        /// <summary>The undo-history retention policy in effect.</summary>
        public HistoryPolicy History { get { VerifyAccess(); return _history; } }

        /// <summary>Opens a project as the current snapshot, resetting history and version. When
        /// <paramref name="startClean"/> the opened snapshot is also the save point (dirty = false).</summary>
        public void Open(Project project, bool startClean = true)
        {
            VerifyAccess();
            _current = project;
            _savePoint = startClean ? project : null;
            _index = ProjectIndex.Build(project);
            _undo.Clear();
            _redo.Clear();
            _version++;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Records the snapshot that was written to disk as the save point (the race fix: pass the exact
        /// snapshot that was saved, not <see cref="Current"/>, so an edit that landed during the save stays dirty).
        /// Flips dirty without bumping <see cref="Version"/>.</summary>
        public void MarkSaved(Project savedSnapshot)
        {
            VerifyAccess();
            _savePoint = savedSnapshot;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Closes the current project, clearing all session state.</summary>
        public void Close()
        {
            VerifyAccess();
            _current = null;
            _savePoint = null;
            _index = null;
            _undo.Clear();
            _redo.Clear();
            _version++;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Applies a command through the full pipeline. See the class remarks for the outcome typing.</summary>
        public EditOutcome Apply(ProjectCommand command, int? baseVersion = null)
        {
            VerifyAccess();
            return ApplyInternal(command, baseVersion, command.Execute);
        }

        /// <summary>Applies a value-producing command, surfacing the produced value on a committed outcome.</summary>
        public EditOutcome<T> Apply<T>(ProjectCommand<T> command, int? baseVersion = null)
        {
            VerifyAccess();
            T? produced = default;
            EditOutcome outcome = ApplyInternal(command, baseVersion, editor => produced = command.ExecuteCore(editor));
            return new EditOutcome<T>(outcome.Status, outcome.Label, outcome.Reason, outcome.Changes,
                outcome.Status == EditStatus.Committed ? produced : default);
        }

        private EditOutcome ApplyInternal(ProjectCommand command, int? baseVersion, Action<ProjectEditor> execute)
        {
            using Activity? activity = Telemetry.ActivitySource.StartActivity(
                nameof(ProjectDocumentSession) + ".Apply", ActivityKind.Internal);
            activity?.SetTag("command", command.GetType().Name);

            if (_current is not { } current)
            {
                return new EditOutcome(EditStatus.Refused, command.GetType().Name, "No project is open.", null);
            }
            // Label resolves against the pre-edit project (D10): a rename shows the old name, a delete the doomed target.
            string label = command.Describe(current);
            if (baseVersion is { } expected && expected != _version)
            {
                return new EditOutcome(EditStatus.Refused, label, "The project changed since this edit was prepared.", null);
            }
            EditVerdict verdict = command.Evaluate(new EditContext(current, _index!));
            if (!verdict.Ok)
            {
                return new EditOutcome(EditStatus.Refused, label, verdict.Reason, null);
            }

            Project? updated;
            try
            {
                updated = TryProduceUpdated(current, execute);
            }
            catch (EditRefusedException ex)   // a deep guard refuses only inside Execute
            {
                ActivityExtensions.SetError(activity, ex);
                return new EditOutcome(EditStatus.Refused, label, ex.Message, null);
            }
            catch (Exception ex)   // any other failure (incl. engine InvalidOperationException on a malformed doc)
            {
                ActivityExtensions.SetError(activity, ex);
                return new EditOutcome(EditStatus.Failed, label, ex.Message, null);
            }

            if (updated is null)   // no-op (an allocator burn makes the project differ, so it is not one)
            {
                return new EditOutcome(EditStatus.NoChange, label, null, null);
            }

            _undo.AddLast(new HistoryEntry(current, label));
            TrimUndo();
            _redo.Clear();
            ProjectChangeSet changes = Transition(current, updated, label, OriginApply);
            return new EditOutcome(EditStatus.Committed, label, null, changes);
        }

        // The single edit kernel BOTH Apply and Preview run: open an editor over <paramref name="current"/>, run the
        // command's mutation, and return the updated project — or null when it produced no change. Because the change
        // set a Preview shows and the change set an Apply commits are derived from the same produced project, their
        // parity is structural, not merely test-enforced (review medium; PreviewApplyParityTests is the guard).
        private static Project? TryProduceUpdated(Project current, Action<ProjectEditor> execute)
        {
            ProjectEditor editor = current.Edit();
            execute(editor);
            Project updated = editor.ToProject();
            return updated.Equals(current) ? null : updated;
        }

        /// <summary>Reverses the most recent edit, or a no-op outcome when the history is empty.</summary>
        public EditOutcome Undo()
        {
            VerifyAccess();
            if (_undo.Count == 0 || _current is not { } current)
            {
                return new EditOutcome(EditStatus.NoChange, "Undo", null, null);
            }
            HistoryEntry entry = _undo.Last!.Value;
            _undo.RemoveLast();
            _redo.Push(new HistoryEntry(current, entry.Label));
            ProjectChangeSet changes = Transition(current, entry.Snapshot, entry.Label, OriginUndo);
            return new EditOutcome(EditStatus.Committed, entry.Label, null, changes);
        }

        /// <summary>Re-applies the most recently undone edit, or a no-op outcome when nothing is redoable.</summary>
        public EditOutcome Redo()
        {
            VerifyAccess();
            if (_redo.Count == 0 || _current is not { } current)
            {
                return new EditOutcome(EditStatus.NoChange, "Redo", null, null);
            }
            HistoryEntry entry = _redo.Pop();
            _undo.AddLast(new HistoryEntry(current, entry.Label));
            TrimUndo();
            ProjectChangeSet changes = Transition(current, entry.Snapshot, entry.Label, OriginRedo);
            return new EditOutcome(EditStatus.Committed, entry.Label, null, changes);
        }

        /// <summary>The change set the command would produce if applied now — without committing — or null when it
        /// would refuse, fail, or make no change. Drives the Preview→confirm→Apply flow (W2-13).</summary>
        public ProjectChangeSet? Preview(ProjectCommand command)
        {
            VerifyAccess();
            if (_current is not { } current || !command.Evaluate(new EditContext(current, _index!)).Ok)
            {
                return null;
            }
            try
            {
                Project? updated = TryProduceUpdated(current, command.Execute);
                return updated is null
                    ? null
                    : ProjectChangeSet.Diff(current, updated, _version, _version, OriginPreview, command.Describe(current));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The command's legality verdict against the current project (cheap — no edit), for drag-over
        /// probes and menu gates.</summary>
        public EditVerdict CanApply(ProjectCommand command)
        {
            VerifyAccess();
            return _current is { } current
                ? command.Evaluate(new EditContext(current, _index!))
                : EditVerdict.Refuse("No project is open.");
        }

        // ---- API-D queries (W2-12): read projections over Current. Questions are calls, not command objects
        // (CQS); each delegates to the W1-5 SDK projection and returns the blank model when no project is open. ----

        /// <summary>Reads the project/customer/installer information (US-039), or the blank model when none is open.</summary>
        public ProjectInfoData GetProjectInfo()
        {
            VerifyAccess();
            return _current?.GetProjectInfo() ?? ProjectInfoData.Empty;
        }

        /// <summary>Reads the data tables (US-049) — the read-only system tables and the editable user-defined
        /// texts — or an empty model when none is open.</summary>
        public DataTablesModel GetDataTables()
        {
            VerifyAccess();
            return _current?.GetDataTables() ?? DataTablesModel.Empty;
        }

        /// <summary>Names the wireless products not yet linked to the controller (US-042 pre-flight), or empty
        /// when none is open.</summary>
        public IReadOnlyList<string> GetUnlinkedWirelessProducts()
        {
            VerifyAccess();
            return _current?.GetUnlinkedWirelessProducts() ?? [];
        }

        /// <summary>Builds the read-only Wired module address map (US-050), or an empty map when none is open.</summary>
        public ModuleAddressMap GetModuleAddressMap()
        {
            VerifyAccess();
            return _current?.GetModuleAddressMap() ?? ModuleAddressMap.Empty;
        }

        private ProjectChangeSet Transition(Project from, Project to, string label, string origin)
        {
            int baseVersion = _version;
            _current = to;
            _index = ProjectIndex.Build(to);
            _version++;
            var changes = ProjectChangeSet.Diff(from, to, baseVersion, _version, origin, label);
            Changed?.Invoke(this, new ProjectChangedEventArgs(changes));
            return changes;
        }

        private void TrimUndo()
        {
            while (_history.Cap is { } cap && _undo.Count > cap)
            {
                _undo.RemoveFirst();
            }
        }

        private void VerifyAccess()
        {
            if (Environment.CurrentManagedThreadId != _ownerThreadId)
            {
                throw new InvalidOperationException(
                    "ProjectDocumentSession is thread-affine and was accessed from a non-owner thread.");
            }
        }
    }
}
