using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Usermanager;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC UserManagerService without any of the soap distractions.
    /// Status: Incomplete.
    /// </summary>
    public interface IUserManagerService : IIHCService
    {
        /// <summary>
        /// Get list of all users registered on the controller.
        /// </summary>
        /// <param name="includePassword">Include password in returned user objects</param>
        public Task<IhcUser[]> GetUsers(bool includePassword);

        /// <summary>
        /// Add a new user to the controller.
        /// </summary>
        /// <param name="user">User information to add</param>
        public Task AddUser(IhcUser user);

        /// <summary>
        /// Remove a user from the controller by username.
        /// </summary>
        /// <param name="username">Username of the user to remove</param>
        public Task RemoveUser(string username);

        /// <summary>
        /// Update an existing user's information on the controller.
        /// </summary>
        /// <param name="user">Updated user information</param>
        public Task UpdateUser(IhcUser user);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC UserManagerService without exposing any of the soap distractions.
    /// </summary>
    public class UserManagerService : ServiceBase, IUserManagerService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Usermanager.UserManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, IhcSettings settings) : base(logger, cookieHandler, settings, "UserManagerService") { }

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
                Password = includePassword ? u.password : UserConstants.REDACTED_PASSWORD,
                Email = u.email,
                Firstname = u.firstname,
                Lastname = u.lastname,
                Phone = u.phone,
                Group = u.group?.type,
                Project = u.project,
                CreatedDate = u.createdDate?.ToDateTimeOffset() ?? DateTimeOffset.MinValue,
                LoginDate = u.loginDate?.ToDateTimeOffset() ?? DateTimeOffset.MinValue
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

        /// <summary>
        /// Create a UserManagerService instance for access to the IHC API related to user management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public UserManagerService(IAuthenticationService authService)
            : base(authService.Logger, authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), settings);
        }

        /// <summary>
        /// Get list of registered controller users and their information.
        /// </summary>
        /// <param name="includePassword">Include password in returned user objects (default)</param>
        public async Task<IhcUser[]> GetUsers(bool includePassword = true)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(includePassword), includePassword));

            var resp = await impl.getUsersAsync(new inputMessageName2() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

            // Note that we for safty reasons can return users without password in return object
            var retv = resp.getUsers1.Where((v) => v != null).Select((u) => mapUser(u, includePassword)).ToArray();

            // Register activity - note that regardless of if password is included, any password will be also not be logged/observed unless LogSensitiveData allows it.
            activity?.SetReturnValue(IhcSettings.LogSensitiveData ? retv.Select(r => r.RedactPasword()).ToArray() : retv);
            return retv;
        }

        /// <summary>
        /// Add a new user to the IHC controller.
        /// </summary>
        /// <param name="user">User information to add</param>
        public async Task AddUser(IhcUser user)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(user), IhcSettings.LogSensitiveData ? user : user.RedactPasword()));

            await impl.addUserAsync(new inputMessageName1() { addUser1 = mapUser(user) }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Remove a user from the IHC controller.
        /// </summary>
        /// <param name="username">Username of the user to remove</param>
        public async Task RemoveUser(string username)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(username), username));

            await impl.removeUserAsync(new inputMessageName3() { removeUser1 = username }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }

        /// <summary>
        /// Update an existing user on the IHC controller.
        /// </summary>
        /// <param name="user">Updated user information</param>
        public async Task UpdateUser(IhcUser user)
        {
            if (user?.Password == UserConstants.REDACTED_PASSWORD)
                throw new ArgumentException($"Password of user can not be set to reserved value ${UserConstants.REDACTED_PASSWORD}. This is likely an error!");

            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters((nameof(user), user));

            await impl.updateUserAsync(new inputMessageName4() { updateUser1 = mapUser(user) }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
        }
    }
}