using Ihc.Soap.Usermanager;
using Authentication = Ihc.Soap.Authentication;

namespace Ihc.Stubbing
{
    /// <summary>The user manager's answers over the one user list in <see cref="StubUserManager"/>.</summary>
    internal sealed class UserManagerAnswers(StubUserManager scenario) : IWireAnswers
    {
        public string Service => "UserManagerService";

        internal Task<outputMessageName2> getUsersAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2
            {
                getUsers1 = [.. scenario.Users.Select(user => GeneratedTwin.Translate<WSUser>(UserWire.ToWire(user))!)],
            });

        internal Task<outputMessageName1> addUserAsync(inputMessageName1 request)
        {
            AnswerContext.RecordPayload(scenario.Add(FromWire(request.addUser1)));
            return Task.FromResult(new outputMessageName1());
        }

        internal Task<outputMessageName4> updateUserAsync(inputMessageName4 request)
        {
            IhcUser user = FromWire(request.updateUser1);
            AnswerContext.RecordPayload(user);
            scenario.Update(user);
            return Task.FromResult(new outputMessageName4());
        }

        internal Task<outputMessageName3> removeUserAsync(inputMessageName3 request)
        {
            AnswerContext.RecordPayload(request.removeUser1);
            scenario.Remove(request.removeUser1);
            return Task.FromResult(new outputMessageName3());
        }

        private static IhcUser FromWire(WSUser? wire) =>
            UserWire.FromWire(GeneratedTwin.Translate<Authentication.WSUser>(wire)
                ?? throw new InvalidOperationException("a user change carried no user"));
    }
}
