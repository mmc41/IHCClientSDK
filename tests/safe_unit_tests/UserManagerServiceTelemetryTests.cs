using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Usermanager;
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
            object? captured = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == Telemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (activity.OperationName == nameof(UserManagerService) + "." + nameof(UserManagerService.GetUsers))
                    {
                        captured = activity.GetTagItem(Telemetry.returnValueTag);
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var users = await NewService(logSensitiveData).GetUsers(includePassword);

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
    }
}
