using System;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Usermanager;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The OUTBOUND half of the user mapping: what <see cref="UserManagerService.AddUser"/> and
    /// <see cref="UserManagerService.UpdateUser"/> actually put on the wire.
    ///
    /// This is a controller WRITE carrying credentials and access rights, so a dropped or transposed
    /// field either grants the wrong rights or silently discards a password change - visible to nobody
    /// until someone cannot log in. The inbound direction is exercised by
    /// <c>UserManagerServiceTelemetryTests</c> through <c>GetUsers</c>; the direction that changes the
    /// controller had no controller-free defence at all.
    ///
    /// Two independent checks, per the Critical tier: one VERBATIM envelope, which pins the wire form a
    /// controller actually receives including the element names the generated layer chooses, and a
    /// ROUND-TRIP law over the pair of mappings, which pins field preservation without either mapping
    /// getting to define what "correct" means on its own.
    /// </summary>
    [TestFixture]
    public class UserManagerServiceMappingTests
    {
        /// <summary>The service under test plus the messages it hands to the SOAP layer.</summary>
        private sealed class Harness
        {
            internal required UserManagerService Service { get; init; }
            internal inputMessageName1? Added { get; set; }
            internal inputMessageName4? Updated { get; set; }
            internal string? Removed { get; set; }
            internal WSUser[] GetUsersAnswer { get; set; } = Array.Empty<WSUser>();
        }

        private static Harness NewHarness()
        {
            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            var harness = new Harness { Service = new UserManagerService(FakeSession.Over(), soap) };

            A.CallTo(() => soap.addUserAsync(A<inputMessageName1>._))
                .Invokes((inputMessageName1 m) => harness.Added = m)
                .Returns(Task.FromResult(new outputMessageName1()));
            A.CallTo(() => soap.updateUserAsync(A<inputMessageName4>._))
                .Invokes((inputMessageName4 m) => harness.Updated = m)
                .Returns(Task.FromResult(new outputMessageName4()));
            A.CallTo(() => soap.removeUserAsync(A<inputMessageName3>._))
                .Invokes((inputMessageName3 m) => harness.Removed = m.removeUser1)
                .Returns(Task.FromResult(new outputMessageName3()));
            A.CallTo(() => soap.getUsersAsync(A<inputMessageName2>._))
                .ReturnsLazily(() => Task.FromResult(new outputMessageName2(harness.GetUsersAnswer)));

            return harness;
        }

        /// <summary>
        /// One fully populated user: every mapped field distinct, so a transposition shows. The dates are
        /// stated at the WS offset so the verbatim envelope below reads as the clock face it carries; a
        /// date at any other offset round-trips just as faithfully, which is what
        /// <see cref="AddUser_ConvertsADateToTheWsOffsetBeforeWritingItsClockFace"/> says.
        /// </summary>
        private static IhcUser Installer() => new()
        {
            Username = "installer",
            Password = "hemmelig",
            Email = "a@b.dk",
            Firstname = "Anna",
            Lastname = "Beck",
            Phone = "+4512345678",
            Group = IhcUserGroup.Administrators,
            Project = "project1",
            CreatedDate = new DateTimeOffset(2019, 10, 1, 20, 54, 24, DateHelper.GetWSTimeOffset()),
            LoginDate = new DateTimeOffset(2025, 10, 17, 18, 31, 54, DateHelper.GetWSTimeOffset())
        };


        // The envelope AddUser sends for the user above. Written out rather than derived, so a change
        // to the mapping or to the generated layer's element naming has to be adopted here deliberately.
        private const string AddUserRequestXml = """
        <soapenv:Envelope xmlns:utcs="utcs" xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header />
          <soapenv:Body>
            <utcs:addUser1>
              <utcs:createdDate>
                <utcs:day>1</utcs:day>
                <utcs:hours>20</utcs:hours>
                <utcs:minutes>54</utcs:minutes>
                <utcs:monthWithJanuaryAsOne>10</utcs:monthWithJanuaryAsOne>
                <utcs:seconds>24</utcs:seconds>
                <utcs:year>2019</utcs:year>
              </utcs:createdDate>
              <utcs:loginDate>
                <utcs:day>17</utcs:day>
                <utcs:hours>18</utcs:hours>
                <utcs:minutes>31</utcs:minutes>
                <utcs:monthWithJanuaryAsOne>10</utcs:monthWithJanuaryAsOne>
                <utcs:seconds>54</utcs:seconds>
                <utcs:year>2025</utcs:year>
              </utcs:loginDate>
              <utcs:username>installer</utcs:username>
              <utcs:password>hemmelig</utcs:password>
              <utcs:email>a@b.dk</utcs:email>
              <utcs:firstname>Anna</utcs:firstname>
              <utcs:lastname>Beck</utcs:lastname>
              <utcs:phone>+4512345678</utcs:phone>
              <utcs:group>
                <utcs:type>text.usermanager.group_administrators</utcs:type>
              </utcs:group>
              <utcs:project>project1</utcs:project>
            </utcs:addUser1>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

        [Test]
        public async Task AddUser_SerializesTheUserVerbatimOnTheWire()
        {
            Harness h = NewHarness();

            await h.Service.AddUser(Installer());

            Assert.That(h.Added, Is.Not.Null, "AddUser must reach the SOAP layer");
            string actual = SoapRequestText.Of(h.Added!);

            Assert.That(SoapRequestText.Normalized(actual),
                Is.EqualTo(SoapRequestText.Normalized(AddUserRequestXml)));
        }

        /// <summary>
        /// Create and update send the SAME user representation - they differ only in the SOAP action.
        /// Worth pinning because the two call sites build their message separately, so a fix applied to
        /// one and not the other is exactly the kind of drift that survives a review.
        /// </summary>
        [Test]
        public async Task UpdateUser_SendsTheSameUserRepresentationAsAddUser()
        {
            Harness h = NewHarness();
            IhcUser user = Installer();

            await h.Service.AddUser(user);
            await h.Service.UpdateUser(user);

            string added = SoapRequestText.Of(h.Added!);
            string updated = SoapRequestText.Of(h.Updated!);

            Assert.That(SoapRequestText.Normalized(updated.Replace("updateUser1", "addUser1", StringComparison.Ordinal)),
                Is.EqualTo(SoapRequestText.Normalized(added)));
        }

        /// <summary>
        /// The field-preservation law: a user written to the wire and read back is the same user. The
        /// two mappings are separate bodies of hand-written assignments, so this is what catches a
        /// field added to one direction and forgotten in the other.
        /// </summary>
        [TestCase(IhcUserGroup.Administrators)]
        [TestCase(IhcUserGroup.Users)]
        public async Task AddUser_ThenReadBack_PreservesEveryField(IhcUserGroup group)
        {
            Harness h = NewHarness();
            IhcUser sent = Installer() with { Group = group };

            await h.Service.AddUser(sent);
            h.GetUsersAnswer = new[] { h.Added!.addUser1 };
            IhcUser back = (await h.Service.GetUsers(includePassword: true)).Single();

            Assert.That(back, Is.EqualTo(sent));
        }

        /// <summary>
        /// The round-trip above holds for a date stated at ANY offset, and this is the case that says so.
        /// <c>WSDate</c> carries a bare clock face and no offset, so the outbound mapping converts to the
        /// WS offset before copying the fields - the offset the inbound mapping reads them back at.
        /// Copying the source's own fields instead wrote a UTC noon as though it were WS-local noon, and
        /// the read-back moved it by the difference: a timestamp that changed by an hour every time it
        /// passed through the controller.
        /// </summary>
        [Test]
        public async Task AddUser_ConvertsADateToTheWsOffsetBeforeWritingItsClockFace()
        {
            Harness h = NewHarness();
            var utcNoon = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);
            DateTimeOffset wsLocal = utcNoon.ToOffset(DateHelper.GetWSTimeOffset());

            await h.Service.AddUser(Installer() with { CreatedDate = utcNoon });
            WSDate written = h.Added!.addUser1.createdDate;
            h.GetUsersAnswer = new[] { h.Added!.addUser1 };
            IhcUser back = (await h.Service.GetUsers(includePassword: true)).Single();

            Assert.Multiple(() =>
            {
                Assert.That(DateHelper.GetWSTimeOffset(), Is.Not.EqualTo(TimeSpan.Zero),
                    "the case only means anything while the WS offset differs from UTC");
                Assert.That(written.hours, Is.EqualTo(wsLocal.Hour), "the clock face written is the WS-local one");
                Assert.That(written.year, Is.EqualTo(wsLocal.Year));
                Assert.That(written.monthWithJanuaryAsOne, Is.EqualTo(wsLocal.Month));
                Assert.That(written.day, Is.EqualTo(wsLocal.Day));
                Assert.That(back.CreatedDate, Is.EqualTo(utcNoon),
                    "and it reads back as the same instant it was stated at, not one shifted by the offset");
            });
        }

        /// <summary>
        /// The controller carries the group as a magic string, and the two directions spell it in two
        /// places. A user whose group is neither of the two known ones has no wire spelling at all, so
        /// the outbound mapping writes an empty group - which reads back as
        /// <see cref="IhcUserGroup.None"/> rather than throwing.
        /// </summary>
        [Test]
        public async Task Groups_RoundTripThroughTheirWireSpelling()
        {
            Harness h = NewHarness();

            await h.Service.AddUser(Installer() with { Group = IhcUserGroup.Administrators });
            WSUser administrators = h.Added!.addUser1;
            await h.Service.AddUser(Installer() with { Group = IhcUserGroup.Users });
            WSUser users = h.Added!.addUser1;

            Assert.Multiple(() =>
            {
                Assert.That(administrators.group.type, Is.EqualTo("text.usermanager.group_administrators"));
                Assert.That(users.group.type, Is.EqualTo("gtext.users"));
                Assert.That(UserManagerService.mapUserGroup(administrators.group.type), Is.EqualTo(IhcUserGroup.Administrators));
                Assert.That(UserManagerService.mapUserGroup(users.group.type), Is.EqualTo(IhcUserGroup.Users));
                Assert.That(UserManagerService.mapUserGroup((string?)null), Is.EqualTo(IhcUserGroup.None));
                Assert.That(() => UserManagerService.mapUserGroup("gtext.plumbers"), Throws.ArgumentException,
                    "a group the SDK does not know must be named, not silently folded into None");
            });
        }

        /// <summary>
        /// The write refusals, which are the reason both methods validate before mapping: a user the
        /// annotations reject must never reach the controller half-written.
        /// </summary>
        [Test]
        public void AddUser_WithAnInvalidUser_IsRefusedBeforeItReachesTheController()
        {
            Harness h = NewHarness();

            Assert.CatchAsync(async () => await h.Service.AddUser(Installer() with { Username = null }));

            Assert.That(h.Added, Is.Null, "validation runs before the SOAP call, not after it");
        }

        /// <summary>
        /// <see cref="UserConstants.REDACTED_PASSWORD"/> is what the SDK substitutes when a caller asked
        /// for users without passwords. Writing it back would replace the real password with the
        /// placeholder, so an update carrying it is refused - the silent-credential-loss case the tier
        /// is about.
        /// </summary>
        [Test]
        public void UpdateUser_WithTheRedactedPasswordPlaceholder_IsRefused()
        {
            Harness h = NewHarness();

            Assert.CatchAsync(async () => await h.Service.UpdateUser(Installer() with { Password = UserConstants.REDACTED_PASSWORD }));

            Assert.That(h.Updated, Is.Null);
        }

        /// <summary>The reserved account the controller needs; deleting it is refused outright.</summary>
        [Test]
        public void RemoveUser_RefusesTheReservedUsbAccount_AndPassesEveryOtherNameThrough()
        {
            Harness h = NewHarness();

            Assert.ThrowsAsync<ArgumentException>(async () => await h.Service.RemoveUser("usb"));
            Assert.That(h.Removed, Is.Null);

            Assert.DoesNotThrowAsync(async () => await h.Service.RemoveUser("installer"));
            Assert.That(h.Removed, Is.EqualTo("installer"));
        }
    }
}
