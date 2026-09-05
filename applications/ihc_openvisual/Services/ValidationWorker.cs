using System;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Vis.Model;
using Ihc.Vis.Problems;
using Ihc.Vis.Projects;
using Ihc.Vis.Validation;

namespace ihc_openvisual.Services;

/// <summary>
/// What the owner hands the worker each time the document might have changed: the immutable snapshot to validate
/// and the two keys that say WHICH document state it is.
/// </summary>
/// <param name="Snapshot">The immutable project to run over. Captured by the owner, on its own thread.</param>
/// <param name="Version">The document version the snapshot was read with — captured in the same breath.</param>
/// <param name="Generation">
/// Which DOCUMENT the snapshot belongs to. Opaque here: the worker compares generations for equality and asks
/// nothing else of them. A new generation means the previous file is gone, so its pending and in-flight work is
/// worthless rather than merely stale — deriving that is the owner's job, not this component's.
/// </param>
public readonly record struct ValidationRequest(Project Snapshot, int Version, int Generation);

/// <summary>A completed run's result, carrying the keys it ran for so a binder can re-check them.</summary>
/// <param name="Faults">
/// What the run BROKE, beside what it found. A crashed rule is not a finding about the project — it is the tool
/// failing to answer, and the list is incomplete by exactly the rules that did not run.
/// <para>
/// It travels on the outcome rather than out a side channel so it inherits step 3 unchanged: a superseded run's
/// faults are discarded with its findings. That is deliberate. Half a discard would be worse than either whole
/// one, and a rule that crashed will crash again on the run that superseded it, so nothing is lost that the next
/// outcome does not carry.
/// </para>
/// </param>
public sealed record ValidationOutcome(
    EquatableArray<ValidationFinding> Findings,
    int Version,
    int Generation,
    EquatableArray<InternalError> Faults);

/// <summary>
/// The background validation loop: ADR-001's five-step host contract plus single-flight coalescing.
///
/// <para>The contract, step by step. The owner does step 1 — it captures snapshot and version together on its own
/// thread and hands both here, because two separate reads off that thread can already disagree. This class does
/// the rest: step 2 runs the validate delegate on the pool through <see cref="Task.Run(Func{TResult}, CancellationToken)"/>;
/// step 3 discards a result whose keys are no longer the latest; step 4 marshals the binding through the
/// caller-supplied post delegate; step 5 honours the token both before the run starts and after it completes.</para>
///
/// <para><b>Single-flight.</b> At most ONE run is in flight and at most ONE request is pending, and the pending
/// one always carries the newest snapshot. Without that, a burst of keystrokes queues a run per keystroke and the
/// panel spends its life catching up on states no one is looking at any more. Coalescing is not an optimisation
/// here; it is what makes continuous validation affordable at all.</para>
///
/// <para><b>No Avalonia, no ProjectWorkflow.</b> Everything framework-shaped is a delegate: the compute, the
/// binding callback, the marshal, and the clock. That is deliberate — it keeps the loop drivable entirely from
/// its own tests, on a fake clock, with no shell to construct. It is also what lets the marshal be
/// <c>Dispatcher.UIThread.Post</c> in the app without this file knowing the name.</para>
///
/// <para><b>Cancellation is coarse</b>, exactly as ADR-001 says: a synchronous engine call cannot be interrupted
/// mid-run, so a token is honoured only BETWEEN runs. Latest-wins bounds the waste at one in-flight run — which
/// is why a superseded run still appears in the validate delegate's record and merely binds nothing.</para>
/// </summary>
public sealed class ValidationWorker : IDisposable
{
    /// <summary>The quiet period a burst of changes must clear before a run starts.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(300);

    private readonly Func<Project, StructuredValidationResult> _validate;
    private readonly Action<ValidationOutcome> _onCompleted;
    private readonly Action<Action> _post;
    private readonly Action<Exception>? _onFaulted;
    private readonly TimeSpan _debounce;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();

