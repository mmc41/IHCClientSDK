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
        /// <returns>The controller's local time. The SDK reads it at a fixed +01:00 offset, so the instant is right
        /// only while the controller runs at UTC+1; <c>DateTimeOffset.MinValue</c> when the reply carries no readable
        /// date.</returns>
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
        /// <returns><c>true</c> when the controller answers result code 1; <c>false</c> for any other code, also when
        /// the reply carries none. See the remarks.</returns>
        /// <remarks>Known defect: the controller's code 0 means success, 1 that the time could not be set and 2 that
        /// the calendar is not available, so this method returns <c>true</c> when the time could not be set and
        /// <c>false</c> when it was set.</remarks>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c> or breaks a limit declared on
        /// <see cref="TimeManagerSettings"/>; nothing is sent.</exception>
        public Task<bool> SetSettings(TimeManagerSettings settings);

        /// <summary>
        /// Asks the controller to fetch the time from a time server itself, resolving a name through its own DNS, and
        /// reports the outcome.
        /// </summary>
        /// <returns>The outcome, or <c>null</c> when the reply carries none.</returns>
        /// <remarks>The operation takes a server name, which this method leaves empty; which server the controller
        /// then asks is not known.</remarks>
        public Task<TimeServerConnectionResult?> GetTimeFromServer();
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
                TimeAndDateInUTC = DateHelper.OrAbsentSentinel(ws.timeAndDateInUTC?.ToDateTimeOffset(), nameof(TimeManagerSettings.TimeAndDateInUTC)),
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
                timeAndDateInUTC = mapWSDate(settings.TimeAndDateInUTC),
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

        private static WSDate? mapWSDate(DateTimeOffset? dateTimeOffset)
        {
            if (!dateTimeOffset.HasValue)
            {
                return null;
            }

            var dto = dateTimeOffset.Value.ToOffset(DateHelper.GetWSTimeOffset());
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
                    var result = resp.getCurrentLocalTime1;
                    var retv = DateHelper.OrAbsentSentinel(result?.ToDateTimeOffset(), nameof(GetCurrentLocalTime));

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
                    var result = resp.setSettings2;
                    var retv = result.HasValue && result.Value == 1;

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

        public async Task<TimeServerConnectionResult?> GetTimeFromServer()
        {
            using (var activity = StartActivity(nameof(GetTimeFromServer)))
            {
                try
                {
                    var resp = await impl.getTimeFromServerAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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