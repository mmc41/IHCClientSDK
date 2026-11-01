using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The user manager keeps one user list, read through the real <see cref="UserManagerService"/>: adding,
    /// updating and removing a user each change what the next read returns.
    /// </summary>
    [TestFixture]
    public class StubUserManagerTests
    {
        private static readonly IhcUser Kitchen = new()
        {
            Username = "kitchen",
            Password = "kitchen-pw",
            Firstname = "Køkken",
            Lastname = "Panel",
            Email = "kitchen@stub.invalid",
            Phone = "12345678",
            Group = IhcUserGroup.Users,
            Project = "stub",
            CreatedDate = new DateTimeOffset(2026, 10, 30, 15, 5, 37, TimeSpan.FromHours(1)),
            LoginDate = new DateTimeOffset(2026, 10, 30, 16, 0, 0, TimeSpan.FromHours(1)),
        };

        [Test]
        public async Task TheDefaultList_IsTheOneAdministrator()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { StubAuthentication.DefaultUser }));
        }

        [Test]
        public async Task AnAddedUser_ReadsBack_WithEveryField()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.UserManager.AddUser(Kitchen);

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { StubAuthentication.DefaultUser, Kitchen }));
            Assert.That(sdk.Stub.Journal.Calls<IUserManagerService>(nameof(IUserManagerService.AddUser)).Single().Payload,
                Is.EqualTo(Kitchen with { Password = null }), "the journal holds the user, never the password");
        }

        /// <summary>A real controller stamps the moment it adds a user, so a user added with no dates reads back
        /// with the stub's instant rather than with none.</summary>
        [Test]
        public async Task AUserAddedWithNoDates_IsStampedWithTheStubsInstant()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.UserManager.AddUser(Kitchen with { CreatedDate = DateTimeOffset.MinValue, LoginDate = DateTimeOffset.MinValue });

            IhcUser added = (await sdk.UserManager.GetUsers()).Single(user => user.Username == Kitchen.Username);
            var instant = new DateTimeOffset(ShapeDefaults.Instant);
            Assert.That((added.CreatedDate, added.LoginDate), Is.EqualTo((instant, instant)));
        }

        [Test]
        public async Task AnUpdatedUser_ReadsBackChanged()
        {
            using var sdk = StubbedSdk.Start();
            await sdk.UserManager.AddUser(Kitchen);
            IhcUser changed = Kitchen with { Password = "new-pw", Firstname = "Stue", Group = IhcUserGroup.Administrators };

            await sdk.UserManager.UpdateUser(changed);

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { StubAuthentication.DefaultUser, changed }));
        }

        [Test]
        public async Task ARemovedUser_IsGone()
        {
            using var sdk = StubbedSdk.Start();
            await sdk.UserManager.AddUser(Kitchen);

            await sdk.UserManager.RemoveUser(Kitchen.Username!);

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { StubAuthentication.DefaultUser }));
            Assert.That(sdk.Stub.Journal.Calls<IUserManagerService>(nameof(IUserManagerService.RemoveUser)).Single().Payload, Is.EqualTo(Kitchen.Username));
        }

        /// <summary>A user is known by name, so adding a name the list already holds replaces that user, and an
        /// update or removal of an unknown name changes nothing.</summary>
        [Test]
        public async Task AUserIsKnownByName()
        {
            using var sdk = StubbedSdk.Start();
            await sdk.UserManager.AddUser(Kitchen);
            IhcUser again = Kitchen with { Phone = "87654321" };

            await sdk.UserManager.AddUser(again);
            await sdk.UserManager.UpdateUser(Kitchen with { Username = "nobody" });
            await sdk.UserManager.RemoveUser("nobody");

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { StubAuthentication.DefaultUser, again }));
        }

        [Test]
        public async Task AListSetOnTheScenario_IsWhatTheSdkReads()
        {
            var stub = new ControllerStub();
            stub.UserManager.Users = [Kitchen];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.UserManager.GetUsers(), Is.EquivalentTo(new[] { Kitchen }));
            Assert.That(() => stub.UserManager.Users = default(ImmutableArray<IhcUser>), Throws.ArgumentException);
        }
    }
}
