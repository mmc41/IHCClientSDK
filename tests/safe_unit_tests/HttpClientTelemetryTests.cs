using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What the SDK's HTTP handler publishes to telemetry.
    ///
    /// These attributes are HTTP semantic conventions, so their names and TYPES are a contract with every
    /// backend that understands HTTP: a status code has to be the number 404, because a consumer filtering
    /// `status_code >= 400` cannot compare against the string "NotFound". Setting the tag from the
    /// <see cref="HttpStatusCode"/> enum is what produced the string, and it looks correct at the call site.
    /// </summary>
    [TestFixture]
    public class HttpClientTelemetryTests
    {
        private sealed class StubTransport : HttpMessageHandler
        {
            private readonly HttpStatusCode status;

            public StubTransport(HttpStatusCode status) => this.status = status;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(status)
                {
                    Content = new StringContent("<ok/>", Encoding.UTF8, "text/xml")
                };
                response.Headers.Add("Set-Cookie", "JSESSIONID=secret-session");
                response.Headers.Add("Server", "IhcController/1.0");
                return Task.FromResult(response);
            }
        }

        /// <summary>Sends one request through the real handler chain and returns the span it produced.</summary>
        private static async Task<(string Name, ActivityStatusCode Status, Dictionary<string, object?> Tags, ActivityKind Kind)>
            SendCapturingSpan(HttpStatusCode status, Action<HttpRequestMessage>? decorate = null)
        {
            // The handler produces the only span here; nothing else runs inside this call, so no name filter.
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            using var client = Client.CreateHttpClient(new StubTransport(status));
            using var request = new HttpRequestMessage(HttpMethod.Post, "http://unit.test.local/ws/TestService")
            {
                Content = new StringContent("<req/>", Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "isSDCardReady");
            decorate?.Invoke(request);

            using var response = await client.SendAsync(request);

            Activity span = capture.Spans.Single();
            var tags = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> tag in span.TagObjects)
            {
                tags[tag.Key] = tag.Value;
            }
            return (span.OperationName, span.Status, tags, span.Kind);
        }

        [Test]
        public async Task Span_RecordsStatusCodeAsAnInteger_NotTheEnumName()
        {
            var (_, _, tags, _) = await SendCapturingSpan(HttpStatusCode.NotFound);

            Assert.Multiple(() =>
            {
                Assert.That(tags["http.response.status_code"], Is.EqualTo(404),
                    "a consumer filtering status_code >= 400 must be able to compare numerically");
                Assert.That(tags["http.response.status_code"], Is.Not.EqualTo("NotFound"),
                    "the HttpStatusCode enum renders as its NAME, which is the defect being pinned out");
            });
        }

        [Test]
        public async Task Span_OnSuccess_LeavesTheStatusUnset()
        {
            var (_, status, _, _) = await SendCapturingSpan(HttpStatusCode.OK);

            Assert.That(status, Is.EqualTo(ActivityStatusCode.Unset),
                "Ok is reserved for an explicit assertion of success by the operation's owner; a 2xx is simply not an error");
        }

        [Test]
        public async Task Span_OnFailure_IsStillMarkedError()
        {
            var (_, status, _, _) = await SendCapturingSpan(HttpStatusCode.InternalServerError);

            Assert.That(status, Is.EqualTo(ActivityStatusCode.Error));
        }

        [Test]
        public async Task Span_IsNamedForTheHttpMethod()
        {
            var (name, _, _, _) = await SendCapturingSpan(HttpStatusCode.OK);

            Assert.That(name, Is.EqualTo("POST"),
                "an HTTP client span is named for the method, not for the C# method that happens to implement it");
        }

        /// <summary>
        /// An outbound call to the controller is a CLIENT span. Left Internal, a backend's HTTP views and
        /// service-dependency graphs do not see it as a call out of the process at all.
        /// </summary>
        [Test]
        public async Task Span_IsAClientSpan()
        {
            var (_, _, _, kind) = await SendCapturingSpan(HttpStatusCode.OK);
            Assert.That(kind, Is.EqualTo(ActivityKind.Client));
        }

        /// <summary>
        /// The protocol tier of the error-type policy: an HTTP failure names itself by its status code, which
        /// is bounded and comparable, rather than by whatever CLR type the handler happened to raise.
        /// </summary>
        [Test]
        public async Task Span_OnAServerError_CarriesTheStatusCodeAsTheErrorType()
        {
            var (_, status, tags, _) = await SendCapturingSpan(HttpStatusCode.InternalServerError);

            Assert.Multiple(() =>
            {
                Assert.That(status, Is.EqualTo(ActivityStatusCode.Error));
                Assert.That(tags["error.type"], Is.EqualTo("500"));
                Assert.That(tags["ihc.edit.status"], Is.EqualTo("failed"));
            });
        }

        [Test]
        public async Task Span_OnANotFound_CarriesThatStatusCodeToo()
        {
            var (_, _, tags, _) = await SendCapturingSpan(HttpStatusCode.NotFound);
            Assert.That(tags["error.type"], Is.EqualTo("404"));
        }

        [Test]
        public async Task Span_UsesCurrentSemanticConventionNames()
        {
            var (_, _, tags, _) = await SendCapturingSpan(HttpStatusCode.OK);

            Assert.Multiple(() =>
            {
                Assert.That(tags["url.full"]?.ToString(), Is.EqualTo("http://unit.test.local/ws/TestService"));
                Assert.That(tags, Does.Not.ContainKey("http.url"), "renamed to url.full by the current conventions");
                Assert.That(tags["http.request.method"], Is.EqualTo("POST"), "the method is a string, not an HttpMethod object");
                Assert.That(tags, Does.Not.ContainKey("http.response.reason"),
                    "the reason phrase is not a convention and adds nothing the status code does not carry");
            });
        }

        /// <summary>
        /// Every header used to be exported, so anything the controller or a proxy chose to send became a span
        /// attribute - unbounded in both count and content. Only an explicit few are carried now.
        /// </summary>
        [Test]
        public async Task Span_ExportsOnlyAllowlistedHeaders_AndRedactsTheSessionCookie()
        {
            var (_, _, tags, _) = await SendCapturingSpan(HttpStatusCode.OK, request =>
            {
                request.Headers.Add("Cookie", "JSESSIONID=secret-session");
                request.Headers.Add("X-Custom-Tracking", "should-not-be-exported");
            });

            Assert.Multiple(() =>
            {
                Assert.That(tags["http.request.header.soapaction"]?.ToString(), Does.Contain("isSDCardReady"),
                    "an allowlisted header is exported under its LOWERCASED name");
                Assert.That(tags["http.request.header.cookie"]?.ToString(), Is.EqualTo(CookieHandler.REDACTED_COOKIE));
                Assert.That(tags["http.response.header.set_cookie"]?.ToString(), Is.EqualTo(CookieHandler.REDACTED_COOKIE));

                Assert.That(tags, Does.Not.ContainKey("http.request.header.x-custom-tracking"),
                    "a header nobody listed must not become a span attribute");
                Assert.That(tags, Does.Not.ContainKey("http.request.header.X-Custom-Tracking"));
                Assert.That(tags, Does.Not.ContainKey("http.response.header.server"),
                    "the response side is allowlisted too, not just the request side");
            });
        }
    }
}
