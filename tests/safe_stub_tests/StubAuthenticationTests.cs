using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The authentication service's three operations, answered from the stub's typed values and read through the
    /// real <see cref="AuthenticationService"/>.
    /// </summary>
    [TestFixture]
    public class StubAuthenticationTests
    {
        [Test]
        public async Task EveryOperation_RunsThroughTheRealWrapper()
        {
            using var sdk = StubbedSdk.Start();

            IhcUser user = await sdk.Authentication.Authenticate();
            bool pinged = await sdk.Authentication.Ping();
            bool connected = await sdk.Authentication.IsAuthenticated();
            await sdk.Authentication.Disconnect();

            Assert.That(user.Username, Is.EqualTo(sdk.Settings.UserName));
            Assert.That((pinged, connected), Is.EqualTo((true, true)));
            Assert.That(sdk.Stub.Journal.All.Select(call => (call.SoapAction, call.Outcome)), Is.EqualTo(new[]
            {
                ("authenticate", StubOutcome.Typed), ("ping", StubOutcome.Typed), ("disconnect", StubOutcome.Typed),
            }));
        }

        [Test]
        public async Task ALogin_IssuesAFreshCookie_EachTime()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Authentication.Authenticate();
            await sdk.Authentication.Authenticate();

            var cookies = sdk.Stub.Journal.Calls<IAuthenticationService>(nameof(IAuthenticationService.Authenticate))
                .Select(call => call.SetCookie).ToList();
            Assert.That(cookies, Has.Count.EqualTo(2).And.Unique);
            Assert.That(cookies, Is.All.StartsWith("JSESSIONID=stub-"));
        }

        [TestCase("any-password")]
        [TestCase("a real password from a settings file")]
        public async Task AnyNonBlankPassword_LogsIn(string password)
        {
            using var sdk = StubbedSdk.Start();

            IhcUser user = await sdk.Authentication.Authenticate("installer", password);

            Assert.That(user.Username, Is.EqualTo("installer"));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(ControllerStub.IllegalPassword)]
        public async Task ABlankOrTheIllegalPassword_IsRefusedAsAnInvalidAccount(string password)
        {
            using var sdk = StubbedSdk.Start();

            var refusal = Assert.ThrowsAsync<ErrorWithCodeException>(() => sdk.Authentication.Authenticate("installer", password));

            Assert.That(refusal!.ErrorCode, Is.EqualTo(Errors.LOGIN_FAILED_DUE_TO_ACCOUNT_INVALID_ERROR));
            Assert.That(await sdk.Authentication.IsAuthenticated(), Is.False);
            StubCall login = sdk.Stub.Journal.All.Single();
            Assert.That((login.Payload, login.SetCookie), Is.EqualTo(("installer", (string?)null)));
        }

        /// <summary>The journal holds the user name of a login, never its password, whether it was accepted or not.</summary>
        [Test]
        public async Task TheJournal_RecordsNoPassword()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Authentication.Authenticate();
            Assert.ThrowsAsync<ErrorWithCodeException>(() => sdk.Authentication.Authenticate("installer", ControllerStub.IllegalPassword));

            string journal = string.Join("\n", sdk.Stub.Journal.All);
            Assert.That(journal, Does.Not.Contain(StubbedSdk.Password).And.Not.Contain(ControllerStub.IllegalPassword));
            Assert.That(sdk.Stub.Journal.All.Select(call => call.Payload),
                Is.EqualTo(new object[] { sdk.Settings.UserName!, "installer" }));
        }

        [Test]
        public async Task ALoginWithNilDates_IsAccepted()
        {
            var stub = new ControllerStub();
            stub.Authentication.User = stub.Authentication.User with { CreatedDate = DateTimeOffset.MinValue, LoginDate = DateTimeOffset.MinValue };
            using var sdk = StubbedSdk.Start(stub);

            IhcUser user = await sdk.Authentication.Authenticate();

            Assert.That((user.CreatedDate, user.LoginDate), Is.EqualTo((DateTimeOffset.MinValue, DateTimeOffset.MinValue)));
        }

        /// <summary>
        /// A non-default user set on the stub comes back equal through the SDK's own mapper, which checks the stub's
        /// reverse mapper and the restated wire offset independently of how either was written.
        /// </summary>
        [TestCase(IhcUserGroup.Administrators)]
        [TestCase(IhcUserGroup.Users)]
        [TestCase(IhcUserGroup.None)]
        public async Task ATypedUser_RoundTripsThroughTheRealWrapper(IhcUserGroup group)
        {
            var expected = new IhcUser
            {
                Username = "round-trip",
                Password = "on-the-wire",
                Firstname = "Round",
                Lastname = "Trip",
                Email = "rt@stub.invalid",
                Phone = "12345678",
                Group = group,
                Project = "rt-project",
                CreatedDate = new DateTimeOffset(2021, 5, 6, 7, 8, 9, TimeSpan.FromHours(2)),
                LoginDate = new DateTimeOffset(2023, 12, 31, 23, 59, 58, TimeSpan.Zero),
            };
            var stub = new ControllerStub();
            stub.Authentication.User = expected;
            using var sdk = StubbedSdk.Start(stub);

            IhcUser user = await sdk.Authentication.Authenticate(expected.Username, StubbedSdk.Password);

            Assert.That(user, Is.EqualTo(expected));
        }
    }
}
