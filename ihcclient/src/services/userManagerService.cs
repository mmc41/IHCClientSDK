using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Usermanager;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC UserManagerService without any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public interface IUserManagerService
    {
        public Task<IhcUser[]> GetUsers(bool includePassword);
    }

    /**
    * A highlevel implementation of a client to the IHC UserManagerService without exposing any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public class UserManagerService : IUserManagerService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Usermanager.UserManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "UserManagerService") { }

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

        /**
        * Create an UserManagerService instance for access to the IHC API related to users.
        * <param name="authService">AuthenticationService instance</param>
        */
        public UserManagerService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Implement remaining high level services.

        /**
        * Get list of registered controller users and their information.
        */
        public async Task<IhcUser[]> GetUsers(bool includePassword)
        {
            var resp = await impl.getUsersAsync(new inputMessageName2() { });
            return resp.getUsers1.Where((v) => v != null).Select((u) => new IhcUser()
            {
                Username = u.username,
                Password = includePassword ? u.password : string.Empty,
                Firstname = u.firstname,
                Lastname = u.lastname,
                Phone = u.phone,
                Group = u.group.type,
                Project = u.project,
                CreatedDate = u.createdDate.ToDateTimeOffset(),
                LoginDate = u.loginDate.ToDateTimeOffset()
            }).ToArray();
        }
    }
}