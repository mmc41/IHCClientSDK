using System;
using System.Diagnostics;
using System.Reflection;

namespace Ihc {
    /// <summary>
    /// Configuration settings for telemetry and observability.
    /// </summary>
    public class TelemetryConfiguration
    {
        /// <summary>
        /// Configuration key for telemetry settings.
        /// </summary>
        public static readonly string Key = "telemetry";

        /// <summary>
        /// Telemetry collector host address.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Traces endpoint path.
        /// </summary>
        public string Traces { get; set; } = string.Empty;

        /// <summary>
        /// Logs endpoint path.
        /// </summary>
        public string Logs { get; set; } = string.Empty;

        /// <summary>
        /// Additional headers for telemetry requests.
        /// </summary>
        public string Headers { get; set; } = string.Empty;
    }

    /// <summary>
    /// Central telemetry and activity tracing configuration for the IHC client SDK.
    /// </summary>
    public static class Telemetry
    {
        /// <summary>
        /// Name of the activity source for distributed tracing.
        /// </summary>
        public const string ActivitySourceName = "ihcclient";

        /// <summary>
        /// The main activity source for SDK operations.
        /// </summary>
        public static ActivitySource ActivitySource { get; } = new ActivitySource(name: ActivitySourceName, version: VersionInfo.GetSdkVersion());

        /// <summary>
        /// Tag prefix for input parameters in activity tags.
        /// </summary>
        public const string argsTagPrefix = "input.";

        /// <summary>
        /// Tag name for return values in activity tags.
        /// </summary>
        public const string returnValueTag = "retv";

    }

    /// <summary>
    /// Extension methods for Activity to simplify telemetry tagging.
    /// </summary>
    public static class ActivityExtensions
    {
        /// <summary>
        /// Sets a return value tag on the activity.
        /// </summary>
        /// <typeparam name="T">The type of the return value</typeparam>
        /// <param name="activity">The activity to add the tag to (can be null)</param>
        /// <param name="value">The return value to record</param>
        /// <returns>The activity for method chaining</returns>
        public static Activity SetReturnValue<T>(this Activity activity, T value)
        {
            activity?.SetTag(Telemetry.returnValueTag, value);
            return activity;
        }

        /// <summary>
        /// Sets parameter tags on the activity with names prefixed by "input.".
        /// </summary>
        /// <param name="activity">The activity to add the tags to (can be null)</param>
        /// <param name="parameters">Variable number of named parameter tuples</param>
        /// <returns>The activity for method chaining</returns>
        public static Activity SetParameters(this Activity activity, params (string name, object value)[] parameters)
        {
            if (activity != null)
            {
                foreach (var (name, value) in parameters)
                {
                    activity.SetTag($"{Telemetry.argsTagPrefix}{name}", value);
                }
            }
            return activity;
        }

        /// <summary>
        /// Sets error status and exception information on the activity.
        /// </summary>
        /// <param name="activity">The activity to add error information to (can be null)</param>
        /// <param name="ex">The exception that occurred</param>
        /// <returns>The activity for method chaining</returns>
        public static Activity SetError(this Activity activity, Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            return activity;
        }
    }

}


