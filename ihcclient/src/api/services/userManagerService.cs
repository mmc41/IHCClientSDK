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

        private readonly Ihc.Soap.Usermanager.UserManagerService impl;

        private static IhcUserGroup mapUserGroup(WSUserGroup? group)
        {
            if (group == null)
                return IhcUserGroup.None;

            return mapUserGroup(group.type);
        }

        internal static IhcUserGroup mapUserGroup(string? wsGroupType)
        {
            if (string.IsNullOrEmpty(wsGroupType))
                return IhcUserGroup.None;

            switch (wsGroupType)
            {
                case "text.usermanager.group_administrators": return IhcUserGroup.Administrators;
                case "gtext.users": return IhcUserGroup.Users;
                default: throw new ArgumentException("Unknown user group " + wsGroupType, nameof(wsGroupType));
            }
        }

        private static WSUserGroup mapUserGroup(IhcUserGroup group)
        {
            string? strType;
            switch (group)
            {
                case IhcUserGroup.Administrators: strType = "text.usermanager.group_administrators"; break;
                case IhcUserGroup.Users: strType = "gtext.users"; break;
                default: strType = null; break;
            }

            return new WSUserGroup() { type = strType };
        }

        private static IhcUser mapUser(Ihc.Soap.Usermanager.WSUser u, bool includePassword)
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
                CreatedDate = DateHelper.OrAbsentSentinel(u.createdDate?.ToDateTimeOffset(), nameof(IhcUser.CreatedDate)),
                LoginDate = DateHelper.OrAbsentSentinel(u.loginDate?.ToDateTimeOffset(), nameof(IhcUser.LoginDate))
            };
        }

        private static Ihc.Soap.Usermanager.WSUser mapUser(IhcUser u)
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
                createdDate = mapWSDate(u.CreatedDate),
                loginDate = mapWSDate(u.LoginDate)
            };
        }

        /// <summary>
        /// The wire carries a bare clock face and no offset, so the value is moved to the WS offset FIRST -
        /// the offset <c>ToDateTimeOffset</c> reads it back at. Copying the source's own fields instead wrote
        /// a date stated at any other offset as though its wall clock had already been WS-local, and it read
        /// back shifted by the difference between the two.
        /// </summary>
        private static Ihc.Soap.Usermanager.WSDate mapWSDate(DateTimeOffset v)
        {
            var dto = v.ToOffset(DateHelper.GetWSTimeOffset());
            return new Ihc.Soap.Usermanager.WSDate()
            {
                year = dto.Year,
                monthWithJanuaryAsOne = dto.Month,
                day = dto.Day,
                hours = dto.Hour,
                minutes = dto.Minute,
                seconds = dto.Second
            };
        }

        /// <summary>
        /// Create a UserManagerService instance for access to the IHC API related to user management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public UserManagerService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal UserManagerService(IAuthenticationService authService, Ihc.Soap.Usermanager.UserManagerService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
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
                    // A null wire entry is discarded silently, so an administrator reading this list cannot tell a
                    // controller with fewer users from one whose answer was partly unreadable. The set is the
                    // contract; the count is the diagnosis, and it is taken over the WIRE entries rather than over
                    // the returned set: IhcUser is a record with value equality and this mapping REDACTS the
                    // password unless the caller asked for it, so two users differing only there merge - a set
                    // smaller than the wire list is not by itself evidence that anything was unreadable.
                    var retv = new HashSet<IhcUser>(
                        WireList.MapPresent(resp.getUsers1, (u) => mapUser(u, includePassword), activity, nameof(GetUsers)));

                    // Register activity - note that regardless of if password is included, any password will be also not be logged/observed unless LogSensitiveData allows it.
                    // Stringified here through the LogSensitiveData-aware overload: the tag is rendered by
                    // whatever exports it, and IhcUser's parameterless ToString() is the unsafe one.
                    activity?.SetReturnValue(string.Join(", ", retv.Select(r => r.ToString(settings.LogSensitiveData))));
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
            ArgumentNullException.ThrowIfNull(user);
            using (var activity = StartActivity(nameof(AddUser)))
            {
                try
                {
                    activity?.SetParameters((nameof(user), IhcSettings.LogSensitiveData ? user : user.RedactPassword()));

                    ValidationHelper.ValidateDataAnnotations(user, nameof(user));

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
            ArgumentNullException.ThrowIfNull(user);
            using (var activity = StartActivity(nameof(UpdateUser)))
            {
                try
                {
                    activity?.SetParameters((nameof(user), IhcSettings.LogSensitiveData ? user : user.RedactPassword()));

                    ValidationHelper.ValidateDataAnnotations(user, nameof(user));

                    if (user.Password == UserConstants.REDACTED_PASSWORD)
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