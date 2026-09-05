using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Every place the SDK answers a caller with a value the controller did not send.
    ///
    /// <para>Each of these substitutions is a CONTRACT and none of them changes here: an absent wire date reads
    /// as <see cref="DateTimeOffset.MinValue"/>, an absent battery level as 0, an absent event package as an
    /// empty not-running one. What was missing is any record that a substitution happened at all — so a caller
    /// could not tell an unanswered read from a genuine zero, a flat battery, or a controller that really is
    /// idle. The span warning is that record, and the value the caller receives is unchanged.</para>
    ///
    /// <para>SAMPLED by call SHAPE, not per site: each family is ONE implementation — <c>DateHelper</c>'s
    /// absent-date answer, <c>WireList</c>'s dropped-entry count — reached through a folded <c>mapDate</c>
    /// helper, a projection over a record, a direct read of a response field, and an epoch-milliseconds
    /// conversion. A fixture per call site would assert the same helper twenty times over. What each case here
    /// pins is that the site is WIRED to the shared answer and passes the right <c>field</c>; that the answer
    /// itself is right is pinned once, where it is written.</para>
    /// </summary>
    [TestFixture]
    public class WireSubstitutionWarningTests
    {
        /// <summary>Every Warning event a span carried, as its tag bag.</summary>
        private static IReadOnlyList<Dictionary<string, object?>> Warnings(Activity span) =>
            [.. span.Events.Where(e => e.Name == "Warning")
                .Select(e => e.Tags.ToDictionary(t => t.Key, t => t.Value))];

        /// <summary>The one warning of this <c>type</c>; fails when there is not exactly one.</summary>
        private static Dictionary<string, object?> Warning(TelemetryCapture capture, string spanName, string type) =>
            Warnings(capture.Span(spanName)).Single(w => (string?)w["type"] == type);

        // ── W18: the absent-date family, one shape per call form ────────────────────────────────────

        /// <summary>The folded <c>mapDate</c> shape — a helper the controller service shares with two siblings
        /// that used to carry a byte-identical copy of it.</summary>
        [Test]
        public async Task AnAbsentProjectDate_KeepsTheSentinelAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getProjectInfoAsync(A<Ihc.Soap.Controller.inputMessageName8>._))
                .Returns(Task.FromResult(new Ihc.Soap.Controller.outputMessageName8(
                    new Ihc.Soap.Controller.WSProjectInfo { lastmodified = null, customerName = "Kunde" })));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            ProjectInfo? info = await new ControllerService(FakeSession.Over(), soap).GetProjectInfo();

            Dictionary<string, object?> warning = Warning(capture, "ControllerService.GetProjectInfo", "AbsentWireDate");
            Assert.Multiple(() =>
            {
                Assert.That(info!.Lastmodified, Is.EqualTo(DateTimeOffset.MinValue), "the contract is unchanged");
                Assert.That(warning["field"], Is.EqualTo(nameof(ProjectInfo.Lastmodified)));
            });
        }

        /// <summary>The projection shape — two dates filled from one wire record.</summary>
        [Test]
        public async Task AbsentUserDates_KeepTheSentinelAndWarnOncePerField()
        {
            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            A.CallTo(() => soap.getUsersAsync(A<Ihc.Soap.Usermanager.inputMessageName2>._))
                .Returns(Task.FromResult(new Ihc.Soap.Usermanager.outputMessageName2(
                [
                    new Ihc.Soap.Usermanager.WSUser { username = "admin", createdDate = null, loginDate = null },
                ])));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            IReadOnlySet<IhcUser> users = await new UserManagerService(FakeSession.Over(), soap).GetUsers();

            IReadOnlyList<Dictionary<string, object?>> warnings = Warnings(capture.Span("UserManagerService.GetUsers"));
            Assert.Multiple(() =>
            {
                Assert.That(users.Single().CreatedDate, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(users.Single().LoginDate, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(warnings.Where(w => (string?)w["type"] == "AbsentWireDate").Select(w => w["field"]),
                    Is.EquivalentTo(new[] { nameof(IhcUser.CreatedDate), nameof(IhcUser.LoginDate) }),
                    "one warning per field, so a partly-answered record is not reported as a wholly absent one");
            });
        }

        /// <summary>The direct-read shape — the response field IS the return value.</summary>
        [Test]
        public async Task AnAbsentControllerClock_KeepsTheSentinelAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.getTimeAsync(A<Ihc.Soap.Openapi.inputMessageName8>._))
                .Returns(Task.FromResult(new Ihc.Soap.Openapi.outputMessageName8(null)));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            DateTimeOffset time = await new OpenAPIService(FakeSession.Over(), soap).GetTime();

            Assert.Multiple(() =>
            {
                Assert.That(time, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(Warning(capture, "OpenAPIService.GetTime", "AbsentWireDate")["field"],
                    Is.EqualTo("GetTime"));
            });
        }

        /// <summary>The epoch-milliseconds shape — a number rather than a <c>WSDate</c>, converted before the
        /// shared helper sees it.</summary>
        [Test]
        public async Task AnAbsentTimeServerDate_KeepsTheSentinelAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Timemanager.TimeManagerService>();
            A.CallTo(() => soap.getTimeFromServerAsync(A<Ihc.Soap.Timemanager.inputMessageName1>._))
                .Returns(Task.FromResult(new Ihc.Soap.Timemanager.outputMessageName1(
                    new Ihc.Soap.Timemanager.WSTimeServerConnectionResult { connectionWasSuccessful = true, dateFromServer = 0 })));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            TimeServerConnectionResult? result =
                await new TimeManagerService(FakeSession.Over(), soap).GetTimeFromServer();

            Assert.Multiple(() =>
            {
                Assert.That(result!.DateFromServer, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(Warning(capture, "TimeManagerService.GetTimeFromServer", "AbsentWireDate")["field"],
                    Is.EqualTo(nameof(TimeServerConnectionResult.DateFromServer)));
            });
        }

        /// <summary>A date the controller DID send warns about nothing — the control every one of these needs.</summary>
        [Test]
        public async Task APresentDate_WarnsAboutNothing()
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.getTimeAsync(A<Ihc.Soap.Openapi.inputMessageName8>._))
                .Returns(Task.FromResult(new Ihc.Soap.Openapi.outputMessageName8(
                    new Ihc.Soap.Openapi.WSDate { year = 2026, monthWithJanuaryAsOne = 9, day = 5, hours = 12, minutes = 0, seconds = 0 })));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            DateTimeOffset time = await new OpenAPIService(FakeSession.Over(), soap).GetTime();

            Assert.Multiple(() =>
            {
                Assert.That(time, Is.EqualTo(new DateTimeOffset(2026, 9, 5, 12, 0, 0, DateHelper.GetWSTimeOffset())));
                Assert.That(Warnings(capture.Span("OpenAPIService.GetTime")), Is.Empty);
            });
        }

        /// <summary>
        /// The sentinel's OTHER producer, and it is a substitution too.
        ///
        /// <para><see cref="DateHelper.CreateDateTimeOffset"/> answers <see cref="DateTimeOffset.MinValue"/> for
        /// a date whose components it cannot read — an hour of 24 here — so such a date reaches the caller as
        /// exactly the same value an ABSENT one does. Warning only on the absent route would have made the
        /// silence at this one read as "the controller sent this instant", which is the confusion the absent
        /// warning exists to remove. Separate tag values because the repair differs: a missing element and a
        /// malformed one are not the same defect.</para>
        /// </summary>
        [Test]
        public async Task AnUnreadableDate_KeepsTheSentinelAndWarnsAsUnreadableRatherThanAbsent()
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.getTimeAsync(A<Ihc.Soap.Openapi.inputMessageName8>._))
                .Returns(Task.FromResult(new Ihc.Soap.Openapi.outputMessageName8(
                    new Ihc.Soap.Openapi.WSDate
                    {
                        year = 2026, monthWithJanuaryAsOne = 9, day = 5, hours = 24, minutes = 0, seconds = 0,
                    })));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            DateTimeOffset time = await new OpenAPIService(FakeSession.Over(), soap).GetTime();

            Dictionary<string, object?> warning = Warning(capture, "OpenAPIService.GetTime", "UnreadableWireDate");
            Assert.Multiple(() =>
            {
                Assert.That(time, Is.EqualTo(DateTimeOffset.MinValue), "the contract is unchanged");
                Assert.That(warning["field"], Is.EqualTo("GetTime"));
                Assert.That(Warnings(capture.Span("OpenAPIService.GetTime"))
                        .Any(w => (string?)w["type"] == "AbsentWireDate"), Is.False,
                    "a malformed element is not a missing one, and the repair differs");
            });
        }

        // ── W19-W21: the single-site absent VALUES ──────────────────────────────────────────────────

        [Test]
        public async Task AnAbsentBatteryLevel_KeepsZeroAndWarnsWithTheResourceId()
        {
            var soap = A.Fake<Ihc.Soap.Airlinkmanagement.AirlinkManagementService>();
            A.CallTo(() => soap.getBatteryLevelAsync(A<Ihc.Soap.Airlinkmanagement.inputMessageName10>._))
                .Returns(Task.FromResult(new Ihc.Soap.Airlinkmanagement.outputMessageName10(null)));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            int level = await new AirlinkManagementService(FakeSession.Over(), soap).GetBatteryLevel(4711);

            Dictionary<string, object?> warning =
                Warning(capture, "AirlinkManagementService.GetBatteryLevel", "AbsentWireValue");
            Assert.Multiple(() =>
            {
                Assert.That(level, Is.Zero, "zero is the contract, and is also a real battery level");
                Assert.That(warning["field"], Is.EqualTo("GetBatteryLevel"));
                Assert.That(warning["resourceId"], Is.EqualTo(4711),
                    "which device could not be read is the half a level of 0 cannot carry");
            });
        }

        [Test]
        public async Task AnAbsentS0MeterValue_KeepsZeroAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Controller.ControllerService>();
            A.CallTo(() => soap.getS0MeterValueAsync(A<Ihc.Soap.Controller.inputMessageName11>._))
                .Returns(Task.FromResult(new Ihc.Soap.Controller.outputMessageName11(null)));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            float value = await new ControllerService(FakeSession.Over(), soap).GetS0MeterValue();

            Assert.Multiple(() =>
            {
                Assert.That(value, Is.Zero);
                Assert.That(Warning(capture, "ControllerService.GetS0MeterValue", "AbsentWireValue")["field"],
                    Is.EqualTo("GetS0MeterValue"));
            });
        }

        [Test]
        public async Task AnAbsentSegmentCount_KeepsZeroAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.getIHCProjectNumberOfSegmentsAsync(A<Ihc.Soap.Openapi.inputMessageName19>._))
                .Returns(Task.FromResult(new Ihc.Soap.Openapi.outputMessageName19(null)));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            int segments = await new OpenAPIService(FakeSession.Over(), soap).GetIHCProjectNumberOfSegments();

            Assert.Multiple(() =>
            {
                Assert.That(segments, Is.Zero);
                Assert.That(Warning(capture, "OpenAPIService.GetIHCProjectNumberOfSegments", "AbsentWireValue")["field"],
                    Is.EqualTo("GetIHCProjectNumberOfSegments"));
            });
        }

        // ── W22: a fabricated event package ─────────────────────────────────────────────────────────

        /// <summary>
        /// The substitution that is not a value but a whole ANSWER: "the controller is not running, has no
        /// events and no subscriptions" is well-formed, actionable, and — when the response carried no package
        /// at all — entirely invented. The shape stays, because <c>WaitForEvents</c> polls this in a loop and a
        /// null would only move the problem to its caller.
        /// </summary>
        [Test]
        public async Task AnAbsentEventPackage_KeepsTheFabricatedShapeAndWarns()
        {
            var soap = A.Fake<Ihc.Soap.Openapi.OpenAPIService>();
            A.CallTo(() => soap.waitForEventsAsync(A<Ihc.Soap.Openapi.inputMessageName5>._))
                .Returns(Task.FromResult(new Ihc.Soap.Openapi.outputMessageName5(null)));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            EventPackage package = await new OpenAPIService(FakeSession.Over(), soap).WaitForEvents(15);

            Assert.Multiple(() =>
            {
                Assert.That(package.ControllerExecutionRunning, Is.False);
                Assert.That(package.ResourceValueEvents, Is.Empty);
                Assert.That(package.SubscriptionAmount, Is.Zero);
                Assert.That(Warnings(capture.Span("OpenAPIService.WaitForEvents"))
                        .Any(w => (string?)w["type"] == "AbsentEventPackage"), Is.True,
                    "nothing in the returned package says the controller never sent one");
            });
        }

        // ── W23-W24: entries dropped from a list ────────────────────────────────────────────────────

        [Test]
        public async Task DroppedLogEntries_AreCountedInTheWarning()
        {
            var soap = A.Fake<Ihc.Soap.Messagecontrollog.MessageControlLogService>();
            A.CallTo(() => soap.getEventsAsync(A<Ihc.Soap.Messagecontrollog.inputMessageName2>._))
                .Returns(Task.FromResult(new Ihc.Soap.Messagecontrollog.outputMessageName2(
                [
                    new Ihc.Soap.Messagecontrollog.WSMessageControlLogEntry { controlType = "sms" },
                    null,
                    null,
                ])));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            IReadOnlyList<LogEventEntry> events =
                await new MessageControlLogService(FakeSession.Over(), soap).GetEvents();

            Dictionary<string, object?> warning =
                Warning(capture, "MessageControlLogService.GetEvents", "DroppedWireEntries");
            Assert.Multiple(() =>
            {
                Assert.That(events, Has.Count.EqualTo(1), "the readable entries are still returned");
                Assert.That(warning["received"], Is.EqualTo(3));
                Assert.That(warning["returned"], Is.EqualTo(1),
                    "a shorter log with nothing saying so is the one loss a log cannot afford");
            });
        }

        [Test]
        public async Task DroppedUserEntries_AreCountedInTheWarning()
        {
            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            A.CallTo(() => soap.getUsersAsync(A<Ihc.Soap.Usermanager.inputMessageName2>._))
                .Returns(Task.FromResult(new Ihc.Soap.Usermanager.outputMessageName2(
                [
                    new Ihc.Soap.Usermanager.WSUser { username = "admin" },
                    null,
                ])));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            IReadOnlySet<IhcUser> users = await new UserManagerService(FakeSession.Over(), soap).GetUsers();

            Dictionary<string, object?> warning = Warning(capture, "UserManagerService.GetUsers", "DroppedWireEntries");
            Assert.Multiple(() =>
            {
                Assert.That(users, Has.Count.EqualTo(1));
                Assert.That(warning["received"], Is.EqualTo(2));
                Assert.That(warning["returned"], Is.EqualTo(1));
            });
        }

        /// <summary>A complete answer drops nothing, and says nothing.</summary>
        [Test]
        public async Task ACompleteUserList_WarnsAboutNothing()
        {
            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            A.CallTo(() => soap.getUsersAsync(A<Ihc.Soap.Usermanager.inputMessageName2>._))
                .Returns(Task.FromResult(new Ihc.Soap.Usermanager.outputMessageName2(
                [
                    new Ihc.Soap.Usermanager.WSUser
                    {
                        username = "admin",
                        createdDate = new Ihc.Soap.Usermanager.WSDate { year = 2026, monthWithJanuaryAsOne = 1, day = 1 },
                        loginDate = new Ihc.Soap.Usermanager.WSDate { year = 2026, monthWithJanuaryAsOne = 1, day = 2 },
                    },
                ])));
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName);

            await new UserManagerService(FakeSession.Over(), soap).GetUsers();

            Assert.That(Warnings(capture.Span("UserManagerService.GetUsers")), Is.Empty);
        }
    }
}
