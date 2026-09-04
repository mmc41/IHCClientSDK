using System;

namespace ihc_openvisual.Services;

/// <summary>
/// Publishes the <see cref="AutomationSnapshot"/> whenever the state it describes moves — and only when the
/// application was started with the test-surface flag.
/// </summary>
/// <remarks>
/// <para><b>The flag gates PUBLICATION, never BEHAVIOUR.</b> With it or without it the application computes the
/// same values, takes the same code paths and reaches the same domain state and user-visible behaviour for the
/// same input. The only difference is whether one accessibility property is populated. There is no test-mode
/// branch in any behaviour path, and nothing downstream reads the flag — which is what keeps this in the
/// "command line at startup" tier rather than the "runtime conditional inside the behaviour path" one.</para>
///
/// <para><b>Off is INERT, not empty.</b> A disabled publisher subscribes to nothing and never calls its output
/// port, so a user's session pays one boolean read at start-up and nothing else, ever — no property write, no
/// change notification, and so nothing for an assistive technology to announce.</para>
///
/// <para><b>Why a value rather than a static.</b> <c>enabled</c> arrives as a constructor argument so a test can
/// construct the publisher in EITHER state, with no process, no <c>Main</c> and no static to reset. A gate
/// tested only in its enabled state is not tested; reading a static set by <c>Main</c> would have left the
/// enabled half unwritable, because nothing that hosts this window in a test runs <c>Main</c> at all.</para>
///
/// <para><b>Why it names no Avalonia type.</b> Where the string GOES is the composition root's business — it
/// arrives as an <see cref="Action{T}"/>, exactly as <c>ProjectWorkflow</c> takes its post and its fault sink.
/// That is what lets this class's own logic be tested without a window.</para>
///
/// <para><b>One field is EVENTUALLY consistent, and it is worth knowing which.</b> The workflow and the
/// validation monitor raise their events on the thread that changed them, so <c>gen</c>, <c>ver</c>,
/// <c>val</c>, <c>dirty</c> and <c>doc</c> are republished synchronously with the change they describe. A fault
/// can be appended from any thread, so <see cref="InternalErrorLog"/> announces through the host's marshal
/// instead — which means <c>faults</c> and <c>fault</c> are rewritten one dispatcher turn after the fault
/// itself. A reader that must not miss the latest fault has to let the dispatcher run first; in a live
/// application it always is.</para>
/// </remarks>
internal sealed class AutomationSnapshotPublisher : IDisposable
{
    private readonly Action<string>? _publish;
    private readonly ProjectWorkflow _session;
    private readonly InternalErrorLog _faults;
    private bool _disposed;

    /// <param name="enabled">Whether to publish at all. False means inert: no subscription and no write.</param>
    /// <param name="publish">Where the rendered snapshot goes.</param>
    /// <param name="session">The workflow the generation, edit state, dirty flag and document name come from.</param>
    /// <param name="faults">The cumulative fault record.</param>
    public AutomationSnapshotPublisher(
        bool enabled, Action<string> publish, ProjectWorkflow session, InternalErrorLog faults)
    {
        ArgumentNullException.ThrowIfNull(publish);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(faults);

        _session = session;
        _faults = faults;
        if (!enabled)
        {
            return;
        }

        _publish = publish;
        session.StateChanged += OnStateChanged;
        session.Validation.Changed += OnStateChanged;
        faults.Changed += OnStateChanged;

        // Once at construction, so a driver that reads before anything has moved sees the state rather than an
        // absent property — which would be indistinguishable from the surface being switched off.
        Publish();
    }

    /// <summary>The snapshot as it stands, whether or not publication is enabled.</summary>
    /// <remarks>
    /// Readable in both states on purpose: the difference the flag makes is whether the value is PUBLISHED, and
    /// a class that could not even compute it when disabled would be a second difference nobody asked for.
    /// </remarks>
    public AutomationSnapshot Current
    {
        get
        {
            // Both fault fields out of ONE reading. Asked twice, the count and the code could come from
            // different moments, which is the very thing the tally is a single value to prevent.
            InternalErrorTally tally = _faults.Tally;
            ValidationOutcome? bound = _session.Validation.Result;
            return new AutomationSnapshot(
                _session.Validation.Generation,
                _session.Version,
                bound?.Generation,
                bound?.Version,
                _session.IsDirty,
                tally.Appended,
                tally.LastCode,
                _session.DocumentName);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_publish is null)
        {
            return;   // Never subscribed, so there is nothing to detach.
        }

        _session.StateChanged -= OnStateChanged;
        _session.Validation.Changed -= OnStateChanged;
        _faults.Changed -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => Publish();

    private void Publish()
    {
        if (!_disposed && _publish is { } publish)
        {
            publish(Current.Format());
        }
    }
}
