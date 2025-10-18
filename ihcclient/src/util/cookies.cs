using System;
using System.Diagnostics;
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
        public const string REDACTED_COOKIE = "**REDACTED**";

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
                using var activity = Telemetry.ActivitySource.StartActivity(nameof(GetCookie), ActivityKind.Internal);
                activity?.SetReturnValue(
                   cookie == null ? "Empty" : (logSensitiveData ? cookie : UserConstants.REDACTED_PASSWORD)
                );

                return cookie;
            }
        }

        public void SetCookie(string _cookie)
        {
            lock (_lock)
            {
                using var activity = Telemetry.ActivitySource.StartActivity(nameof(SetCookie), ActivityKind.Internal);
                activity?.SetParameters(
                    (nameof(_cookie), _cookie == null ? "Empty" : (logSensitiveData ? _cookie : UserConstants.REDACTED_PASSWORD))
                );
            
                cookie = _cookie;
            }
        }
    }
}