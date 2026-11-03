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

        private static Harness NewHarness(int? setSettingsAnswer = 0)
        {
            var soap = A.Fake<Ihc.Soap.Timemanager.TimeManagerService>();
            var harness = new Harness { Service = new TimeManagerService(FakeSession.Over(), soap), Soap = soap };

            A.CallTo(() => soap.setSettingsAsync(A<inputMessageName4>._))
                .Invokes((inputMessageName4 m) => harness.Sent = m.setSettings1)
                .Returns(Task.FromResult(new outputMessageName4(setSettingsAnswer)));

            return harness;
        }

        /// <summary>
        /// One fully populated settings block. The timestamp is stated in UTC because the wire carries a bare
        /// clock face, and in <c>timeAndDateInUTC</c> that face is the controller's UTC clock - see
        /// <see cref="SetSettings_WritesTheInstantsUtcClockFace"/>.
        /// </summary>
        private static TimeManagerSettings Configured() => new()
        {
            SynchroniseTimeAgainstServer = true,
            UseDST = true,
            GmtOffsetInHours = 1,
            ServerName = "dk.pool.ntp.org",
            SyncIntervalInHours = 24,
            TimeAndDateInUTC = new DateTimeOffset(2026, 3, 4, 9, 30, 15, TimeSpan.Zero),
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

        /// <summary>
        /// IHC Administrator offers offsets from -12 to +14 only, and with synchronisation on it requires an interval
        /// of at least one hour and a server to synchronise with; the SDK refuses the same before sending.
        /// </summary>
        [TestCase(-13, true, 24, "dk.pool.ntp.org", "GmtOffsetInHours")]
        [TestCase(15, true, 24, "dk.pool.ntp.org", "GmtOffsetInHours")]
        [TestCase(1, true, 0, "dk.pool.ntp.org", "SyncIntervalInHours")]
        [TestCase(1, true, 24, " ", "ServerName")]
        [TestCase(1, true, 24, null, "ServerName")]
        public void SetSettings_RefusesWhatTheAdministratorRefuses_AndSendsNothing(int offset, bool sync, int interval, string? server, string named)
        {
            Harness h = NewHarness();

            Assert.That(async () => await h.Service.SetSettings(Configured() with
                {
                    GmtOffsetInHours = offset, SynchroniseTimeAgainstServer = sync, SyncIntervalInHours = interval, ServerName = server,
                }),
                Throws.ArgumentException.With.Message.Contains(named));
            Assert.That(h.Sent, Is.Null);
        }

        /// <summary>With synchronisation off the interval and server are kept but not used, so neither is
        /// required; the offset's ends are allowed.</summary>
        [TestCase(-12, 0, null)]
        [TestCase(14, 0, "")]
        public async Task SetSettings_WithSyncOff_NeedsNoIntervalOrServer(int offset, int interval, string? server)
        {
            Harness h = NewHarness();

            await h.Service.SetSettings(Configured() with
            {
                GmtOffsetInHours = offset, SynchroniseTimeAgainstServer = false, SyncIntervalInHours = interval, ServerName = server,
            });

            Assert.That(h.Sent, Is.Not.Null);
        }

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
        /// <c>timeAndDateInUTC</c> is the controller's clock in UTC: IHC Visual sent its pickers' time less one hour
        /// from a UTC+1 PC. So an instant stated at any offset is written as its UTC clock face - here 00:30 on
        /// 5 March at +02:00 is 22:30 on 4 March, the day before.
        /// </summary>
        [Test]
        public async Task SetSettings_WritesTheInstantsUtcClockFace()
        {
            Harness h = NewHarness();
            var halfPastMidnightAtPlusTwo = new DateTimeOffset(2026, 3, 5, 0, 30, 0, TimeSpan.FromHours(2));

            await h.Service.SetSettings(Configured() with { TimeAndDateInUTC = halfPastMidnightAtPlusTwo });

            WSDate written = h.Sent!.timeAndDateInUTC;
            Assert.That((written.year, written.monthWithJanuaryAsOne, written.day, written.hours, written.minutes, written.seconds),
                Is.EqualTo((2026, 3, 4, 22, 30, 0)));
        }

        /// <summary>
        /// The read side of the same field: a <c>timeAndDateInUTC</c> clock face is a UTC instant, offset 0, so it
        /// equals the <c>realtimeclock</c> the controller reports with a <c>Z</c>. The face is synthetic, in the shape
        /// a getSettings reply carries.
        /// </summary>
        [Test]
        public async Task GetSettings_ReadsTheUtcClockFaceAtOffsetZero()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getSettingsAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3(new WSTimeManagerSettings
                {
                    serverName = "1.schneider.pool.ntp.org",
                    timeAndDateInUTC = new WSDate { year = 2025, monthWithJanuaryAsOne = 10, day = 14, hours = 10, minutes = 18, seconds = 48 },
                })));

            DateTimeOffset read = (await h.Service.GetSettings())!.TimeAndDateInUTC;

            Assert.That(read, Is.EqualTo(new DateTimeOffset(2025, 10, 14, 10, 18, 48, TimeSpan.Zero)));
            Assert.That(read.Offset, Is.EqualTo(TimeSpan.Zero));
        }

        /// <summary>
        /// The controller answers a settings write with a result code, and only 0 means it took: the vendor names
        /// them RESULT_OK = 0, TIMESETTING_FAILED = 1 and CALENDAR_NOT_AVAILABLE = 2, and both IHC Administrator and
        /// IHC Visual report success on 0 alone. A refusal must read false rather than leave the caller believing the
        /// building's clock was changed.
        /// </summary>
        [TestCase(0, true)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        public async Task SetSettings_ReportsSuccessOnlyForResultCodeZero(int answer, bool expected)
        {
            Harness h = NewHarness(answer);

            Assert.That(await h.Service.SetSettings(Configured()), Is.EqualTo(expected));
        }

        /// <summary>
        /// A reply with no result code is not a refusal: the settings were sent, and the answer did not say what
        /// became of them. Folded to <c>false</c> it would read as a declined write, so it throws instead.
        /// </summary>
        [Test]
        public void SetSettings_WithNoResultCode_ThrowsRatherThanReportingADeclinedWrite()
        {
            Harness h = NewHarness(null);

            Assert.That(async () => await h.Service.SetSettings(Configured()),
                Throws.InvalidOperationException.With.Message.Contains("unknown"));
            Assert.That(h.Sent, Is.Not.Null, "the settings were sent before the reply was read");
        }

        /// <summary>
        /// A captured controller stores <c>1.schneider.pool.ntp.org</c>, 24 characters, and the vendor's help names the
        /// 24-character <c>0.schneider.pool.ntp.org</c> as the default; the WSDL declares the name an unrestricted
        /// string, and the Administrator's 20 columns are only its field's width. So settings read from such a
        /// controller write back as they are, the name sent whole.
        /// </summary>
        [Test]
        public async Task SetSettings_WithTheCapturedControllersServerName_SendsItWhole()
        {
            Harness h = NewHarness();

            Assert.That(await h.Service.SetSettings(Configured() with { ServerName = "1.schneider.pool.ntp.org" }), Is.True);

            Assert.That(h.Sent!.serverName, Is.EqualTo("1.schneider.pool.ntp.org"));
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

        /// <summary>
        /// The controller's local clock carries no offset, and a fixed one is wrong for part of the year, so the
        /// offset is the controller's own: its local clock face less its UTC clock face, read in the same call and
        /// rounded to the nearest 15 minutes, which absorbs the second or so between the two reads.
        /// </summary>
        /// <remarks>
        /// The first case holds captured clock faces: a controller set to GMT+1 with summer time read 10:18:48 on its
        /// UTC clock and 12:18:49 on its local clock a second later, in October, so UTC+2; the date is synthetic. The
        /// others are synthetic: winter at UTC+1, and a half-hour zone.
        /// </remarks>
        [TestCase(12, 18, 49, 10, 18, 48, 120, TestName = "GetCurrentLocalTime_InSummerTime_IsAtPlusTwo_TheCapturedCase")]
        [TestCase(9, 30, 15, 8, 30, 14, 60, TestName = "GetCurrentLocalTime_InWinter_IsAtPlusOne")]
        [TestCase(15, 48, 49, 10, 18, 48, 330, TestName = "GetCurrentLocalTime_InAHalfHourZone_IsRoundedToTheQuarterHour")]
        public async Task GetCurrentLocalTime_TakesTheControllersCurrentOffsetFromItsUtcClock(
            int localHours, int localMinutes, int localSeconds, int utcHours, int utcMinutes, int utcSeconds, int offsetMinutes)
        {
            Harness h = NewHarness();
            AnswerLocalTime(h, new WSDate { year = 2025, monthWithJanuaryAsOne = 10, day = 14, hours = localHours, minutes = localMinutes, seconds = localSeconds });
            AnswerUtcClock(h, new WSDate { year = 2025, monthWithJanuaryAsOne = 10, day = 14, hours = utcHours, minutes = utcMinutes, seconds = utcSeconds });

            DateTimeOffset local = await h.Service.GetCurrentLocalTime();

            Assert.That(local.Offset, Is.EqualTo(TimeSpan.FromMinutes(offsetMinutes)));
            Assert.That((local.Year, local.Month, local.Day, local.Hour, local.Minute, local.Second),
                Is.EqualTo((2025, 10, 14, localHours, localMinutes, localSeconds)), "the local clock face is kept");
        }

        /// <summary>The two clocks can stand on either side of midnight, and the offset is still their difference.</summary>
        [Test]
        public async Task GetCurrentLocalTime_AcrossMidnight_StillTakesTheOffsetOfTheTwoClocks()
        {
            Harness h = NewHarness();
            AnswerLocalTime(h, new WSDate { year = 2026, monthWithJanuaryAsOne = 1, day = 1, hours = 0, minutes = 30, seconds = 0 });
            AnswerUtcClock(h, new WSDate { year = 2025, monthWithJanuaryAsOne = 12, day = 31, hours = 23, minutes = 30, seconds = 0 });

            Assert.That(await h.Service.GetCurrentLocalTime(),
                Is.EqualTo(new DateTimeOffset(2026, 1, 1, 0, 30, 0, TimeSpan.FromHours(1))));
        }

        /// <summary>
        /// A clock the controller did not report is not a clock at midnight year one - but that is what
        /// the caller gets, so the value is pinned rather than left to be discovered by whoever renders
        /// it. <see cref="DateTimeOffset.MinValue"/> is the SDK's "no reading" throughout. With no local clock
        /// there is nothing to place at an offset, so the UTC clock is not read.
        /// </summary>
        [Test]
        public async Task GetCurrentLocalTime_WithNoAnswer_IsTheNoReadingValue()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getCurrentLocalTimeAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(null)));

            Assert.That(await h.Service.GetCurrentLocalTime(), Is.EqualTo(DateTimeOffset.MinValue));
            A.CallTo(() => h.Soap.getSettingsAsync(A<inputMessageName3>._)).MustNotHaveHappened();
        }

        private static void AnswerLocalTime(Harness h, WSDate local) =>
            A.CallTo(() => h.Soap.getCurrentLocalTimeAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(local)));

        private static void AnswerUtcClock(Harness h, WSDate? utc) =>
            A.CallTo(() => h.Soap.getSettingsAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3(new WSTimeManagerSettings { serverName = "pool.ntp.org", timeAndDateInUTC = utc })));

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

            TimeServerConnectionResult? result = await h.Service.GetTimeFromServer("dk.pool.ntp.org");

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

            TimeServerConnectionResult? result = await h.Service.GetTimeFromServer("dk.pool.ntp.org");

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

            Assert.That(await h.Service.GetTimeFromServer("dk.pool.ntp.org"), Is.Null);
        }

        /// <summary>
        /// The operation's one part is the server to test, a host name or dotted IPv4 address with no scheme or port:
        /// IHC Administrator sends its unsaved time server there, captured as <c>1.schneider.pool.ntp.org</c>.
        /// </summary>
        [Test]
        public async Task GetTimeFromServer_SendsTheServerToTest()
        {
            Harness h = NewHarness();
            inputMessageName1? sent = null;
            A.CallTo(() => h.Soap.getTimeFromServerAsync(A<inputMessageName1>._))
                .Invokes((inputMessageName1 request) => sent = request)
                .Returns(Task.FromResult(new outputMessageName1(null)));

            await h.Service.GetTimeFromServer("1.schneider.pool.ntp.org");

            Assert.That(SoapRequestText.Normalized(SoapRequestText.Of(sent!)),
                Does.Contain("<utcs:getTimeFromServer1>1.schneider.pool.ntp.org</utcs:getTimeFromServer1>"));
        }

        /// <summary>A test of no server is refused before anything is sent: what a controller does with none is not
        /// known.</summary>
        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("", typeof(ArgumentException))]
        [TestCase("   ", typeof(ArgumentException))]
        public void GetTimeFromServer_WithNoServer_IsRefusedBeforeSending(string? serverName, Type refusal)
        {
            Harness h = NewHarness();

            Assert.That(async () => await h.Service.GetTimeFromServer(serverName!), Throws.TypeOf(refusal));
            A.CallTo(() => h.Soap.getTimeFromServerAsync(A<inputMessageName1>._)).MustNotHaveHappened();
        }
    }
}
