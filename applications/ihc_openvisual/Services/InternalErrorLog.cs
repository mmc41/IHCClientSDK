using System;
using System.Collections.Generic;
using ihc_openvisual.Configuration;
using Ihc.Vis.Problems;

namespace ihc_openvisual.Services;

/// <summary>One distinct fault the application has seen, and how many times.</summary>
/// <param name="Error">The fault as first observed — its timestamp is the FIRST sighting, which with
/// <paramref name="Occurrences"/> says more than the latest one would.</param>
/// <param name="Occurrences">How many times this exact fault has been appended.</param>
public sealed record InternalErrorRow(InternalError Error, int Occurrences);

/// <summary>The cumulative fault record: every fault the process has seen, and the last one's code.</summary>
/// <remarks>
/// <see cref="InternalErrorLog.Rows"/> answers "what is wrong NOW", and is shaped for a person reading a panel:
/// capacity-bounded, occurrence-collapsed, and cleared when a project is loaded. Each of those makes it
/// unusable as the oracle for "nothing faulted while this ran" — a count that is not a count of faults, over a
/// window that ends at the last document change. This is the same observation with none of those properties, so
/// something outside the process can take a baseline and compare.
/// </remarks>
/// <param name="Appended">
/// How many faults have been appended since the process started — every occurrence, including the repeats a row
/// folds into its count and the rows the ring has since dropped. Never reset.
/// </param>
/// <param name="LastCode">
/// The most recent fault's code, or <see langword="null"/> when nothing has faulted. A moved count says only
/// THAT something faulted; this says WHICH. A SET of every code seen could not: a repeat of an already-seen code
/// moves the count and leaves the set identical, so a delta would have no diagnostic beside it.
/// </param>
public readonly record struct InternalErrorTally(long Appended, string? LastCode);

/// <summary>
/// Where the application's own faults collect — the one place a fault in the TOOL can be reported to, as
/// distinct from a finding about the project.
///
/// <para><b>Bounded, because a fault storm is the normal shape of a bad day.</b> A handler that faults once
/// usually faults on every gesture, and an unbounded list would exhaust memory and make the panel unreadable at
/// the same time. The ring drops the OLDEST distinct fault when it is full: the newest are the ones a user is
/// about to describe to someone.</para>
///
/// <para><b>De-duplicated by code and captured detail</b>, with a count. The same fault ten times is one row that
/// says ten, not ten rows that say the same thing — which is what makes the bound generous rather than tight.</para>
///
/// <para><b>Thread-safe on append.</b> The dispatcher, the unobserved-task layer and <c>AppDomain</c> all
/// deliver faults from threads that are not the UI's, so appending takes a lock and the <see cref="Changed"/>
/// announcement is marshalled back through the host's own post.</para>
/// </summary>
public sealed class InternalErrorLog
{
    /// <summary>How many DISTINCT faults are kept. Distinct, not total — repeats cost a counter, not a slot.</summary>
    public const int DefaultCapacity = 50;

    private readonly object _gate = new();
    private readonly List<InternalErrorRow> _rows = [];
    private readonly Action<Action> _post;
    private readonly int _capacity;
    // Null until a generation has been followed at all. The FIRST one establishes the baseline rather
    // than clearing: the app always opens a document at start-up, so treating 0 -> 1 as a move would
    // wipe exactly the start-up faults that are hardest to reproduce and most worth keeping.
    private int? _generation;
    private long _appended;
    private string? _lastCode;

    /// <param name="post">
    /// The marshal back to the owning thread, supplied by the composition root because that is the only layer
    /// allowed to name a UI framework. A caller that omits it gets inline invocation, which is right in a
    /// single-threaded test and wrong anywhere else.
    /// </param>
    /// <param name="capacity">How many distinct faults to keep.</param>
    public InternalErrorLog(Action<Action>? post = null, int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _post = post ?? (action => action());
        _capacity = capacity;
    }

    /// <summary>Raised after the rows change, on the owning thread.</summary>
    public event EventHandler? Changed;

    /// <summary>The faults seen, oldest first. A snapshot: it does not change under a reader.</summary>
    public IReadOnlyList<InternalErrorRow> Rows
    {
        get
        {
            lock (_gate)
            {
                return [.. _rows];
            }
        }
    }

    /// <summary>
    /// The cumulative record, read atomically so the count and the code can never come from different moments.
    /// Unlike <see cref="Rows"/> it survives <see cref="FollowGeneration"/>, the capacity bound and de-duplication.
    /// </summary>
    public InternalErrorTally Tally
    {
        get
        {
            lock (_gate)
            {
                return new InternalErrorTally(_appended, _lastCode);
            }
        }
    }