    /// <summary>
    /// The keys of the last request SEEN — the yardstick a completing run is measured against, and the memory a
    /// save-only notification is recognised by.
    /// </summary>
    /// <remarks>
    /// KEYS, not the request. The snapshot is not needed to answer either question, and this field outlives every
    /// run: holding a <c>Project</c> here would root the whole tree of a document nobody is looking at any more,
    /// for as long as the worker lives. <see cref="_pending"/> is the only place a snapshot is retained, and only
    /// until its run starts.
    /// </remarks>
    private DocumentKey? _latest;

    /// <summary>The request waiting for a run. At most one, always the newest.</summary>
    private ValidationRequest? _pending;

    /// <summary>The activity context that asked for the pending run, for the run's link. Default when none.</summary>
    private System.Diagnostics.ActivityContext _pendingLink;

    private bool _running;

    /// <summary>Set when the quiet period elapsed while a run was in flight, so the completing run knows the
    /// follow-up is due NOW rather than waiting for a timer that has already fired.</summary>
    private bool _duringRun;

    private bool _disposed;
    private CancellationTokenSource _generation = new();
    private TaskCompletionSource _idle = Completed();

    /// <param name="validate">
    /// The compute. Synchronous and thread-agnostic by the SDK contract, so this class is the party that decides
    /// to spend a pool thread on it. It takes no <see cref="CancellationToken"/> on purpose: the engine cannot
    /// honour one mid-run, and ADR-001 forbids publishing a token on a door that ignores it.
    /// </param>
    /// <param name="onCompleted">Invoked through <paramref name="post"/> for a result that is still current.</param>
    /// <param name="post">
    /// The marshal back to the owner's thread — <c>Dispatcher.UIThread.Post</c> in the shell. Taken as a delegate
    /// so this component names no UI framework.
    /// </param>
    /// <param name="time">The clock the quiet period is measured on; a fake one makes every test deterministic.</param>
    /// <param name="onFaulted">
    /// Where a failing run is reported. Optional, but a caller that passes nothing is choosing to drop the fault:
    /// the loop still survives it, because the alternative — letting it escape — leaves an orphaned pool task and
    /// a panel that silently stops updating.
    /// </param>
    /// <param name="debounce">Overrides <see cref="DefaultDebounce"/>.</param>
    public ValidationWorker(
        Func<Project, StructuredValidationResult> validate,
        Action<ValidationOutcome> onCompleted,
        Action<Action> post,
        TimeProvider time,
        Action<Exception>? onFaulted = null,
        TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(validate);
        ArgumentNullException.ThrowIfNull(onCompleted);
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(time);

        _validate = validate;
        _onCompleted = onCompleted;
        _post = post;
        _onFaulted = onFaulted;
        _debounce = debounce ?? DefaultDebounce;
        _timer = time.CreateTimer(_ => OnQuietPeriodElapsed(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Completes when nothing is running and nothing is pending. Tests await it instead of sleeping; the loop
    /// itself never reads it.
    /// </summary>
    public Task Idle
    {
        get
        {
            lock (_gate)
                return _idle.Task;
        }
    }

    /// <summary>
    /// Reports that the document may have moved. Cheap and non-blocking: it records the request and (re)arms the
    /// quiet period, so it is safe to call from the owner's thread on every change event.
    /// </summary>
    /// <remarks>
    /// Three shapes, and the difference between them is the whole point of carrying two keys:
    /// <list type="bullet">
    /// <item>Same generation, SAME version — nothing about the document changed (a save is the case that produces
    /// this). Runs nothing.</item>
    /// <item>Same generation, new version — an ordinary edit. Debounced, then run.</item>
    /// <item>New generation — a different document. Everything in flight or pending for the old one is abandoned
    /// first, because binding its rows into the new project is worse than binding nothing.</item>
    /// </list>
    /// </remarks>
    public void Notify(ValidationRequest request)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (_latest is { } previous)
            {
                if (previous.Generation == request.Generation && previous.Version == request.Version)
                    return;

                if (previous.Generation != request.Generation)
                    AbandonGeneration();
            }

            _latest = new DocumentKey(request.Generation, request.Version);
            _pending = request;
            // Captured HERE, on the notifying thread, because that is the only place the triggering activity
            // is still ambient - the run itself is off-thread and starts with no Activity.Current at all
            // (measured; see the characterization test). It becomes a LINK rather than a parent: a debounced
            // run serves every edit that coalesced into it, so naming one of them its parent would assert a
            // causal ownership that is false for the rest.
            _pendingLink = System.Diagnostics.Activity.Current?.Context ?? default;
            _duringRun = false;
            MarkBusy();
        }

        _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        CancellationTokenSource? abandoned;
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _pending = null;
            _duringRun = false;
            abandoned = _generation;
            _generation = new CancellationTokenSource();
            _idle.TrySetResult();
        }

        _timer.Dispose();
        abandoned.Cancel();
        abandoned.Dispose();
    }

