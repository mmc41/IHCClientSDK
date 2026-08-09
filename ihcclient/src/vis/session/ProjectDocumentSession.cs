#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;

namespace Ihc.Vis.Session
{
    /// <summary>
    /// The GUI-free document session (proposal §3.3): holds the current <see cref="Project"/>, applies
    /// <see cref="ProjectCommand"/>s through one <see cref="Apply(ProjectCommand, int?)"/> pipeline with labelled
    /// undo/redo, and tracks dirty state against the written save point. No Avalonia, no dialogs — it emits OTel
    /// spans and raises <see cref="Changed"/>/<see cref="StateChanged"/> for the UI to project. Implements
    /// <see cref="IProjectDocument"/> DIRECTLY (crudarch D01 — no wrapper): frontends obtain it as the port from
    /// <see cref="ProjectAppService.OpenDocument"/>; the concrete type stays out of GUI reach (arch-enforced).
    /// </summary>
    /// <remarks>
    /// <b>Lock-serialized (crudarch D04, supersedes the D12 thread-affinity):</b> a private monitor serializes
    /// every member body, so any thread may READ while another edits — the backup-timer shape: a worker sampling
    /// <see cref="Current"/> mid-edit observes the pre- or post-edit snapshot (immutable <see cref="Project"/>
    /// instances), never a torn state. <see cref="Changed"/>/<see cref="StateChanged"/> are raised OUTSIDE the
    /// lock, synchronously on the thread that performed the state change — never marshalled or deferred — so
    /// interactive callers must issue all MUTATIONS from one thread (the UI thread in a GUI) for handler
    /// ordering to stay meaningful; any thread may read.
    /// </remarks>
    public sealed class ProjectDocumentSession : IProjectDocument
    {
        private readonly object _sync = new();
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
        private const string OriginRollback = "rollback";
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
        public Project? Current { get { lock (_sync) { return _current; } } }

        /// <summary>Bumps on apply/undo/redo/open/close (never on <see cref="MarkSaved"/>); the base-version guard
        /// compares against it to refuse a stale dialog commit.</summary>
        public int Version { get { lock (_sync) { return _version; } } }

        /// <summary>Whether the current snapshot differs from the last written save point — computed, never stored,
        /// so undoing back to the saved snapshot reads clean (US-052). Compared modulo <c>last_unique_id</c>: undo
        /// keeps the raised allocator high-water (see <see cref="WithMonotonicAllocator"/>), and that bookkeeping
        /// alone is not an unsaved change the installer should be prompted about.</summary>
        public bool IsDirty { get { lock (_sync) { return _current is { } current && !IsAtSavePoint(current, _savePoint); } } }

        /// <summary>Whether there is an edit to undo.</summary>
        public bool CanUndo { get { lock (_sync) { return _undo.Count > 0; } } }

        /// <summary>Whether there is an undone edit to redo.</summary>
        public bool CanRedo { get { lock (_sync) { return _redo.Count > 0; } } }

        /// <summary>The label of the edit that <see cref="Undo"/> would reverse, or null.</summary>
        public string? UndoLabel { get { lock (_sync) { return _undo.Count > 0 ? _undo.Last!.Value.Label : null; } } }

        /// <summary>The label of the edit that <see cref="Redo"/> would re-apply, or null.</summary>
        public string? RedoLabel { get { lock (_sync) { return _redo.Count > 0 ? _redo.Peek().Label : null; } } }

        /// <summary>The undo-history retention policy in effect (immutable — set at construction).</summary>
        public HistoryPolicy History => _history;

