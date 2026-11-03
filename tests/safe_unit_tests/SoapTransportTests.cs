using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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

        /// <summary>A well-formed SOAP response for <see cref="TestSoapService.Call"/>.</summary>
        private static string SoapResponse(bool value) => TestSoapService.Response(value);

        [Test]
        public async Task SoapPost_SuccessfulCall_DisposesTheResponse()
        {
            using var content = new DisposeTrackingContent(SoapResponse(true));
            using var transport = Client.CreateHttpClient(new StubTransport(HttpStatusCode.OK, content));
            var service = new TestSoapService(FakeSession.Settings(), new CookieHandler(false), transport);

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
            var service = new TestSoapService(FakeSession.Settings(), new CookieHandler(false), transport);

            Assert.ThrowsAsync<HttpRequestException>(async () => await service.Call());

            Assert.That(content.Disposed, Is.True, "a non-2xx answer must not walk out with the response still open");
        }

        /// <summary>
        /// A SOAP fault in a reply, whatever the HTTP status it comes with (which no capture records), is the
        /// controller's own refusal: it raises <see cref="Errors.CONTROLLER_SOAP_FAULT"/> carrying the fault's code and
        /// text, instead of a bare HTTP error that discards them (500) or an empty message read as nothing answered
        /// (200).
        /// </summary>
        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.OK)]
        public void SoapPost_AFault_RaisesTheCodedSoapFault_CarryingItsCodeAndText(HttpStatusCode status)
        {
            var (raised, disposed) = CallAnswered(status, SoapFaultText.Of("SOAP-ENV:Server", "Resource 4711 is unknown"));

            Assert.Multiple(() =>
            {
                Assert.That(raised, Is.InstanceOf<ErrorWithCodeException>());
                Assert.That((raised as ErrorWithCodeException)?.ErrorCode, Is.EqualTo(Errors.CONTROLLER_SOAP_FAULT));
                Assert.That(raised?.Message, Does.StartWith(nameof(Errors.CONTROLLER_SOAP_FAULT))
                    .And.Contain("SOAP-ENV:Server").And.Contain("Resource 4711 is unknown"));
                Assert.That(disposed, Is.True);
            });
        }

        /// <summary><c>Logon Failed</c> is the fault a controller answers a call without a working session with, and
        /// a host re-logs in on it, so it has a code of its own.</summary>
        [TestCase(HttpStatusCode.InternalServerError)]
        [TestCase(HttpStatusCode.OK)]
        public void SoapPost_ALogonFailedFault_RaisesTheCodedLogonFailure(HttpStatusCode status)
        {
            Exception? raised = CallAnswered(status, SoapFaultText.LogonFailed).Raised;

            Assert.That((raised as ErrorWithCodeException)?.ErrorCode, Is.EqualTo(Errors.CONTROLLER_LOGON_FAILED));
            Assert.That(raised?.Message, Does.StartWith(nameof(Errors.CONTROLLER_LOGON_FAILED)).And.Contain("Logon Failed"));
        }

        /// <summary>A non-success status whose body is no SOAP envelope at all, or an envelope without a fault, stays
        /// the transport's error: there are no fault details to carry.</summary>
        [TestCase("")]
        [TestCase("<html>failure</html>")]
        [TestCase("<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\"><SOAP-ENV:Body/></SOAP-ENV:Envelope>")]
        public void SoapPost_ANonSuccessStatusWithoutAFault_StaysAnHttpError(string body)
        {
            Assert.That(CallAnswered(HttpStatusCode.InternalServerError, body).Raised, Is.InstanceOf<HttpRequestException>());
        }

        /// <summary>What a call raises when the controller answers <paramref name="status"/> with <paramref name="body"/>,
        /// and whether the response was disposed afterwards.</summary>
        private static (Exception? Raised, bool Disposed) CallAnswered(HttpStatusCode status, string body)
        {
            using var content = new DisposeTrackingContent(body);
            using var transport = Client.CreateHttpClient(new StubTransport(status, content));
            var service = new TestSoapService(FakeSession.Settings(), new CookieHandler(false), transport);
            Exception? raised = Assert.CatchAsync(async () => await service.Call());
            return (raised, content.Disposed);
        }

        [Test]
        public async Task SoapPost_SendsSoapActionUserAgentAndCookieAsRequestHeaders()
        {
            using var content = new DisposeTrackingContent(SoapResponse(true));
            var stub = new StubTransport(HttpStatusCode.OK, content);
            using var transport = Client.CreateHttpClient(stub);
            var cookieHandler = new CookieHandler(false);
            cookieHandler.SetCookie("JSESSIONID=abc123");
            var service = new TestSoapService(FakeSession.Settings(), cookieHandler, transport);

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
            var service = new TestSoapService(FakeSession.Settings(), new CookieHandler(false), transport);

            await service.Call();

            Assert.That(stub.RequestHeaders, Does.Not.ContainKey("Cookie"));
        }
    }
}
