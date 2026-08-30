using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Ihc.Envelope;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Encodings.Web;

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

        protected AppServiceBase()
        {
            telemetry = new OperationTelemetry(SdkTelemetryRegistry.Surface, this.GetType().Name);
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
        protected Activity StartActivity(string operationName) => telemetry.StartSpan(operationName);

        // The scaffold, delegated to the instrumentation core: the core owns the span's name and timing, the
        // outcome on both signals, the normalized error.type and the fail-open guarantee, so this layer no
        // longer restates any of it. The body still receives the Activity rather than the scope, because that
        // is the shipped signature every ProjectAppService method is written against — a body that wants a
        // metric dimension supplies a binding instead.
        //
        // The optional binding is what "paired emission" means here: pass one and the operation's duration and
        // occurrence are recorded from the SAME operation as the span, carrying the same status and error type.

        // The four shipped signatures keep exactly their shipped shape. An optional parameter would have been
        // source-compatible but is a DIFFERENT symbol, which retires the shipped entry - so the binding is a
        // separate overload rather than a default argument.

        /// <summary>Runs a synchronous <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected T RunTraced<T>(string operationName, Func<Activity, T> body) =>
            RunTraced(operationName, body, null);

        /// <summary>Runs a synchronous <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected T RunTraced<T>(string operationName, Func<Activity, T> body, MetricBinding metrics) =>
            telemetry.Run(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs a synchronous void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected void RunTraced(string operationName, Action<Activity> body) =>
            RunTraced(operationName, body, null);

        /// <summary>Runs a synchronous void <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected void RunTraced(string operationName, Action<Activity> body, MetricBinding metrics) =>
            telemetry.Run(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs an async <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected Task<T> RunTracedAsync<T>(string operationName, Func<Activity, Task<T>> body) =>
            RunTracedAsync(operationName, body, null);

        /// <summary>Runs an async <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected Task<T> RunTracedAsync<T>(string operationName, Func<Activity, Task<T>> body, MetricBinding metrics) =>
            telemetry.RunAsync(operationName, scope => body(scope.Activity), metrics);

        /// <summary>Runs an async void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected Task RunTracedAsync(string operationName, Func<Activity, Task> body) =>
            RunTracedAsync(operationName, body, null);

        /// <summary>Runs an async void <paramref name="body"/>, also recording <paramref name="metrics"/>.</summary>
        protected Task RunTracedAsync(string operationName, Func<Activity, Task> body, MetricBinding metrics) =>
            telemetry.RunAsync(operationName, scope => body(scope.Activity), metrics);
    }
}