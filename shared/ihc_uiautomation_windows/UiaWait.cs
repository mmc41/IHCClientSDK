using System;
using System.Diagnostics;
using System.Threading;

namespace Ihc.UiAutomation;

/// <summary>
/// What a bounded wait saw — the value it settled on, and enough of the attempt to classify a failure.
/// </summary>
/// <typeparam name="T">What the probe produces.</typeparam>
/// <param name="Satisfied">Whether the condition held before the wait ended.</param>
/// <param name="Value">The value the condition held for, or the last one probed when it never held.</param>
/// <param name="Elapsed">How long the wait actually took.</param>
/// <param name="Polls">How many times the probe ran. One means the wait never got to look a second time.</param>
/// <param name="LastSeen">
/// The last probed value rendered for a human, or the reason the wait was abandoned. This is the field that
/// turns a timeout from "the condition never held" into a sentence naming the cause.
/// </param>
public readonly record struct UiaWaitResult<T>(
    bool Satisfied,
    T? Value,
    TimeSpan Elapsed,
    int Polls,
    string LastSeen)
{
    /// <summary>A one-line account of the attempt, for a refusal message or an assertion failure.</summary>
    public override string ToString() =>
        Satisfied
            ? $"satisfied on poll {Polls} after {(long)Elapsed.TotalMilliseconds} ms"
            : $"never satisfied: polled {Polls} times over {(long)Elapsed.TotalMilliseconds} ms; last saw {LastSeen}";
}

/// <summary>
/// Bounded waits over a condition the caller probes, and a diagnostic when one is not met.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Every other synchronization in a driver built on this toolkit is the caller's
/// fixed sleep. A sleep bounds nothing: shorter than the work, it reads a state that has not happened yet;
/// longer, it is waste — and which of the two it is changes with the machine. A poll against a condition
/// cannot return before the thing it waits for has happened, which is a correctness property rather than a
/// speed one: a wait on a real signal may well take LONGER than the sleep it replaces.</para>
///
/// <para><b>Why the diagnostic is half of it.</b> A timeout that says only "the condition never held" makes
/// somebody re-derive the cause by hand from a run that no longer exists. One that says how many times it
/// looked, over how long, and what it last saw usually names the cause outright — which is the same reason
/// this toolkit's callers report "rows seen: …" rather than "no match".</para>
///
/// <para><b>A destroyed element is not an exception here.</b> Every <see cref="UiaElement"/> property read
/// already fails soft, so a destroyed element reads as its default and
/// <see cref="UiaElement.ProcessId"/> reads as ZERO — the signal that what is being waited on is gone rather
/// than merely late. This class cannot tell those apart on the caller's behalf; a probe that cares should
/// surface it through <c>describe</c>, so the timeout line says "gone" instead of showing a plausible-looking
/// default. A probe that throws is left to throw: swallowing it would turn a real fault into a timeout whose
/// diagnostic points at the wrong thing.</para>
///
/// <para><b>Platform.</b> Nothing in this type calls Windows. It carries the assembly's
/// <c>SupportedOSPlatform("windows6.1")</c> anyway, because everything that calls it does, and narrowing the
/// contract for one type would trade a guarantee away for nothing.</para>
/// </remarks>
public static class UiaWait
{
    /// <summary>
    /// How often a wait looks again when the caller states no interval of its own. Polling a published value
    /// this often is cheap next to a cross-process property read, and invisible next to a human gesture.
    /// </summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(50);

    private const string ConditionHeld = "true";

