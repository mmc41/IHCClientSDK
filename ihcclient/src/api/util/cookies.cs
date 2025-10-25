using System;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// Cookie management interface.
    /// </summary>
    public interface ICookieHandler
    {
        /// <summary>
        /// Gets the current session cookie.
        /// </summary>
        /// <returns>The cookie string, or null if not set.</returns>
        string GetCookie();

        /// <summary>
        /// Sets the session cookie.
        /// </summary>
        /// <param name="_cookie">The cookie string to set.</param>
        void SetCookie(string _cookie);
    }

    /// <summary>
    /// Interface that authentication services provide for cookie handling.
    /// </summary>
    public interface ICookieHandlerService
    {
        /// <summary>
        /// Gets the cookie handler instance.
        /// </summary>
        /// <returns>The cookie handler.</returns>
        ICookieHandler GetCookieHandler();
    }

    internal sealed class CookieHandler : ICookieHandler
    {
        public const string REDACTED_COOKIE = "**REDACTED**";

        private readonly object _lock = new object();
        private readonly bool logSensitiveData;
        private string cookie = null;

        /// <summary>
        /// Create a CookieHandler for managing session cookies.
        /// </summary>
        /// <param name="logSensitiveData">
        /// If true, log actual cookie values. If false (default), only log that cookies are being set/cleared without showing values.
        /// WARNING: Enabling this may expose session tokens in logs. Only enable for debugging in secure environments.
        /// </param>
        public CookieHandler(bool logSensitiveData)
        {
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