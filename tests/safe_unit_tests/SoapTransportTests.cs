using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ihc;
using Ihc.Envelope;
using Ihc.Soap.Controller;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The HTTP side of a SOAP call, exercised through the real handler chain with a stub transport in place of
    /// the socket.
    ///
    /// Two contracts are pinned here. Ownership: HttpClient disposes neither the request it sends nor the
    /// response it hands back (it disposes the response only on its own failure path), so the SDK must dispose
    /// the response itself - on the success path AND when the controller answers non-2xx. And placement:
    /// SOAPAction, User-Agent and the session cookie are request headers, not content headers.
    /// </summary>
    [TestFixture]
    public class SoapTransportTests
    {
        /// <summary>Content that reports whether it was disposed - i.e. whether the response was.</summary>
        private sealed class DisposeTrackingContent : StringContent
        {
            public bool Disposed { get; private set; }

            public DisposeTrackingContent(string content) : base(content, Encoding.UTF8, "text/xml") { }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }

        /// <summary>Stands in for the socket: records what was sent, answers with a canned response.</summary>
        private sealed class StubTransport : HttpMessageHandler
        {
            private readonly HttpStatusCode status;
            private readonly HttpContent response;

            public StubTransport(HttpStatusCode status, HttpContent response)
            {
                this.status = status;
                this.response = response;
            }

            public string? RequestBody { get; private set; }
            public Dictionary<string, string> RequestHeaders { get; } = new();
            public Dictionary<string, string> ContentHeaders { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                // Snapshot the headers: the request is disposed once the send returns.
                foreach (var header in request.Headers)
                {
                    RequestHeaders[header.Key] = string.Join(",", header.Value);
                }
                foreach (var header in request.Content!.Headers)
                {
                    ContentHeaders[header.Key] = string.Join(",", header.Value);
                }

                return new HttpResponseMessage(status) { Content = response };
            }
        }

        /// <summary>A minimal SOAP service over the stub transport, so soapPost runs exactly as in production.</summary>
        private sealed class TestSoapService : ServiceBaseImpl
        {
            public TestSoapService(IhcSettings settings, ICookieHandler cookieHandler, HttpClient transport)
                : base(cookieHandler, settings, "TestService", transport) { }

            public Task<outputMessageName9> Call() =>
                soapPost<outputMessageName9, inputMessageName9>("isSDCardReady", new inputMessageName9());
        }

        private static IhcSettings Settings() =>
            new IhcSettings { Endpoint = "http://unit.test.local", AsyncContinueOnCapturedContext = false };

        /// <summary>A well-formed SOAP response for the call above, built with the SDK's own serializer.</summary>
        private static string SoapResponse(bool value) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName9>>(
                new ResponseEnvelope<outputMessageName9>(new outputMessageName9(value)));

        [Test]
        public async Task SoapPost_SuccessfulCall_DisposesTheResponse()
        {
            using var content = new DisposeTrackingContent(SoapResponse(true));
            using var transport = Client.CreateHttpClient(new StubTransport(HttpStatusCode.OK, content));
            var service = new TestSoapService(Settings(), new CookieHandler(false), transport);

            outputMessageName9 result = await service.Call();

            Assert.Multiple(() =>
            {
                Assert.That(result.isSDCardReady1, Is.True, "the response body must still be readable before disposal");
                Assert.That(content.Disposed, Is.True, "the response must be disposed once the call has been read");
            });
        }

        /// <summary>
        /// The path that used to leak outright: EnsureSuccessStatusCode throws without disposing the content,
        /// so a controller answering 500 left the response open.
        /// </summary>
        [Test]
        public void SoapPost_NonSuccessStatus_DisposesTheResponse()
        {
            using var content = new DisposeTrackingContent("<html>failure</html>");
            using var transport = Client.CreateHttpClient(new StubTransport(HttpStatusCode.InternalServerError, content));
            var service = new TestSoapService(Settings(), new CookieHandler(false), transport);

            Assert.ThrowsAsync<HttpRequestException>(async () => await service.Call());

            Assert.That(content.Disposed, Is.True, "a non-2xx answer must not walk out with the response still open");
        }

        [Test]
        public async Task SoapPost_SendsSoapActionUserAgentAndCookieAsRequestHeaders()
        {
            using var content = new DisposeTrackingContent(SoapResponse(true));
            var stub = new StubTransport(HttpStatusCode.OK, content);
            using var transport = Client.CreateHttpClient(stub);
            var cookieHandler = new CookieHandler(false);
            cookieHandler.SetCookie("JSESSIONID=abc123");
            var service = new TestSoapService(Settings(), cookieHandler, transport);

            await service.Call();

            Assert.Multiple(() =>
            {
                Assert.That(stub.RequestHeaders, Does.ContainKey("SOAPAction"));
                Assert.That(stub.RequestHeaders["SOAPAction"], Is.EqualTo("isSDCardReady"));
                // Was sent as the non-standard name "UserAgent" while it lived on the content headers.
                Assert.That(stub.RequestHeaders, Does.ContainKey("User-Agent"));
                Assert.That(stub.RequestHeaders["User-Agent"], Is.EqualTo("ihcclient"));
                Assert.That(stub.RequestHeaders, Does.ContainKey("Cookie"));
                Assert.That(stub.RequestHeaders["Cookie"], Is.EqualTo("JSESSIONID=abc123"));

                // Content-Length is the pipeline's own; the three above must not appear here.
                Assert.That(stub.ContentHeaders.Keys.Intersect(new[] { "SOAPAction", "UserAgent", "User-Agent", "Cookie" }),
                    Is.Empty, "request headers must not be smuggled through the content headers");
                Assert.That(stub.ContentHeaders["Content-Type"], Is.EqualTo("text/xml; charset=utf-8"));
                Assert.That(stub.RequestBody, Does.Contain("Envelope"));
            });
        }

        /// <summary>No cookie set yet (the login call): the header is simply absent.</summary>
        [Test]
        public async Task SoapPost_WithoutSessionCookie_SendsNoCookieHeader()
        {
            using var content = new DisposeTrackingContent(SoapResponse(false));
            var stub = new StubTransport(HttpStatusCode.OK, content);
            using var transport = Client.CreateHttpClient(stub);
            var service = new TestSoapService(Settings(), new CookieHandler(false), transport);

            await service.Call();

            Assert.That(stub.RequestHeaders, Does.Not.ContainKey("Cookie"));
        }
    }
}
