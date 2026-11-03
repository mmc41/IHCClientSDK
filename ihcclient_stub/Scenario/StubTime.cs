namespace Ihc.Stubbing
{
    /// <summary>
    /// The time manager's clock, settings and time-server outcome. The clock is fixed, so a test that reads it
    /// reads the same moment every run; the settings and the outcome are a real controller's answers.
    /// </summary>
    public sealed class StubTime
    {
        private readonly Lock _gate;

        private DateTimeOffset _now = ShapeDefaults.Instant;

        /// <summary>The settings as last set; their clock reading is never read, because <see cref="Now"/> is the
        /// clock.</summary>
        private TimeManagerSettings _settings = new()
        {
            SynchroniseTimeAgainstServer = false,
            UseDST = true,
            GmtOffsetInHours = 1,
            ServerName = "1.schneider.pool.ntp.org",
            SyncIntervalInHours = 1,
            OnlineCalendarUpdateOnline = false,
            OnlineCalendarCountry = "dk",
            OnlineCalendarValidUntil = 2027,
        };

        internal StubTime(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>The controller's clock, the one both of its readings come from: the local time it answers, and
        /// the UTC time in <see cref="Settings"/>.</summary>
        public DateTimeOffset Now
        {
            get => Gated.Read(_gate, ref _now);
            set => Gated.Write(_gate, ref _now, value);
        }

        /// <summary>
        /// The offset of the controller's local clock from UTC: the local time it answers is <see cref="Now"/> at this
        /// offset. The default, +01:00, is a Danish controller in winter; a captured one in summer time ran at +02:00.
        /// </summary>
        public TimeSpan UtcOffset
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = TimeSpan.FromHours(1);

        /// <summary>
        /// The time settings a get answers and a set replaces. The clock reading they carry is <see cref="Now"/> in
        /// UTC, so each read is a new record of that moment, and setting them sets the clock to theirs, as a
        /// controller's own write sets it when it synchronises with no time server; whether one that does still takes
        /// the clock from a write is not known, and taking it is the stand-in.
        /// </summary>
        public TimeManagerSettings Settings
        {
            get
            {
                lock (_gate)
                {
                    return _settings with { TimeAndDateInUTC = _now.ToUniversalTime() };
                }
            }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                lock (_gate)
                {
                    _settings = value;
                    _now = value.TimeAndDateInUTC;
                }
            }
        }

        /// <summary>
        /// The result code a settings write answers: 0 accepts it and replaces <see cref="Settings"/>; the vendor's
        /// refusals, 1 (the time could not be set) and 2 (the calendar is not available), leave them as they were.
        /// Whether a real controller keeps part of a refused write is not known; keeping all of it is the stand-in.
        /// </summary>
        public int SetSettingsResultCode
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

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
