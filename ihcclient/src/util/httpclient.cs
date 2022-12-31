using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ihc {
    /**
     * Custom http client for IHC with logging and special cookie support.

     * The class is based on a singleton instance of HttpClient shared among all client instances,
     * as recommended by MS.
     */
    internal class Client {
        private class LoggingHandler : DelegatingHandler
        {
            private ILogger logger;

            public LoggingHandler(ILogger logger, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                this.logger = logger;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                logger.LogTrace("Request: " + request.ToString());
                if (request.Content != null)
                {
                    logger.LogTrace(await request.Content.ReadAsStringAsync());
                }

                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

                logger.LogTrace("Response: " + response.ToString());
                if (response.Content != null)
                {
                    logger.LogTrace(await response.Content.ReadAsStringAsync());
                }

                return response;
            }
        }

        // Shared httpClient across all instances.
        static private readonly object _lock = new object();
        static private HttpClient _httpClientSingleton = null;

        /**
         * Return the singleton instance of the configured HttpClient we are using.
         * Only the first caller of this function will actually set the log.
         * The log argument is ignored for subsequent callers.
         */
        static private HttpClient GetOrCreateHttpClient(ILogger logger) {
            lock(_lock) {
                if (_httpClientSingleton == null) {
                    HttpClientHandler handler = new HttpClientHandler();
                    handler.AllowAutoRedirect = false;
                    // Disable build-in cookie container as it does not
                    // apply cookies correctly across services:
                    handler.UseCookies = false;
                    // Do not do any kind of certificate check.
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                    LoggingHandler loggingHandler = new LoggingHandler(logger, handler);
                    _httpClientSingleton = new HttpClient(loggingHandler);
                }

                return _httpClientSingleton;
            }
        }

        private readonly string url;
        private readonly ILogger logger;
        private readonly ICookieHandler cookieHandler;

        public Client(ILogger logger, ICookieHandler cookieHandler, string url) {
            this.url = url;
            this.logger = logger;
            this.cookieHandler = cookieHandler;
        }

       /**
        * Do HTTP SOAP post against IHC.
        */
        public Task<HttpResponseMessage> Post(string action, string body) {
            var content = new StringContent(body, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", action);
            content.Headers.Add("UserAgent", "HomeAutomation");
            // Manually apply our global cookie if set:
            string cookie = cookieHandler.GetCookie();
            if (cookie != null) {
                content.Headers.Add("Cookie", cookie);
            }
            return GetOrCreateHttpClient(this.logger).PostAsync(this.url, content);
        }
    };
}