using System;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Timemanager;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller's CLOCK, in both directions.
    ///
    /// <see cref="TimeManagerSettings"/> is the controller's time source, DST rule and timezone offset.
    /// A wrong write here makes every schedule in a real building fire at the wrong time - silently,
    /// persistently, and noticed only much later by someone who has no reason to suspect the clock. It
    /// is the Critical tier's shape exactly, and the whole service sat at zero coverage because nothing
    /// above the socket could be substituted until it grew a test seam.
    /// </summary>
    [TestFixture]
    public class TimeManagerServiceMappingTests
    {
        private sealed class Harness
        {
            internal required TimeManagerService Service { get; init; }
            internal required Ihc.Soap.Timemanager.TimeManagerService Soap { get; init; }
            internal WSTimeManagerSettings? Sent { get; set; }
        }

        private static Harness NewHarness(int? setSettingsAnswer = 1)
        {
            var soap = A.Fake<Ihc.Soap.Timemanager.TimeManagerService>();
            var harness = new Harness { Service = new TimeManagerService(FakeSession.Over(), soap), Soap = soap };

            A.CallTo(() => soap.setSettingsAsync(A<inputMessageName4>._))
                .Invokes((inputMessageName4 m) => harness.Sent = m.setSettings1)
                .Returns(Task.FromResult(new outputMessageName4(setSettingsAnswer)));

            return harness;
        }

        /// <summary>
        /// One fully populated settings block. The timestamp is stated at the WS offset because the
        /// wire carries a bare clock face and no offset of its own - see
        /// <see cref="SetSettings_ConvertsTheTimestampToTheWireOffsetBeforeWritingItsClockFace"/>.
        /// </summary>
        private static TimeManagerSettings Configured() => new()
        {
            SynchroniseTimeAgainstServer = true,
            UseDST = true,
            GmtOffsetInHours = 1,
            ServerName = "dk.pool.ntp.org",
            SyncIntervalInHours = 24,
            TimeAndDateInUTC = new DateTimeOffset(2026, 3, 4, 9, 30, 15, DateHelper.GetWSTimeOffset()),
            OnlineCalendarUpdateOnline = true,
            OnlineCalendarCountry = "DK",
            OnlineCalendarValidUntil = 2030
        };


        // The envelope SetSettings sends for the settings above, written out rather than derived so a
        // change to the mapping or to the generated layer's element naming has to be adopted here.
        private const string SetSettingsRequestXml = """
        <soapenv:Envelope xmlns:utcs="utcs" xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Header />
          <soapenv:Body>
            <utcs:setSettings1>
              <utcs:synchroniseTimeAgainstServer>true</utcs:synchroniseTimeAgainstServer>
              <utcs:useDST>true</utcs:useDST>
              <utcs:gmtOffsetInHours>1</utcs:gmtOffsetInHours>
              <utcs:serverName>dk.pool.ntp.org</utcs:serverName>
              <utcs:syncIntervalInHours>24</utcs:syncIntervalInHours>
              <utcs:timeAndDateInUTC>
                <utcs:monthWithJanuaryAsOne>3</utcs:monthWithJanuaryAsOne>
                <utcs:day>4</utcs:day>
                <utcs:hours>9</utcs:hours>
                <utcs:minutes>30</utcs:minutes>
                <utcs:seconds>15</utcs:seconds>
                <utcs:year>2026</utcs:year>
              </utcs:timeAndDateInUTC>
              <utcs:online_calendar_update_online>true</utcs:online_calendar_update_online>
              <utcs:online_calendar_country>DK</utcs:online_calendar_country>
              <utcs:online_calendar_valid_until>2030</utcs:online_calendar_valid_until>
            </utcs:setSettings1>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

        [Test]
        public async Task SetSettings_SerializesTheSettingsVerbatimOnTheWire()
        {
            Harness h = NewHarness();

            await h.Service.SetSettings(Configured());

            string actual = SoapRequestText.Of(new inputMessageName4(h.Sent));

            Assert.That(SoapRequestText.Normalized(actual),
                Is.EqualTo(SoapRequestText.Normalized(SetSettingsRequestXml)));
        }

        /// <summary>
        /// The field-preservation law over the pair of mappings: the clock the controller is told to
        /// keep is the clock it reports back.
        /// </summary>
        [Test]
        public async Task Settings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();
            TimeManagerSettings sent = Configured();

            await h.Service.SetSettings(sent);
            A.CallTo(() => h.Soap.getSettingsAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3(h.Sent)));

            Assert.That(await h.Service.GetSettings(), Is.EqualTo(sent));
        }

        /// <summary>
        /// Unlike the user mapping, this one converts to the WS offset BEFORE writing the clock face -
        /// so a timestamp stated anywhere else still reaches the controller as the same instant.
        /// </summary>
        [Test]
        public async Task SetSettings_ConvertsTheTimestampToTheWireOffsetBeforeWritingItsClockFace()
        {
            Harness h = NewHarness();
            var utcNoon = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero);

            await h.Service.SetSettings(Configured() with { TimeAndDateInUTC = utcNoon });

            WSDate written = h.Sent!.timeAndDateInUTC;
            DateTimeOffset atWireOffset = utcNoon.ToOffset(DateHelper.GetWSTimeOffset());
            Assert.Multiple(() =>
            {
                Assert.That(written.hours, Is.EqualTo(atWireOffset.Hour));
                Assert.That(written.minutes, Is.EqualTo(atWireOffset.Minute));
                Assert.That(written.day, Is.EqualTo(atWireOffset.Day));
                Assert.That(written.monthWithJanuaryAsOne, Is.EqualTo(atWireOffset.Month));
                Assert.That(written.year, Is.EqualTo(atWireOffset.Year));
            });
        }

        /// <summary>
        /// The controller acknowledges a settings write with an int, and only 1 means it took. Anything
        /// else - a different number, or no answer at all - must report false rather than leaving the
        /// caller believing the building's clock was changed.
        /// </summary>
        [TestCase(1, true)]
        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(null, false)]
        public async Task SetSettings_ReportsSuccessOnlyForTheControllersAcknowledgement(int? answer, bool expected)
        {
            Harness h = NewHarness(answer);

            Assert.That(await h.Service.SetSettings(Configured()), Is.EqualTo(expected));
        }

        /// <summary>A server name past what the controller accepts must not be written at all.</summary>
        [Test]
        public void SetSettings_WithAnOverlongServerName_IsRefusedBeforeItReachesTheController()
        {
            Harness h = NewHarness();

            Assert.CatchAsync(async () => await h.Service.SetSettings(
                Configured() with { ServerName = new string('x', 21) }));

            Assert.That(h.Sent, Is.Null);
        }

        [Test]
        public async Task GetSettings_WithNoAnswer_IsNull()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getSettingsAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3(null)));

            Assert.That(await h.Service.GetSettings(), Is.Null);
        }

        // ---------------------------------------------------------------- the readings

        [Test]
        public async Task GetCurrentLocalTime_ReadsTheControllersClockAtTheWireOffset()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getCurrentLocalTimeAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(new WSDate
                {
                    year = 2026, monthWithJanuaryAsOne = 3, day = 4, hours = 9, minutes = 30, seconds = 15
                })));

            Assert.That(await h.Service.GetCurrentLocalTime(),
                Is.EqualTo(new DateTimeOffset(2026, 3, 4, 9, 30, 15, DateHelper.GetWSTimeOffset())));
        }

        /// <summary>
        /// A clock the controller did not report is not a clock at midnight year one - but that is what
        /// the caller gets, so the value is pinned rather than left to be discovered by whoever renders
        /// it. <see cref="DateTimeOffset.MinValue"/> is the SDK's "no reading" throughout.
        /// </summary>
        [Test]
        public async Task GetCurrentLocalTime_WithNoAnswer_IsTheNoReadingValue()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getCurrentLocalTimeAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(null)));

            Assert.That(await h.Service.GetCurrentLocalTime(), Is.EqualTo(DateTimeOffset.MinValue));
        }

        [TestCase(90_000L, 90_000d)]
        [TestCase(0L, 0d)]
        public async Task GetUptime_ReadsTheControllersUptimeAsMilliseconds(long reported, double expectedMs)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getUptimeAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(reported)));

            Assert.That(await h.Service.GetUptime(), Is.EqualTo(TimeSpan.FromMilliseconds(expectedMs)));
        }

        [Test]
        public async Task GetUptime_WithNoAnswer_IsZero()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getUptimeAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(null)));

            Assert.That(await h.Service.GetUptime(), Is.EqualTo(TimeSpan.Zero));
        }

        /// <summary>
        /// The time-server reading carries its date as epoch MILLISECONDS, unlike every other date on
        /// this service - so its conversion is a second, separate hand-written mapping.
        /// </summary>
        [Test]
        public async Task GetTimeFromServer_MapsTheEpochMillisecondsReading()
        {
            Harness h = NewHarness();
            var expected = new DateTimeOffset(2026, 3, 4, 9, 30, 15, TimeSpan.Zero);
            A.CallTo(() => h.Soap.getTimeFromServerAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(new WSTimeServerConnectionResult
                {
                    connectionWasSuccessful = true,
                    dateFromServer = expected.ToUnixTimeMilliseconds(),
                    connectionFailedDueToUnknownHost = false,
                    connectionFailedDueToOtherErrors = false
                })));

            TimeServerConnectionResult? result = await h.Service.GetTimeFromServer();

            Assert.Multiple(() =>
            {
                Assert.That(result!.ConnectionWasSuccessful, Is.True);
                Assert.That(result.DateFromServer, Is.EqualTo(expected));
                Assert.That(result.ConnectionFailedDueToUnknownHost, Is.False);
                Assert.That(result.ConnectionFailedDueToOtherErrors, Is.False);
            });
        }

        /// <summary>
        /// A failed sync reports no date, and epoch zero is not a date - it is the absence of one, so it
        /// must not be handed to a caller as 1970.
        /// </summary>
        [Test]
        public async Task GetTimeFromServer_WhenTheSyncFailed_ReportsNoDateRatherThanTheEpoch()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getTimeFromServerAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(new WSTimeServerConnectionResult
                {
                    connectionWasSuccessful = false,
                    dateFromServer = 0,
                    connectionFailedDueToUnknownHost = true
                })));

            TimeServerConnectionResult? result = await h.Service.GetTimeFromServer();

            Assert.Multiple(() =>
            {
                Assert.That(result!.ConnectionWasSuccessful, Is.False);
                Assert.That(result.ConnectionFailedDueToUnknownHost, Is.True);
                Assert.That(result.DateFromServer, Is.EqualTo(DateTimeOffset.MinValue));
            });
        }

        [Test]
        public async Task GetTimeFromServer_WithNoAnswer_IsNull()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getTimeFromServerAsync(A<inputMessageName1>._))
                .Returns(Task.FromResult(new outputMessageName1(null)));

            Assert.That(await h.Service.GetTimeFromServer(), Is.Null);
        }
    }
}
