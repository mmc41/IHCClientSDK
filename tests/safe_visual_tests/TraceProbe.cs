using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ihc.Tests.Shared;

namespace safe_visual_tests;

/// <summary>
/// A test-owned activity that says which captured spans are THIS test's.
///
/// <para><b>Why a telemetry test needs one at all.</b> <see cref="TelemetryCapture"/>'s listener is
/// PROCESS-WIDE — <c>ShouldListenTo</c> matches an instrumentation scope, not an owner — so every span the
/// whole assembly emits from that scope lands in every open capture. A fixture that then picks its span with
/// <c>Single(…)</c> over an operation name, or positionally with an <c>OrderBy(StartTimeUtc).Last()</c>, is
/// asserting that no other live workflow emitted one; a straggler from anywhere makes the first throw and the
/// second silently examine somebody else's span. The sort does not save it either:
/// <see cref="Activity.StartTimeUtc"/> is <c>DateTime.UtcNow</c>, whose granularity on Windows is ~15.6 ms, so
/// spans that start in the same tick tie and the "last" one is whichever order the queue happened to hold.</para>
///
/// <para><b>What identifies a span instead.</b> Everything this test causes is reachable from the probe by one
/// of two edges, and both are the product's own:</para>
/// <list type="bullet">
/// <item>A span started while the probe is <see cref="Activity.Current"/> becomes its descendant and inherits
/// its <see cref="Activity.TraceId"/> — the ordinary case, since the operations run on the calling thread.</item>
/// <item>A span the product deliberately LINKS rather than parents starts a trace of its own. The debounced
/// validation run is the one that does this, and for a good reason: one run serves every edit that coalesced
/// into it, so naming one of them its parent would assert an ownership false for the rest. Its own descendants
/// (the panel's bind, posted from inside the run) sit under that second trace.</item>
/// </list>
///
/// <para>So a span is this test's when its trace is the probe's trace, or a trace whose span links back to the
/// probe. That is <see cref="Owns"/>, and it uses the very mechanism under test rather than a clock.</para>
/// </summary>
internal sealed class TraceProbe : IDisposable
{
    /// <summary>A source of its own, so the probe is never mistaken for product signal.</summary>
    private const string SourceName = "Ihc.TestProbe";

    private static readonly ActivitySource Source = new(SourceName);

    private readonly ActivityListener _listener;
    private readonly Activity _activity;

    private TraceProbe(string name)
    {
        // An ActivitySource with no listener creates NOTHING, so the probe needs one of its own — the capture's
        // listener is bound to the product scope and would never sample this.
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
        _activity = Source.StartActivity(name)
            ?? throw new InvalidOperationException("the probe listener did not sample the probe activity");
    }

    /// <summary>
    /// Opens a probe and makes it ambient. Everything the test then does runs under it, so keep it alive for
    /// exactly the operations whose spans the assertions are about.
    /// </summary>
    public static TraceProbe Start(string name = "Probe") => new(name);

    /// <summary>The probe's own span id — what a linking span points back at.</summary>
    public ActivitySpanId SpanId => _activity.SpanId;

    /// <summary>Whether <paramref name="span"/> belongs to this test. See the class remarks for the rule.</summary>
    public bool Owns(TelemetryCapture capture, Activity span) => Traces(capture).Contains(span.TraceId);

    /// <summary>Every captured span of one operation that belongs to this test, in the order they started.</summary>
    public IReadOnlyList<Activity> SpansNamed(TelemetryCapture capture, string operationName) =>
        [.. capture.SpansNamed(operationName).Where(s => Owns(capture, s))];

    /// <summary>Every captured span that belongs to this test, in the order the operations ENDED.</summary>
    public IReadOnlyList<Activity> Spans(TelemetryCapture capture) =>
        [.. capture.Spans.Where(s => Owns(capture, s))];

    /// <summary>
    /// THE span of one operation in this test — and a failure when there is not exactly one, which is the
    /// honest outcome: two means the test caused the operation twice and has to say which it means.
    /// </summary>
    public Activity Span(TelemetryCapture capture, string operationName) =>
        SpansNamed(capture, operationName).Single();

    /// <summary>The traces this test owns: its own, plus every trace opened by a span that links back to it.</summary>
    private HashSet<ActivityTraceId> Traces(TelemetryCapture capture)
    {
        HashSet<ActivityTraceId> traces = [_activity.TraceId];
        foreach (Activity span in capture.Spans)
        {
            if (span.Links.Any(l => l.Context.SpanId == SpanId))
            {
                traces.Add(span.TraceId);
            }
        }

        return traces;
    }

    public void Dispose()
    {
        _activity.Dispose();
        _listener.Dispose();
    }
}
