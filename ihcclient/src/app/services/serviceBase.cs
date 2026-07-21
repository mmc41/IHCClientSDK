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
        protected AppServiceBase()
        {
        }

        protected Activity StartActivity(string operationName)
        {
            Activity activity = Telemetry.ActivitySource.StartActivity(this.GetType().Name + "." + operationName, ActivityKind.Internal);
            activity?.SetTag("service.name", this.GetType().Name); // Set name of IHC webservice highlevel wrapper as telemetry service name.
            activity?.SetTag("service.operation", operationName); // Set name of IHC webservice highlevel wrapper operation.
            return activity;
        }

        // The StartActivity + try/catch + SetError scaffold, once (M2/D02): run <paramref name="body"/> inside a named
        // activity, tag its error on any throw, and rethrow. The body receives the activity so it can record a return
        // value / metric (activity?.SetReturnValue(...)) exactly as the hand-inlined copies did. Currently routed only
        // from ProjectAppService (D02). The async wrappers ConfigureAwait(false) on the wrapper's own trivial
        // continuation — each body keeps its inner awaits (and their configured context) unchanged.

        /// <summary>Runs a synchronous <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected T RunTraced<T>(string operationName, Func<Activity, T> body)
        {
            using Activity activity = StartActivity(operationName);
            try
            {
                return body(activity);
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                throw;
            }
        }

        /// <summary>Runs a synchronous void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected void RunTraced(string operationName, Action<Activity> body)
        {
            using Activity activity = StartActivity(operationName);
            try
            {
                body(activity);
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                throw;
            }
        }

        /// <summary>Runs an async <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected async Task<T> RunTracedAsync<T>(string operationName, Func<Activity, Task<T>> body)
        {
            using Activity activity = StartActivity(operationName);
            try
            {
                return await body(activity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                throw;
            }
        }

        /// <summary>Runs an async void <paramref name="body"/> inside a named activity, tagging + rethrowing on error.</summary>
        protected async Task RunTracedAsync(string operationName, Func<Activity, Task> body)
        {
            using Activity activity = StartActivity(operationName);
            try
            {
                await body(activity).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                activity?.SetError(ex);
                throw;
            }
        }
    }
}