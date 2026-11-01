using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using IhcLab;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Lab's own setup over a controller stub: the services <see cref="IhcSetup"/> builds are the real ones, so what
    /// a test changes through them lands in the stub it registered and in no other.
    /// </summary>
    [TestFixture]
    public class LabSetupStubTests
    {
        private static IhcUser NewUser(string username) => new()
        {
            Username = username,
            Password = "Pass123",
            Email = "iso@stub.invalid",
            Firstname = "Iso",
            Lastname = "Lated",
            Phone = "+4500000000",
            Group = IhcUserGroup.Users,
            Project = "Stub Project"
        };

        /// <summary>Lab's setup, rebuilt on settings that name <paramref name="endpoint"/>.</summary>
        private static IhcSetup SetupOn(string endpoint)
        {
            var setup = new IhcSetup { IhcSettings = new IhcSettings { Endpoint = endpoint, UserName = "lab", Password = "lab-pw" } };
            setup.UpdateSetup();
            return setup;
        }

        [Test]
        public async Task AUserAddedThroughLab_IsStoredInItsOwnStub_AndInNoOther()
        {
            var stubA = new ControllerStub();
            var stubB = new ControllerStub();
            using (ControllerStubRegistration registrationA = ControllerStubs.Register(stubA))
            using (ControllerStubRegistration registrationB = ControllerStubs.Register(stubB))
            {
                IhcSetup labA = SetupOn(registrationA.Endpoint);
                IhcSetup labB = SetupOn(registrationB.Endpoint);
                try
                {
                    await labA.UserManagerService.AddUser(NewUser("isolated_user"));

                    Assert.That((await labA.UserManagerService.GetUsers(false)).Select(user => user.Username), Contains.Item("isolated_user"));
                    Assert.That(stubA.UserManager.Users.Select(user => user.Username), Contains.Item("isolated_user"));
                    Assert.That((await labB.UserManagerService.GetUsers(false)).Select(user => user.Username), Has.No.Member("isolated_user"),
                        "a second stub holds its own store");
                }
                finally
                {
                    labA.Dispose();
                    labB.Dispose();
                }
            }
            stubA.VerifyNoFaults();
            stubB.VerifyNoFaults();
        }
    }
}
