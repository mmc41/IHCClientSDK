using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Usermanager;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Collections.Generic;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC UserManagerService without any of the soap distractions.
    /// Status: Incomplete.
    /// </summary>
    public interface IUserManagerService : IIHCApiService
    {
        /// <summary>
        /// Get set of all users registered on the controller.
        /// </summary>
        /// <param name="includePassword">Include password in returned user objects</param>
        public Task<IReadOnlySet<IhcUser>> GetUsers(bool includePassword);

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
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "UserManagerService") { }

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

        private IhcUserGroup mapUserGroup(WSUserGroup group)
        {
            if (group == null)
                return IhcUserGroup.None;

            return mapUserGroup(group.type);
        }

        internal static IhcUserGroup mapUserGroup(string wsGroupType)
        {
            if (string.IsNullOrEmpty(wsGroupType))
                return IhcUserGroup.None;

            switch (wsGroupType)
            {
                case "text.usermanager.group_administrators": return IhcUserGroup.Administrators;
                case "gtext.users": return IhcUserGroup.Users;
                default: throw new Exception("Unkown user group " + wsGroupType);
            }
        }

        private WSUserGroup mapUserGroup(IhcUserGroup group)
        {
            string strType;
            switch (group)
            {
                case IhcUserGroup.Administrators: strType = "text.usermanager.group_administrators"; break;
                case IhcUserGroup.Users: strType = "gtext.users"; break;
                default: strType = null; break;
            }

            return new WSUserGroup() { type = strType };
        }

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
                Group = mapUserGroup(u.group),
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
                group = mapUserGroup(u.Group),
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
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>
        /// Get set of registered controller users and their information.
        /// </summary>
        /// <param name="includePassword">Include password in returned user objects (default)</param>
        public async Task<IReadOnlySet<IhcUser>> GetUsers(bool includePassword = true)
        {
            using (var activity = StartActivity(nameof(GetUsers)))
            {
                try
                {
                    activity?.SetParameters((nameof(includePassword), includePassword));

                    var resp = await impl.getUsersAsync(new inputMessageName2() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Note that we for safty reasons can return users without password in return object
                    var retv = new HashSet<IhcUser>(resp.getUsers1.Where((v) => v != null).Select((u) => mapUser(u, includePassword)));

                    // Register activity - note that regardless of if password is included, any password will be also not be logged/observed unless LogSensitiveData allows it.
                    activity?.SetReturnValue(IhcSettings.LogSensitiveData ? retv.Select(r => r.RedactPasword()).ToArray() : retv.ToArray());
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Add a new user to the IHC controller.
        /// </summary>
        /// <param name="user">User information to add</param>
        public async Task AddUser(IhcUser user)
        {              
            using (var activity = StartActivity(nameof(AddUser)))
            {
                try
                {
                    activity?.SetParameters((nameof(user), IhcSettings.LogSensitiveData ? user : user.RedactPasword()));

                    ValidationHelper.ValidateObject(user, nameof(user));

                    await impl.addUserAsync(new inputMessageName1() { addUser1 = mapUser(user) }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Remove a user from the IHC controller.
        /// </summary>
        /// <param name="username">Username of the user to remove</param>
        public async Task RemoveUser(string username)
        {
            using (var activity = StartActivity(nameof(RemoveUser)))
            {
                try
                {
                    activity?.SetParameters((nameof(username), username));

                    if (username == "usb") // Extra security from potential harm here.
                        throw new ArgumentException(message: "Can not delete reserved usb user", paramName: nameof(username));


                    await impl.removeUserAsync(new inputMessageName3() { removeUser1 = username }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Update an existing user on the IHC controller.
        /// </summary>
        /// <param name="user">Updated user information</param>
        public async Task UpdateUser(IhcUser user)
        {
            using (var activity = StartActivity(nameof(UpdateUser)))
            {
                try
                {
                    activity?.SetParameters((nameof(user), IhcSettings.LogSensitiveData ? user : user.RedactPasword()));

                    ValidationHelper.ValidateObject(user, nameof(user));

                    if (user?.Password == UserConstants.REDACTED_PASSWORD)
                        throw new ArgumentException($"Password of user should not be set to reserved value ${UserConstants.REDACTED_PASSWORD}. This is likely an error!");


                    await impl.updateUserAsync(new inputMessageName4() { updateUser1 = mapUser(user) }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }
    }
}