        /// <summary>Opens a project as the current snapshot, resetting history and version. When
        /// <paramref name="startClean"/> the opened snapshot is also the save point (dirty = false).</summary>
        public void Open(Project project, bool startClean = true)
        {
            lock (_sync)
            {
                _current = project;
                _savePoint = startClean ? project : null;
                _index = ProjectIndex.Build(project);
                _undo.Clear();
                _redo.Clear();
                _version++;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Records the snapshot that was written to disk as the save point (the race fix: pass the exact
        /// snapshot that was saved, not <see cref="Current"/>, so an edit that landed during the save stays dirty).
        /// Flips dirty without bumping <see cref="Version"/>.</summary>
        public void MarkSaved(Project savedSnapshot)
        {
            lock (_sync)
            {
                _savePoint = savedSnapshot;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Closes the current project, clearing all session state.</summary>
        public void Close()
        {
            lock (_sync)
            {
                _current = null;
                _savePoint = null;
                _index = null;
                _undo.Clear();
                _redo.Clear();
                _version++;
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Applies a command through the full pipeline. See the class remarks for the outcome typing.</summary>
        public EditOutcome Apply(ProjectCommand command, int? baseVersion = null)
        {
            EditOutcome outcome;
            lock (_sync)
            {
                outcome = ApplyInternal(command, baseVersion, command.Execute);
            }
            NotifyChanged(outcome.Changes);
            return outcome;
        }

        /// <summary>Applies a value-producing command, surfacing the produced value on a committed outcome.</summary>
        public EditOutcome<T> Apply<T>(ProjectCommand<T> command, int? baseVersion = null)
        {
            T? produced = default;
            EditOutcome outcome;
            lock (_sync)
            {
                outcome = ApplyInternal(command, baseVersion, editor => produced = command.ExecuteCore(editor));
            }
            NotifyChanged(outcome.Changes);
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
            EditOutcome outcome;
            lock (_sync)
            {
                if (_undo.Count == 0 || _current is not { } current)
                {
                    outcome = new EditOutcome(EditStatus.NoChange, "Undo", null, null);
                }
                else
                {
                    HistoryEntry entry = _undo.Last!.Value;
                    _undo.RemoveLast();
                    _redo.Push(new HistoryEntry(current, entry.Label));
                    outcome = new EditOutcome(EditStatus.Committed, entry.Label, null,
                        Transition(current, WithMonotonicAllocator(current, entry.Snapshot), entry.Label, OriginUndo));
                }
            }
            NotifyChanged(outcome.Changes);
            return outcome;
        }

        /// <summary>Re-applies the most recently undone edit, or a no-op outcome when nothing is redoable.</summary>
        public EditOutcome Redo()
        {
            EditOutcome outcome;
            lock (_sync)
            {
                if (_redo.Count == 0 || _current is not { } current)
                {
                    outcome = new EditOutcome(EditStatus.NoChange, "Redo", null, null);
                }
                else
                {
                    HistoryEntry entry = _redo.Pop();
                    _undo.AddLast(new HistoryEntry(current, entry.Label));
                    TrimUndo();
                    outcome = new EditOutcome(EditStatus.Committed, entry.Label, null,
                        Transition(current, WithMonotonicAllocator(current, entry.Snapshot), entry.Label, OriginRedo));
                }
            }
            NotifyChanged(outcome.Changes);
            return outcome;
        }

        /// <summary>Discards the most recent committed edit as if it never happened — see
        /// <see cref="IProjectDocument.Rollback"/>. The verbatim restore (no <see cref="WithMonotonicAllocator"/>)
        /// and the absent redo push are the two deliberate differences from <see cref="Undo"/>: a cancelled
        /// gesture burns no ids (vendor-measured, uxparity S-12) and cannot be redone.</summary>
        public EditOutcome Rollback()
        {
            EditOutcome outcome;
            lock (_sync)
            {
                if (_undo.Count == 0 || _current is not { } current)
                {
                    outcome = new EditOutcome(EditStatus.NoChange, "Rollback", null, null);
                }
                else
                {
                    HistoryEntry entry = _undo.Last!.Value;
                    _undo.RemoveLast();
                    outcome = new EditOutcome(EditStatus.Committed, entry.Label, null,
                        Transition(current, entry.Snapshot, entry.Label, OriginRollback));
                }
            }
            NotifyChanged(outcome.Changes);
            return outcome;
        }

        /// <summary>The typed preview of a command applied now — without committing — mirroring <see cref="Apply"/>
        /// (M8/D05): <see cref="PreviewStatus.WouldChange"/> carries the delta the subsequent Apply would commit,
        /// while a legality refusal, a deep-guard <see cref="EditRefusedException"/>, a no-change and an unexpected
        /// engine fault are each their own status — so the GUI can report a genuine bug instead of swallowing it as
        /// "nothing to preview". Drives the Preview→confirm→Apply flow (W2-13).</summary>
        public PreviewOutcome Preview(ProjectCommand command)
        {
            lock (_sync)
            {
                if (_current is not { } current)
                {
                    return PreviewOutcome.Refused("No project is open.");
                }
                EditVerdict verdict = command.Evaluate(new EditContext(current, _index!));
                if (!verdict.Ok)
                {
                    return PreviewOutcome.Refused(verdict.Reason);
                }
                Project? updated;
                try
                {
                    updated = TryProduceUpdated(current, command.Execute);
                }
                catch (EditRefusedException ex)   // a deep guard refuses only inside Execute — a refusal, not a fault
                {
                    return PreviewOutcome.Refused(ex.Message);
                }
                catch (Exception ex)   // an unexpected engine fault — surfaced, not swallowed as "nothing to preview" (D05)
                {
                    return PreviewOutcome.Faulted(ex.Message);
                }
                return updated is null
                    ? PreviewOutcome.NoChange
                    : PreviewOutcome.WouldChange(
                        ProjectChangeSet.Diff(current, updated, _version, _version, OriginPreview, command.Describe(current)));
            }
        }

        /// <summary>The command's legality verdict against the current project (cheap — no edit), for drag-over
        /// probes and menu gates.</summary>
        public EditVerdict CanApply(ProjectCommand command)
        {
            lock (_sync)
            {
                return _current is { } current
                    ? command.Evaluate(new EditContext(current, _index!))
                    : EditVerdict.Refuse("No project is open.");
            }
        }

        /// <summary>Whether the pair is a reorderable same-tag sibling pair (US-055) — the index-backed drag-over
        /// probe (review F5): forwards to the gateway's shared rule against the per-commit index, so the pointer
        /// path pays dictionary lookups instead of full-tree walks. False when no project is open.</summary>
        public bool CanReorderNode(ElementId dragged, ElementId target)
        {
            lock (_sync)
            {
                // The reorderable-pair rule PLUS the locked-block gate the ReorderNode command enforces (review F02/A1):
                // a node strictly inside a locked block cannot be reordered, so the drag-over hint must agree with the
                // Apply it previews — the same IsWithinLockedBlock the command's Evaluate reads, so the two never diverge.
                return _index is { } index && _current is { } current
                    && ProjectCommands.CanReorderNode(index, dragged, target)
                    && !ProjectEditor.IsWithinLockedBlock(current.Root, dragged, inclusive: false);
            }
        }

        /// <summary>Whether <paramref name="id"/> can move <paramref name="delta"/> positions among its same-tag
        /// siblings (US-055) — the index-backed MENU-gate peer of <see cref="CanReorderNode"/> (review F02): the
        /// gateway's own boundary rule resolved from the per-commit index, then the reorder command's own verdict,
        /// so the gate can never disagree with the Apply it guards and the caller mints nothing until Execute.
        /// False when no project is open.</summary>
        public bool CanReorder(ElementId id, int delta)
        {
            lock (_sync)
            {
                return _current is { } current && _index is { } index
                    && ProjectCommands.ReorderNode(index, id, delta) is { } command
                    && command.Evaluate(new EditContext(current, index)).Ok;
            }
        }

        // ---- API-D queries (W2-12): read projections over Current. Questions are calls, not command objects
        // (CQS); each delegates to the W1-5 SDK projection and returns the blank model when no project is open. ----

        /// <summary>Reads the project/customer/installer information (US-039), or the blank model when none is open.</summary>
        public ProjectInfoData GetProjectInfo()
        {
            lock (_sync)
            {
                return _current?.GetProjectInfo() ?? ProjectInfoData.Empty;
            }
        }

        /// <summary>Reads the data tables (US-049) — the read-only system tables and the editable user-defined
        /// texts — or an empty model when none is open.</summary>
        public DataTablesModel GetDataTables()
        {
            lock (_sync)
            {
                return _current?.GetDataTables() ?? DataTablesModel.Empty;
            }
        }

        /// <summary>Names the wireless products not yet linked to the controller (US-042 pre-flight), or empty
        /// when none is open.</summary>
        public IReadOnlyList<string> GetUnlinkedWirelessProducts()
        {
            lock (_sync)
            {
                return _current?.GetUnlinkedWirelessProducts() ?? [];
            }
        }

        /// <summary>Builds the read-only Wired module address map (US-050), or an empty map when none is open.</summary>
        public ModuleAddressMap GetModuleAddressMap()
        {
            lock (_sync)
            {
                return _current?.GetModuleAddressMap() ?? ModuleAddressMap.Empty;
            }
        }

        // Commits a state transition under the caller-held lock and returns its change set. The Changed raise is
        // deliberately NOT here: the public member that committed raises it via NotifyChanged AFTER releasing the
        // lock (D04 — outside the lock, on the mutating thread), so a handler re-entering the session never runs
        // inside the monitor.
        private ProjectChangeSet Transition(Project from, Project to, string label, string origin)
        {
            int baseVersion = _version;
            _current = to;
            _index = ProjectIndex.Build(to);
            _version++;
            return ProjectChangeSet.Diff(from, to, baseVersion, _version, origin, label);
        }

        // D04: raises Changed for a committed transition — outside the lock, synchronously on the thread that
        // performed the state change, never marshalled or deferred. Null changes (refused/no-op paths) raise nothing.
        private void NotifyChanged(ProjectChangeSet? changes)
        {
            if (changes is not null)
            {
                Changed?.Invoke(this, new ProjectChangedEventArgs(changes));
            }
        }

        /// <summary>
        /// Alignment F-10 (tmp/align-campaign-2026-08-09.md): the id allocator is monotonic ACROSS history
        /// navigation. Measured against the vendor 2026-08-09: insert→undo→insert allocates the NEXT counter
        /// (0x52 after 0x51), and a save straight after the undo still writes the RAISED <c>last_unique_id</c>
        /// (0x51 with no 0x51 element present — a permanent hole). So undo/redo restore the CONTENT, never the
        /// allocator: a restored snapshot keeps the highest <c>last_unique_id</c> the session has reached, and the
        /// next edit's <see cref="Ihc.Vis.Io.IdAllocator"/> seeds off it — re-minting an undone element's counter
        /// for a different element is exactly the reuse FR-8.3 and the Part-3 invariant oracle forbid.
        /// </summary>
        private static Project WithMonotonicAllocator(Project current, Project restored)
        {
            long currentLuid = HexToken.ParseValueOrDefault(current.LastUniqueId);
            return HexToken.ParseValueOrDefault(restored.LastUniqueId) >= currentLuid
                ? restored
                : restored with { Root = restored.Root.WithAttribute("last_unique_id", HexToken.Format(currentLuid)) };
        }

        // The save-point comparison behind IsDirty: reference-equal is clean (the common case — Transition restored
        // the very snapshot MarkSaved holds); otherwise value-equal modulo the allocator attribute, because
        // WithMonotonicAllocator patches a restored snapshot's last_unique_id and bookkeeping alone must not read
        // as an unsaved change. The normalization rewrites one root attribute; the child arrays keep their shared
        // references, so the deep Equals short-circuits on them.
        private static bool IsAtSavePoint(Project current, Project? savePoint)
        {
            if (ReferenceEquals(current, savePoint))
            {
                return true;
            }
            if (savePoint is null)
            {
                return false;
            }
            return NormalizeAllocator(current.Root).Equals(NormalizeAllocator(savePoint.Root));
        }

        private static ProjectElement NormalizeAllocator(ProjectElement root) =>
            root.GetAttribute("last_unique_id") is null ? root : root.WithAttribute("last_unique_id", "_0x0");

        private void TrimUndo()
        {
            while (_history.Cap is { } cap && _undo.Count > cap)
            {
                _undo.RemoveFirst();
            }
        }
    }
}
