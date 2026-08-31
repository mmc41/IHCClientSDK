using NUnit.Framework;
using Ihc;
using Ihc.Tests.Shared;

namespace Ihc.Tests
{
    /// <summary>
    /// Runs the shared <see cref="ReflectionUtilContract"/> against a REAL service instance. No controller is
    /// contacted — constructing the service is the whole arrange, and no operation is called.
    /// </summary>
    [TestFixture]
    public class ReflectionUtilRealTest
    {
        private const string Description = "Real AuthenticationService";

        private static IAuthenticationService Service => new AuthenticationService(Setup.settings!);

        [Test]
        public void GetServiceType_RealAuthenticationService_ReturnsInterfaceType() =>
            ReflectionUtilContract.AssertServiceTypeIsTheInterface(Service, Description);

        [Test]
        public void GetMethods_RealService_ReturnsInterfaceMethods() =>
            ReflectionUtilContract.AssertMethodsAreTheInterfaceMethods(Service, Description);
    }
}
