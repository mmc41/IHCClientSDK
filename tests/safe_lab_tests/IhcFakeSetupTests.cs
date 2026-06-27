using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Ihc;
using IhcLab;

namespace Ihc.Tests
{
    /// <summary>
    /// Tests for the fake IHC services in <see cref="IhcFakeSetup"/>.
    ///
    /// Focus: the stateful UserManager fake must keep its state isolated to each service instance so that
    /// mutations made through one fake (or in one test) do not leak into another fake/test.
    /// </summary>
    [TestFixture]
    public class IhcFakeSetupTests
    {
        private static IhcUser NewUser(string username) => new IhcUser
        {
            Username = username,
            Password = "Pass123",
            Email = "iso@mock.com",
            Firstname = "Iso",
            Lastname = "Lated",
            Phone = "+4500000000",
            Group = IhcUserGroup.Users,
            Project = "Mock Project"
        };

        [Test]
        public async Task SetupUserManagerService_State_IsIsolatedPerServiceInstance()
        {
            var settings = new IhcSettings();

            var serviceA = IhcFakeSetup.SetupUserManagerService(settings);
            var serviceB = IhcFakeSetup.SetupUserManagerService(settings);

            // Mutate only service A's user store.
            await serviceA.AddUser(NewUser("isolated_user"));

            var usersA = await serviceA.GetUsers(false);
            var usersB = await serviceB.GetUsers(false);

            Assert.That(usersA.Any(u => u.Username == "isolated_user"), Is.True,
                "Service A should see the user it added.");
            Assert.That(usersB.Any(u => u.Username == "isolated_user"), Is.False,
                "Service B must NOT see users added through a different fake instance - fake state must not " +
                "leak across instances (or across tests).");
        }
    }
}
