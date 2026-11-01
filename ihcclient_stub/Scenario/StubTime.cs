namespace Ihc.Stubbing
{
    /// <summary>
    /// The time manager's clock, settings and time-server outcome. The clock is fixed, so a test that reads it
    /// reads the same moment every run; the settings and the outcome are a real controller's answers.
    /// </summary>
    public sealed class StubTime
    {
        private readonly Lock _gate;

        internal StubTime(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>The controller's current local time.</summary>
        public DateTimeOffset Now
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = ShapeDefaults.Instant;

        /// <summary>The time settings a get answers and a set replaces. The clock reading they carry is the stub's
        /// fixed instant rather than the captured one, so it agrees with <see cref="Now"/>.</summary>
        public TimeManagerSettings Settings
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            SynchroniseTimeAgainstServer = false,
            UseDST = true,
            GmtOffsetInHours = 1,
            ServerName = "1.schneider.pool.ntp.org",
            SyncIntervalInHours = 1,
            TimeAndDateInUTC = ShapeDefaults.Instant,
            OnlineCalendarUpdateOnline = false,
            OnlineCalendarCountry = "dk",
            OnlineCalendarValidUntil = 2027,
        };

        /// <summary>
        /// What asking for the time from a server answers. The default is a success dated at the stub's fixed instant,
        /// the positive flow. The captured controller could not resolve its server and answered with no date, which
        /// the SDK reads as a date it has to substitute; a test that wants that outcome sets it.
        /// </summary>
        public TimeServerConnectionResult ServerResult
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            ConnectionWasSuccessful = true,
            DateFromServer = ShapeDefaults.Instant,
            ConnectionFailedDueToUnknownHost = false,
            ConnectionFailedDueToOtherErrors = false,
        };
    }
}
