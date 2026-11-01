namespace Ihc.Stubbing
{
    /// <summary>
    /// The stub's authentication: who a login answers as, and which logins it refuses. It keeps no session store,
    /// because the stub enforces no cookie; each successful login simply issues a fresh one.
    /// </summary>
    public sealed class StubAuthentication
    {
        /// <summary>The user a login answers as before a test sets one. It carries a password, as a real controller's
        /// user list does, so it is also a user the SDK's own validation accepts for adding or updating.</summary>
        public static readonly IhcUser DefaultUser = new()
        {
            Username = "stub",
            Password = "stub-user-pw",
            Firstname = "Stub",
            Lastname = "Controller",
            Email = "stub@stub.invalid",
            Phone = "00000000",
            Group = IhcUserGroup.Administrators,
            Project = "stub",
            CreatedDate = ShapeDefaults.Instant,
            LoginDate = ShapeDefaults.Instant,
        };

        private readonly Lock _gate;
        private int _sessions;

        internal StubAuthentication(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>
        /// The user a successful login answers with, under the user name the login gave. A date of
        /// <see cref="DateTimeOffset.MinValue"/> goes on the wire as absent.
        /// </summary>
        public IhcUser User
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = DefaultUser;

        /// <summary>
        /// Whether a login is accepted. The stub is forgiving: any user name with any password succeeds, except a
        /// blank password, which no controller accepts, and <see cref="ControllerStub.IllegalPassword"/>, which a
        /// negative test sends to be refused. A real password from a settings file therefore logs in too.
        /// </summary>
        internal static bool Accepts(string? userName, string? password) =>
            !string.IsNullOrEmpty(userName)
            && !string.IsNullOrWhiteSpace(password)
            && !string.Equals(password, ControllerStub.IllegalPassword, StringComparison.Ordinal);

        /// <summary>A session cookie no earlier login of this stub was issued.</summary>
        internal string NextSessionCookie()
        {
            lock (_gate)
            {
                return $"JSESSIONID=stub-{++_sessions}";
            }
        }
    }
}
