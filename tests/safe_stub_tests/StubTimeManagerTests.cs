using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The time manager answers from typed values, read through the real <see cref="TimeManagerService"/>: a fixed
    /// clock, one settings property a set replaces, and a time-server outcome.
    /// </summary>
    [TestFixture]
    public class StubTimeManagerTests
    {
        private static readonly DateTimeOffset Instant = new(ShapeDefaults.Instant);

        [Test]
        public async Task TheClock_IsFixed_AndReadsWhatTheScenarioSets()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Time.GetCurrentLocalTime(), Is.EqualTo(Instant));
            Assert.That(await sdk.Time.GetCurrentLocalTime(), Is.EqualTo(Instant), "a second read is the same moment");

            var later = new DateTimeOffset(2026, 10, 30, 15, 4, 48, TimeSpan.FromHours(1));
            stub.Time.Now = later;
            Assert.That(await sdk.Time.GetCurrentLocalTime(), Is.EqualTo(later));
        }

        /// <summary>
        /// The local clock and the settings' UTC clock are two readings of one clock, so setting it moves both: the
        /// SDK, which takes the offset from the difference between them, still reads the controller's own offset.
        /// </summary>
        [Test]
        public async Task SettingTheClock_MovesBothOfItsReadings()
        {
            var stub = new ControllerStub();
            stub.Time.UtcOffset = TimeSpan.FromHours(2);
            using var sdk = StubbedSdk.Start(stub);
            DateTimeOffset anHourLater = Instant.AddHours(1);

            stub.Time.Now = anHourLater;

            DateTimeOffset local = await sdk.Time.GetCurrentLocalTime();
            Assert.That((local, local.Offset), Is.EqualTo((anHourLater, TimeSpan.FromHours(2))));
            Assert.That((await sdk.Time.GetSettings())!.TimeAndDateInUTC, Is.EqualTo(anHourLater));
            Assert.That(stub.Time.Settings.TimeAndDateInUTC, Is.EqualTo(anHourLater));
        }

        /// <summary>
        /// A controller in summer time: its local clock reads two hours past its UTC clock, and the SDK reads that
        /// offset from the two clocks in one call rather than assuming +01:00.
        /// </summary>
        [Test]
        public async Task TheLocalClock_IsAtTheControllersOwnOffset()
        {
            var stub = new ControllerStub();
            stub.Time.UtcOffset = TimeSpan.FromHours(2);
            using var sdk = StubbedSdk.Start(stub);

            DateTimeOffset local = await sdk.Time.GetCurrentLocalTime();

            Assert.That(local.Offset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(local, Is.EqualTo(Instant), "the same moment as the UTC clock");
            Assert.That(sdk.Stub.Journal.All.Select(call => call.SoapAction), Is.EqualTo(new[] { "getCurrentLocalTime", "getSettings" }));
        }

        /// <summary>A default controller reads one moment whichever service reports its clock.</summary>
        [Test]
        public async Task TheSystemInfosClock_ReadsTheTimeManagersMoment()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That((await sdk.Configuration.GetSystemInfo()).Realtimeclock, Is.EqualTo(await sdk.Time.GetCurrentLocalTime()));
        }

        /// <summary>
        /// Two services report the controller's UTC clock: the system information as an <c>xsd:dateTime</c> with a
        /// <c>Z</c>, and the time settings as a bare clock face in <c>timeAndDateInUTC</c>. Read through the SDK, both
        /// are one instant at offset 0, which a +01:00 reading of the clock face would put an hour early.
        /// </summary>
        [Test]
        public async Task TheSettingsUtcClock_ReadsTheSystemInfosRealtimeClock()
        {
            using var sdk = StubbedSdk.Start();

            DateTimeOffset settingsClock = (await sdk.Time.GetSettings())!.TimeAndDateInUTC;

            Assert.That(settingsClock, Is.EqualTo((await sdk.Configuration.GetSystemInfo()).Realtimeclock));
            Assert.That(settingsClock.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public async Task TheDefaultSettings_AreTheCapturedControllers()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Time.GetSettings(), Is.EqualTo(new ControllerStub().Time.Settings));
        }

        [Test]
        public async Task SetSettings_ReadsBack_AndIsJournaled()
        {
            using var sdk = StubbedSdk.Start();
            var changed = new TimeManagerSettings
            {
                SynchroniseTimeAgainstServer = true,
                UseDST = false,
                GmtOffsetInHours = -3,
                ServerName = "dk.pool.ntp.org",
                SyncIntervalInHours = 24,
                TimeAndDateInUTC = new DateTimeOffset(2026, 10, 30, 14, 52, 6, TimeSpan.Zero),
                OnlineCalendarUpdateOnline = true,
                OnlineCalendarCountry = "se",
                OnlineCalendarValidUntil = 2030,
            };

            Assert.That(await sdk.Time.SetSettings(changed), Is.True);

            Assert.That(await sdk.Time.GetSettings(), Is.EqualTo(changed));
            Assert.That(sdk.Stub.Time.Settings, Is.EqualTo(changed));
            Assert.That(await sdk.Time.GetCurrentLocalTime(), Is.EqualTo(changed.TimeAndDateInUTC), "the clock written is the clock");
            Assert.That(sdk.Stub.Journal.Calls<ITimeManagerService>(nameof(ITimeManagerService.SetSettings)).Single().Payload, Is.EqualTo(changed));
        }

        /// <summary>The controller's refusals, TIMESETTING_FAILED (1) and CALENDAR_NOT_AVAILABLE (2), read as a declined
        /// write, and the stub keeps the settings it had.</summary>
        [TestCase(1)]
        [TestCase(2)]
        public async Task ARefusedSetSettings_AnswersFalse_AndKeepsTheSettings(int resultCode)
        {
            var stub = new ControllerStub();
            TimeManagerSettings before = stub.Time.Settings;
            stub.Time.SetSettingsResultCode = resultCode;
            using var sdk = StubbedSdk.Start(stub);

            bool accepted = await sdk.Time.SetSettings(before with { ServerName = "dk.pool.ntp.org", GmtOffsetInHours = -3 });

            Assert.That(accepted, Is.False);
            Assert.That(await sdk.Time.GetSettings(), Is.EqualTo(before));
        }

        [Test]
        public async Task TheDefaultServerOutcome_IsASuccess_AtTheStubsInstant()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Time.GetTimeFromServer("dk.pool.ntp.org"), Is.EqualTo(new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = true,
                DateFromServer = Instant,
            }));
        }

        /// <summary>The server to test reaches the stub through the real wrapper and serializer, and is journalled:
        /// here the one IHC Administrator was captured sending.</summary>
        [Test]
        public async Task TheServerToTest_IsSent_AndJournalled()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Time.GetTimeFromServer("1.schneider.pool.ntp.org");

            Assert.That(sdk.Stub.Journal.Calls<ITimeManagerService>(nameof(ITimeManagerService.GetTimeFromServer)).Single().Payload,
                Is.EqualTo("1.schneider.pool.ntp.org"));
        }

        [Test]
        public async Task AServerOutcomeSetOnTheScenario_CarriesItsDate_ToTheMillisecond()
        {
            var stub = new ControllerStub();
            var success = new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = true,
                DateFromServer = new DateTimeOffset(2026, 10, 30, 14, 52, 6, 250, TimeSpan.Zero),
            };
            stub.Time.ServerResult = success;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Time.GetTimeFromServer("dk.pool.ntp.org"), Is.EqualTo(success));
        }

        /// <summary>The captured controller's outcome: it could not resolve its server, so it sent no date.</summary>
        [Test]
        public async Task TheCapturedUnknownHostOutcome_ReadsBackWithNoDate()
        {
            var stub = new ControllerStub();
            var unknownHost = new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = false,
                DateFromServer = DateTimeOffset.MinValue,
                ConnectionFailedDueToUnknownHost = true,
            };
            stub.Time.ServerResult = unknownHost;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Time.GetTimeFromServer("dk.pool.ntp.org"), Is.EqualTo(unknownHost));
        }
    }
}
