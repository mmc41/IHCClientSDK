using System.Threading.Tasks;
using System;
using Ihc.Soap.Timemanager;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// Reads the controller's clock and uptime, and reads and writes its time configuration: time zone, summer time,
    /// time-server sync and the online holiday calendar.
    /// </summary>
    public interface ITimeManagerService : IIHCApiService
    {
        /// <summary>
        /// Gets the current local time from the controller: UTC plus its time-zone offset and, when enabled, summer
        /// time.
        /// </summary>
        /// <returns>The controller's local time at its current UTC offset, which the SDK takes in the same call from
        /// the UTC clock in <see cref="GetSettings"/>: the local clock less the UTC clock, rounded to the nearest 15
        /// minutes. When the settings carry no usable UTC clock, the offset is +01:00 and the span records a warning.
        /// <c>DateTimeOffset.MinValue</c> when the reply carries no readable local time.</returns>
        /// <remarks>The offset is the controller's current one, so it does not date an older timestamp: whether the
        /// controller's summer-time rule changed the offset since is not known from these two clocks.</remarks>
        public Task<DateTimeOffset> GetCurrentLocalTime();

        /// <summary>
        /// Gets the controller's uptime since its last restart.
        /// </summary>
        /// <returns>The uptime, which the controller sends in milliseconds as in
        /// <see cref="IConfigurationService.GetSystemInfo"/>; <c>TimeSpan.Zero</c> when the reply carries no
        /// value.</returns>
        public Task<TimeSpan> GetUptime();

        /// <summary>
        /// Gets the time configuration (time-server sync, summer time and time zone) and the controller's clock in
        /// UTC.
        /// </summary>
        /// <returns>The settings, or <c>null</c> when the reply carries none.</returns>
        public Task<TimeManagerSettings?> GetSettings();

        /// <summary>
        /// Writes the controller's complete time configuration; with server sync off,
        /// <see cref="TimeManagerSettings.TimeAndDateInUTC"/> is the clock to set.
        /// </summary>
        /// <param name="settings">The complete time settings to store; read them with <see cref="GetSettings"/> to
        /// change part of them.</param>
        /// <returns><c>true</c> when the controller answers result code 0, success; <c>false</c> for any other code:
        /// 1 means the time could not be set and 2 that the calendar is not available.</returns>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c>, its
        /// <see cref="TimeManagerSettings.GmtOffsetInHours"/> is outside -12 to +14, or synchronisation is on with an
        /// interval under one hour or no server name; nothing is sent.</exception>
        /// <exception cref="InvalidOperationException">The reply carries no result code; the settings were sent, so
        /// whether the controller applied them is unknown.</exception>
        public Task<bool> SetSettings(TimeManagerSettings settings);

        /// <summary>
        /// Asks the controller to fetch the time from <paramref name="serverName"/> itself, resolving a name through its
        /// own DNS, and reports the outcome; IHC Administrator tests its unsaved time server this way.
        /// </summary>
        /// <param name="serverName">Host name or dotted IPv4 address of the time server to test, with no scheme or
        /// port, sent as given.</param>
        /// <returns>The outcome, or <c>null</c> when the reply carries none.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="serverName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="serverName"/> is empty or whitespace; nothing is
        /// sent.</exception>
        public Task<TimeServerConnectionResult?> GetTimeFromServer(string serverName);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC TimeManagerService without exposing any of the soap distractions.
    /// </summary>
    public class TimeManagerService : ServiceBase, ITimeManagerService
    {
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Timemanager.TimeManagerService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "TimeManagerService") { }

            public Task<outputMessageName2> getCurrentLocalTimeAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getCurrentLocalTime", request);
            }

            public Task<outputMessageName3> getSettingsAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getSettings", request);
            }

            public Task<outputMessageName1> getTimeFromServerAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getTimeFromServer", request);
            }

            public Task<outputMessageName5> getUptimeAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getUptime", request);
            }

            public Task<outputMessageName4> setSettingsAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("setSettings", request);
            }
        }

        private readonly Ihc.Soap.Timemanager.TimeManagerService impl;

        /// <summary>
        /// Create a TimeManagerService instance for access to the IHC API related to time management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public TimeManagerService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal TimeManagerService(IAuthenticationService authService, Ihc.Soap.Timemanager.TimeManagerService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }
        
        // Map methods for translating between SOAP models and high-level models

        private static TimeManagerSettings? mapSettings(WSTimeManagerSettings? ws)
        {
            if (ws == null)
                return null;

            return new TimeManagerSettings
            {
                SynchroniseTimeAgainstServer = ws.synchroniseTimeAgainstServer,
                UseDST = ws.useDST,
                GmtOffsetInHours = ws.gmtOffsetInHours,
                ServerName = ws.serverName,
                SyncIntervalInHours = ws.syncIntervalInHours,
                TimeAndDateInUTC = DateHelper.OrAbsentSentinel(clockFaceAtUtc(ws.timeAndDateInUTC), nameof(TimeManagerSettings.TimeAndDateInUTC)),
                OnlineCalendarUpdateOnline = ws.online_calendar_update_online,
                OnlineCalendarCountry = ws.online_calendar_country,
                OnlineCalendarValidUntil = ws.online_calendar_valid_until
            };
        }

        private static WSTimeManagerSettings mapSettings(TimeManagerSettings settings)
        {
            return new WSTimeManagerSettings
            {
                synchroniseTimeAgainstServer = settings.SynchroniseTimeAgainstServer,
                useDST = settings.UseDST,
                gmtOffsetInHours = settings.GmtOffsetInHours,
                serverName = settings.ServerName,
                syncIntervalInHours = settings.SyncIntervalInHours,
                timeAndDateInUTC = toUtcWSDate(settings.TimeAndDateInUTC),
                online_calendar_update_online = settings.OnlineCalendarUpdateOnline,
                online_calendar_country = settings.OnlineCalendarCountry,
                online_calendar_valid_until = settings.OnlineCalendarValidUntil
            };
        }

        private static TimeServerConnectionResult? mapTimeServerConnectionResult(WSTimeServerConnectionResult? ws)
        {
            if (ws == null)
                return null;

            return new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = ws.connectionWasSuccessful,
                DateFromServer = DateHelper.OrAbsentSentinel(
                    ws.dateFromServer > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(ws.dateFromServer) : null,
                    nameof(TimeServerConnectionResult.DateFromServer)),
                ConnectionFailedDueToUnknownHost = ws.connectionFailedDueToUnknownHost,
                ConnectionFailedDueToOtherErrors = ws.connectionFailedDueToOtherErrors
            };
        }

        // timeAndDateInUTC is the controller's clock in UTC in both directions: read, it equals the reply's HTTP Date
        // header and the system information's realtimeclock; written, IHC Visual sends its pickers' time less one
        // hour from a UTC+1 PC. Its clock face therefore takes offset 0, never the WS offset other dates are read at.
        // The local clock's face is read the same way, and placed at the controller's offset once that is known.
        private static DateTimeOffset? clockFaceAtUtc(WSDate? face) => face?.ToDateTimeOffset(TimeSpan.Zero);

        private static readonly TimeSpan OffsetGranularity = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan LargestUtcOffset = TimeSpan.FromHours(14);

        // The local clock carries no offset, and a fixed one is wrong for part of the year (a controller at GMT+1 with
        // summer time was captured at UTC+2 in October). Its CURRENT offset is the local clock face less the UTC
        // clock face, read moments apart; every zone offset is a whole quarter hour, so rounding to one absorbs the
        // gap between the two reads. Without a usable UTC reading, the WS offset (+01:00) stands in, recorded.
        private static TimeSpan controllerUtcOffset(DateTimeOffset localFace, DateTimeOffset? utc, Activity? activity)
        {
            if (utc is { } utcClock && utcClock != DateTimeOffset.MinValue)
            {
                double quarters = Math.Round((localFace - utcClock) / OffsetGranularity, MidpointRounding.AwayFromZero);
                TimeSpan offset = OffsetGranularity * quarters;
                if (offset.Duration() <= LargestUtcOffset)
                    return offset;
            }

            TimeSpan assumed = DateHelper.GetWSTimeOffset();
            activity.AddWarning(
                $"The controller's settings carried no usable UTC clock reading to take its current UTC offset from; reading its local time at {assumed:hh\\:mm} after UTC.",
                ("type", "AssumedUtcOffset"),
                ("field", nameof(TimeManagerSettings.TimeAndDateInUTC)));
            return assumed;
        }

        private static WSDate? toUtcWSDate(DateTimeOffset? dateTimeOffset)
        {
            if (!dateTimeOffset.HasValue)
            {
                return null;
            }

            var dto = dateTimeOffset.Value.ToUniversalTime();
            return new WSDate
            {
                year = dto.Year,
                monthWithJanuaryAsOne = dto.Month,
                day = dto.Day,
                hours = dto.Hour,
                minutes = dto.Minute,
                seconds = dto.Second
            };
        }

        public async Task<DateTimeOffset> GetCurrentLocalTime()
        {
            using (var activity = StartActivity(nameof(GetCurrentLocalTime)))
            {
                try
                {
                    var resp = await impl.getCurrentLocalTimeAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    // The local clock face, held at offset 0 until the controller's own offset is known.
                    var localFace = DateHelper.OrAbsentSentinel(clockFaceAtUtc(resp.getCurrentLocalTime1), nameof(GetCurrentLocalTime));
                    if (localFace == DateTimeOffset.MinValue)
                    {
                        activity?.SetReturnValue(localFace);
                        return localFace;
                    }

                    var settingsResp = await impl.getSettingsAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var utc = clockFaceAtUtc(settingsResp?.getSettings1?.timeAndDateInUTC);
                    var retv = DateHelper.CreateDateTimeOffset(localFace.Year, localFace.Month, localFace.Day,
                        localFace.Hour, localFace.Minute, localFace.Second, controllerUtcOffset(localFace, utc, activity));

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<TimeSpan> GetUptime()
        {
            using (var activity = StartActivity(nameof(GetUptime)))
            {
                try
                {
                    var resp = await impl.getUptimeAsync(new inputMessageName5()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var result = resp.getUptime1;
                    var retv = result.HasValue ? TimeSpan.FromMilliseconds(result.Value) : TimeSpan.Zero;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<TimeManagerSettings?> GetSettings()
        {
            using (var activity = StartActivity(nameof(GetSettings)))
            {
                try
                {
                    var resp = await impl.getSettingsAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSettings(resp.getSettings1);

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> SetSettings(TimeManagerSettings settings)
        {
            using (var activity = StartActivity(nameof(SetSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

                    var wsSettings = mapSettings(settings);
                    var resp = await impl.setSettingsAsync(new inputMessageName4 { setSettings1 = wsSettings }).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                    var result = Acknowledged(resp.setSettings2, "the time settings");
                    // The vendor's codes are RESULT_OK = 0, TIMESETTING_FAILED = 1 and CALENDAR_NOT_AVAILABLE = 2.
                    var retv = result == 0;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<TimeServerConnectionResult?> GetTimeFromServer(string serverName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
            using (var activity = StartActivity(nameof(GetTimeFromServer)))
            {
                try
                {
                    activity?.SetParameters((nameof(serverName), serverName));

                    var resp = await impl.getTimeFromServerAsync(new inputMessageName1(serverName)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapTimeServerConnectionResult(resp.getTimeFromServer2);

                    activity?.SetReturnValue(retv);
                    return retv;
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