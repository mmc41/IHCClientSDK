using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ihc.Vis.Model;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace ihc_openvisual.Services;

/// <summary>
/// Keeps a whole-project validation in step with the open document, and publishes what it found.
///
/// <para><b>Why this is a workflow concern and not a panel's.</b> "Does this document have blocking findings" is
/// a fact ABOUT THE DOCUMENT, and more than one thing asks it — the transfer gate, the Problemer panel, and
/// whatever asks next. Sourcing it from a panel would make a presentation component load-bearing for a gate:
/// closing the panel, or simply never constructing it, would silently reopen the gate. So the run lives beside
/// the document it is about, and the panel is one READER of it.</para>
///
/// <para><b>It owns a GENERATION counter, and that is the part worth reading carefully.</b> The workflow publishes
/// a monotone <c>Version</c> and a <c>StateChanged</c> event, and neither one answers "is this still the same
/// document?". <c>Open</c> BUMPS the version rather than resetting it, and <c>MarkSaved</c> raises the event with
/// the version unchanged. So version alone cannot tell an edit from a file replacement, and a loop keyed on it
/// would stale-while-revalidate the PREVIOUS file's result into a freshly opened project. The derivation, from
/// facts the workflow already publishes:</para>
/// <list type="bullet">
/// <item><c>LastChange != null</c> — an edit (commit/undo/redo). Same generation; run.</item>
/// <item><c>LastChange == null</c> and the version is UNCHANGED — a save. Same generation; run nothing, because
/// nothing about the document moved.</item>
/// <item><c>LastChange == null</c> and the version MOVED — a replacement (Ny/Åbn/Luk all reach <c>Open</c>). New
/// generation: drop the bound result at once, abandon the old generation's pending work, and validate the
/// replacement exactly once.</item>
/// </list>
///
/// <para><b>What it does not do.</b> It holds no snapshot of its own (every edit rebuilds the tree, so a retained
/// one is stale immediately) and it names no UI framework — the marshal back to the owning thread arrives as a
/// delegate, which is what lets the whole loop run deterministically in a test with no shell and no
/// dispatcher.</para>
/// </summary>
public sealed class ValidationMonitor : IDisposable
{
    private readonly ProjectWorkflow _session;
    private readonly ValidationWorker _worker;
    private readonly EventHandler _onStateChanged;

    /// <summary>The version last seen, and the yardstick the three branches are told apart by.</summary>
    private int? _seenVersion;

    private bool _disposed;

    /// <summary>What <see cref="HasBlockingFindings"/> last said, so <see cref="BlockingChanged"/> means it moved.</summary>
    private bool _wasBlocking;

    /// <param name="session">The document whose changes drive the loop.</param>
    /// <param name="validate">
    /// The whole-project run. Injected rather than reached through <paramref name="session"/> so a test can drive
    /// every state transition without the cost — or the unpredictability — of a real validation.
    /// </param>
    /// <param name="loggerFactory">
    /// Where a crashed validation run is reported. Optional so every existing caller and test keeps working,
    /// but a monitor built without one silently drops rule crashes exactly as this class used to.
    /// </param>
    public ValidationMonitor(
        ProjectWorkflow session,
        Func<Project, EquatableArray<ValidationFinding>> validate,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(validate);

        _logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance)
            .CreateLogger<ValidationMonitor>();
        _session = session;
        _worker = new ValidationWorker(validate, Bind, session.Post, session.Time, OnFaulted);

