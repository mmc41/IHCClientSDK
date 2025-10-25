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
    /**
     * Custom http client for IHC with logging and special cookie support.

     * The class is based on a singleton instance of HttpClient shared among all client instances,
     * as recommended by MS.
     */
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

                string responseLogString = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;

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

                return response;
            }
        }

        // Shared httpClient across all instances.
        static private readonly object _lock = new object();
        static private HttpClient _httpClientSingleton = null;

        /**
         * Return the singleton instance of the configured HttpClient we are using.
         * Only the first caller of this function will actually set the settings.
         * The settings argument is ignored for subsequent callers.
         */
        static private HttpClient GetOrCreateHttpClient(IhcSettings settings) {
            lock(_lock) {
                if (_httpClientSingleton == null) {
                    HttpClientHandler handler = new HttpClientHandler();
                    handler.AllowAutoRedirect = false;
                    // Disable build-in cookie container as it does not
                    // apply cookies correctly across services:
                    handler.UseCookies = false;
                    // Do not do any kind of certificate check.
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                    LoggingHandler loggingHandler = new LoggingHandler(settings, handler);
                    _httpClientSingleton = new HttpClient(loggingHandler);
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

       /**
        * Do HTTP SOAP post against IHC.
        */
        public Task<HttpResponseMessage> Post(string action, string body) {
            var content = new StringContent(body, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", action);
            content.Headers.Add("UserAgent", "ihcclient");
            // Manually apply our global cookie if set:
            string cookie = cookieHandler.GetCookie();
            if (cookie != null) {
                content.Headers.Add("Cookie", cookie);
            }
            return GetOrCreateHttpClient(this.settings).PostAsync(this.url, content);
        }
    };
}