    /// <summary>Records a fault. Safe to call from any thread.</summary>
    /// <param name="error">The fault to record.</param>
    public void Append(InternalError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        lock (_gate)
        {
            // Before the ring, and outside every rule it applies: the tally counts what HAPPENED, where the
            // rows describe what is worth showing. A repeat that costs a row only a counter still costs the
            // tally a fault, and so does one whose row the ring is about to drop.
            _appended++;
            _lastCode = error.Code.Value;

            int at = _rows.FindIndex(row => Same(row.Error, error));
            if (at >= 0)
            {
                // The row keeps its POSITION and its first-seen error; only the count moves. A repeat that
                // jumped to the end would make a storm reorder the list under the reader on every fault.
                _rows[at] = _rows[at] with { Occurrences = _rows[at].Occurrences + 1 };
            }
            else
            {
                if (_rows.Count == _capacity)
                {
                    _rows.RemoveAt(0);
                }
                _rows.Add(new InternalErrorRow(error, 1));
            }
        }
        // Counted per OCCURRENCE, including the repeats the ring folds into one row: the list answers "what is
        // wrong", and the metric answers "how often", which is the question a row with a count cannot answer
        // once it has been cleared by a project load.
        AppTelemetryRegistry.InternalErrorObserved.Add(1,
            new KeyValuePair<string, object?>(AppTelemetryRegistry.Attributes.ProblemCode, error.Code.Value),
            new KeyValuePair<string, object?>(
                AppTelemetryRegistry.Attributes.InternalErrorOrigin, error.Origin.ToString()));
        Announce();
    }

    /// <summary>
    /// Follows the validation generation, clearing when it moves — an internal error lives for the SESSION and
    /// is cleared when a project is created or loaded (D02).
    /// <para>
    /// The generation is the trigger, NOT a claim that opening a project has any bearing on whether a GLib
    /// fault, a clipboard fault or a dispatcher fault still matters. It plainly does not. What was chosen is a
    /// lifetime the user can PREDICT — the same moment the findings list resets — over one that is exactly
    /// right per fault and explicable to nobody. A failed open does not bump the generation, so the only row
    /// lost is one superseded by deliberately loading another project.
    /// </para>
    /// <para>
    /// The FIRST generation followed clears nothing. The application opens a document as it starts, so a sink
    /// that treated that first bump as a move would discard every fault raised while starting up — which is
    /// precisely the set with no other record and the least chance of being reproduced.
    /// </para>
    /// <para>
    /// <see cref="Tally"/> is NOT cleared here, and that is the whole reason it exists separately. A fault
    /// erased by a document change is exactly the one a run that only reads <see cref="Rows"/> would report
    /// nothing about.
    /// </para>
    /// </summary>
    /// <param name="generation">The monitor's current generation.</param>
    public void FollowGeneration(int generation)
    {
        bool cleared;
        lock (_gate)
        {
            cleared = _generation is { } previous && generation != previous && _rows.Count > 0;
            _generation = generation;
            if (cleared)
            {
                _rows.Clear();
            }
        }
        if (cleared)
        {
            Announce();
        }
    }

    // Two faults are the same fault when they carry the same code and the same captured detail. The timestamp
    // is deliberately not part of it: every occurrence has a different one, which would make dedupe impossible.
    private static bool Same(InternalError left, InternalError right) =>
        left.Code == right.Code && string.Equals(left.Detail, right.Detail, StringComparison.Ordinal);

    private void Announce() => _post(() =>
    {
        if (Changed is not { } announcement)
        {
            return;
        }

        // ONE SUBSCRIBER AT A TIME, because the subscribers are not interchangeable. Raised as a single
        // multicast call, the first one that throws ends the invocation and everything attached after it is
        // never told: the panel attaches while the view-model is built and the automation snapshot attaches
        // after it, so the published fault count would stop moving while faults kept arriving, and a run
        // reading that count would report the absence of an observer as the absence of faults.
        foreach (Delegate subscriber in announcement.GetInvocationList())
        {
            try
            {
                ((EventHandler)subscriber)(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // FAIL-OPEN, and this is the last place in the application where that can be said: a subscriber
                // that throws while being told about a fault would otherwise destroy the record of the fault it
                // was being told about. There is nothing above this to report to — that is what makes it the sink.
            }
        }
    });
}
