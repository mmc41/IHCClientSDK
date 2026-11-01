using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The controller's users: one list that adding, updating and removing change. A user is known by name, so
    /// the list never holds two users of the same name.
    /// </summary>
    public sealed class StubUserManager
    {
        private readonly Lock _gate;

        /// <summary>The list, explicit because a change reads and replaces it in one hold of the gate.</summary>
        private ImmutableArray<IhcUser> _users = [StubAuthentication.DefaultUser];

        internal StubUserManager(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>The users, in the order they were added. The default is one administrator, as on a real
        /// controller that has only its installer.</summary>
        public ImmutableArray<IhcUser> Users
        {
            get => Gated.Read(_gate, ref _users);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref _users, value);
            }
        }

        /// <summary>
        /// Adds <paramref name="user"/>, replacing a user of the same name in place. A date the caller left out is
        /// stamped with the stub's instant, as a real controller stamps the moment it adds a user.
        /// </summary>
        internal IhcUser Add(IhcUser user)
        {
            IhcUser added = user with
            {
                CreatedDate = Stamped(user.CreatedDate),
                LoginDate = Stamped(user.LoginDate),
            };
            lock (_gate)
            {
                int existing = IndexOf(added.Username);
                _users = existing < 0 ? _users.Add(added) : _users.SetItem(existing, added);
            }
            return added;
        }

        /// <summary>Replaces the user of the same name; a name with no user changes nothing.</summary>
        internal void Update(IhcUser user)
        {
            lock (_gate)
            {
                int existing = IndexOf(user.Username);
                if (existing >= 0)
                {
                    _users = _users.SetItem(existing, user);
                }
            }
        }

        /// <summary>Removes the user of that name; a name with no user changes nothing.</summary>
        internal void Remove(string? userName)
        {
            lock (_gate)
            {
                int existing = IndexOf(userName);
                if (existing >= 0)
                {
                    _users = _users.RemoveAt(existing);
                }
            }
        }

        private int IndexOf(string? userName)
        {
            for (int index = 0; index < _users.Length; index++)
            {
                if (_users[index].Username == userName)
                {
                    return index;
                }
            }
            return -1;
        }

        private static DateTimeOffset Stamped(DateTimeOffset date) => date == DateTimeOffset.MinValue ? ShapeDefaults.Instant : date;
    }
}