    private void OnQuietPeriodElapsed()
    {
        ValidationRequest request;
        CancellationToken token;
        System.Diagnostics.ActivityContext link;
        lock (_gate)
        {
            if (_disposed || _pending is not { } waiting)
                return;

            // A run is in flight. The pending request stays exactly where it is — that IS the coalescing — and the
            // completing run picks it up, which is what this flag tells it.
            if (_running)
            {
                _duringRun = true;
                return;
            }

            request = waiting;
            link = _pendingLink;
            _pending = null;
            _running = true;
            token = _generation.Token;
        }

        // Fire-and-forget, and observed TWICE over: RunAsync catches everything, so no fault reaches
        // TaskScheduler.UnobservedTaskException and no run can wedge the loop — and the supervisor observes the
        // task itself, so a fault that escaped that catch still reaches the sink instead of the finalizer.
        TaskSupervisor.Fire(
            RunAsync(request, link, token),
            $"{nameof(ValidationWorker)}.{nameof(OnQuietPeriodElapsed)}");
    }

    private async Task RunAsync(ValidationRequest request, System.Diagnostics.ActivityContext link,
        CancellationToken token)
    {
        System.Collections.Generic.IEnumerable<System.Diagnostics.ActivityLink>? links =
            link == default ? null : new[] { new System.Diagnostics.ActivityLink(link) };
        // A TRACE ROOT, forced. The link below is the causal statement this run is entitled to make; being
        // some ambient activity's CHILD is one it is not, and until the shell opened a span around its own
        // composition nothing made that distinction do any work — there simply was no ambient activity to
        // inherit. Now there is, and a timer captures the context it was CREATED in: the debounce timer is
        // built in this worker's constructor, so every run for the life of the application inherited whatever
        // was current at composition. Measured on a live launch: three runs, one of them minutes after
        // start-up, all parented to a start-up span that had closed long before.
        //
        // Cleared without restoring, and the guarantee is this method's OWN shape rather than its caller's: an
        // async method's kickoff restores the execution context after the synchronous prefix, so an ambient
        // change made here cannot escape into whoever called it. That holds for the pool thread the real timer
        // uses AND for a test's fake clock, which fires the callback inline on the advancing thread.
        System.Diagnostics.Activity.Current = null;
        using Ihc.OperationScope scope = _telemetry.Start(
            "Run", System.Diagnostics.ActivityKind.Internal,
            RunMetrics, links);
        try
        {
            // Step 2 and the first half of step 5: an abandoned run never starts.
            StructuredValidationResult result =
                await Task.Run(() => _validate(request.Snapshot), token);

            // Step 5 again, and step 3. Both are re-checked HERE rather than trusted from before the run: the
            // document is free to move while the pool thread works, and that is the normal case, not the edge.
            if (!token.IsCancellationRequested && IsStillCurrent(request))
            {
                RecordOutcome(scope, ihc_openvisual.Configuration.AppTelemetryRegistry.Values.ValidationBound);
                _post(() => _onCompleted(new ValidationOutcome(
                    result.Findings, request.Version, request.Generation, result.Faults)));
            }
            else
            {
                // The run finished, and its answer was already stale. Reporting this as success would make a
                // coalescing storm look like healthy throughput; reporting it as failure would make the
                // debounce working as designed look like breakage. It is its own thing.
                RecordOutcome(scope, ihc_openvisual.Configuration.AppTelemetryRegistry.Values.ValidationSuperseded);
            }
        }
        catch (OperationCanceledException)
        {
            // The document was replaced or the worker disposed. Nothing to report - the caller asked for this.
            RecordOutcome(scope, ihc_openvisual.Configuration.AppTelemetryRegistry.Values.ValidationAbandoned);
        }
        catch (Exception ex)
        {
            RecordOutcome(scope, ihc_openvisual.Configuration.AppTelemetryRegistry.Values.ValidationFaulted);
            scope.SetOutcome(Ihc.OperationOutcome.Failed(ex));
            _post(() => _onFaulted?.Invoke(ex));
        }
        finally
        {
            StartFollowUpOrGoIdle();
        }
    }

