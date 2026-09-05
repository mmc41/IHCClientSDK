using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// The session an authenticated service is built from, faked.
    ///
    /// Every service takes an <see cref="IAuthenticationService"/> and reads its settings through it, so a
    /// fixture that drives one over a faked SOAP layer opens with the same three lines. They are here rather
    /// than repeated per fixture because they are one arrangement rather than several: what a fixture varies is
    /// its SOAP layer, never its session.
    ///
    /// The endpoint is under the reserved <c>.invalid</c> TLD, which RFC 2606 guarantees is never delegated -
    /// the safety rule's last line of defence behind the seam itself, and the reason a service built here could
    /// not reach a controller even if a fixture called something on it. Not <c>.local</c>: that suffix is mDNS
    /// and a name under it is answered by whatever host on the LAN claims it.
    /// </summary>
    internal static class FakeSession
    {
        internal const string Endpoint = "http://unit.test.invalid";

        /// <summary>Settings no controller answers.</summary>
        internal static IhcSettings Settings(bool logSensitiveData = false) => new()
        {
            Endpoint = Endpoint,
            AsyncContinueOnCapturedContext = false,
            LogSensitiveData = logSensitiveData,
        };

        /// <summary>A session reporting the given settings, and nothing else.</summary>
        internal static IAuthenticationService Over(IhcSettings settings)
        {
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(settings);
            return auth;
        }

        /// <summary>A session over settings this helper makes - the common case, where the fixture varies only
        /// its SOAP layer.</summary>
        internal static IAuthenticationService Over(bool logSensitiveData = false) =>
            Over(Settings(logSensitiveData));
    }
}
