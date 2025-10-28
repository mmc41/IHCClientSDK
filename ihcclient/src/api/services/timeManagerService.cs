using System.Threading.Tasks;
using System;
using Ihc.Soap.Timemanager;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC TimeManagerService without any of the soap distractions.
    /// </summary>
    public interface ITimeManagerService : IIHCApiService
    {
        /// <summary>
        /// Get the current local time from the controller.
        /// </summary>
        public Task<DateTimeOffset> GetCurrentLocalTime();

        /// <summary>
        /// Get controller uptime since last restart.
        /// </summary>
        public Task<TimeSpan> GetUptime();

        /// <summary>
        /// Get time manager settings including time sync, DST, and timezone configuration.
        /// </summary>
        public Task<TimeManagerSettings> GetSettings();

        /// <summary>
        /// Set time manager settings for time synchronization and timezone.
        /// </summary>
        /// <param name="settings">Time manager settings to configure</param>
        public Task<bool> SetSettings(TimeManagerSettings settings);

        /// <summary>
        /// Synchronize time with configured NTP server and get connection result.
        /// </summary>
        public Task<TimeServerConnectionResult> GetTimeFromServer();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC TimeManagerService without exposing any of the soap distractions.
    /// </summary>
    public class TimeManagerService : ServiceBase, ITimeManagerService
    {
        private readonly IAuthenticationService authService;

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

        private readonly SoapImpl impl;

        /// <summary>
        /// Create a TimeManagerService instance for access to the IHC API related to time management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public TimeManagerService(IAuthenticationService authService)
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }
        
        // Map methods for translating between SOAP models and high-level models

        private TimeManagerSettings mapSettings(WSTimeManagerSettings ws)
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
                TimeAndDateInUTC = ws.timeAndDateInUTC?.ToDateTimeOffset() ?? DateTimeOffset.MinValue,
                OnlineCalendarUpdateOnline = ws.online_calendar_update_online,
                OnlineCalendarCountry = ws.online_calendar_country,
                OnlineCalendarValidUntil = ws.online_calendar_valid_until
            };
        }

        private WSTimeManagerSettings mapSettings(TimeManagerSettings settings)
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

        private TimeServerConnectionResult mapTimeServerConnectionResult(WSTimeServerConnectionResult ws)
        {
            if (ws == null)
                return null;

            return new TimeServerConnectionResult
            {
                ConnectionWasSuccessful = ws.connectionWasSuccessful,
                DateFromServer = ws.dateFromServer > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ws.dateFromServer)
                    : DateTimeOffset.MinValue,
                ConnectionFailedDueToUnknownHost = ws.connectionFailedDueToUnknownHost,
                ConnectionFailedDueToOtherErrors = ws.connectionFailedDueToOtherErrors
            };
        }

        private WSDate mapWSDate(DateTimeOffset? dateTimeOffset)
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
                    var retv = result != null ? result.ToDateTimeOffset() : DateTimeOffset.MinValue;

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

        public async Task<TimeManagerSettings> GetSettings()
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

                    ValidationHelper.ValidateObject(settings, nameof(settings));

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

        public async Task<TimeServerConnectionResult> GetTimeFromServer()
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