using System.Diagnostics;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Tests.Shared;
using NUnit.Framework;
using Api = Ihc.Soap.Openapi;

namespace Ihc.Tests
{
    /// <summary>
    /// What <see cref="OpenAPIService.Authenticate(string, string)"/> puts on its span.
    ///
    /// This service takes the password DIRECTLY rather than through <see cref="AuthenticationService"/>,
    /// so it is a second place the credential reaches a trace - and the flag deciding whether it is
    /// published there has to be the same documented opt-in, or the setting a consumer turns on to see
    /// credentials and the setting that actually publishes them are two different settings.
    /// </summary>
    [TestFixture]
    public class OpenApiAuthenticateTests
    {
        private const string UserName = "installer";
        private const string Password = "sup3r-s3cret-pw";
        private const string SpanName = nameof(OpenAPIService) + "." + nameof(OpenAPIService.Authenticate);

        [Test]
        public async Task Authenticate_WithSensitiveLoggingOff_RedactsThePasswordInTheSpan()
        {
            CredentialInSpan.AssertRedacted(
                await AuthenticateCapturingParameterTags(FakeSession.Settings(logSensitiveData: false)),
                Password, UserName);
        }

        [Test]
        public async Task Authenticate_WithSensitiveLoggingOn_PublishesThePassword()
        {
            CredentialInSpan.AssertPublished(
                await AuthenticateCapturingParameterTags(FakeSession.Settings(logSensitiveData: true)), Password);
        }

        /// <summary>
        /// The setting that must NOT decide this. <c>AsyncContinueOnCapturedContext</c> sits beside
        /// <c>LogSensitiveData</c> in the same settings object and governs await continuations, so a span
        /// reading it publishes the credential to a consumer that changed a threading option and says
        /// nothing about secrets - while leaving the documented opt-in doing nothing at all.
        /// </summary>
        [Test]
        public async Task Authenticate_DoesNotPublishThePasswordOnAnUnrelatedSetting()
        {
            IhcSettings settings = FakeSession.Settings(logSensitiveData: false);
            settings.AsyncContinueOnCapturedContext = true;

            string tags = await AuthenticateCapturingParameterTags(settings);

            Assert.That(tags, Does.Not.Contain(Password));
        }

        private static async Task<string> AuthenticateCapturingParameterTags(IhcSettings settings)
        {
            var soap = A.Fake<Api.OpenAPIService>();
            A.CallTo(() => soap.authenticateAsync(A<Api.inputMessageName13>._))
                .Returns(Task.FromResult(new Api.outputMessageName13(true)));

            using TelemetryCapture capture = TelemetryCapture.Listen(
                Telemetry.ActivitySourceName, spanNames: [SpanName]);

            await new OpenAPIService(FakeSession.Over(settings), soap).Authenticate(UserName, Password);

            return TelemetryCapture.TagText(capture.Span(SpanName));
        }
    }
}
