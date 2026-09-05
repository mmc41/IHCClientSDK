using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Tests
{
    /// <summary>
    /// Stands in for the socket at the seam <see cref="ServiceBaseImpl"/> already carries: records what
    /// was sent and answers with a canned response, so the REAL handler chain - serializer, headers,
    /// cookie handler, response disposal - runs exactly as in production.
    ///
    /// Shared by the fixtures that drive a service over HTTP rather than over a faked SOAP layer. A SOAP
    /// fake replaces the mapping's counterpart; this replaces only the network, which is what makes it
    /// the only way to reach the login exchange - <see cref="AuthenticationService"/> owns its SOAP
    /// implementation because it owns the cookie session every other service borrows.
    /// </summary>
    internal sealed class StubTransport : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> respond;

        /// <summary>Answers per request - typically off its SOAPAction, which is what a multi-call exchange
        /// varies. The body it sent is <see cref="RequestBody"/> by the time this runs.</summary>
        internal StubTransport(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this.respond = respond;
        }

        /// <summary>Answers every request the same way.</summary>
        internal StubTransport(HttpStatusCode status, HttpContent response)
            : this(_ => new HttpResponseMessage(status) { Content = response })
        {
        }

        /// <summary>The body of the LAST request; the header snapshots below are the same request's.</summary>
        internal string? RequestBody { get; private set; }

        /// <summary>Every SOAP action sent, in order - what a multi-call exchange is asserted against.</summary>
        internal List<string> SoapActions { get; } = new();

        internal Dictionary<string, string> RequestHeaders { get; } = new();
        internal Dictionary<string, string> ContentHeaders { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Snapshot rather than hold: the request is disposed once the send returns.
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            RequestHeaders.Clear();
            ContentHeaders.Clear();
            foreach (var header in request.Headers)
            {
                RequestHeaders[header.Key] = string.Join(",", header.Value);
            }
            foreach (var header in request.Content!.Headers)
            {
                ContentHeaders[header.Key] = string.Join(",", header.Value);
            }
            SoapActions.Add(RequestHeaders.TryGetValue("SOAPAction", out string? action) ? action : string.Empty);

            return respond(request);
        }
    }
}