    /// <summary>
    /// The run's own four-valued outcome, on the span and as the histogram's dimension. It is deliberately
    /// separate from the operation status the core records: only `faulted` is an ERROR, while superseded and
    /// abandoned are the debounce working exactly as designed.
    /// </summary>
    private static void RecordOutcome(Ihc.OperationScope scope, string outcome)
    {
        scope.AddSharedTag(ihc_openvisual.Configuration.AppTelemetryRegistry.Attributes.ValidationOutcome, outcome);
    }

    /// <summary>The worker's entry point into the instrumentation core.</summary>
    private readonly Ihc.OperationTelemetry _telemetry =
        new(ihc_openvisual.Configuration.AppTelemetryRegistry.Surface, nameof(ValidationWorker));

    /// <summary>The binding is IMMUTABLE and its instruments are static, so it is built once rather than per operation.</summary>
    private static readonly Ihc.MetricBinding RunMetrics =
        Ihc.MetricBinding.For(ihc_openvisual.Configuration.AppTelemetryRegistry.ValidationRunDuration);

    private bool IsStillCurrent(ValidationRequest request)
    {
        lock (_gate)
            return _latest == new DocumentKey(request.Generation, request.Version);
    }

    /// <summary>Which document state a request names, with no snapshot attached.</summary>
    private readonly record struct DocumentKey(int Generation, int Version);

    private void StartFollowUpOrGoIdle()
    {
        ValidationRequest follow;
        CancellationToken token;
        System.Diagnostics.ActivityContext link;
        lock (_gate)
        {
            _running = false;

            // The follow-up runs immediately only when its own quiet period already elapsed while this run was in
            // flight. Otherwise the timer still owes it a tick, and starting now would defeat the debounce.
            if (_disposed || !_duringRun || _pending is not { } waiting)
            {
                if (_pending is null)
                    _idle.TrySetResult();
                return;
            }

            follow = waiting;
            link = _pendingLink;
            _pending = null;
            _duringRun = false;
            _running = true;
            token = _generation.Token;
        }

        TaskSupervisor.Fire(
            RunAsync(follow, link, token),
            $"{nameof(ValidationWorker)}.{nameof(StartFollowUpOrGoIdle)}");
    }

    /// <summary>Cancels everything belonging to the outgoing document. Called under <see cref="_gate"/>.</summary>
    private void AbandonGeneration()
    {
        CancellationTokenSource abandoned = _generation;
        _generation = new CancellationTokenSource();
        _pending = null;
        _duringRun = false;

        // Cancelled while _gate is held, which is safe HERE for a reason worth stating rather than assuming:
        // Cancel() runs its registrations synchronously on this thread, and the only registration on this token
        // is the one Task.Run makes for itself. No callback of ours runs, so nothing can re-enter _gate and
        // deadlock. A token this class ever exposed to arbitrary callbacks would have to be cancelled outside
        // the lock instead.
        abandoned.Cancel();
        abandoned.Dispose();
    }

    /// <summary>Called under <see cref="_gate"/>.</summary>
    private void MarkBusy()
    {
        if (_idle.Task.IsCompleted)
            _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static TaskCompletionSource Completed()
    {
        TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        source.SetResult();
        return source;
    }
}
