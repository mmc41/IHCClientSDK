using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc {
    /// <summary>
    /// Custom HTTP client for IHC with logging and special cookie support.
    /// The class is based on a singleton instance of HttpClient shared among all client instances,
    /// as recommended by Microsoft.
    /// </summary>
    internal class Client {
        private sealed class LoggingHandler : DelegatingHandler
        {
            public LoggingHandler(HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                using var activity = Telemetry.ActivitySource.StartActivity(nameof(SendAsync), ActivityKind.Internal);
                activity?.SetTag("http.request.method", request.Method); // Use opentel standard attribute name for method
                activity?.SetTag("http.url", request.RequestUri); // Use opentel standard attribute name for url
                foreach (var header in request.Headers) {
                    if (header.Key == CookieHeaderName)
                    {
                        activity?.SetTag("http.request.header.cookie", CookieHandler.REDACTED_COOKIE);
                    }
                    else
                    {
                        activity?.SetTag("http.request.header." + header.Key, header.Value); // Not sure what standard attribute is for this.
                    }
                }

                HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Deliberately does NOT read the response body: the SOAP layer already logs the response
                // (ServiceBaseImpl.soapPost), so buffering it a second time here only bought an extra full copy
                // of every payload - and getProject responses are megabytes of base64.
                //
                // The response has not reached the caller yet, so nothing outside this method can dispose it.
                // Anything that throws between the send and the return would therefore leak the response and its
                // connection, hence the guard.
                try
                {
                    activity?.SetTag("http.response.status_code", response.StatusCode); // Use opentel standard attribute name for status code.
                    activity?.SetTag("http.response.reason", response.ReasonPhrase); // Not sure what standard attribute is for this.
                    foreach (var header in response.Headers)
                    {
                        if (header.Key == "Set-Cookie")
                        {
                            activity?.SetTag("http.response.header.set_cookie", CookieHandler.REDACTED_COOKIE);
                        }
                        else
                        {
                            activity?.SetTag("http.response.header." + header.Key, header.Value);
                        }
                    }

                    if (response.IsSuccessStatusCode)
                        activity?.SetStatus(ActivityStatusCode.Ok);
                    else activity?.SetStatus(ActivityStatusCode.Error);
                }
                catch
                {
                    response.Dispose();
                    throw;
                }

                return response;
            }
        }

        private const string CookieHeaderName = "Cookie";

        /// <summary>
        /// How long a pooled connection may be reused before it is replaced. Without this the process-wide
        /// singleton never re-resolves DNS, so a controller that changes address (DHCP, or a hostname
        /// repointed) stays unreachable for the lifetime of the process - the documented cost of sharing one
        /// HttpClient, and the reason SocketsHttpHandler is used here rather than HttpClientHandler
        /// (which does not expose the setting).
        /// </summary>
        private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(15);

        // Shared httpClient across all instances.
        static private readonly object _lock = new object();
        static private HttpClient _httpClientSingleton = null;

        /// <summary>
        /// The primary handler the SDK talks to IHC through. Configuration is fixed rather than settings-derived,
        /// which is what lets a single instance be shared by every service in the process.
        /// </summary>
        static private HttpMessageHandler CreatePrimaryHandler() {
            return new SocketsHttpHandler {
                AllowAutoRedirect = false,
                // Disable build-in cookie container as it does not
                // apply cookies correctly across services:
                UseCookies = false,
                PooledConnectionLifetime = PooledConnectionLifetime,
                // CA5359: do not do any kind of certificate check. Unchanged behaviour - this is what
                // HttpClientHandler.DangerousAcceptAnyServerCertificateValidator did before, and it is required
                // because IHC controllers serve a self-signed certificate for an address the client cannot
                // validate. The rule stayed quiet only because that BCL helper is a named allow-anything callback;
                // SocketsHttpHandler has no equivalent, so the suppression is what carries the same decision.
#pragma warning disable CA5359
                SslOptions = new SslClientAuthenticationOptions {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true
                }
#pragma warning restore CA5359
            };
        }

        /// <summary>
        /// Wraps <paramref name="primaryHandler"/> in the SDK's telemetry handler and returns the HttpClient
        /// built on that chain. The client owns the whole chain, so disposing the client disposes the handlers.
        /// Also the seam unit tests substitute a stub transport through - see the optional constructor argument.
        /// </summary>
        /// <param name="primaryHandler">The innermost handler that performs the actual transport.</param>
        /// <returns>An HttpClient over the telemetry-wrapped handler chain.</returns>
        static internal HttpClient CreateHttpClient(HttpMessageHandler primaryHandler) {
            // CA2000: the handler is handed to the HttpClient, which owns it from here on. Disposing it in this
            // scope would tear down the transport the returned client needs.
#pragma warning disable CA2000
            return new HttpClient(new LoggingHandler(primaryHandler));
#pragma warning restore CA2000
        }

        /// <summary>
        /// Returns the singleton instance of the configured HttpClient we are using.
        /// </summary>
        /// <returns>The singleton HttpClient instance.</returns>
        static private HttpClient GetOrCreateHttpClient() {
            lock(_lock) {
                // CA2000: the handler chain is owned by the HttpClient below, which lives for the lifetime of
                // this process-wide singleton. There is no scope for them to be disposed at.
#pragma warning disable CA2000
                _httpClientSingleton ??= CreateHttpClient(CreatePrimaryHandler());
#pragma warning restore CA2000

                return _httpClientSingleton;
            }
        }

        private readonly string url;
        private readonly ICookieHandler cookieHandler;
        private readonly HttpClient httpClient;
        private readonly IhcSettings settings;

        /// <summary>
        /// Creates a client for one IHC service endpoint.
        /// </summary>
        /// <param name="cookieHandler">Session cookie source.</param>
        /// <param name="url">Full URL of the IHC service.</param>
        /// <param name="settings">IHC settings.</param>
        /// <param name="httpClient">
        /// Transport to use instead of the process-wide singleton. Null in production; the seam unit tests
        /// substitute a stub transport through (they own it and dispose it).
        /// </param>
        public Client(ICookieHandler cookieHandler, string url, IhcSettings settings, HttpClient httpClient = null) {
            this.url = url;
            this.cookieHandler = cookieHandler;
            this.settings = settings;
            this.httpClient = httpClient;
        }

        /// <summary>
        /// Performs an HTTP SOAP POST against IHC.
        /// </summary>
        /// <param name="action">SOAP action.</param>
        /// <param name="body">Request body content.</param>
        /// <returns>
        /// Task with the HTTP response message. The caller owns the returned response and must dispose it -
        /// HttpClient disposes neither the request nor the response it hands back (see HttpClient.FinishSend).
        /// </returns>
        public async Task<HttpResponseMessage> Post(string action, string body) {
            // The request (and with it the content) is ours to dispose: .NET Core dropped the .NET Framework
            // behaviour where HttpClient disposed the request message for you, and nothing has owned it since.
            // Awaiting the send rather than returning its task is what gives this a scope to dispose at -
            // SendAsync defaults to ResponseContentRead, so by then the body has been sent and the response
            // fully buffered.
            //
            // SOAPAction/User-Agent/Cookie are REQUEST headers and belong on the request. They used to be added
            // to the content headers, which happens to be accepted for custom names (and for Cookie, which the
            // BCL registers as HttpHeaderType.Custom) but would throw "misused header name" for a genuine
            // request header - which is why the user agent went out under the non-standard name "UserAgent".
            using var request = new HttpRequestMessage(HttpMethod.Post, this.url) {
                Content = new StringContent(body, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", action);
            request.Headers.UserAgent.ParseAdd("ihcclient");
            // Manually apply our global cookie if set:
            string cookie = cookieHandler.GetCookie();
            if (cookie != null) {
                request.Headers.Add(CookieHeaderName, cookie);
            }
            return await (this.httpClient ?? GetOrCreateHttpClient()).SendAsync(request).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }
    };
}
