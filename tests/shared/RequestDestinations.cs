using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ihc;

namespace Ihc.Tests.Shared
{
    /// <summary>
    /// Where requests went, read from telemetry: the SDK's own HTTP client spans, and the runtime's
    /// <c>System.Net.Http</c> activities, which see a request whether or not it went through the SDK's handler.
    /// A suite that runs only against stubs uses these to show that nothing it sent left for anywhere else.
    /// </summary>
    internal static class RequestDestinations
    {
        /// <summary>The instrumentation scope the runtime's HTTP client reports on.</summary>
        internal const string RuntimeHttpScope = "System.Net.Http";

        /// <summary>The runtime's activity for one outgoing request.</summary>
        internal const string RuntimeRequestActivity = "System.Net.Http.HttpRequestOut";

        /// <summary>The only address a stub run's runtime requests may go to: a loopback stub host.</summary>
        internal const string Loopback = "127.0.0.1";

        /// <summary>The SDK's HTTP client spans: the ones that carry the URL a request was sent to.</summary>
        internal static IReadOnlyList<Activity> SdkRequests(IEnumerable<Activity> spans) =>
            [.. spans.Where(span => span.Kind == ActivityKind.Client && span.GetTagItem("url.full") is not null)];

        /// <summary>The URL of every SDK request that did not go to a stub host.</summary>
        internal static IEnumerable<string> NonStubDestinations(IEnumerable<Activity> sdkRequests) =>
            sdkRequests.Select(span => (string)span.GetTagItem("url.full")!)
                .Where(url => !new IhcSettings { Endpoint = url }.IsStubConfigured());

        /// <summary>The runtime's outgoing-request activities.</summary>
        internal static IReadOnlyList<Activity> RuntimeRequests(IEnumerable<Activity> spans) =>
            [.. spans.Where(span => span.OperationName == RuntimeRequestActivity)];

        /// <summary>The URL of every runtime request whose server was not the loopback address.</summary>
        internal static IEnumerable<string> NonLoopbackDestinations(IEnumerable<Activity> runtimeRequests) =>
            runtimeRequests
                .Where(span => span.GetTagItem("server.address") as string != Loopback)
                .Select(span => span.GetTagItem("url.full") as string ?? "(no url)");
    }
}
