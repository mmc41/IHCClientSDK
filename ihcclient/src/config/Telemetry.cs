using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Ihc {
    /// <summary>
    /// Configuration settings for telemetry and observability.
    /// </summary>
    public record TelemetryConfiguration
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

        /// <summary>
        /// Reads Telemetry configuiration from IConfiguration
        /// </summary>
        /// <param name="config">The configuration root</param>
        /// <returns>The IHC client settings.</returns>
        public static TelemetryConfiguration GetFromConfiguration(IConfigurationRoot config)
        {
            TelemetryConfiguration telemetryConfig = config.GetSection("telemetry").Get<TelemetryConfiguration>();
            if (telemetryConfig == null)
            {
                throw new InvalidOperationException("Could not read Telemtry settings from configuration");
            }
            return telemetryConfig;
        }
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

        /// <summary>
        /// Adds a warning event to the activity with detailed context information.
        /// Used to report non-fatal issues during operations that don't throw exceptions
        /// but might be important for debugging or monitoring.
        /// </summary>
        /// <param name="activity">The activity to add the warning to (can be null)</param>
        /// <param name="message">Human-readable description of the warning, including any relevant context such as location/path</param>
        /// <param name="tags">Additional context tags as key-value pairs. It's recommended to include a "type" tag to categorize warnings.</param>
        /// <returns>The activity for method chaining</returns>
        /// <remarks>
        /// <para>The warning event is emitted with a generic "Warning" name. Callers should include a "type" tag
        /// to categorize the warning (e.g., ("type", "ComparerFallback")). This allows consumers to filter
        /// warnings generically while still distinguishing between different warning categories via tags.</para>
        /// <para>The message should be self-contained and include all relevant context (e.g., location, path, property name)
        /// to make the warning understandable without needing to parse tags.</para>
        /// </remarks>
        /// <example>
        /// <code>
        /// activity?.AddWarning(
        ///     "Dictionary comparer could not be preserved at path: root.MyProperty",
        ///     ("type", "ComparerFallback"),
        ///     ("sourceType", dict.GetType().FullName),
        ///     ("path", "root.MyProperty"));
        /// </code>
        /// </example>
        public static Activity AddWarning(this Activity activity, string message, params (string key, object value)[] tags)
        {
            if (activity != null)
            {
                var eventTags = new ActivityTagsCollection
                {
                    { "message", message },
                    { "severity", "warning" }
                };

                foreach (var (key, value) in tags)
                {
                    eventTags.Add(key, value);
                }

                activity.AddEvent(new ActivityEvent("Warning", tags: eventTags));
            }
            return activity;
        }
    }

}


