using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ihc.App
{
    /// <summary>
    /// An application service built on an authenticated controller session, which it may OWN and must then
    /// close.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ownership is the whole distinction: a service constructed from an <see cref="IhcSettings"/> creates its
    /// own <see cref="IAuthenticationService"/> and must close that session, while one handed a service from
    /// outside must not — the caller owns what the caller built, and closing it would end a session still in
    /// use elsewhere. Which of the two happened is fixed at construction and cannot change afterwards, so it
    /// is a constructor argument rather than a mutable flag.
    /// </para>
    /// <para>
    /// The session field lives HERE, with the disposal that closes it. Holding it on the derived type while
    /// disposing it from the base is what CA2213 objects to, and the analyzer is right: the type that owns a
    /// disposable is the type that should release it.
    /// </para>
    /// <para>
    /// It sits between <see cref="AppServiceBase"/> and the services that need it rather than on
    /// <c>AppServiceBase</c> itself, because that would make every application service disposable —
    /// <c>LabAppService</c> owns no session and has nothing to close.
    /// </para>
    /// <para>
    /// One settings-constructed service stays outside this base and should not be read as evidence that it
    /// needs no session closed: <c>ProjectAppService.CreateWithControllerBridge</c> mints an
    /// <see cref="AuthenticationService"/> of its own, but the type is <c>sealed</c> and non-disposable, so it
    /// cannot inherit the contract. Making ownership composable — a small owned-session value the three hold
    /// as a field — is what would cover it; a base class cannot.
    /// </para>
    /// </remarks>
    public abstract class AuthenticatedAppServiceBase : AppServiceBase, IDisposable, IAsyncDisposable
    {
        /// <summary>The authenticated controller session this service works through.</summary>
        public readonly IAuthenticationService authService;

        /// <summary>
        /// The settings this service was built from. <c>private protected</c> for the reason
        /// <see cref="AppServiceBase"/> gives for its fault port: a <c>protected</c> member on a public base
        /// publishes it to anyone deriving from outside the SDK, and these settings carry the endpoint and
        /// credentials. Every real deriver is in this assembly.
        /// </summary>
        private protected readonly IhcSettings settings;

        private readonly bool ownsAuthService;

        /// <summary>
        /// Claimed by whichever disposal runs first, so an owned session is closed once however many times, in
        /// whichever mix of forms, and from however many threads this service is disposed. Benign against
        /// today's <c>AuthenticationService</c>, whose disconnect is itself guarded — so this is a guarantee
        /// the type makes rather than a live defect it repairs, and it is what lets a differently-behaved
        /// owned service be introduced later without auditing every caller for how many times it disposes.
        /// </summary>
        /// <remarks>
        /// An <c>int</c> claimed with <see cref="Interlocked.Exchange(ref int, int)"/> rather than a
        /// <c>bool</c> read and then written, because a read followed by a write is two steps that two
        /// concurrent disposals can both take while it still reads false — which would close the session
        /// twice and make once the type documents a hope rather than a guarantee.
        /// </remarks>
        private int sessionClosed;

        /// <param name="settings">IHC configuration settings.</param>
        /// <param name="authService">The authenticated session to work through.</param>
        /// <param name="ownsAuthService">
        /// True when this instance CREATED <paramref name="authService"/> and must therefore close it; false
        /// when it was handed one that outlives it.
        /// </param>
        protected AuthenticatedAppServiceBase(
            IhcSettings settings, IAuthenticationService authService, bool ownsAuthService)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.authService = authService ?? throw new ArgumentNullException(nameof(authService));
            this.ownsAuthService = ownsAuthService;
        }

        /// <summary>
        /// Dispose of owned services if they were created by this instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the owned services. A derived type that overrides this must call the base implementation.
        /// </summary>
        /// <param name="disposing">True when called from <see cref="Dispose()"/>, false from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && ownsAuthService && Interlocked.Exchange(ref sessionClosed, 1) == 0)
            {
                authService.Dispose();
            }
        }

        /// <summary>
        /// Async dispose of owned services if they were created by this instance.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (ownsAuthService && Interlocked.Exchange(ref sessionClosed, 1) == 0)
            {
                await authService.DisposeAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }

            GC.SuppressFinalize(this);
        }
    }
}