    /// <summary>
    /// Polls <paramref name="probe"/> until it yields a value <paramref name="satisfied"/> accepts, the
    /// timeout runs out, or <paramref name="giveUp"/> says further looking is pointless.
    /// </summary>
    /// <remarks>
    /// The probe always runs at least once, so a zero timeout is "look now" rather than "do not look". The last
    /// look is taken AT the deadline, never a poll interval past it: each sleep is clamped to the time left, so
    /// a long interval bounds nothing, and a condition that first holds after the deadline is reported as never
    /// having held. What remains visible is scheduler slop — a sleep returns late by a timer tick, never early —
    /// which is not something a wait can promise away.
    /// </remarks>
    /// <typeparam name="T">What the probe produces. A null probe result means "not there yet".</typeparam>
    /// <param name="probe">Reads the current state. Null when there is nothing to judge yet.</param>
    /// <param name="satisfied">What makes a probed value the one being waited for. Never called with null.</param>
    /// <param name="timeout">How long to keep looking.</param>
    /// <param name="poll">How long to wait between looks. <see cref="DefaultPollInterval"/> when omitted.</param>
    /// <param name="describe">
    /// Renders a probed value for the timeout line. Without one the value's own <c>ToString</c> is used, which
    /// for most UI-Automation elements says nothing useful — supply one wherever a failure has to be readable.
    /// </param>
    /// <param name="giveUp">
    /// Checked before every look: a non-null return is the reason to stop early, and becomes
    /// <see cref="UiaWaitResult{T}.LastSeen"/>. This is how a dead process reports as "exited with code 1"
    /// after a millisecond rather than as a timeout after ten seconds.
    /// </param>
    public static UiaWaitResult<T> Until<T>(
        Func<T?> probe,
        Func<T, bool> satisfied,
        TimeSpan timeout,
        TimeSpan? poll = null,
        Func<T?, string>? describe = null,
        Func<string?>? giveUp = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(satisfied);

        TimeSpan interval = poll ?? DefaultPollInterval;
        Stopwatch clock = Stopwatch.StartNew();
        int polls = 0;
        T? last = null;

        while (true)
        {
            if (giveUp?.Invoke() is { } reason)
                return new UiaWaitResult<T>(false, last, clock.Elapsed, polls, reason);

            polls++;
            last = probe();

            if (last is not null && satisfied(last))
                return new UiaWaitResult<T>(true, last, clock.Elapsed, polls, Render(last, describe));

            TimeSpan remaining = timeout - clock.Elapsed;
            if (remaining <= TimeSpan.Zero)
                return new UiaWaitResult<T>(false, last, clock.Elapsed, polls, Render(last, describe));

            // Clamped to what is left, and rounded UP to the millisecond a sleep is granular to, so the next look
            // lands on the deadline rather than a whole interval — or a sub-millisecond spin — beyond it.
            // Unclamped, a wait polling every second against a 100 ms timeout looked once, slept the second out,
            // and then reported as satisfied whatever happened to hold nine hundred milliseconds late.
            double sleepMs = Math.Min(interval.TotalMilliseconds, Math.Ceiling(remaining.TotalMilliseconds));
            Thread.Sleep((int)sleepMs);
        }
    }

    /// <summary>
    /// Polls <paramref name="condition"/> until it holds or the timeout runs out.
    /// </summary>
    /// <param name="condition">The condition being waited for.</param>
    /// <param name="timeout">How long to keep looking.</param>
    /// <param name="poll">How long to wait between looks. <see cref="DefaultPollInterval"/> when omitted.</param>
    /// <param name="giveUp">The reason to stop early, as on the generic overload.</param>
    public static UiaWaitResult<bool> Until(
        Func<bool> condition,
        TimeSpan timeout,
        TimeSpan? poll = null,
        Func<string?>? giveUp = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        // Expressed through the generic form rather than repeated: one loop, one deadline, one diagnostic. A
        // bool has nothing to describe beyond itself, so the rendering is the word.
        UiaWaitResult<string> held = Until(
            () => condition() ? ConditionHeld : null,
            _ => true,
            timeout,
            poll,
            value => value ?? "false",
            giveUp);

        return new UiaWaitResult<bool>(held.Satisfied, held.Satisfied, held.Elapsed, held.Polls, held.LastSeen);
    }

    private static string Render<T>(T? value, Func<T?, string>? describe)
        where T : class =>
        describe is not null ? describe(value) : value?.ToString() ?? "nothing";
}
