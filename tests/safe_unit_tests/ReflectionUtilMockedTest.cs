using NUnit.Framework;
using Ihc;
using Ihc.Tests.Shared;
using FakeItEasy;

namespace Ihc.Tests
{
    /// <summary>
    /// Runs the shared <see cref="ReflectionUtilContract"/> against a FakeItEasy proxy — the implementation shape
    /// that would break it, since a dynamic proxy's own type is neither the interface nor named like it.
    /// </summary>
    [TestFixture]
    public class ReflectionUtilMockedTest
    {
        private const string Description = "FakeItEasy IAuthenticationService";

        private static IAuthenticationService Service => A.Fake<IAuthenticationService>();

        [Test]
        public void GetServiceType_FakeAuthenticationService_ReturnsInterfaceType() =>
            ReflectionUtilContract.AssertServiceTypeIsTheInterface(Service, Description);

        [Test]
        public void GetMethods_FakeService_ReturnsInterfaceMethods() =>
            ReflectionUtilContract.AssertMethodsAreTheInterfaceMethods(Service, Description);
    }
}
