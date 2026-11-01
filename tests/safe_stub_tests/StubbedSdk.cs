using System;
using System.Collections.Generic;
using System.Linq;
using Ihc.Stubbing;

namespace Ihc.Tests
{
    /// <summary>
    /// The real SDK services, every one of them, built on a registered <see cref="ControllerStub"/>: what the stub's
    /// fidelity cases call, so a reverse mapper is checked against the SDK's own forward mapper rather than against
    /// itself. Disposing ends the session, releases the registration, and then runs the stub's teardown check.
    /// </summary>
    internal sealed class StubbedSdk : IDisposable
    {
        /// <summary>The password the settings carry. A stub-only value, which the stub accepts like any other.</summary>
        internal const string Password = "stub-only";

        private readonly ControllerStubRegistration _registration;

        private readonly IReadOnlyList<IIHCApiService> _services;

        private StubbedSdk(ControllerStub stub, ControllerStubRegistration registration, IhcSettings settings,
            AuthenticationService authentication, IReadOnlyList<IIHCApiService> services)
        {
            Stub = stub;
            _registration = registration;
            Settings = settings;
            Authentication = authentication;
            _services = [authentication, .. services];
        }

        internal ControllerStub Stub { get; }

        internal IhcSettings Settings { get; }

        internal AuthenticationService Authentication { get; }

        internal ControllerService Controller => Service<ControllerService>();

        internal ConfigurationService Configuration => Service<ConfigurationService>();

        internal ResourceInteractionService Resources => Service<ResourceInteractionService>();

        /// <summary>OpenAPI on the same session, so it needs no login of its own.</summary>
        internal OpenAPIService OpenApi => Service<OpenAPIService>();

        internal UserManagerService UserManager => Service<UserManagerService>();

        internal TimeManagerService Time => Service<TimeManagerService>();

        internal SmsModemService SmsModem => Service<SmsModemService>();

        internal LedDimmerManagementService LedDimmers => Service<LedDimmerManagementService>();

        internal AirlinkManagementService Airlink => Service<AirlinkManagementService>();

        /// <summary>The one service that implements <paramref name="serviceInterface"/>.</summary>
        internal IIHCApiService Service(Type serviceInterface) => _services.Single(serviceInterface.IsInstanceOfType);

        /// <summary>
        /// The one place these cases build real services from settings, admitted in ControllerReachGuard: their
        /// services DO call, but their endpoint is a stub endpoint, which the SDK routes only to a registration
        /// and refuses when none is registered, never to a socket.
        /// </summary>
        internal static StubbedSdk Start(ControllerStub? stub = null)
        {
            stub ??= new ControllerStub();
            return Start(stub, ControllerStubs.Register(stub));
        }

        /// <summary>
        /// The same services on a registration the caller made, such as a loopback one, which this takes ownership
        /// of: disposing the result disposes it.
        /// </summary>
        internal static StubbedSdk Start(ControllerStub stub, ControllerStubRegistration registration)
        {
            var settings = new IhcSettings { Endpoint = registration.Endpoint, UserName = "stub-user", Password = Password };
            var authentication = new AuthenticationService(settings);
            return new StubbedSdk(stub, registration, settings, authentication,
            [
                new ControllerService(authentication), new ConfigurationService(authentication),
                new ResourceInteractionService(authentication), new OpenAPIService(authentication),
                new UserManagerService(authentication), new TimeManagerService(authentication),
                new SmsModemService(authentication), new LedDimmerManagementService(authentication),
                new AirlinkManagementService(authentication), new InternalTestService(authentication),
                new MessageControlLogService(authentication), new ModuleService(authentication),
                new NotificationManagerService(authentication), new ProductionTestService(authentication),
            ]);
        }

        public void Dispose()
        {
            Authentication.Dispose();
            _registration.Dispose();
            Stub.VerifyNoFaults();
        }

        private T Service<T>() where T : IIHCApiService => _services.OfType<T>().Single();
    }
}
