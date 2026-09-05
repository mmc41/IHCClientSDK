using System.Net.Http;
using System.Threading.Tasks;
using Ihc.Envelope;
using Ihc.Soap.Controller;

namespace Ihc.Tests
{
    /// <summary>
    /// A minimal SOAP service over an injected transport, so <c>soapPost</c> runs exactly as in production - the
    /// real serializer, headers, cookie handler and response disposal - with only the socket substituted.
    ///
    /// Shared by every fixture that drives the transport rather than a service's mapping: the HTTP-ownership
    /// pins, the span-tag pins, and the response-envelope guards. <c>isSDCardReady</c> is the call it makes for
    /// no reason beyond being the smallest one on the generated contract; what varies between the fixtures is
    /// the transport's answer, never the service.
    /// </summary>
    internal sealed class TestSoapService : ServiceBaseImpl
    {
        public TestSoapService(IhcSettings settings, ICookieHandler cookieHandler, HttpClient transport)
            : base(cookieHandler, settings, "TestService", transport) { }

        /// <summary>The span this call opens is named <c>soapPost.isSDCardReady</c>.</summary>
        public const string SpanName = "soapPost.isSDCardReady";

        public Task<outputMessageName9> Call() =>
            soapPost<outputMessageName9, inputMessageName9>("isSDCardReady", new inputMessageName9());

        /// <summary>A well-formed response for <see cref="Call"/>, built with the SDK's own serializer.</summary>
        public static string Response(bool value) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName9>>(
                new ResponseEnvelope<outputMessageName9>(new outputMessageName9(value)));
    }
}
