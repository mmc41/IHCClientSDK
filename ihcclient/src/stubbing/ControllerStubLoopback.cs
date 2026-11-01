using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The SDK's side of a controller stub that runs in another process on this machine: the validation of its
    /// address, the client that alone reaches it, and the identity check that a registration passes before it exists.
    /// </summary>
    /// <remarks>
    /// Every lock here exists because a loopback address is not by itself proof of a stub: a port-forward or a tunnel
    /// can put a real controller behind <c>127.0.0.1</c>. A controller cannot echo a token it never saw, so the
    /// registration checks the echo at once, and every later response must carry the same token.
    /// </remarks>
    internal static class ControllerStubLoopback
    {
        /// <summary>The response header every answer of a loopback stub host carries exactly once, holding its token.</summary>
        internal const string IdentityHeader = "X-Ihc-Controller-Stub";

        /// <summary>The path, under the host's address, that answers the identity check.</summary>
        internal const string IdentifyPath = "identify";

        /// <summary>What the identity check answers, before the token.</summary>
        internal const string IdentityAnswerPrefix = "ihcclient-controller-stub/1 ";

        /// <summary>The header a stub host adds, with <see cref="DefectValue"/>, when the stub broke while answering.</summary>
        internal const string DefectHeader = "X-Ihc-Controller-Stub-Fault";

        internal const string DefectValue = "defect";

        /// <summary>How an identity failure at registration ends: the identity check is no controller request.</summary>
        internal const string NothingSent = "Nothing was sent to it as a controller.";

        /// <summary>
        /// How an identity failure on an exchange's answer ends: the request went out before its answer could be
        /// checked, so whatever received it may have carried it out.
        /// </summary>
        internal const string SentBeforeChecked = "The request was sent before its answer could be checked, so "
            + "whatever answers there may have acted on it; the answer was discarded.";

        private const string ServicePath = "ws/";

        /// <summary>
        /// How long a registration's identity check may take. A per-request limit, so the client's own timeout stays
        /// the one every SOAP call has.
        /// </summary>
        internal static readonly TimeSpan PreflightLimit = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Marks the identity check's own request, so a refusal of its answer can say that no controller request was
        /// sent. Every unmarked request is taken to be an exchange, whose refusal says the opposite.
        /// </summary>
        private static readonly HttpRequestOptionsKey<bool> IdentityCheck = new("Ihc.Stubbing.IdentityCheck");

        /// <summary>
        /// The host's address, rebuilt from its parts once it is known to be plain http to a loopback IP literal on
        /// an explicit port, with one token path segment and nothing else.
        /// </summary>
        /// <remarks>
        /// An IP literal rather than <c>localhost</c>, because a name is resolved, and a resolver can answer with any
        /// address. Port 80 is refused with the default port, since a URL does not keep whether it was written; a
        /// stub host binds an ephemeral port.
        /// </remarks>
        internal static Uri ValidatedBase(Uri baseAddress)
        {
            if (!baseAddress.IsAbsoluteUri)
            {
                throw Invalid(baseAddress, "it is not an absolute URL");
            }
            if (baseAddress.Scheme != Uri.UriSchemeHttp)
            {
                throw Invalid(baseAddress, "its scheme is not http");
            }
            if (baseAddress.UserInfo.Length > 0)
            {
                throw Invalid(baseAddress, "it carries user info, which hides the host it names");
            }
            if (baseAddress.HostNameType is not (UriHostNameType.IPv4 or UriHostNameType.IPv6)
                || !IPAddress.TryParse(baseAddress.Host.Trim('[', ']'), out IPAddress? address)
                || !IPAddress.IsLoopback(address))
            {
                throw Invalid(baseAddress, "its host is not a loopback IP literal (127.0.0.0/8 or [::1])");
            }
            if (baseAddress.IsDefaultPort)
            {
                throw Invalid(baseAddress, "it names no explicit port");
            }
            if (baseAddress.Query.Length > 0 || baseAddress.Fragment.Length > 0)
            {
                throw Invalid(baseAddress, "it carries a query or a fragment");
            }
            string token = baseAddress.AbsolutePath.Trim('/');
            if (token.Length == 0 || token.Contains('/', StringComparison.Ordinal))
            {
                throw Invalid(baseAddress, "its path is not exactly one token segment");
            }
            return new Uri(baseAddress.GetLeftPart(UriPartial.Authority) + "/" + token + "/");
        }

        /// <summary>The token a validated base address carries.</summary>
        internal static string TokenOf(Uri loopbackBase) => loopbackBase.AbsolutePath.Trim('/');

        /// <summary>
        /// The client every exchange of one loopback registration goes through: the SDK's own logging handler on top,
        /// so a call is traced as any other, then the rewrite from <paramref name="stubEndpoint"/> to the host and the
        /// per-response identity check, then a socket handler that connects to nothing but a loopback address.
        /// </summary>
        internal static HttpClient CreateClient(Uri loopbackBase, string stubEndpoint)
        {
            // CA2000: each handler is owned by the one above it, and the chain by the client the caller owns.
#pragma warning disable CA2000
            return Client.CreateHttpClient(new RewriteToHost(loopbackBase, new Uri(stubEndpoint).Host, CreatePrimaryHandler()));
#pragma warning restore CA2000
        }

        /// <summary>
        /// The socket handler under a loopback registration. No proxy, which could carry the request anywhere; no
        /// redirect, which could too; no cookie container, because the SDK sets the cookie itself and a container
        /// would re-attach one the SDK deliberately withheld, as the production handler is configured for the same
        /// reason. The connect callback refuses any remote that is not a loopback address.
        /// </summary>
        internal static SocketsHttpHandler CreatePrimaryHandler() => new()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectCallback = ConnectLoopbackOnlyAsync,
        };

        /// <summary>
        /// Asks the host who it is, before the registration that would use it exists, and refuses it unless it echoes
        /// <paramref name="loopbackBase"/>'s token within <see cref="PreflightLimit"/> on
        /// <paramref name="timeProvider"/>'s clock. Sent through the registration's own client, so the SDK traces it.
        /// </summary>
        /// <remarks>
        /// Sent asynchronously even for a caller that blocks on it, because a synchronous send would pass under the
        /// SDK's logging handler, which traces only the asynchronous path. A host still silent at the limit has failed
        /// to prove itself, but a cancellation through <paramref name="cancellationToken"/> is the caller's own: it
        /// ends the task cancelled, with that token, so a caller never reads its own cancellation as a host that
        /// failed.
        /// </remarks>
        internal static async Task PreflightAsync(HttpClient client, Uri loopbackBase, TimeProvider timeProvider,
            CancellationToken cancellationToken)
        {
            string token = TokenOf(loopbackBase);
            using var limit = new CancellationTokenSource(PreflightLimit, timeProvider);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, limit.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(loopbackBase, IdentifyPath));
            request.Options.Set(IdentityCheck, true);
            string answer;
            HttpStatusCode status;
            try
            {
                using HttpResponseMessage response = await client.SendAsync(request, deadline.Token).ConfigureAwait(false);
                status = response.StatusCode;
                answer = await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (ErrorWithCodeException)
            {
                throw;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The caller cancelled the identity check.", ex, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException)
            {
                throw IdentityFailed(loopbackBase, "the identity check could not be sent or was not answered in time",
                    NothingSent, ex);
            }
            if (status != HttpStatusCode.OK || answer.Trim() != IdentityAnswerPrefix + token)
            {
                throw IdentityFailed(loopbackBase,
                    $"the identity check was answered with status {(int)status} and without this address's token",
                    NothingSent, null);
            }
        }

        /// <summary>
        /// Connects only to a loopback IP literal, and refuses anything else before resolving it, so even a request
        /// that escaped every other check opens no connection off this machine.
        /// </summary>
        private static async ValueTask<Stream> ConnectLoopbackOnlyAsync(SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            DnsEndPoint target = context.DnsEndPoint;
            if (!IPAddress.TryParse(target.Host.Trim('[', ']'), out IPAddress? address) || !IPAddress.IsLoopback(address))
            {
                throw new ErrorWithCodeException(Errors.CONTROLLER_STUB_ENDPOINT_INVALID,
                    $"{nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID)}: the loopback stub client connects only to a "
                    + $"loopback IP literal, so it refused {target.Host}:{target.Port} without resolving it.");
            }
            // CA2000: on success the socket is owned by the stream returned to the handler, and disposed on failure.
#pragma warning disable CA2000
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
#pragma warning restore CA2000
            try
            {
                socket.NoDelay = true;
                await socket.ConnectAsync(new IPEndPoint(address, target.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        private static ErrorWithCodeException Invalid(Uri baseAddress, string why) =>
            new(Errors.CONTROLLER_STUB_ENDPOINT_INVALID,
                $"{nameof(Errors.CONTROLLER_STUB_ENDPOINT_INVALID)}: '{baseAddress.OriginalString}' is not the address "
                + $"of a loopback controller stub, because {why}. Expected http://127.0.0.1:<port>/<token>/ as the stub "
                + "host prints it.");

        // Names the host by its authority only, because a refusal is read by people and pasted into reports, and the
        // token is what proves a stub. It is no credential: the host prints it, and the identity check's own span
        // names it in url.full, which the request-destination checks read. The consequence says whether a
        // controller request had reached the host by then.
        private static ErrorWithCodeException IdentityFailed(Uri loopbackBase, string why, string consequence,
            Exception? inner)
        {
            string message = $"{nameof(Errors.CONTROLLER_STUB_IDENTITY_FAILED)}: whatever answers at "
                + $"{loopbackBase.GetLeftPart(UriPartial.Authority)} did not prove it is the controller stub this "
                + $"registration names: {why}. {consequence}";
            return inner is null
                ? new ErrorWithCodeException(Errors.CONTROLLER_STUB_IDENTITY_FAILED, message)
                : new ErrorWithCodeException(Errors.CONTROLLER_STUB_IDENTITY_FAILED, message, inner);
        }

        /// <summary>
        /// Sends a service URL built on the registration's stub endpoint to the host instead, and refuses any answer
        /// that does not carry the host's token exactly once. It sits below the SDK's logging handler, so a trace names
        /// the stub endpoint a service was built on, as an in-process call's does.
        /// </summary>
        private sealed class RewriteToHost(Uri loopbackBase, string stubHost, HttpMessageHandler inner)
            : DelegatingHandler(inner)
        {
            private readonly string token = TokenOf(loopbackBase);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                if (request.RequestUri is { } uri && string.Equals(uri.Host, stubHost, StringComparison.OrdinalIgnoreCase)
                    && uri.AbsolutePath.LastIndexOf("/" + ServicePath, StringComparison.Ordinal) is var service and >= 0)
                {
                    request.RequestUri = new Uri(loopbackBase, uri.AbsolutePath[(service + 1)..]);
                }

                HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!CarriesToken(response))
                {
                    response.Dispose();
                    bool identityCheck = request.Options.TryGetValue(IdentityCheck, out bool marked) && marked;
                    throw IdentityFailed(loopbackBase,
                        $"its answer did not carry the token in exactly one {IdentityHeader} header",
                        identityCheck ? NothingSent : SentBeforeChecked, null);
                }
                if (response.Headers.TryGetValues(DefectHeader, out var defect) && defect.Contains(DefectValue))
                {
                    response.Dispose();
                    throw new ErrorWithCodeException(Errors.CONTROLLER_STUB_FAULTED,
                        $"{nameof(Errors.CONTROLLER_STUB_FAULTED)}: the controller stub at "
                        + $"{loopbackBase.GetLeftPart(UriPartial.Authority)} broke while answering, which is a defect in "
                        + "the stub rather than a controller's answer.");
                }
                return response;
            }

            // Exactly one value, as the host writes it. Counted rather than read with SingleOrDefault, which throws on
            // a second value and so would let that answer escape both the coded refusal and its disposal.
            private bool CarriesToken(HttpResponseMessage response) =>
                response.Headers.TryGetValues(IdentityHeader, out var values)
                && values.ToArray() is [var only]
                && only == token;
        }
    }
}
