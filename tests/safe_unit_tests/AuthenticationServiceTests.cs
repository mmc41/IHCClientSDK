using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ihc.Envelope;
using Ihc.Soap.Authentication;
using Ihc.Tests.Shared;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The login exchange, driven over the real handler chain with a stub socket.
    ///
    /// TESTSTRATEGY puts the SDK's transport, authentication and session handling in the High tier, and
    /// every other service depends on this one for both its settings and its cookie session - so a
    /// wrong answer here is not confined to this service. The three things that decide what a caller
    /// believes are all decided in <c>DoAuthenticate</c>: whether the login SUCCEEDED, whether the
    /// session is now CONNECTED, and whether the session cookie was CAPTURED.
    ///
    /// Driven through the transport seam rather than a faked SOAP layer because this service owns its
    /// SOAP implementation - it is the one that mints the cookie handler the others borrow, so there is
    /// nothing above the socket to substitute.
    /// </summary>
    [TestFixture]
    public class AuthenticationServiceTests
    {
        private const string SessionCookie = "JSESSIONID=90DB1B4A0F6D3E";
        private const string Password = "sup3r-s3cret-pw";

        /// <summary>The shared session settings, with the credentials this fixture logs in with.</summary>
        private static IhcSettings Settings(bool logSensitiveData = false)
        {
            IhcSettings settings = FakeSession.Settings(logSensitiveData);
            settings.UserName = "installer";
            settings.Password = Password;
            return settings;
        }

        /// <summary>
        /// The whole arrangement of a login fixture: a stub socket answering the given result, and the service
        /// over it. Both are disposed by the caller's <c>using</c>, so the pair is handed back rather than a
        /// service whose transport nobody owns.
        /// </summary>
        private sealed class Login : IDisposable
        {
            internal required StubTransport Socket { get; init; }
            internal required HttpClient Transport { get; init; }
            internal required AuthenticationService Service { get; init; }

            internal static Login Over(StubTransport socket, bool logSensitiveData = false)
            {
                HttpClient transport = Client.CreateHttpClient(socket);
                return new Login
                {
                    Socket = socket,
                    Transport = transport,
                    Service = new AuthenticationService(Settings(logSensitiveData), transport),
                };
            }

            internal static Login Answering(WSLoginResult result) => Over(AnsweringLogin(result));

            public void Dispose()
            {
                Service.Dispose();
                Transport.Dispose();
            }
        }

        private static WSUser LoggedIn() => new()
        {
            username = "installer",
            password = Password,
            email = "anna@beck.dk",
            firstname = "Anna",
            lastname = "Beck",
            phone = "+4512345678",
            group = new WSUserGroup { type = "text.usermanager.group_administrators" },
            project = "project1",
            createdDate = new WSDate { year = 2019, monthWithJanuaryAsOne = 10, day = 1, hours = 20, minutes = 54, seconds = 24 },
            loginDate = new WSDate { year = 2025, monthWithJanuaryAsOne = 10, day = 17, hours = 18, minutes = 31, seconds = 54 }
        };

        private static string AuthenticateResponse(WSLoginResult result) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName2>>(
                new ResponseEnvelope<outputMessageName2>(new outputMessageName2(result)));

        private static string DisconnectResponse(bool? value) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName1>>(
                new ResponseEnvelope<outputMessageName1>(new outputMessageName1(value)));

        private static string PingResponse(bool? value) =>
            Serialization.SerializeXml<ResponseEnvelope<outputMessageName3>>(
                new ResponseEnvelope<outputMessageName3>(new outputMessageName3(value)));

        /// <summary>
        /// Answers each SOAP action from the map, and attaches a Set-Cookie to the authenticate answer
        /// the way the controller does - which is the only place the session cookie ever comes from.
        /// </summary>
        private static StubTransport Answering(IReadOnlyDictionary<string, string> byAction) =>
            new(request =>
            {
                string action = request.Headers.TryGetValues("SOAPAction", out var values) ? values.First() : string.Empty;
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(byAction[action], Encoding.UTF8, "text/xml")
                };
                if (action == "authenticate")
                {
                    response.Headers.TryAddWithoutValidation("Set-Cookie", SessionCookie);
                }
                return response;
            });

        private static StubTransport AnsweringLogin(WSLoginResult result) =>
            Answering(new Dictionary<string, string>
            {
                ["authenticate"] = AuthenticateResponse(result),
                ["disconnect"] = DisconnectResponse(true),
                ["ping"] = PingResponse(true)
            });

        private static WSLoginResult Success() => new() { loginWasSuccessful = true, loggedInUser = LoggedIn() };

        /// <summary>
        /// Answers the login - WITH the session cookie, as the controller does - and then fails everything
        /// after it. The cookie is what makes a fixture over this able to see what a failed call LEAVES
        /// behind; without it, asserting that the session ended asserts over a session that never began.
        /// </summary>
        private static StubTransport LoginThenFailure(WSLoginResult? result = null) =>
            new(request =>
            {
                if (request.Headers.GetValues("SOAPAction").First() != "authenticate")
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent("<html>boom</html>")
                    };
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(AuthenticateResponse(result ?? Success()), Encoding.UTF8, "text/xml")
                };
                response.Headers.TryAddWithoutValidation("Set-Cookie", SessionCookie);
                return response;
            });

        // ---------------------------------------------------------------- success

        /// <summary>
        /// The whole point of a successful login: the user is mapped, the session is marked connected,
        /// and the controller's cookie is captured so every service built from this one carries it.
        /// </summary>
        [Test]
        public async Task Authenticate_OnSuccess_MapsTheUser_MarksConnected_AndCapturesTheSessionCookie()
        {
            using Login login = Login.Answering(Success());
            AuthenticationService auth = login.Service;

            Assert.That(await auth.IsAuthenticated(), Is.False, "no login has happened yet");

            IhcUser user = await auth.Authenticate();

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(user.Username, Is.EqualTo("installer"));
                Assert.That(user.Firstname, Is.EqualTo("Anna"));
                Assert.That(user.Lastname, Is.EqualTo("Beck"));
                Assert.That(user.Phone, Is.EqualTo("+4512345678"));
                // The login answer carries the same WSUser fields UserManagerService maps, and every one
                // of them is part of what a caller is told about who it is logged in as.
                Assert.That(user.Email, Is.EqualTo("anna@beck.dk"));
                Assert.That(user.Project, Is.EqualTo("project1"));
                Assert.That(user.Group, Is.EqualTo(IhcUserGroup.Administrators));
                Assert.That(await auth.IsAuthenticated(), Is.True);
                Assert.That(auth.GetCookieHandler().GetCookie(), Is.EqualTo(SessionCookie));
            });
        }

        /// <summary>
        /// The application name reaches the controller lowercased through an INVARIANT fold: a Turkish
        /// locale would otherwise fold the I in any member carrying one to a dotless 'i', and the
        /// controller matches the name exactly.
        /// </summary>
        [Test]
        public async Task Authenticate_SendsTheApplicationNameLowercased()
        {
            using Login login = Login.Answering(Success());

            await login.Service.Authenticate("installer", Password, Application.administrator);

            Assert.That(login.Socket.RequestBody, Does.Contain("<utcs:application>administrator</utcs:application>"));
        }

        // ---------------------------------------------------------------- refusals

        /// <summary>
        /// The controller reports WHY a login failed through three booleans, and each maps to its own
        /// coded error - the codes a caller branches on to tell "wrong password" from "this account may
        /// not connect from here". A collapsed mapping shows a user the wrong reason.
        /// </summary>
        [TestCase(nameof(WSLoginResult.loginFailedDueToAccountInvalid), Errors.LOGIN_FAILED_DUE_TO_ACCOUNT_INVALID_ERROR)]
        [TestCase(nameof(WSLoginResult.loginFailedDueToConnectionRestrictions), Errors.LOGIN_FAILED_DUE_TO_CONNECTION_RESTRUCTIONS_ERROR)]
        [TestCase(nameof(WSLoginResult.loginFailedDueToInsufficientUserRights), Errors.LOGIN_FAILED_DUE_TO_INSUFFICIENT_USER_RIGHTS_ERROR)]
        // A failure the controller gives no reason for is still a refusal, not a success.
        [TestCase(null, Errors.LOGIN_UNKNOWN_ERROR)]
        public async Task Authenticate_OnRefusal_RaisesTheCodeForTheReasonReported(string? reason, int expectedCode)
        {
            var result = new WSLoginResult { loginWasSuccessful = false };
            if (reason is not null)
            {
                typeof(WSLoginResult).GetProperty(reason)!.SetValue(result, true);
            }

            Refused refused = await RefusedLoginAsync(result);

            Assert.Multiple(() =>
            {
                Assert.That(refused.Refusal.ErrorCode, Is.EqualTo(expectedCode));
                Assert.That(refused.Cookie, Is.Null,
                    "a login the controller REFUSED establishes no session, so it leaves no cookie behind");
                Assert.That(refused.SoapActions, Is.EqualTo(new[] { "authenticate" }),
                    "and there is no session to log out of");
            });
        }

        /// <summary>What a refused login left behind: the coded exception, the cookie and what was posted.</summary>
        private sealed record Refused(ErrorWithCodeException Refusal, string? Cookie, IReadOnlyList<string> SoapActions);

        /// <summary>
        /// Attempts a login that ends in a refusal and hands back what it left behind - having already asserted
        /// the one thing true of every refusal, that the session is not connected. The rest differs by case and
        /// is the caller's to judge.
        /// </summary>
        private static async Task<Refused> RefusedLoginAsync(WSLoginResult result)
        {
            using Login login = Login.Answering(result);

            var refusal = Assert.CatchAsync<ErrorWithCodeException>(async () => await login.Service.Authenticate())!;

            Assert.That(await login.Service.IsAuthenticated(), Is.False,
                "a refused login must not leave the session connected");
            return new Refused(refusal, login.Service.GetCookieHandler().GetCookie(), [.. login.Socket.SoapActions]);
        }

        /// <summary>
        /// The case the caller cannot see coming: the controller says the login SUCCEEDED but sends no
        /// user. Returning a hollow user here would leave a caller believing it is authenticated on the
        /// strength of a flag alone - so it is refused, and the session stays disconnected.
        ///
        /// Unlike a refused login, this one succeeded as far as the CONTROLLER is concerned - it said so -
        /// so a session exists on it and its cookie was captured before the SDK refused the answer. That
        /// session belongs to nobody: <c>Dispose</c> logs out only a session it believes is live, and this
        /// one never became connected, so left alone it would sit on the controller until it timed out.
        /// It is logged out here instead, and the cookie goes with it.
        /// </summary>
        [Test]
        public async Task Authenticate_WhenSuccessCarriesNoUser_IsRefusedAndLogsOutTheSessionItOpened()
        {
            Refused refused = await RefusedLoginAsync(
                new WSLoginResult { loginWasSuccessful = true, loggedInUser = null });

            Assert.Multiple(() =>
            {
                Assert.That(refused.Refusal.ErrorCode, Is.EqualTo(Errors.LOGIN_UNKNOWN_ERROR));
                Assert.That(refused.Refusal.Message, Does.Contain(FakeSession.Endpoint + "/ws/AuthenticationService"),
                    "the diagnostic names the endpoint that answered");
                Assert.That(refused.SoapActions, Is.EqualTo(new[] { "authenticate", "disconnect" }),
                    "the session the controller opened is ended rather than abandoned on it");
                Assert.That(refused.Cookie, Is.Null,
                    "and nothing this instance sends afterwards rides a session it disowned");
            });
        }

        /// <summary>
        /// The same, with the logout itself failing. The refusal a caller acts on is the LOGIN's, not the
        /// cleanup's - a controller that will not answer the logout leaves nothing this side can do about
        /// its session - but the cookie is dropped regardless.
        /// </summary>
        [Test]
        public async Task Authenticate_WhenSuccessCarriesNoUser_AndTheLogoutFails_StillRaisesTheLoginRefusal()
        {
            using Login login = Login.Over(
                LoginThenFailure(new WSLoginResult { loginWasSuccessful = true, loggedInUser = null }));

            var refusal = Assert.CatchAsync<ErrorWithCodeException>(
                async () => await login.Service.Authenticate())!;

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(refusal.ErrorCode, Is.EqualTo(Errors.LOGIN_UNKNOWN_ERROR));
                Assert.That(await login.Service.IsAuthenticated(), Is.False);
                Assert.That(login.Service.GetCookieHandler().GetCookie(), Is.Null);
            });
        }

        /// <summary>
        /// A login the controller reports as SUCCESSFUL whose user cannot be mapped. An unknown group
        /// type is the shape that reaches this: <see cref="IhcUserGroup"/> is a closed set and the
        /// controller's is not, so the mapping throws.
        ///
        /// What is at stake is the SESSION rather than the exception. The controller opened one and sent
        /// its cookie before the SDK looked at the answer at all, so a mapping that throws AFTER the
        /// session was marked live leaves <c>IsAuthenticated</c> true and the cookie attached on a login
        /// the caller was told had failed - and a caller that reads the throw as "login failed" and does
        /// not dispose leaves that session sitting on the controller until it times out. The session is
        /// therefore handed over only once a user exists to hand over with it.
        /// </summary>
        [Test]
        public async Task Authenticate_WhenTheUserCannotBeMapped_IsRefusedAndLogsOutTheSessionItOpened()
        {
            WSUser unmappable = LoggedIn();
            unmappable.group = new WSUserGroup { type = "text.usermanager.group_that_does_not_exist" };

            Refused refused = await RefusedLoginAsync(
                new WSLoginResult { loginWasSuccessful = true, loggedInUser = unmappable });

            Assert.Multiple(() =>
            {
                Assert.That(refused.Refusal.ErrorCode, Is.EqualTo(Errors.LOGIN_UNKNOWN_ERROR),
                    "a login that cannot be completed is refused with a login code, not with whatever the mapping threw");
                Assert.That(refused.SoapActions, Is.EqualTo(new[] { "authenticate", "disconnect" }),
                    "the session the controller opened is ended rather than abandoned on it");
                Assert.That(refused.Cookie, Is.Null,
                    "and nothing this instance sends afterwards rides a session it disowned");
            });
        }

        /// <summary>
        /// The login answer's dates are nullable on the wire, and every other SDK site reading a WSDate
        /// takes an absent one as <see cref="DateTimeOffset.MinValue"/> - <see cref="UserManagerService"/>
        /// included, which maps the very same WSUser fields. Dereferencing an absent date here instead
        /// would make one account readable through one service and a NullReferenceException through the
        /// other, and would fail a login over metadata that is not part of who the caller is logged in as.
        /// </summary>
        [Test]
        public async Task Authenticate_WhenTheAnswerCarriesNoDates_MapsThemUnsetRatherThanFailing()
        {
            WSUser undated = LoggedIn();
            undated.createdDate = null;
            undated.loginDate = null;

            using Login login = Login.Answering(
                new WSLoginResult { loginWasSuccessful = true, loggedInUser = undated });

            IhcUser user = await login.Service.Authenticate();

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(user.CreatedDate, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(user.LoginDate, Is.EqualTo(DateTimeOffset.MinValue));
                Assert.That(user.Username, Is.EqualTo("installer"), "the rest of the answer still maps");
                Assert.That(await login.Service.IsAuthenticated(), Is.True,
                    "an absent date is not a failed login");
            });
        }

        // ---------------------------------------------------------------- session end

        [Test]
        public async Task Disconnect_ClearsTheSessionAndItsCookie()
        {
            using Login login = Login.Answering(Success());
            AuthenticationService auth = login.Service;
            await auth.Authenticate();

            bool disconnected = await auth.Disconnect();

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(disconnected, Is.True);
                Assert.That(await auth.IsAuthenticated(), Is.False);
                Assert.That(auth.GetCookieHandler().GetCookie(), Is.Null);
            });
        }

        /// <summary>
        /// A controller that answers <c>disconnect</c> with nothing at all has not confirmed the logout,
        /// so the call reports false - but the LOCAL session must end regardless, or a caller that
        /// checked the boolean and retried would keep posting under a session the controller may already
        /// have dropped.
        /// </summary>
        [Test]
        public async Task Disconnect_WithNoAnswerFromTheController_ReportsFalseButStillEndsTheLocalSession()
        {
            using Login login = Login.Over(Answering(new Dictionary<string, string>
            {
                ["authenticate"] = AuthenticateResponse(Success()),
                ["disconnect"] = DisconnectResponse(null)
            }));
            AuthenticationService auth = login.Service;
            await auth.Authenticate();

            bool disconnected = await auth.Disconnect();

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(disconnected, Is.False);
                Assert.That(await auth.IsAuthenticated(), Is.False);
            });
        }

        /// <summary>
        /// A failed disconnect must not leave the session believing it is still connected: the
        /// <c>finally</c> that clears the flag is what stops <c>Dispose</c> from retrying the same
        /// failing logout on the way out.
        ///
        /// BOTH halves of the local session end there, and the cookie is the half that matters on the
        /// wire: the flag only decides what <c>IsAuthenticated</c> answers, while the cookie is attached
        /// to every later request by every service sharing this session's handler. Left behind, a session
        /// reporting itself disconnected goes on presenting itself as live to the controller.
        /// </summary>
        [Test]
        public async Task Disconnect_WhenTheControllerFails_StillEndsTheLocalSessionAndItsCookie()
        {
            using Login login = Login.Over(LoginThenFailure());
            AuthenticationService auth = login.Service;
            await auth.Authenticate();
            Assert.That(auth.GetCookieHandler().GetCookie(), Is.EqualTo(SessionCookie),
                "the login must have established a cookie, or this fixture asserts over nothing");

            Assert.CatchAsync(async () => await auth.Disconnect());

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await auth.IsAuthenticated(), Is.False);
                Assert.That(auth.GetCookieHandler().GetCookie(), Is.Null);
            });
        }

        /// <summary>
        /// Disposing a live session logs it out - the reason <c>IAuthenticationService</c> is
        /// <see cref="IDisposable"/> at all. Asserted through the SOAP actions the socket saw, because
        /// the disconnect is the observable effect rather than the return value.
        /// </summary>
        [Test]
        public async Task Dispose_OnAConnectedSession_LogsOut()
        {
            using Login login = Login.Answering(Success());
            await login.Service.Authenticate();

            login.Service.Dispose();

            Assert.That(login.Socket.SoapActions, Is.EqualTo(new[] { "authenticate", "disconnect" }));
        }

        /// <summary>
        /// And disposing a session that never connected must post nothing: an unauthenticated logout is
        /// a call to a controller that has no session to end.
        /// </summary>
        [Test]
        public void Dispose_OnASessionThatNeverConnected_PostsNothing()
        {
            using Login login = Login.Answering(Success());

            login.Service.Dispose();

            Assert.That(login.Socket.SoapActions, Is.Empty);
        }

        /// <summary>
        /// Dispose is documented to swallow: it runs on a path the caller cannot handle an exception on,
        /// and a session being torn down has nothing left to lose by a logout that fails.
        /// </summary>
        [Test]
        public async Task Dispose_WhenTheLogoutFails_DoesNotThrow()
        {
            using Login login = Login.Over(LoginThenFailure());
            await login.Service.Authenticate();

            Assert.DoesNotThrow(() => login.Service.Dispose());
        }

        [Test]
        public async Task DisposeAsync_OnAConnectedSession_LogsOut()
        {
            using Login login = Login.Answering(Success());
            await login.Service.Authenticate();

            await login.Service.DisposeAsync();

            Assert.That(login.Socket.SoapActions, Is.EqualTo(new[] { "authenticate", "disconnect" }));
        }

        // ---------------------------------------------------------------- ping

        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(null, false)]
        public async Task Ping_ReportsWhatTheControllerAnswered_TreatingSilenceAsDown(bool? answered, bool expected)
        {
            using Login login = Login.Over(
                Answering(new Dictionary<string, string> { ["ping"] = PingResponse(answered) }));

            Assert.That(await login.Service.Ping(), Is.EqualTo(expected));
        }

        // ---------------------------------------------------------------- the credential

        /// <summary>
        /// The password is the one parameter this span must not carry. Asserted on the CAPTURED span
        /// rather than on log text, per TESTSTRATEGY: the tag is what an exporter ships, and it is the
        /// only place the value could leak from.
        /// </summary>
        [Test]
        public async Task Authenticate_WithSensitiveLoggingOff_RedactsThePasswordInTheSpan()
        {
            CredentialInSpan.AssertRedacted(
                await AuthenticateCapturingParameterTags(logSensitiveData: false), Password, "installer");
        }

        [Test]
        public async Task Authenticate_WithSensitiveLoggingOn_PublishesThePassword()
        {
            CredentialInSpan.AssertPublished(
                await AuthenticateCapturingParameterTags(logSensitiveData: true), Password);
        }

        private static async Task<string> AuthenticateCapturingParameterTags(bool logSensitiveData)
        {
            using TelemetryCapture capture = TelemetryCapture.Listen(Telemetry.ActivitySourceName,
                spanNames: new[] { nameof(AuthenticationService) + "." + nameof(AuthenticationService.Authenticate) });

            using Login login = Login.Over(AnsweringLogin(Success()), logSensitiveData);
            await login.Service.Authenticate();

            return TelemetryCapture.TagText(capture.Spans.Single());
        }
    }
}
