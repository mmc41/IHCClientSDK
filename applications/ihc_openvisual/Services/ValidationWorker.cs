using System;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Vis.Model;
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
public sealed record ValidationOutcome(
    EquatableArray<ValidationFinding> Findings,
    int Version,
    int Generation);

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

    private readonly Func<Project, EquatableArray<ValidationFinding>> _validate;
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
        Func<Project, EquatableArray<ValidationFinding>> validate,
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
            _pending = null;
            _running = true;
            token = _generation.Token;
        }

        // Fire-and-forget, and observed: RunAsync catches everything, so no fault reaches
        // TaskScheduler.UnobservedTaskException and no run can wedge the loop.
        _ = RunAsync(request, token);
    }

    private async Task RunAsync(ValidationRequest request, CancellationToken token)
    {
        try
        {
            // Step 2 and the first half of step 5: an abandoned run never starts.
            EquatableArray<ValidationFinding> findings =
                await Task.Run(() => _validate(request.Snapshot), token);

            // Step 5 again, and step 3. Both are re-checked HERE rather than trusted from before the run: the
            // document is free to move while the pool thread works, and that is the normal case, not the edge.
            if (!token.IsCancellationRequested && IsStillCurrent(request))
                _post(() => _onCompleted(new ValidationOutcome(findings, request.Version, request.Generation)));
        }
        catch (OperationCanceledException)
        {
            // The document was replaced or the worker disposed. Nothing to report — the caller asked for this.
        }
        catch (Exception ex)
        {
            _post(() => _onFaulted?.Invoke(ex));
        }
        finally
        {
            StartFollowUpOrGoIdle();
        }
    }

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
            _pending = null;
            _duringRun = false;
            _running = true;
            token = _generation.Token;
        }

        _ = RunAsync(follow, token);
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
