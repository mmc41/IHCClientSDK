using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Ihc.Vis.Editing;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
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
    /// <b>Lock-serialized (crudarch D04, supersedes the D12 thread-affinity):</b> a private
    /// <see cref="System.Threading.Lock"/> serializes
    /// every member body, so any thread may READ while another edits — the backup-timer shape: a worker sampling
    /// <see cref="Current"/> mid-edit observes the pre- or post-edit snapshot (immutable <see cref="Project"/>
    /// instances), never a torn state. <see cref="Changed"/>/<see cref="StateChanged"/> are raised OUTSIDE the
    /// lock, synchronously on the thread that performed the state change — never marshalled or deferred — so
    /// interactive callers must issue all MUTATIONS from one thread (the UI thread in a GUI) for handler
    /// ordering to stay meaningful; any thread may read.
    /// </remarks>
    public sealed class ProjectDocumentSession : IProjectDocument
    {
        // .NET 9+/C# 13 dedicated lock type, not an `object` monitor: the type-safe default Microsoft's
        // managed-threading guidance now names for a synchronous lock target.
        private readonly Lock _sync = new();
        private readonly HistoryPolicy _history;
        private readonly LinkedList<HistoryEntry> _undo = new();   // Last = top of the undo stack
        private readonly Stack<HistoryEntry> _redo = new();

        private Project? _current;
        private Project? _savePoint;
        private ProjectIndex? _index;
        private int _version;

        /// <summary>Where a fault this session mints is reported, or null when nothing collects them.</summary>
        private readonly Action<Problems.InternalError>? _faultPort;

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
        public ProjectDocumentSession(HistoryPolicy? history = null)
            : this(history, faultPort: null)
        {
        }

        /// <summary>
        /// The same session over the owning service's fault port, so a fault this layer MINTS is reported by the
        /// layer that minted it.
        /// </summary>
        /// <remarks>
        /// Internal rather than a defaulted parameter on the public constructor: that signature is shipped, and
        /// a port is not something a caller constructing a session by hand supplies — the service that owns the
        /// port is the only one with one to give.
        /// </remarks>
        /// <param name="history">The history policy, as above.</param>
        /// <param name="faultPort">Where a minted fault is reported, or null to report nowhere.</param>
        internal ProjectDocumentSession(HistoryPolicy? history, Action<Problems.InternalError>? faultPort)
        {
            _history = history ?? HistoryPolicy.Unlimited;
            _faultPort = faultPort;
        }

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
            ArgumentNullException.ThrowIfNull(command);
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
            ArgumentNullException.ThrowIfNull(command);
            T? produced = default;
            EditOutcome outcome;
            lock (_sync)
            {
                outcome = ApplyInternal(command, baseVersion, editor => produced = command.ExecuteCore(editor));
            }
            NotifyChanged(outcome.Changes);
            // The FAULT travels too. It is a trailing optional argument, so leaving it off compiled and read null
            // — and a host shows its failure dialog only for an outcome that carries one, which made every broken
            // INSERT (they all come through this overload) report nothing at all to the installer.
            return new EditOutcome<T>(outcome.Status, outcome.Label, outcome.Reason, outcome.Changes,
                outcome.Status == EditStatus.Committed ? produced : default, outcome.Code, outcome.Fault);
        }

        // Through the core, classifying the RETURNED outcome rather than tagging each exit. The method has many
        // outcome-producing exits and every one of them would otherwise need its own status, code and metric
        // call - which is how a branch gets forgotten, and how a refusal ends up reported as a failure.
        private EditOutcome ApplyInternal(ProjectCommand command, int? baseVersion, Action<ProjectEditor> execute) =>
            _telemetry.Run("Apply", scope =>
                {
                    scope.AddSharedTag(SdkTelemetryRegistry.Attributes.EditCommand, command.GetType().Name);
                    return ApplyCore(command, baseVersion, execute, scope);
                },
                EditApplyMetrics,
                ClassifyEdit);

        /// <summary>
        /// The code a faulted edit reports. Naming it is what gives the span an <c>error.type</c> at all: an
        /// outcome with no code normalizes to the catch-all bucket, so every engine fault in every command used
        /// to arrive as one indistinguishable kind with no exception attached.
        /// </summary>
        private static readonly Problems.ProblemCode EditFailedCode = new("internal.edit-failed");

        /// <summary>The code a faulted PREVIEW reports — its own, because a preview commits nothing.</summary>
        private static readonly Problems.ProblemCode PreviewFailedCode = new("internal.preview-failed");

        /// <summary>
        /// Offers a minted fault to the port and hands the SAME value back, so a mint site reports and returns in
        /// one expression and the two can never drift into describing different events.
        /// </summary>
        /// <remarks>
        /// Reporting is ADDITIVE: the fault still travels on the outcome, so a caller that never wired a port
        /// loses nothing and a shell's dialog reads the value it always read. What it buys is that the layer
        /// which CAUGHT the exception is the layer that announces it — a consumer above no longer has to infer
        /// "a Failed outcome means something broke", and the one-shot facade doors, which return before anyone
        /// above could infer anything, are covered by the same line.
        /// <para>
        /// FAIL-OPEN, for the reason <c>AppServiceBase</c>'s port is: a broken sink must not turn a reportable
        /// fault into a second, worse one on top of the outcome the caller is owed.
        /// </para>
        /// </remarks>
        private Problems.InternalError Report(Problems.InternalError fault)
        {
            if (_faultPort is { } port)
            {
                try
                {
                    port(fault);
                }
                catch (Exception)
                {
                    // See the fail-open note above.
                }
            }
            return fault;
        }

        /// <summary>
        /// Turns the edit's own outcome into the operation's. Reserving Error for <see cref="EditStatus.Failed"/>
        /// is the point: a refusal is the rules working, and the two refusal paths used to report differently
        /// - one via SetError (Error) and one by returning quietly (Unset) - for outcomes that are the same kind
        /// of thing.
        /// </summary>
        private static OperationOutcome ClassifyEdit(EditOutcome outcome) => outcome.Status switch
        {
            EditStatus.Committed or EditStatus.NoChange => OperationOutcome.Ok,
            EditStatus.Refused => OperationOutcome.Refused(outcome.Code.Value),
            EditStatus.Failed => OperationOutcome.FailedWith(outcome.Code.Value),
            _ => OperationOutcome.Ok,
        };

        private readonly OperationTelemetry _telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(ProjectDocumentSession));

        /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
        private static readonly MetricBinding EditApplyMetrics =
            MetricBinding.For(SdkTelemetryRegistry.EditApplyDuration, SdkTelemetryRegistry.EditApply);

        private EditOutcome ApplyCore(ProjectCommand command, int? baseVersion, Action<ProjectEditor> execute,
            OperationScope scope)
        {
            Activity? activity = scope.Activity;

            if (_current is not { } current)
            {
                // The SAME sentence CanApply answers this condition with (D13) — the two doors are asked the same
                // question by the same GUI, so a hand-written second wording here would answer it in another voice
                // (and, until this was fixed, in another LANGUAGE) depending only on which door happened to be used.
                return new EditOutcome(EditStatus.Refused, command.GetType().Name, EditRefusals.NoProjectOpenRefusal,
                    null, EditRefusalCodes.NoProjectOpen);
            }
            // Label resolves against the pre-edit project (D10): a rename shows the old name, a delete the doomed target.
            string label = command.Describe(current);
            if (baseVersion is { } expected && expected != _version)
            {
                // Danish, like every refusal: a Refused reason is forwarded to the installer verbatim (FR-2.6 / D13),
                // so this is user-facing text that happens to live in the engine — not an internal diagnostic.
                return new EditOutcome(EditStatus.Refused, label, EditRefusals.StaleBaseVersionRefusal,
                    null, EditRefusalCodes.StaleBaseVersion);
            }
            EditVerdict verdict = command.Evaluate(new EditContext(current, _index!));
            if (!verdict.Ok)
            {
                // The verdict's OWN code, carried through unchanged: this is what makes "what the gate refuses,
                // the door refuses" checkable by identity instead of by comparing two Danish sentences.
                return new EditOutcome(EditStatus.Refused, label, verdict.Reason, null, verdict.Code);
            }

            Project? updated;
            try
            {
                updated = Produce(current, execute);
            }
            catch (EditRefusedException ex)   // a deep guard refuses only inside Execute
            {
                // No SetError here: this is a REFUSAL, and marking it Error is exactly the Error/Unset split
                // between the two refusal paths that the classification removes.
                // The guard's OWN code, not a blanket edit.deep-guard. A deep guard refused from inside Execute
                // after the gate allowed, which is a fact about where it was raised; what was refused is the
                // guard's to say, and it says it. Sites that name nothing still report edit.deep-guard, because
                // that is what the exception defaults its code to.
                return new EditOutcome(EditStatus.Refused, label, ex.Message, null, ex.Code);
            }
            catch (Exception ex)
            {
                // A CODED refusal raised below the gate is a REFUSAL, not a failure. The contract's central claim
                // is that one catch shape covers every coded refusal, so this asks for the interface rather than
                // for a list of exception types — an edit-open guard and a refused write both arrive carrying a
                // Danish sentence and a published cause id. Without this branch a caller got the engine's ENGLISH
                // diagnostic under EditStatus.Failed, which is invariant 10 breached at the last step.
                //
                // One catch with a test inside, rather than two catches with a `when` filter: CA1508 cannot see
                // through an exception filter and reports the test as always true.
                if (ex is IProblemCarrier carrier)
                {
                    // BOTH shapes. A carrier answers with a chain or with an aggregate, and matching only the
                    // chain is precisely the half-honoured contract the widened interface was chosen to prevent
                    // — a site that tests for one shape and forgets the other reports a coded refusal as an
                    // untyped failure. Two tests cost little because there are two such sites, this and the peer
                    // arm in Preview; the rejected sibling-interface design would have needed them everywhere.
                    if (carrier.Problems is { } refusal)
                    {
                        return new EditOutcome(
                            EditStatus.Refused, label, refusal.Cause.Message, null, refusal.Cause.Code);
                    }

                    if (carrier.Aggregate is { } aggregate)
                    {
                        // The HEAD, which names the operation and how much is wrong. An EditOutcome has room for
                        // one sentence, so the items do not fit — a caller that needs them asks the validation
                        // door, which is where a set of findings belongs.
                        return new EditOutcome(
                            EditStatus.Refused, label, aggregate.Head.Message, null, aggregate.Head.Code);
                    }
                }

                // Anything else is broken rather than refused (incl. an engine InvalidOperationException on a
                // malformed doc): its English text goes to the caller's log, not to an installer's dialog.
                //
                // The fault is CAPTURED HERE, where the exception is still in hand — by the time this record
                // reaches a caller the stack is gone, and a bare code would leave a host holding an id it is
                // forbidden to resolve against the catalogue. Reason keeps ex.Message for the log; the Danish
                // sentence a host may show travels on Fault.
                return new EditOutcome(EditStatus.Failed, label, ex.Message, null, EditFailedCode,
                    Report(new Problems.InternalError(
                        EditFailedCode,
                        EditRefusals.EditFailedMessage,
                        ex.Message,
                        Problems.InternalErrorOrigin.Sdk,
                        ex.ToString(),
                        DateTimeOffset.UtcNow)));
            }

            if (updated is null)   // no-op (an allocator burn makes the project differ, so it is not one)
            {
                return new EditOutcome(EditStatus.NoChange, label, null, null);
            }

            _undo.AddLast(new HistoryEntry(current, label));
            TrimUndo();
            _redo.Clear();
            ProjectChangeSet changes = Transition(current, updated, label, OriginApply);
            activity?.SetTag(SdkTelemetryRegistry.Attributes.EditAddedCount, changes.Added.Count);
            activity?.SetTag(SdkTelemetryRegistry.Attributes.EditRemovedCount, changes.Removed.Count);
            activity?.SetTag(SdkTelemetryRegistry.Attributes.EditChangedCount, changes.Changed.Count);
            return new EditOutcome(EditStatus.Committed, label, null, changes);
        }

        /// <summary>
        /// The edit kernel, TIMED. <c>Apply</c> covers four things — the gate, the mutation, the index rebuild
        /// and the diff — of which the last two already reported spans of their own, so the mutation was the
        /// unnamed remainder: measured on a live edit, 4 ms of 9.6 were accounted for and nothing said what the
        /// rest was. It is also the part that grows with the project, which is the reason to be able to see it.
        /// </summary>
        /// <remarks>
        /// The classification is the point of the wrapper, not the timing. A deep guard REFUSES by throwing, so
        /// a phase span that read every escaping exception as a failure would mark the commonest refusal path
        /// Error — reintroducing one level down the Error/Unset split that classifying the outcome removed at
        /// the apply level.
        /// </remarks>
        private Project? Produce(Project current, Action<ProjectEditor> execute)
        {
            using OperationScope produce = _telemetry.Start("Produce");
            try
            {
                return TryProduceUpdated(current, execute);
            }
            catch (Exception ex)
            {
                produce.SetOutcome(ClassifyThrow(ex));
                throw;
            }
        }

        /// <summary>
        /// What an exception escaping the edit body MEANS, in the same terms the catch arms below give the
        /// caller: a coded refusal is a refusal, and everything else is broken. Only the identity is derived
        /// here — the sentence a refusal carries stays with the arm that builds the outcome, so there is one
        /// place a user-facing message comes from.
        /// </summary>
        private static OperationOutcome ClassifyThrow(Exception failure) => failure switch
        {
            EditRefusedException refused => OperationOutcome.Refused(refused.Code.Value),
            IProblemCarrier { Problems: { } chain } => OperationOutcome.Refused(chain.Cause.Code.Value),
            IProblemCarrier { Aggregate: { } aggregate } => OperationOutcome.Refused(aggregate.Head.Code.Value),
            _ => OperationOutcome.Failed(failure),
        };

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
        public EditOutcome Undo() => HistoryStep("Undo", OriginUndo, current =>
        {
            if (_undo.Count == 0)
            {
                return null;
            }
            HistoryEntry entry = _undo.Last!.Value;
            _undo.RemoveLast();
            _redo.Push(new HistoryEntry(current, entry.Label));
            return (entry.Label, WithMonotonicAllocator(current, entry.Snapshot));
        });

        /// <summary>Re-applies the most recently undone edit, or a no-op outcome when nothing is redoable.</summary>
        public EditOutcome Redo() => HistoryStep("Redo", OriginRedo, current =>
        {
            if (_redo.Count == 0)
            {
                return null;
            }
            HistoryEntry entry = _redo.Pop();
            _undo.AddLast(new HistoryEntry(current, entry.Label));
            TrimUndo();
            return (entry.Label, WithMonotonicAllocator(current, entry.Snapshot));
        });

        /// <summary>Discards the most recent committed edit as if it never happened — see
        /// <see cref="IProjectDocument.Rollback"/>. The verbatim restore (no <see cref="WithMonotonicAllocator"/>)
        /// and the absent redo push are the two deliberate differences from <see cref="Undo"/>: a cancelled
        /// gesture burns no ids (vendor-measured, uxparity S-12) and cannot be redone.</summary>
        public EditOutcome Rollback() => HistoryStep("Rollback", OriginRollback, current =>
        {
            if (_undo.Count == 0)
            {
                return null;
            }
            HistoryEntry entry = _undo.Last!.Value;
            _undo.RemoveLast();
            return (entry.Label, entry.Snapshot);
        });

        // The one skeleton behind Undo/Redo/Rollback: take the lock, produce a no-op outcome when there is no open
        // project or nothing to take, otherwise commit the transition — and raise Changed only after the lock is
        // released. <paramref name="step"/> runs UNDER the lock and owns the whole of what differs between the
        // three: which stack it pops, whether it pushes onto the other one, and whether the restored snapshot is
        // re-seeded with a monotonic allocator. Returning null means "nothing to take". Keeping the skeleton here
        // makes those three differences the only thing each member states.
        private EditOutcome HistoryStep(string noOpLabel, string origin,
            Func<Project, (string Label, Project Restored)?> step)
        {
            EditOutcome outcome;
            lock (_sync)
            {
                outcome = _current is { } current && step(current) is { } taken
                    ? new EditOutcome(EditStatus.Committed, taken.Label, null,
                        Transition(current, taken.Restored, taken.Label, origin))
                    : new EditOutcome(EditStatus.NoChange, noOpLabel, null, null);
            }
            NotifyChanged(outcome.Changes);
            return outcome;
        }

        /// <summary>The typed preview of a command applied now — without committing — mirroring <see cref="Apply"/>
        /// (M8/D05): <see cref="PreviewStatus.WouldChange"/> carries the delta the subsequent Apply would commit,
        /// while a legality refusal, a deep-guard <see cref="EditRefusedException"/>, any other CODED refusal, a
        /// no-change and an unexpected engine fault are each their own status — so the GUI can report a genuine bug
        /// instead of swallowing it as "nothing to preview". Drives the Preview→confirm→Apply flow (W2-13).
        /// <para>Mirroring Apply is a REQUIREMENT, not a description: the two run one kernel, so a throw either
        /// classifies the same way in both or the doors contradict each other about the same document.</para></summary>
        public PreviewOutcome Preview(ProjectCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            lock (_sync)
            {
                if (_current is not { } current)
                {
                    return PreviewOutcome.Refused(EditRefusals.NoProjectOpenRefusal, EditRefusalCodes.NoProjectOpen);
                }
                EditVerdict verdict = command.Evaluate(new EditContext(current, _index!));
                if (!verdict.Ok)
                {
                    return PreviewOutcome.Refused(verdict);
                }
                Project? updated;
                try
                {
                    updated = Produce(current, command.Execute);
                }
                catch (EditRefusedException ex)   // a deep guard refuses only inside Execute — a refusal, not a fault
                {
                    return PreviewOutcome.Refused(ex.Message, ex.Code);
                }
                catch (Exception ex)   // an unexpected engine fault — surfaced, not swallowed as "nothing to preview" (D05)
                {
                    // A CODED refusal raised below the gate is a REFUSAL here too, for the reason Apply's peer
                    // arm gives: the sentence a caller may show is the catalogue's Danish one, so reporting it as
                    // an engine fault hands over the ENGLISH diagnostic AND files an internal-error row for the
                    // rules working. Preview and Apply run the same kernel, so without this they disagreed about
                    // the same throw — and the throw is not exotic: the editor-open guard raises it for a
                    // duplicate id token or an undeclared attribute, which is EVERY command on such a project.
                    if (ex is IProblemCarrier carrier)
                    {
                        // Both carrier shapes, as at the Apply site: matching only the chain is the
                        // half-honoured contract the widened interface exists to prevent.
                        if (carrier.Problems is { } refusal)
                        {
                            return PreviewOutcome.Refused(refusal.Cause.Message, refusal.Cause.Code);
                        }

                        if (carrier.Aggregate is { } aggregate)
                        {
                            return PreviewOutcome.Refused(aggregate.Head.Message, aggregate.Head.Code);
                        }
                    }

                    // Captured HERE, where the exception still exists; the factory only threads it through.
                    return PreviewOutcome.Faulted(
                        ex.Message,
                        Report(new Problems.InternalError(
                            PreviewFailedCode,
                            EditRefusals.PreviewFailedMessage,
                            ex.Message,
                            Problems.InternalErrorOrigin.Sdk,
                            ex.ToString(),
                            DateTimeOffset.UtcNow)));
                }
                return updated is null
                    ? PreviewOutcome.NoChange
                    : PreviewOutcome.WouldChange(
                        ProjectChangeSet.Diff(current, updated, _version, _version, OriginPreview, command.Describe(current)));
            }
        }

        /// <summary>The command's legality verdict against the current project (cheap — no edit, and no
        /// whole-project validation scan per evaluation), for drag-over probes and menu gates.
        /// <para>It runs the command's own <c>Evaluate</c> against the open snapshot and nothing else, which is why
        /// a host may re-ask it on every pointer event. A pure query: asking changes nothing, and a REFUSED apply
        /// through the door beside it leaves no undo entry, no version bump and no dirty flag, so a gate and a
        /// click cannot leave the document in different states.</para></summary>
        public EditVerdict CanApply(ProjectCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            lock (_sync)
            {
                return _current is { } current
                    ? command.Evaluate(new EditContext(current, _index!))
                    : EditVerdict.Refuse(EditRefusalCodes.NoProjectOpen, EditRefusals.NoProjectOpenRefusal);
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
                return _index is { } index
                    && ProjectCommands.CanReorderNode(index, dragged, target)
                    && !index.IsWithinLockedBlock(dragged, inclusive: false);
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
        // inside the lock.
        private ProjectChangeSet Transition(Project from, Project to, string label, string origin)
        {
            int baseVersion = _version;
            ProjectIndex? fromIndex = _index;   // built for `from` (Open/the previous Transition) — reuse, don't re-walk
            _current = to;
            _index = ProjectIndex.Build(to);
            _version++;
            return ProjectChangeSet.Diff(from, to, baseVersion, _version, origin, label,
                fromIndex?.ById, _index.ById);
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
        /// Alignment F-10: the id allocator is monotonic ACROSS history
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
            root.GetAttribute("last_unique_id") is null ? root : root.WithAttribute("last_unique_id", ElementId.NullToken);

        private void TrimUndo()
        {
            while (_history.Cap is { } cap && _undo.Count > cap)
            {
                _undo.RemoveFirst();
            }
        }
    }
}
