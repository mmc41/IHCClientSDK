using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Usermanager;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// What <see cref="UserManagerService.GetUsers"/> publishes to telemetry.
    ///
    /// The controller's user list carries cleartext passwords, so the span's return-value tag may only
    /// carry them when <see cref="IhcSettings.LogSensitiveData"/> says so - the flag is the whole trust
    /// boundary here, and it defaults to false. The tag holds an object rather than a string, and an
    /// exporter renders a non-primitive through ToString(), so these tests render the captured tag the
    /// same way instead of trusting its declared type.
    /// </summary>
    [TestFixture]
    public class UserManagerServiceTelemetryTests
    {
        private const string Secret = "sup3r-s3cret-pw";

        private static UserManagerService NewService(bool logSensitiveData)
        {
            var settings = new IhcSettings { Endpoint = "http://unit.test.local", LogSensitiveData = logSensitiveData };
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(settings);

            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            A.CallTo(() => soap.getUsersAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(new[]
                {
                    new WSUser
                    {
                        username = "installer",
                        password = Secret,
                        group = new WSUserGroup { type = "text.usermanager.group_administrators" }
                    }
                })));

            return new UserManagerService(auth, soap);
        }

        /// <summary>Renders a tag value the way an exporter does: a non-primitive becomes its ToString().</summary>
        private static string RenderTag(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string s => s,
                IEnumerable items => string.Join(", ", items.Cast<object?>().Select(i => i?.ToString())),
                _ => value.ToString() ?? string.Empty
            };
        }

        private static async Task<(string Tag, IhcUser User)> GetUsersCapturingReturnTag(bool logSensitiveData, bool includePassword)
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { nameof(UserManagerService) + "." + nameof(UserManagerService.GetUsers) });

            var users = await NewService(logSensitiveData).GetUsers(includePassword);

            object? captured = capture.Spans.Single().GetTagItem(Telemetry.returnValueTag);
            return (RenderTag(captured), users.Single());
        }

        [Test]
        public async Task GetUsers_WithSensitiveLoggingOff_DoesNotPublishPasswordToTelemetry()
        {
            var (tag, _) = await GetUsersCapturingReturnTag(logSensitiveData: false, includePassword: true);

            Assert.Multiple(() =>
            {
                Assert.That(tag, Does.Not.Contain(Secret),
                    "LogSensitiveData is off, so the password must never reach the telemetry tag");
                Assert.That(tag, Does.Contain(UserConstants.REDACTED_PASSWORD),
                    "the user should still be observable in the trace - redacted, not omitted");
            });
        }

        [Test]
        public async Task GetUsers_WithSensitiveLoggingOn_PublishesPasswordToTelemetry()
        {
            var (tag, _) = await GetUsersCapturingReturnTag(logSensitiveData: true, includePassword: true);

            Assert.That(tag, Does.Contain(Secret),
                "LogSensitiveData is the opt-in that permits cleartext passwords in traces");
        }

        [Test]
        public async Task GetUsers_TelemetryRedaction_DoesNotRedactTheReturnedUser()
        {
            var (_, user) = await GetUsersCapturingReturnTag(logSensitiveData: false, includePassword: true);

            Assert.That(user.Password, Is.EqualTo(Secret),
                "redaction applies to the trace only - the caller asked for the password and must get it");
        }

        /// <summary>
        /// <c>service.name</c> is a RESOURCE-level semantic convention identifying the whole process, which the
        /// host already sets (to <c>IhcOpenVisual</c>, say). Setting it per span squatted that key with a
        /// conflicting value - the wrapper class name - and the backend flattens both into the same field, so the
        /// two were indistinguishable downstream. <c>service.operation</c> is not a convention at all and only
        /// repeated the second half of the span name. Both are gone; this pins them out.
        /// </summary>
        [Test]
        public async Task GetUsers_SpanDoesNotSquatTheResourceLevelServiceKeys()
        {
            var tags = await GetUsersCapturingAllTags();

            Assert.Multiple(() =>
            {
                Assert.That(tags, Does.Not.ContainKey("service.name"),
                    "service.name identifies the process and belongs to the Resource, never to a span");
                Assert.That(tags, Does.Not.ContainKey("service.operation"),
                    "the span name already carries <Service>.<operation>, so this tag was pure duplication");
            });
        }

        /// <summary>
        /// The api tier keeps its own <c>StartActivity</c> + <c>SetError</c> scaffold at its call sites, so the
        /// only way the normalized error type can reach all of them is through the one extension they all
        /// already call. The span's NAME must survive the re-routing too: it is what every existing query and
        /// dashboard addresses these operations by.
        /// </summary>
        [Test]
        public void GetUsers_WhenTheControllerCallThrows_KeepsItsNameAndCarriesTheNormalizedErrorType()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { nameof(UserManagerService) + "." + nameof(UserManagerService.GetUsers) });

            var settings = new IhcSettings { Endpoint = "http://unit.test.local" };
            var auth = A.Fake<IAuthenticationService>();
            A.CallTo(() => auth.IhcSettings).Returns(settings);
            var soap = A.Fake<Ihc.Soap.Usermanager.UserManagerService>();
            A.CallTo(() => soap.getUsersAsync(A<inputMessageName2>._))
                .Throws(new System.IO.FileNotFoundException("the controller is unreachable"));

            Assert.CatchAsync(async () => await new UserManagerService(auth, soap).GetUsers(false));

            Assert.That(capture.Spans, Is.Not.Empty, "the span must still be produced under its existing name");
            Activity captured = capture.Spans.Single();
            Assert.Multiple(() =>
            {
                Assert.That(captured.Status, Is.EqualTo(ActivityStatusCode.Error));
                Assert.That(captured.GetTagItem("error.type"), Is.EqualTo("System.IO.FileNotFoundException"),
                    "the api tier inherits the core's error-type policy without any of its call sites changing");
            });
        }

        private static async Task<Dictionary<string, object?>> GetUsersCapturingAllTags()
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { nameof(UserManagerService) + "." + nameof(UserManagerService.GetUsers) });

            await NewService(logSensitiveData: false).GetUsers(false);

            var tags = new Dictionary<string, object?>();
            foreach (KeyValuePair<string, object?> tag in capture.Spans.Single().TagObjects)
            {
                tags[tag.Key] = tag.Value;
            }
            return tags;
        }
    }
}
