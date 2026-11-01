using Ihc.Soap.Authentication;

namespace Ihc.Stubbing
{
    /// <summary>The authentication service's answers, read from <see cref="StubAuthentication"/>.</summary>
    internal sealed class AuthenticationAnswers(StubAuthentication scenario) : IWireAnswers
    {
        public string Service => "AuthenticationService";

        internal Task<outputMessageName2> authenticateAsync(inputMessageName2 request)
        {
            string? userName = request.authenticate1?.username;
            if (!Login(scenario, userName, request.authenticate1?.password))
            {
                return Task.FromResult(new outputMessageName2 { authenticate2 = new WSLoginResult { loginFailedDueToAccountInvalid = true } });
            }

            return Task.FromResult(new outputMessageName2
            {
                authenticate2 = new WSLoginResult { loginWasSuccessful = true, loggedInUser = UserWire.ToWire(scenario.User with { Username = userName }) },
            });
        }

        /// <summary>
        /// The login rule both APIs answer with: a session cookie for an accepted password. It journals the user
        /// name alone, because a journal is read and printed freely and the password must never reach it.
        /// </summary>
        internal static bool Login(StubAuthentication scenario, string? userName, string? password)
        {
            AnswerContext.RecordPayload(userName);
            bool accepted = StubAuthentication.Accepts(userName, password);
            if (accepted)
            {
                AnswerContext.IssueCookie(scenario.NextSessionCookie());
            }
            return accepted;
        }

        internal static Task<outputMessageName1> disconnectAsync(inputMessageName1 request) =>
            Task.FromResult(new outputMessageName1 { disconnect1 = true });

        internal static Task<outputMessageName3> pingAsync(inputMessageName3 request) =>
            Task.FromResult(new outputMessageName3 { ping1 = true });
    }
}