        _onStateChanged = (_, _) => OnDocumentChanged();
        _session.StateChanged += _onStateChanged;
        // Sync to whatever is already open: a monitor built after a document was loaded must not sit at
        // "nothing seen yet" until the next edit.
        OnDocumentChanged();
    }

    /// <summary>
    /// Raised whenever anything below moves: a result binds, the document moves past the bound one, the document
    /// is replaced, or a run fails. One event rather than several, because every reader has to re-read more than
    /// one of these together anyway.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>Raised only when <see cref="HasBlockingFindings"/> actually FLIPS.</summary>
    /// <remarks>
    /// Separate from <see cref="Changed"/> because a gate and a panel want different granularities. The panel
    /// re-reads on every move; a gate rebuilds a whole command context, and doing that on each staleness tick
    /// would rebuild it repeatedly per edit for an answer that did not change.
    /// </remarks>
    public event EventHandler? BlockingChanged;

    /// <summary>Which document the monitor is currently about. Monotone; opaque to everything above.</summary>
    public int Generation { get; private set; }

    /// <summary>
    /// The last result bound for the CURRENT generation, or null while none is — the first run of a generation,
    /// and every moment before it completes.
    /// </summary>
    /// <remarks>
    /// Reference identity is meaningful: a reader that has already projected a result can compare against the
    /// one it projected and skip the work when the same result is still bound.
    /// </remarks>
    public ValidationOutcome? Result { get; private set; }

    /// <summary>
    /// Whether the document has moved past <see cref="Result"/> — a quiet period is running or a run is in
    /// flight. False while nothing is bound at all; that state is <see cref="Result"/> being null.
    /// </summary>
    public bool IsStale => Result is { } bound && (bound.Generation, bound.Version) != (Generation, _seenVersion ?? 0);

    /// <summary>
    /// Whether the latest COMPLETED result BLOCKS — what a transfer gate reads. Deliberately false while nothing
    /// is bound: "not yet validated" is not evidence of a fault.
    /// </summary>
    /// <remarks>
    /// Decided by <see cref="ValidationGate"/> over the run's own findings, never by counting rows somewhere.
    /// Blocking is the SDK's definition and every gate in the app is one reader of it; a second derivation would
    /// be free to disagree with the refusal the transfer itself raises.
    /// </remarks>
    public bool HasBlockingFindings => Result is { } bound && !bound.Findings.IsValid;

    /// <summary>
    /// Completes when no validation is running or pending. Tests await it instead of sleeping; nothing in the
    /// loop reads it.
    /// </summary>
    public Task Idle => _worker.Idle;

    /// <summary>
    /// The three-branch derivation. Runs on the owning thread, which is what makes the snapshot/version pair it
    /// captures atomic — ADR-001 host contract, step 1.
    /// </summary>
    private void OnDocumentChanged()
    {
        if (_disposed || _session.Current is not { } snapshot)
            return;

        int version = _session.Version;
        bool firstEver = _seenVersion is null;
        bool replacement = _session.LastChange is null && !firstEver && version != _seenVersion;

        // WHICH of the four transitions this is, on the span. The class derives the branch from two facts
        // that are individually ambiguous - Open bumps the version, MarkSaved does not move it - so a
        // generation that fails to increment on a replacement is a bug whose only symptom is the PREVIOUS
        // file's findings answering questions about the new one. Recording the branch is what makes that
        // visible instead of merely wrong.
        using Ihc.OperationScope scope = _telemetry.Start(nameof(OnDocumentChanged));
        scope.Activity?.SetTag(ihc_openvisual.Configuration.AppTelemetryRegistry.Attributes.DocumentBranch,
            firstEver ? ihc_openvisual.Configuration.AppTelemetryRegistry.Values.BranchFirst
            : replacement ? ihc_openvisual.Configuration.AppTelemetryRegistry.Values.BranchReplacement
            : _session.LastChange is not null ? ihc_openvisual.Configuration.AppTelemetryRegistry.Values.BranchEdit
            : ihc_openvisual.Configuration.AppTelemetryRegistry.Values.BranchSave);

        if (firstEver || replacement)
        {
            Generation++;
            // Dropped IMMEDIATELY, before any run: a result about the previous file must not answer one question
            // about the new one. The worker abandons that generation's pending work when the new request reaches it.
            Result = null;
        }

        scope.Activity?.SetTag(ihc_openvisual.Configuration.AppTelemetryRegistry.Attributes.DocumentGeneration, Generation);

        _seenVersion = version;
        _worker.Notify(new ValidationRequest(snapshot, version, Generation));
        Publish();
    }

    /// <summary>Called through the marshal on the owning thread, for a result that is still current.</summary>
    private void Bind(ValidationOutcome outcome)
    {
        if (_disposed || outcome.Generation != Generation)
            return;

        Result = outcome;
        Publish();
    }

    private void OnFaulted(Exception fault)
    {
        // A crashed rule used to leave NOTHING behind: the exception arrived here and was dropped, so the
        // only symptom was a Problemer panel that quietly stopped updating. The run's own span already
        // carries the faulted outcome and the normalized error type; this is the human-readable half, and it
        // goes through the real ILogger pipeline so it is exported like any other error.
        _logger.LogError(fault, "Validation run faulted for generation {Generation}", Generation);

        // Whatever was bound STAYS bound rather than being dropped: a failed run is not evidence that the
        // previous findings went away, and blanking the gate on a fault would open it on no evidence at all.
        if (!_disposed)
            Publish();
    }

    private readonly ILogger _logger;

    /// <summary>The monitor's entry point into the instrumentation core.</summary>
    private readonly Ihc.OperationTelemetry _telemetry =
        new(ihc_openvisual.Configuration.AppTelemetryRegistry.Surface, nameof(ValidationMonitor));

    private void Publish()
    {
        Changed?.Invoke(this, EventArgs.Empty);

        bool blocking = HasBlockingFindings;
        if (blocking == _wasBlocking)
            return;
        _wasBlocking = blocking;
        BlockingChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _session.StateChanged -= _onStateChanged;
        _worker.Dispose();
    }
}
