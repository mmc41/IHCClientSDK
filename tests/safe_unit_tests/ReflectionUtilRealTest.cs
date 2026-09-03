using NUnit.Framework;
using Ihc;
using Ihc.Tests.Shared;

namespace Ihc.Tests
{
    /// <summary>
    /// Runs the shared <see cref="ReflectionUtilContract"/> against a REAL service instance — the twin of
    /// <see cref="ReflectionUtilMockedTest"/>, which runs the same assertions over a FakeItEasy proxy. No
    /// controller is contacted: constructing the service is the whole arrange, and no operation is called.
    /// </summary>
    [TestFixture]
    public class ReflectionUtilRealTest
    {
        private const string Description = "Real AuthenticationService";

        /// <summary>The endpoint is never dialled, so it only has to be well-formed. That is what lets a
        /// REAL service be constructed in a suite which has no ihcsettings.json to read one from.</summary>
        private static IAuthenticationService Service =>
            new AuthenticationService(new IhcSettings { Endpoint = "http://localhost:1" });

        [Test]
        public void GetServiceType_RealAuthenticationService_ReturnsInterfaceType() =>
            ReflectionUtilContract.AssertServiceTypeIsTheInterface(Service, Description);

        [Test]
        public void GetMethods_RealService_ReturnsInterfaceMethods() =>
            ReflectionUtilContract.AssertMethodsAreTheInterfaceMethods(Service, Description);
    }
}
