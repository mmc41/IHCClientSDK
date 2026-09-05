using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FakeItEasy;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The PRODUCTION construction path of every authenticated service.
    ///
    /// ARCHITECTURE.md states the invariant this fixture exists for: "construct authenticated services
    /// from <c>IAuthenticationService</c> so they share its settings and cookie session". Every service
    /// has a public <c>(IAuthenticationService)</c> constructor that does exactly that, and a handful
    /// also have an internal test-seam constructor that bypasses the SOAP layer - and it was only ever
    /// the seams that any test constructed. So the constructor an application actually calls, on every
    /// service, went unexercised, and with it the one line where a service could quietly mint a cookie
    /// session of its own and authenticate against nothing.
    ///
    /// It stays here rather than in the architecture suite because what it asserts is runtime object
    /// IDENTITY - the same settings instance, the same cookie handler instance - which a structural rule
    /// reading types and references cannot see.
    ///
    /// The service list is REFLECTED rather than written out, so a service added later is covered on
    /// arrival instead of being remembered.
    ///
    /// <para>That reflection also means the reflected cases are INVISIBLE to
    /// <c>ControllerReachGuard</c>: its scan reads a literal <c>newobj</c>, and there is none here, so only
    /// the cases below that name a constructor in source appear in its admitted list. What holds the
    /// reflected ones is the same thing that holds every admitted site - no operation is called on any
    /// service built here, and the endpoint is <see cref="FakeSession.Endpoint"/>, under the reserved
    /// <c>.invalid</c> TLD.</para>
    /// </summary>
    [TestFixture]
    public class ServiceConstructionTests
    {
        private static IEnumerable<Type> AuthenticatedServices() =>
            typeof(IIHCApiService).Assembly.GetExportedTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IIHCApiService).IsAssignableFrom(t))
                .Where(t => t.GetConstructor(new[] { typeof(IAuthenticationService) }) is not null)
                .OrderBy(t => t.Name, StringComparer.Ordinal);

        private static IEnumerable<TestCaseData> ServiceCases() =>
            AuthenticatedServices().Select(t => new TestCaseData(t).SetName($"{{m}}({t.Name})"));

        // Named explicitly rather than resolved by argument: OpenAPIService also constructs from
        // IhcSettings, so a null argument alone leaves the overload ambiguous.
        private static ConstructorInfo SessionConstructorOf(Type serviceType) =>
            serviceType.GetConstructor(new[] { typeof(IAuthenticationService) })!;

        /// <summary>A session that also hands out a cookie handler, which is the half of the invariant
        /// <see cref="FakeSession.Over(IhcSettings)"/> leaves to the fixture that needs it.</summary>
        private static (IAuthenticationService Auth, IhcSettings Settings, ICookieHandler Cookies) NewSession()
        {
            IhcSettings settings = FakeSession.Settings();
            var cookies = new CookieHandler(logSensitiveData: false);
            IAuthenticationService auth = FakeSession.Over(settings);
            A.CallTo(() => auth.GetCookieHandler()).Returns(cookies);
            return (auth, settings, cookies);
        }

        /// <summary>Every SOAP implementation a service holds, whatever the field is named.</summary>
        private static IEnumerable<object> SoapImplementationsOf(object service) =>
            service.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(f => f.GetValue(service))
                .OfType<object>()
                .Where(v => v.GetType().IsSubclassOf(typeof(ServiceBaseImpl)));

        private static ICookieHandler CookieHandlerOf(object soapImpl) =>
            (ICookieHandler)typeof(ServiceBaseImpl)
                .GetField("cookieHandler", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(soapImpl)!;

        /// <summary>
        /// The invariant itself. A service that copied the settings, or built its own
        /// <see cref="CookieHandler"/>, would send its calls without the session cookie the login
        /// established - every call answered as unauthenticated, from a service that looks correctly
        /// constructed.
        /// </summary>
        [TestCaseSource(nameof(ServiceCases))]
        public void PublicConstructor_SharesTheSessionsSettingsAndCookieHandler(Type serviceType)
        {
            (IAuthenticationService auth, IhcSettings settings, ICookieHandler cookies) = NewSession();

            var service = (IIHCApiService)SessionConstructorOf(serviceType).Invoke(new object?[] { auth });

            Assert.That(service.IhcSettings, Is.SameAs(settings),
                "the service must carry the session's settings instance, not a copy");

            // ProductionTestService is deliberately operation-free until the controller WSDL gains
            // operations, so it holds no SOAP implementation to check - the settings assertion above is
            // the whole of its contract today.
            foreach (object impl in SoapImplementationsOf(service))
            {
                Assert.That(CookieHandlerOf(impl), Is.SameAs(cookies),
                    $"{serviceType.Name} must post through the session's cookie handler");
            }
        }

        /// <summary>
        /// Every service reaches its base through <c>ServiceBase.SettingsOf</c>, which refuses a null
        /// session BEFORE the base initializer can dereference it. A caller compiled without nullable
        /// reference types - the caller the guard exists for - must get an argument refusal naming the
        /// parameter rather than a NullReferenceException raised inside the SDK.
        /// </summary>
        [TestCaseSource(nameof(ServiceCases))]
        public void PublicConstructor_WithNoSession_RefusesTheArgumentByName(Type serviceType)
        {
            var thrown = Assert.Throws<TargetInvocationException>(
                () => SessionConstructorOf(serviceType).Invoke(new object?[] { null }));

            Assert.That(thrown!.InnerException, Is.InstanceOf<ArgumentNullException>());
            Assert.That(((ArgumentNullException)thrown.InnerException!).ParamName, Is.EqualTo("authService"));
        }

        /// <summary>
        /// The one service that also constructs standalone, because it owns the login rather than
        /// inheriting one. It mints its OWN cookie handler there - correctly, since there is no session
        /// to share yet - which is the case that makes the shared-handler assertion above meaningful
        /// rather than tautological.
        /// </summary>
        [Test]
        public void AuthenticationService_ConstructedFromSettings_OwnsItsCookieSession()
        {
            var settings = FakeSession.Settings();

            using var auth = new AuthenticationService(settings);

            Assert.Multiple(() =>
            {
                Assert.That(auth.IhcSettings, Is.SameAs(settings));
                Assert.That(auth.GetCookieHandler(), Is.Not.Null);
                Assert.That(SoapImplementationsOf(auth).Select(CookieHandlerOf),
                    Is.All.SameAs(auth.GetCookieHandler()),
                    "the login posts through the same handler it hands to every other service");
            });
        }

        /// <summary>
        /// <see cref="OpenAPIService"/> is the exception ARCHITECTURE.md names: it supports either form.
        /// Handed a session it must adopt that session's handler outright - it exposes its own through
        /// <see cref="ICookieHandlerService"/>, so the sharing is directly observable here.
        /// </summary>
        [Test]
        public void OpenAPIService_ConstructedFromASession_AdoptsThatSessionsCookieHandler()
        {
            (IAuthenticationService auth, IhcSettings settings, ICookieHandler cookies) = NewSession();

            var openApi = new OpenAPIService(auth);

            Assert.Multiple(() =>
            {
                Assert.That(openApi.IhcSettings, Is.SameAs(settings));
                Assert.That(openApi.GetCookieHandler(), Is.SameAs(cookies));
            });
        }

        /// <summary>
        /// Standalone, OpenAPI owns the login itself, so it mints its own handler - the counterpart of
        /// the case above, and the reason the service carries two constructors at all.
        /// </summary>
        [Test]
        public void OpenAPIService_ConstructedFromSettings_OwnsItsCookieSession()
        {
            (_, IhcSettings _, ICookieHandler foreign) = NewSession();
            var settings = FakeSession.Settings();

            var openApi = new OpenAPIService(settings);

            Assert.Multiple(() =>
            {
                Assert.That(openApi.IhcSettings, Is.SameAs(settings));
                Assert.That(openApi.GetCookieHandler(), Is.Not.SameAs(foreign));
                Assert.That(SoapImplementationsOf(openApi).Select(CookieHandlerOf),
                    Is.All.SameAs(openApi.GetCookieHandler()));
            });
        }

        /// <summary>
        /// <c>ServiceBase</c> refuses a settings object with no endpoint, and one naming the mocked
        /// endpoint prefix - a real service constructed against the mocked prefix would otherwise post
        /// to a URL built from it. Asserted through one representative service; the check is on the
        /// shared base every service reaches.
        /// </summary>
        [TestCase(null, TestName = "ServiceBase_WithNoEndpoint_IsRefused")]
        [TestCase(SpecialEndpoints.MockedPrefix + "whatever", TestName = "ServiceBase_WithTheMockedEndpointPrefix_IsRefused")]
        public void ServiceBase_RefusesAnUnusableEndpoint(string? endpoint)
        {
            // Endpoint is declared non-nullable; the null case is exactly the oblivious caller the
            // guard inside ServiceBase exists for, so the test has to reach past the annotation to
            // reproduce it.
            IAuthenticationService auth = FakeSession.Over(new IhcSettings { Endpoint = endpoint! });

            Assert.Throws<ArgumentException>(() => new UserManagerService(auth));
        }

        /// <summary>
        /// The reflected list is the point of this fixture, so a change that empties it must fail rather
        /// than silently pass every case above vacuously.
        /// </summary>
        [Test]
        public void EveryServiceWrapperIsReachedByTheseCases()
        {
            IReadOnlyList<Type> covered = AuthenticatedServices().ToList();
            IEnumerable<Type> wrappers = typeof(IIHCApiService).Assembly.GetExportedTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(ServiceBase)));

            Assert.That(covered, Is.Not.Empty);
            Assert.That(wrappers.Except(covered).Select(t => t.Name),
                Is.EquivalentTo(new[] { nameof(AuthenticationService) }),
                "AuthenticationService is the only wrapper that takes settings rather than a session; " +
                "any other wrapper missing a public (IAuthenticationService) constructor is a break in the invariant");
        }
    }
}
