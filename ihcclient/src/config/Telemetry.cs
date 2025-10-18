using System;
using System.Diagnostics;

namespace Ihc {
    public class TelemetryConfiguration
    {
        public static readonly string Key = "telemetry";
        public string Host { get; set; } = string.Empty;
        public string Traces { get; set; } = string.Empty;
        public string Logs { get; set; } = string.Empty;
        public string Headers { get; set; } = string.Empty;
    }

    public static class Telemetry
    {
        public const string ActivitySourceName = "ihcclient";
        public static ActivitySource ActivitySource { get; } = new ActivitySource(ActivitySourceName);

        public const string argsTagPrefix = "input.";
        public const string returnValueTag = "retv";

    }

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

        public static Activity SetError(this Activity activity, Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            return activity;
        }
    }

}


