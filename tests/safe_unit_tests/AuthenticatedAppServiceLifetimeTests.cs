using NUnit.Framework;
using System.Threading.Tasks;
using Ihc;
using Ihc.App;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// The owned-session lifetime <see cref="AuthenticatedAppServiceBase"/> guarantees: a service closes the
    /// session it CREATED, never one it was handed, and closes it at most once however many times it is
    /// disposed.
    /// </summary>
    /// <remarks>
    /// The base is exercised directly rather than through <c>AdminAppService</c> or
    /// <c>InformationAppService</c>, because their owning constructors build a real
    /// <c>AuthenticationService</c> from settings and there is no seam to observe its disposal through. The
    /// lifetime is the base's behaviour now, so the base is the right subject.
    /// </remarks>
    public class AuthenticatedAppServiceLifetimeTests
    {
        private sealed class TestService : AuthenticatedAppServiceBase
        {
            public TestService(IhcSettings settings, IAuthenticationService authService, bool ownsAuthService)
                : base(settings, authService, ownsAuthService)
            {
            }
        }

        private static IhcSettings Settings() => new IhcSettings { Endpoint = "http://localhost:1" };

        [Test]
        public void Dispose_CalledTwice_ClosesTheOwnedSessionOnce()
        {
            var authService = A.Fake<IAuthenticationService>();
            var service = new TestService(Settings(), authService, ownsAuthService: true);

            service.Dispose();
            service.Dispose();

            A.CallTo(() => authService.Dispose()).MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Dispose_AfterDisposeAsync_DoesNotCloseTheSessionASecondTime()
        {
            var authService = A.Fake<IAuthenticationService>();
            var service = new TestService(Settings(), authService, ownsAuthService: true);

            await service.DisposeAsync();
            service.Dispose();

            A.CallTo(() => authService.DisposeAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => authService.Dispose()).MustNotHaveHappened();
        }

        [Test]
        public async Task DisposeAsync_CalledTwice_ClosesTheOwnedSessionOnce()
        {
            var authService = A.Fake<IAuthenticationService>();
            var service = new TestService(Settings(), authService, ownsAuthService: true);

            await service.DisposeAsync();
            await service.DisposeAsync();

            A.CallTo(() => authService.DisposeAsync()).MustHaveHappenedOnceExactly();
        }

        // The half that matters most to a caller: a session handed in outlives the service that borrowed it,
        // so disposing the service must not end a session the caller is still using elsewhere.
        [Test]
        public async Task Dispose_WhenTheSessionWasHandedIn_LeavesItOpen()
        {
            var authService = A.Fake<IAuthenticationService>();
            var service = new TestService(Settings(), authService, ownsAuthService: false);

            service.Dispose();
            await service.DisposeAsync();

            A.CallTo(() => authService.Dispose()).MustNotHaveHappened();
            A.CallTo(() => authService.DisposeAsync()).MustNotHaveHappened();
        }
    }
}
