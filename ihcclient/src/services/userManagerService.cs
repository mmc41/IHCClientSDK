using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Usermanager;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC UserManagerService without any of the soap distractions.
    *
    * Status: Incomplete.
    */
    public interface IUserManagerService : IIHCService
    {
        /**
        * Get list of all users registered on the controller.
        * @param includePassword Include password in returned user objects
        */
        public Task<IhcUser[]> GetUsers(bool includePassword);

        /**
        * Add a new user to the controller.
        */
        public Task AddUser(IhcUser user);

        /**
        * Remove a user from the controller by username.
        */
        public Task RemoveUser(string username);

        /**
        * Update an existing user's information on the controller.
        */
        public Task UpdateUser(IhcUser user);
    }

    /**
    * A highlevel implementation of a client to the IHC UserManagerService without exposing any of the soap distractions.
    */
    public class UserManagerService : ServiceBase, IUserManagerService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Usermanager.UserManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "UserManagerService", asyncContinueOnCapturedContext) { }

            public Task<outputMessageName1> addUserAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("addUser", request);
            }

            public Task<outputMessageName2> getUsersAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getUsers", request);
            }

            public Task<outputMessageName3> removeUserAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("removeUser", request);
            }

            public Task<outputMessageName4> updateUserAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("updateUser", request);
            }
        }

        private readonly SoapImpl impl;

        private IhcUser mapUser(Ihc.Soap.Usermanager.WSUser u, bool includePassword)
        {
            return new IhcUser()
            {
                Username = u.username,
                Password = includePassword ? u.password : string.Empty,
                Email = u.email,
                Firstname = u.firstname,
                Lastname = u.lastname,
                Phone = u.phone,
                Group = u.group?.type,
                Project = u.project,
                CreatedDate = u.createdDate != null ? u.createdDate.ToDateTimeOffset() : DateTimeOffset.MinValue,
                LoginDate = u.loginDate != null ? u.loginDate.ToDateTimeOffset() : DateTimeOffset.MinValue
            };
        }

        private Ihc.Soap.Usermanager.WSUser mapUser(IhcUser u)
        {
            return new Ihc.Soap.Usermanager.WSUser()
            {
                username = u.Username,
                password = u.Password,
                email = u.Email,
                firstname = u.Firstname,
                lastname = u.Lastname,
                phone = u.Phone,
                group = new Ihc.Soap.Usermanager.WSUserGroup() { type = u.Group },
                project = u.Project,
                createdDate = new Ihc.Soap.Usermanager.WSDate()
                {
                    year = u.CreatedDate.Year,
                    monthWithJanuaryAsOne = u.CreatedDate.Month,
                    day = u.CreatedDate.Day,
                    hours = u.CreatedDate.Hour,
                    minutes = u.CreatedDate.Minute,
                    seconds = u.CreatedDate.Second
                },
                loginDate = new Ihc.Soap.Usermanager.WSDate()
                {
                    year = u.LoginDate.Year,
                    monthWithJanuaryAsOne = u.LoginDate.Month,
                    day = u.LoginDate.Day,
                    hours = u.LoginDate.Hour,
                    minutes = u.LoginDate.Minute,
                    seconds = u.LoginDate.Second
                }
            };
        }

        /**
        * Create an UserManagerService instance for access to the IHC API related to users.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public UserManagerService(IAuthenticationService authService, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, asyncContinueOnCapturedContext);
        }

        /**
        * Get list of registered controller users and their information.
        */
        public async Task<IhcUser[]> GetUsers(bool includePassword)
        {
            var resp = await impl.getUsersAsync(new inputMessageName2() { }).ConfigureAwait(asyncContinueOnCapturedContext);
            return resp.getUsers1.Where((v) => v != null).Select((u) => mapUser(u, includePassword)).ToArray();
        }

        /**
        * Add a new user to the IHC controller.
        */
        public async Task AddUser(IhcUser user)
        {
            await impl.addUserAsync(new inputMessageName1() { addUser1 = mapUser(user) }).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        /**
        * Remove a user from the IHC controller.
        */
        public async Task RemoveUser(string username)
        {
            await impl.removeUserAsync(new inputMessageName3() { removeUser1 = username }).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        /**
        * Update an existing user on the IHC controller.
        */
        public async Task UpdateUser(IhcUser user)
        {
            await impl.updateUserAsync(new inputMessageName4() { updateUser1 = mapUser(user) }).ConfigureAwait(asyncContinueOnCapturedContext);
        }
    }
}