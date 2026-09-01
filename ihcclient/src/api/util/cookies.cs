using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;

namespace Ihc {
    /// <summary>
    /// Reads the session cookie off an HTTP response.
    /// </summary>
    internal static class SetCookieHeader
    {
        private const string Name = "Set-Cookie";

        /// <summary>
        /// The first <c>Set-Cookie</c> value on <paramref name="response"/>, or <c>null</c> when the response
        /// carries none.
        /// </summary>
        /// <remarks>
        /// Uses <c>TryGetValues</c> rather than <c>GetValues</c> on purpose: <c>GetValues</c> throws
        /// <see cref="InvalidOperationException"/> for an absent header, so a controller that answers 200 without
        /// a cookie surfaced as an opaque exception thrown from inside a response callback. An absent cookie is a
        /// value the caller already handles, not an error.
        /// </remarks>
        public static string? FirstOrNull(HttpResponseMessage response) =>
            response.Headers.TryGetValues(Name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
    }

    /// <summary>
    /// Cookie management interface.
    /// </summary>
    public interface ICookieHandler
    {
        /// <summary>
        /// Gets the current session cookie.
        /// </summary>
        /// <returns>The cookie string, or null if not set.</returns>
        string? GetCookie();

        /// <summary>
        /// Sets the session cookie.
        /// </summary>
        /// <param name="_cookie">The cookie string to set.</param>
        void SetCookie(string? _cookie);
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
        private string? cookie = null;

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

        // Through the core so these spans gain an outcome and an error path: as bare decoration they could
        // only ever report that the call happened, never that it went wrong.
        private readonly OperationTelemetry telemetry =
            new OperationTelemetry(SdkTelemetryRegistry.Surface, nameof(CookieHandler));

        public string? GetCookie()
        {
            lock (_lock)
            {
                return telemetry.Run(nameof(GetCookie), scope =>
                {
                    scope.Activity?.SetReturnValue(
                       cookie == null ? "Empty" : (logSensitiveData ? cookie : UserConstants.REDACTED_PASSWORD)
                    );

                    return cookie;
                });
            }
        }

        public void SetCookie(string? _cookie)
        {
            lock (_lock)
            {
                telemetry.Run(nameof(SetCookie), scope =>
                {
                    scope.Activity.SetParameters(
                        (nameof(_cookie), _cookie == null ? "Empty" : (logSensitiveData ? _cookie : UserConstants.REDACTED_PASSWORD))
                    );

                    cookie = _cookie;
                });
            }
        }
    }
}