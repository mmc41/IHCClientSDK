using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
        /// The exact URL the startup connectivity self-check probes. Setting this ENABLES the check;
        /// leaving it empty disables it. The check probes the endpoint once at startup and reports the
        /// result, so a wrong endpoint or bad Authorization header fails loudly instead of the OTLP
        /// exporter dropping all telemetry silently. Typically the <see cref="Traces"/> or
        /// <see cref="Logs"/> OTLP endpoint - they share host and auth, so one probe validates
        /// connectivity and the Authorization header - but it may point at any backend health URL.
        /// </summary>
        public string SelfCheckEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Regex the HTTP status code must FULLY match for the self-check to count as success, e.g.
        /// <c>^2\d\d$</c> for any 2xx, or <c>^(200|204)$</c> for specific codes. Required when
        /// <see cref="SelfCheckEndpoint"/> is set (there is no impl default). An invalid regex is
        /// reported as a configuration problem and disables the check.
        /// </summary>
        public string SelfCheckExpectedStatus { get; set; } = string.Empty;

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
        public static ActivitySource ActivitySource { get; } = new ActivitySource(name: ActivitySourceName, version: VersionInfo.GetSdkVersionStr());

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

    /// <summary>Outcome category of a telemetry connectivity self-check (<see cref="TelemetrySelfCheck.ProbeAsync"/>).</summary>
    public enum TelemetrySelfCheckStatus
    {
        /// <summary>No <see cref="TelemetryConfiguration.SelfCheckEndpoint"/> configured; the check did not run.</summary>
        Disabled,
        /// <summary>The check could not run because its configuration is missing or invalid (a problem to surface).</summary>
        ConfigError,
        /// <summary>The endpoint answered with a status matching <see cref="TelemetryConfiguration.SelfCheckExpectedStatus"/>.</summary>
        Reachable,
        /// <summary>The endpoint answered, but with a non-matching status - exported telemetry would be dropped.</summary>
        Rejected,
        /// <summary>The endpoint could not be reached at all - exported telemetry would be dropped.</summary>
        Unreachable
    }

    /// <summary>The result of a telemetry self-check: its <see cref="Status"/> and a ready-to-log <see cref="Message"/>.</summary>
    public sealed record TelemetrySelfCheckResult(TelemetrySelfCheckStatus Status, string Message)
    {
        /// <summary>True when the outcome is a problem the operator should see (config error, rejected, or unreachable).</summary>
        public bool IsProblem => Status is TelemetrySelfCheckStatus.ConfigError
            or TelemetrySelfCheckStatus.Rejected or TelemetrySelfCheckStatus.Unreachable;
    }

    /// <summary>
    /// The startup telemetry connectivity self-check, shared by every app/utility that wires OpenTelemetry.
    /// The OTLP exporter drops rejected or unreachable batches silently, so a wrong endpoint or a bad
    /// Authorization header otherwise produces no visible error - telemetry just never arrives. This probes
    /// the configured endpoint once and returns a ready-to-report <see cref="TelemetrySelfCheckResult"/>;
    /// mapping that result onto a logger/console is the caller's (app-specific) concern, which keeps this SDK
    /// helper free of any logging dependency.
    /// <para>The probe is standards-based and backend-agnostic: it POSTs an empty OTLP/HTTP request (a
    /// zero-byte ExportServiceRequest with <c>Content-Type: application/x-protobuf</c>, the same protocol the
    /// exporter uses). Per the OTLP/HTTP spec an empty request is valid and yields 2xx, so this exercises
    /// endpoint + auth without depending on any vendor's JSON handling.</para>
    /// </summary>
    public static class TelemetrySelfCheck
    {
        /// <summary>
        /// Probes the endpoint (via <see cref="ProbeAsync"/>) and reports the outcome to Console/Trace — never
        /// <c>ILogger</c>, so this stays in the logging-free SDK. Every outcome is written to
        /// <see cref="System.Diagnostics.Trace"/>; a problem is additionally written to <see cref="Console.Error"/>
        /// so it is visible even when the only logging provider is the dead OTLP endpoint. Non-blocking (callers
        /// fire-and-forget) and never throws.
        /// </summary>
        public static async Task ProbeAndReportAsync(TelemetryConfiguration telemetry)
        {
            TelemetrySelfCheckResult result = await ProbeAsync(telemetry).ConfigureAwait(false);
            Trace.WriteLine(result.Message);
            if (result.IsProblem)
                Console.Error.WriteLine(result.Message);
        }

        /// <summary>
        /// Probes <see cref="TelemetryConfiguration.SelfCheckEndpoint"/> once (5s timeout) and returns the outcome.
        /// <see cref="TelemetrySelfCheckStatus.Disabled"/> when no endpoint is set; a
        /// <see cref="TelemetrySelfCheckStatus.ConfigError"/> when the endpoint is set without a valid
        /// <see cref="TelemetryConfiguration.SelfCheckExpectedStatus"/> regex. Never throws.
        /// </summary>
        public static async Task<TelemetrySelfCheckResult> ProbeAsync(TelemetryConfiguration telemetry)
        {
            if (string.IsNullOrWhiteSpace(telemetry.SelfCheckEndpoint))
                return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.Disabled,
                    "Telemetry self-check disabled (no telemetry.SelfCheckEndpoint configured); skipping.");

            if (string.IsNullOrWhiteSpace(telemetry.SelfCheckExpectedStatus))
                return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.ConfigError,
                    "telemetry.SelfCheckEndpoint is set but telemetry.SelfCheckExpectedStatus (success-status regex) is not set in ihcsettings.json; skipping self-check.");

            Regex expectedStatus;
            try
            {
                expectedStatus = new Regex(telemetry.SelfCheckExpectedStatus);
            }
            catch (ArgumentException ex)
            {
                return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.ConfigError,
                    $"Telemetry self-check disabled: telemetry.SelfCheckExpectedStatus is not a valid regex ('{telemetry.SelfCheckExpectedStatus}'): {ex.Message}");
            }

            string url = telemetry.SelfCheckEndpoint;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                // Empty OTLP/HTTP request: a zero-byte body is a valid ExportServiceRequest per the spec, and
                // application/x-protobuf is the protocol the real exporter uses - portable across OTLP backends.
                var content = new ByteArrayContent(Array.Empty<byte>());
                content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
                foreach ((string key, string value) in ParseHeaders(telemetry.Headers))
                {
                    // Authorization/custom headers go on the request; fall back to content headers if rejected.
                    if (!request.Headers.TryAddWithoutValidation(key, value))
                        request.Content.Headers.TryAddWithoutValidation(key, value);
                }

                using HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false);
                int status = (int)response.StatusCode;
                if (StatusMatches(expectedStatus, status))
                    return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.Reachable,
                        $"Telemetry endpoint reachable (HTTP {status}) at {url}.");

                return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.Rejected,
                    $"Telemetry endpoint REJECTED export: HTTP {status} at {url} (does not match SelfCheckExpectedStatus '{telemetry.SelfCheckExpectedStatus}'). " +
                    "Exported data will be dropped silently. Check the endpoint path and the Authorization header in ihcsettings.json.");
            }
            catch (Exception ex)
            {
                return new TelemetrySelfCheckResult(TelemetrySelfCheckStatus.Unreachable,
                    $"Telemetry endpoint UNREACHABLE at {url}: {ex.Message}. " +
                    "Exported data will be dropped silently. Check that the collector is running and the endpoint is correct in ihcsettings.json.");
            }
        }

        /// <summary>
        /// True when the regex matches the entire status-code string. Matching the whole value (rather than
        /// a substring) avoids surprises like "20" accepting 500 - callers can still write anchors themselves.
        /// </summary>
        private static bool StatusMatches(Regex expectedStatus, int status)
        {
            string text = status.ToString(CultureInfo.InvariantCulture);
            Match m = expectedStatus.Match(text);
            return m.Success && m.Index == 0 && m.Length == text.Length;
        }

        /// <summary>
        /// Parses the OTLP header string ("key1=value1, key2=value2, ...") the same way the exporter does,
        /// splitting each pair on its FIRST '=' so base64 values containing '=' padding stay intact.
        /// </summary>
        private static IEnumerable<(string key, string value)> ParseHeaders(string headers)
        {
            if (string.IsNullOrEmpty(headers))
                yield break;

            foreach (string part in headers.Split(','))
            {
                int idx = part.IndexOf('=');
                if (idx <= 0)
                    continue;
                string key = part.Substring(0, idx).Trim();
                string value = part.Substring(idx + 1).Trim();
                if (key.Length > 0)
                    yield return (key, value);
            }
        }
    }

}


