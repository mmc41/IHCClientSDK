using Ihc.Soap.Authentication;

namespace Ihc.Stubbing
{
    /// <summary>
    /// A controller user as the authentication service carries it. The user manager generates a twin of the same
    /// types, which <see cref="GeneratedTwin"/> crosses to, so the one mapping serves both services.
    /// </summary>
    internal static class UserWire
    {
        private const string Administrators = "text.usermanager.group_administrators";

        private const string Users = "gtext.users";

        internal static WSUser ToWire(IhcUser user) => new()
        {
            username = user.Username,
            password = user.Password,
            firstname = user.Firstname,
            lastname = user.Lastname,
            email = user.Email,
            phone = user.Phone,
            group = GroupToWire(user.Group),
            project = user.Project,
            createdDate = WireDates.ToWire<WSDate>(user.CreatedDate),
            loginDate = WireDates.ToWire<WSDate>(user.LoginDate),
        };

        internal static IhcUser FromWire(WSUser wire)
        {
            return new IhcUser
            {
                Username = wire.username,
                Password = wire.password,
                Firstname = wire.firstname,
                Lastname = wire.lastname,
                Email = wire.email,
                Phone = wire.phone,
                Group = wire.group?.type switch
                {
                    Administrators => IhcUserGroup.Administrators,
                    Users => IhcUserGroup.Users,
                    _ => IhcUserGroup.None,
                },
                Project = wire.project,
                CreatedDate = WireDates.FromWire(wire.createdDate),
                LoginDate = WireDates.FromWire(wire.loginDate),
            };
        }

        /// <summary>The group as the controller names it; no group is sent as none, which the SDK reads as
        /// <see cref="IhcUserGroup.None"/>.</summary>
        private static WSUserGroup? GroupToWire(IhcUserGroup group) => group switch
        {
            IhcUserGroup.Administrators => new WSUserGroup { type = Administrators },
            IhcUserGroup.Users => new WSUserGroup { type = Users },
            _ => null,
        };
    }
}
