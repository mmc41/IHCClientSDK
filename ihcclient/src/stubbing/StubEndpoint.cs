using System;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The one test of whether an endpoint names a controller stub rather than a controller. Routing, and every
    /// indicator that tells a person a stub is in use, ask this and nothing else, so an indicator says stub
    /// exactly when calls go to a stub.
    /// </summary>
    internal static class StubEndpoint
    {
        /// <summary>
        /// The host suffix of every generated stub endpoint. <c>.invalid</c> is reserved never to resolve, so even
        /// a call that escaped routing could reach no controller by this name.
        /// </summary>
        internal const string HostSuffix = ".controllerstub.invalid";

        /// <summary>
        /// True for a <c>mock://</c> endpoint, and for plain http to a host under <see cref="HostSuffix"/> with no
        /// user info. Also true for a service URL built from such an endpoint, since that keeps its scheme and host.
        /// </summary>
        /// <remarks>
        /// User info is refused because it hides the real host behind the suffix:
        /// <c>http://s1.controllerstub.invalid@10.0.0.5</c> is a request to <c>10.0.0.5</c>.
        /// </remarks>
        internal static bool Is(string? endpoint)
        {
            if (endpoint is null)
            {
                return false;
            }
            if (endpoint.StartsWith(SpecialEndpoints.MockedPrefix, StringComparison.Ordinal))
            {
                return true;
            }
            return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && uri.Scheme == Uri.UriSchemeHttp
                && uri.UserInfo.Length == 0
                && uri.Host.EndsWith(HostSuffix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
