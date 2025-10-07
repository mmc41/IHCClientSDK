using System;
using Microsoft.Extensions.Logging;

namespace Ihc {
    /**
    * Cookie managment interface.
    */
    public interface ICookieHandler
    {
        string GetCookie();
        void SetCookie(string _cookie);
    }

    /**
    * Interface that authentication services provide.
    */
    public interface ICookieHandlerService
    {
        ICookieHandler GetCookieHandler();
    }

    internal sealed class CookieHandler : ICookieHandler
    {
        private readonly object _lock = new object();
        private readonly ILogger logger;
        private readonly bool logSensitiveData;
        private string cookie = null;

        /**
        * Create a CookieHandler for managing session cookies.
        *
        * @param logger Logger instance for diagnostics
        * @param logSensitiveData If true, log actual cookie values. If false (default), only log that cookies are being set/cleared without showing values.
        *                         WARNING: Enabling this may expose session tokens in logs. Only enable for debugging in secure environments.
        */
        public CookieHandler(ILogger logger, bool logSensitiveData)
        {
            this.logger = logger;
            this.logSensitiveData = logSensitiveData;
        }

        public string GetCookie()
        {
            lock (_lock)
            {
                if (logSensitiveData)
                {
                    logger.LogTrace("USING COOKIE '" + cookie + "'");
                }
                else
                {
                    logger.LogTrace(cookie != null ? "Using session cookie (value redacted)" : "No session cookie set");
                }
                return cookie;
            }
        }

        public void SetCookie(string _cookie)
        {
            lock (_lock)
            {
                if (_cookie == null)
                {
                    logger.LogInformation("CLEARING COOKIE");
                }
                else
                {
                    if (logSensitiveData)
                    {
                        logger.LogInformation("SETTING COOKIE TO: '" + _cookie + "'");
                    }
                    else
                    {
                        logger.LogInformation("Setting session cookie (value redacted for security)");
                    }
                }

                cookie = _cookie;
            }
        }
    }
}