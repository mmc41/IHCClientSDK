using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc {
    /// <summary>
    /// Custom HTTP client for IHC with logging and special cookie support.
    /// The class is based on a singleton instance of HttpClient shared among all client instances,
    /// as recommended by Microsoft.
    /// </summary>
    internal class Client {
        private class LoggingHandler : DelegatingHandler
        {
            private readonly IhcSettings settings;

            public LoggingHandler(IhcSettings settings, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                this.settings = settings;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                using var activity = Telemetry.ActivitySource.StartActivity(nameof(SendAsync), ActivityKind.Internal);
                activity?.SetTag("http.request.method", request.Method); // Use opentel standard attribute name for method
                activity?.SetTag("http.url", request.RequestUri); // Use opentel standard attribute name for url
                foreach (var header in request.Headers) {
                    activity?.SetTag("http.request.header." + header.Key, header.Value); // Not sure what standard attribute is for this.
                }
               
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                // Everything below runs on a response the caller has not received yet, so nothing outside this
                // method can dispose it. Buffering the body can genuinely throw here — cancellation between the
                // headers arriving and the body completing, or a connection dropped mid-stream — and letting that
                // escape bare would leak the response and its still-open connection back to the pool's owner.
                try
                {
                    string responseLogString = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) : null;

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

        // Shared httpClient across all instances.
        static private readonly object _lock = new object();
        static private HttpClient _httpClientSingleton = null;

        /// <summary>
        /// Returns the singleton instance of the configured HttpClient we are using.
        /// Only the first caller of this function will actually set the settings.
        /// The settings argument is ignored for subsequent callers.
        /// </summary>
        /// <param name="settings">IHC settings for initial configuration.</param>
        /// <returns>The singleton HttpClient instance.</returns>
        static private HttpClient GetOrCreateHttpClient(IhcSettings settings) {
            lock(_lock) {
                if (_httpClientSingleton == null) {
                    // CA2000: both handlers are handed to the HttpClient below, which owns the handler chain for
                    // the lifetime of this process-wide singleton. There is no scope for them to be disposed at.
#pragma warning disable CA2000
                    HttpClientHandler handler = new HttpClientHandler();
                    handler.AllowAutoRedirect = false;
                    // Disable build-in cookie container as it does not
                    // apply cookies correctly across services:
                    handler.UseCookies = false;
                    // Do not do any kind of certificate check.
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                    LoggingHandler loggingHandler = new LoggingHandler(settings, handler);
                    _httpClientSingleton = new HttpClient(loggingHandler);
#pragma warning restore CA2000
                }

                return _httpClientSingleton;
            }
        }

        private readonly string url;
        private readonly ICookieHandler cookieHandler;
        private IhcSettings settings;

        public Client(ICookieHandler cookieHandler, string url, IhcSettings settings) {
            this.url = url;
            this.cookieHandler = cookieHandler;
            this.settings = settings;
        }

        /// <summary>
        /// Performs an HTTP SOAP POST against IHC.
        /// </summary>
        /// <param name="action">SOAP action.</param>
        /// <param name="body">Request body content.</param>
        /// <returns>Task with the HTTP response message.</returns>
        public async Task<HttpResponseMessage> Post(string action, string body) {
            // The request content is ours to dispose: .NET Core dropped the .NET Framework behaviour where
            // HttpClient disposed the request message for you, and nothing has owned it since (measured against
            // .NET 10, 2026-08-17: the content re-reads fine after PostAsync completes). Awaiting the send rather
            // than returning its task is what gives this a scope to dispose at — PostAsync defaults to
            // ResponseContentRead, so by then the body has been sent and the response fully buffered.
            using var content = new StringContent(body, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", action);
            content.Headers.Add("UserAgent", "ihcclient");
            // Manually apply our global cookie if set:
            string cookie = cookieHandler.GetCookie();
            if (cookie != null) {
                content.Headers.Add("Cookie", cookie);
            }
            return await GetOrCreateHttpClient(this.settings).PostAsync(this.url, content).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }
    };
}