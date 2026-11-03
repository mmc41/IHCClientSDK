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

        /// <summary>
        /// A real controller answers an unset e-mail, phone and project nil, with a line break inside each element,
        /// although its schema does not declare them nillable. The reply here has that captured shape, with a
        /// synthetic account; through the whole SDK the three read as null, so a read-modify-write sends no line
        /// break back.
        /// </summary>
        [Test]
        public async Task AUserWithNilStrings_ReadsThemAsNull()
        {
            const string reply =
                "<SOAP-ENV:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                "<SOAP-ENV:Body><ns1:getUsers1 xmlns:ns1=\"utcs\"><ns1:arrayItem xsi:type=\"ns1:WSUser\">" +
                "<ns1:username xsi:type=\"xsd:string\">installer</ns1:username>" +
                "<ns1:email xsi:type=\"xsd:string\" xsi:nil=\"true\">\n</ns1:email>" +
                "<ns1:firstname xsi:type=\"xsd:string\">Ida</ns1:firstname>" +
                "<ns1:lastname xsi:type=\"xsd:string\">Installatør</ns1:lastname>" +
                "<ns1:phone xsi:type=\"xsd:string\" xsi:nil=\"true\">\n</ns1:phone>" +
                "<ns1:group xsi:type=\"ns1:WSUserGroup\"><ns1:type xsi:type=\"xsd:string\">text.usermanager.group_administrators</ns1:type></ns1:group>" +
                "<ns1:project xsi:type=\"xsd:string\" xsi:nil=\"true\">\n</ns1:project>" +
                "</ns1:arrayItem></ns1:getUsers1></SOAP-ENV:Body></SOAP-ENV:Envelope>";
            var stub = new ControllerStub();
            stub.OverrideRaw("UserManagerService", "getUsers", (_, _) => Task.FromResult(new ControllerStubResponse(200, reply)));
            using var sdk = StubbedSdk.Start(stub);

            IhcUser user = (await sdk.UserManager.GetUsers()).Single();

            Assert.That((user.Email, user.Phone, user.Project), Is.EqualTo(((string?)null, (string?)null, (string?)null)));
            Assert.That((user.Username, user.Firstname, user.Lastname), Is.EqualTo(("installer", "Ida", "Installatør")));
        }
    }
}
