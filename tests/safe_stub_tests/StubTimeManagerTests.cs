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

        /// <summary>A default controller reads one moment whichever service reports its clock.</summary>
        [Test]
        public async Task TheSystemInfosClock_ReadsTheTimeManagersMoment()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That((await sdk.Configuration.GetSystemInfo()).Realtimeclock, Is.EqualTo(await sdk.Time.GetCurrentLocalTime()));
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
            Assert.That(sdk.Stub.Journal.Calls<ITimeManagerService>(nameof(ITimeManagerService.SetSettings)).Single().Payload, Is.EqualTo(changed));
        }

        [Test]
        public async Task TheDefaultServerOutcome_IsASuccess_AtTheStubsInstant()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Time.GetTimeFromServer(), Is.EqualTo(new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = true,
                DateFromServer = Instant,
            }));
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

            Assert.That(await sdk.Time.GetTimeFromServer(), Is.EqualTo(success));
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

            Assert.That(await sdk.Time.GetTimeFromServer(), Is.EqualTo(unknownHost));
        }
    }
}
