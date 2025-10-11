using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
            private bool logSensitiveData;

            public LoggingHandler(ILogger logger, bool logSensitiveData, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                this.logger = logger;
                this.logSensitiveData = logSensitiveData;
            }

            /// <summary>
            /// Redacts password values from XML content in SOAP request/response logging.
            /// </summary>
            private string redactPassword(string input)
            {
                if (input == null)
                {
                    return null;
                }

                // Redact password content in XML elements like <ns1:password>xxx</ns1:password>
                // where ns1 can be any namespace prefix (or no prefix at all)
                // Pattern explanation:
                // - (<\w*:?password>) - captures opening tag with optional namespace prefix (e.g., <password>, <ns1:password>, <utcs:password>)
                // - [^<]+ - matches the password content (one or more characters except '<')
                // - (</\w*:?password>) - captures closing tag with optional namespace prefix
                string pattern = @"(<\w*:?password>)[^<]+(</\w*:?password>)";
                string replacement = "$1***REDACTED***$2";

                return Regex.Replace(input, pattern, replacement, RegexOptions.IgnoreCase);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

                string requestLogString = request.Content != null ? await request.Content.ReadAsStringAsync().ConfigureAwait(false) : null;
                if (!logSensitiveData)
                    requestLogString = redactPassword(requestLogString);

                logger.LogTrace("Request: " + requestLogString);
                activity?.SetParameters(
                    (nameof(request), requestLogString),
                    (nameof(cancellationToken), cancellationToken)
                );
                
                HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                string responseLogString = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;

                logger.LogTrace("Response: " + responseLogString);
                activity?.SetReturnValue(responseLogString);

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
        static private HttpClient GetOrCreateHttpClient(ILogger logger, bool logSensitiveData) {
            lock(_lock) {
                if (_httpClientSingleton == null) {
                    HttpClientHandler handler = new HttpClientHandler();
                    handler.AllowAutoRedirect = false;
                    // Disable build-in cookie container as it does not
                    // apply cookies correctly across services:
                    handler.UseCookies = false;
                    // Do not do any kind of certificate check.
                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                    LoggingHandler loggingHandler = new LoggingHandler(logger, logSensitiveData, handler);
                    _httpClientSingleton = new HttpClient(loggingHandler);
                }

                return _httpClientSingleton;
            }
        }

        private readonly string url;
        private readonly ILogger logger;
        private readonly ICookieHandler cookieHandler;
        private readonly bool logSensitiveData;
        private bool asyncContinueOnCapturedContext;

        public Client(ILogger logger, ICookieHandler cookieHandler, string url, bool logSensitiveData, bool asyncContinueOnCapturedContext) {
            this.url = url;
            this.logger = logger;
            this.cookieHandler = cookieHandler;
            this.logSensitiveData = logSensitiveData;
            this.asyncContinueOnCapturedContext = asyncContinueOnCapturedContext;
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
            return GetOrCreateHttpClient(this.logger, this.logSensitiveData).PostAsync(this.url, content);
        }
    };
}