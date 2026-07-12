#nullable enable
using System;
using FakeItEasy;
using Microsoft.Extensions.Time.Testing;

namespace Ihc.Vis.Tests
{
    /// <summary>Shared fake-service arrangements (a peer of <see cref="Tree"/>/<see cref="TestData"/>).</summary>
    internal static class Fakes
    {
        /// <summary>
        /// The controller-bridge <see cref="ProjectAppService"/> recipe (a REAL app service per the test rules,
        /// with a fake <see cref="ICatalog"/> and a fixed clock). The bridge auto-authenticates before every
        /// controller call, so the default fake <see cref="IAuthenticationService"/> reports already-authenticated
        /// to keep that a no-op; pass <paramref name="auth"/> to exercise the authentication path itself, and
        /// <paramref name="clock"/> when a test pins re-stamped metadata.
        /// </summary>
        public static ProjectAppService BridgeService(IControllerService controller, TimeProvider? clock = null,
            IAuthenticationService? auth = null)
        {
            if (auth is null)
            {
                auth = A.Fake<IAuthenticationService>();
                A.CallTo(() => auth.IsAuthenticated()).Returns(true);
            }
            return new ProjectAppService(TestSetup.Settings, A.Fake<ICatalog>(),
                clock ?? new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), controller, auth);
        }
    }
}
