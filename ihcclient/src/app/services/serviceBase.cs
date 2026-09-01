using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;
using Ihc.Vis.Problems;

namespace Ihc.App
{
    /// <summary>
    /// Base class for IHC Application services. The services are modular abstractions upon multiple IIHCApiServices that
    /// combined implement a backend for a application for a specific use. All services should be tested with
    /// mocked IIHCApiServices. It is not adviced to mock any application for testing. Mock underlaying IIHCApiServices instead.
    /// </summary>
    public interface IIHCAppService {}
    
    /// <summary>
    /// Base class for IHC Application services
    /// </summary>
    public abstract class AppServiceBase : IIHCAppService
    {
        // One entry point per service instance, named for the concrete type so spans keep reading
        // "<service>.<operation>". Created here rather than per call: the owner cannot change over an
        // instance's life, and minting one per operation would allocate on every traced call.
        private readonly OperationTelemetry telemetry;

        // Where an unexpected fault is REPORTED, as opposed to where it is thrown. Null when the host supplied
        // none, which is every caller that has nowhere to put one.
        private readonly Action<InternalError>? faultPort;

        protected AppServiceBase()
            : this(null)
        {
        }

        /// <summary>
        /// Builds the service with a FAULT PORT: an unexpected fault escaping a traced operation is minted as
        /// <c>internal.unexpected</c> and handed to <paramref name="faultPort"/> before the original exception
        /// continues on its way.
        /// <para>
        /// A port rather than a return value, because these operations already have a contract — they throw —
        /// and changing that would move every caller. Reporting is a second, additive channel: the exception is
        /// rethrown UNCHANGED, the same instance with the same stack, so nothing downstream can tell the
        /// difference except that the fault was also seen.
        /// </para>
        /// </summary>
        /// <param name="faultPort">Where to report an unexpected fault, or null to report nowhere.</param>
        protected AppServiceBase(Action<InternalError>? faultPort)
        {
            // Reported from the CORE's catch, which already runs for every traced operation, rather than from a
            // second catch wrapped around each RunTraced overload. Four such catches were four places the rule
            // had to be remembered, and they turned the two async wrappers into state machines for a rethrow the
            // core had already done.
            this.faultPort = faultPort;
            telemetry = new OperationTelemetry(
                SdkTelemetryRegistry.Surface, this.GetType().Name, ReportUnexpected);
        }

        /// <summary>
        /// The same port, for a component this service OWNS that mints its own fault rather than throwing one.
        /// <para>
        /// <see cref="ReportUnexpected"/> cannot serve those: it fires only when an exception ESCAPES a traced
        /// operation, and it mints <c>internal.unexpected</c>. A layer that catches its own exception, converts
        /// it into a value and hands that value back — the document session's failed edit — never escapes and
        /// already has a code of its own. It reports what it minted, through here.
        /// </para>
        /// <para>
        /// <c>private protected</c>: the components that mint their own faults are this assembly's, and a
        /// <c>protected</c> member on a public base would publish the port to anyone deriving from outside.
        /// </para>
        /// </summary>
        private protected Action<InternalError>? FaultPort => faultPort;

        /// <summary>
        /// Mints <c>internal.unexpected</c> for <paramref name="failure"/> and offers it to the port.
        /// <para>
        /// The <c>{operation}</c> slot binds from the operation name each <c>RunTraced</c> overload already
        /// takes, so no site passes a duplicated literal and no scope member had to be invented to carry it.
        /// </para>
        /// <para>
        /// FAIL-OPEN, deliberately: a port that throws must not turn a reportable fault into a second, worse one
        /// on top of the caller's original. There is nowhere else to put that — this layer has no logger by
        /// design — so it is dropped, and the caller's own exception continues untouched.
        /// </para>
        /// </summary>
        private void ReportUnexpected(string operationName, Exception failure)
        {
            if (faultPort is not { } port)
            {
                return;
            }
            // Claimed BEFORE reporting, and only when there is a port to report to: the exception is rethrown
            // unchanged, so a host catch one level up sees the same instance and would otherwise record a second
            // row for one fault. This layer is the innermost, and the only one that can name the operation.
            if (!InternalError.ClaimReport(failure))
            {
                return;
            }
            try
            {
                Problem problem = Problem.Unexpected(operationName, failure.Message, failure);
                port(InternalError.From(problem, InternalErrorOrigin.Sdk, failure.ToString()));
            }
            catch (Exception)
            {
                // See the fail-open note above.
            }
        }

        /// <summary>
        /// Starts a span named <c>&lt;service&gt;.&lt;operation&gt;</c>. Kept for its shipped signature; new
        /// work uses <c>RunTraced</c>/<c>RunTracedAsync</c> instead, which also record the outcome, the
        /// normalized error type and any bound instruments.
        /// </summary>
        /// <remarks>
        /// Returning a bare <see cref="Activity"/> is what limits this helper: the caller owns the
        /// <c>using</c>, so the helper cannot be there when the operation ENDS - and an outcome, a duration
        /// and a metric can only be recorded then. That is why it delegates the naming and nothing else.
        /// </remarks>
        protected Activity? StartActivity(string operationName) => telemetry.StartSpan(operationName);

        // The scaffold, delegated to the instrumentation core: the core owns the span's name and timing, the
        // outcome on both signals, the normalized error.type and the fail-open guarantee, so this layer no
        // longer restates any of it. The body still receives the Activity rather than the scope, because that
        // is the shipped signature every ProjectAppService method is written against — a body that wants a
        // metric dimension supplies a binding instead.
        //
        // The optional binding is what "paired emission" means here: pass one and the operation's duration and
        // occurrence are recorded from the SAME operation as the span, carrying the same status and error type.

        // The shipped signatures keep exactly their shipped shape. An optional parameter would have been
        // source-compatible but is a DIFFERENT symbol, which retires the shipped entry - so the binding is a
        // separate overload rather than a default argument.

        /// <summary>Runs a synchronous <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected T RunTraced<T>(string operationName, Func<Activity?, T> body) =>
            RunTraced(operationName, body, null);

        /// <summary>Runs a synchronous <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected T RunTraced<T>(string operationName, Func<Activity?, T> body, MetricBinding? metrics) =>
            telemetry.Run(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs a synchronous void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected void RunTraced(string operationName, Action<Activity?> body) =>
            RunTraced(operationName, body, null);

        /// <summary>Runs a synchronous void <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected void RunTraced(string operationName, Action<Activity?> body, MetricBinding? metrics) =>
            telemetry.Run(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs an async <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected Task<T> RunTracedAsync<T>(string operationName, Func<Activity?, Task<T>> body) =>
            RunTracedAsync(operationName, body, null);

        /// <summary>Runs an async <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected Task<T> RunTracedAsync<T>(
            string operationName, Func<Activity?, Task<T>> body, MetricBinding? metrics) =>
            telemetry.RunAsync(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs an async void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected Task RunTracedAsync(string operationName, Func<Activity?, Task> body) =>
            RunTracedAsync(operationName, body, null);

        /// <summary>Runs an async void <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected Task RunTracedAsync(string operationName, Func<Activity?, Task> body, MetricBinding? metrics) =>
            telemetry.RunAsync(operationName, scope => body(scope.Activity), metrics);
    }
}