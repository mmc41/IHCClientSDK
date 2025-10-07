using System.Threading.Tasks;
using System;
using Ihc.Soap.Timemanager;
using Microsoft.Extensions.Logging;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC TimeManagerService without any of the soap distractions.
    */
    public interface ITimeManagerService : IIHCService
    {
        /**
        * Get the current local time from the controller.
        */
        public Task<DateTimeOffset> GetCurrentLocalTime();

        /**
        * Get controller uptime since last restart.
        */
        public Task<TimeSpan> GetUptime();

        /**
        * Get time manager settings including time sync, DST, and timezone configuration.
        */
        public Task<TimeManagerSettings> GetSettings();

        /**
        * Set time manager settings for time synchronization and timezone.
        */
        public Task<bool> SetSettings(TimeManagerSettings settings);

        /**
        * Synchronize time with configured NTP server and get connection result.
        */
        public Task<TimeServerConnectionResult> GetTimeFromServer();
    }

    /**
    * A highlevel implementation of a client to the IHC TimeManagerService without exposing any of the soap distractions.
    */
    public class TimeManagerService : ServiceBase, ITimeManagerService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;
        private readonly bool asyncContinueOnCapturedContext;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Timemanager.TimeManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "TimeManagerService", asyncContinueOnCapturedContext) { }

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

        /**
        * Create an TimeManagerService instance for access to the IHC API related to time/settings.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public TimeManagerService(IAuthenticationService authService, bool asyncContinueOnCapturedContext = false)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.asyncContinueOnCapturedContext = asyncContinueOnCapturedContext;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, asyncContinueOnCapturedContext);
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
                TimeAndDateInUTC = ws.timeAndDateInUTC?.ToDateTimeOffset(),
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
                    : null,
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
            var resp = await impl.getCurrentLocalTimeAsync(new inputMessageName2()).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = resp.getCurrentLocalTime1;
            if (result != null)
            {
                return result.ToDateTimeOffset();
            }
            else return DateTimeOffset.MinValue;
        }

        public async Task<TimeSpan> GetUptime()
        {
            var resp = await impl.getUptimeAsync(new inputMessageName5()).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = resp.getUptime1;
            return result.HasValue ? TimeSpan.FromMilliseconds(result.Value) : TimeSpan.Zero;
        }

        public async Task<TimeManagerSettings> GetSettings()
        {
            var resp = await impl.getSettingsAsync(new inputMessageName3()).ConfigureAwait(asyncContinueOnCapturedContext);
            return mapSettings(resp.getSettings1);
        }

        public async Task<bool> SetSettings(TimeManagerSettings settings)
        {
            var wsSettings = mapSettings(settings);
            var resp = await impl.setSettingsAsync(new inputMessageName4 { setSettings1 = wsSettings }).ConfigureAwait(asyncContinueOnCapturedContext);
            var result = resp.setSettings2;
            return result.HasValue && result.Value == 1;
        }

        public async Task<TimeServerConnectionResult> GetTimeFromServer()
        {
            var resp = await impl.getTimeFromServerAsync(new inputMessageName1()).ConfigureAwait(asyncContinueOnCapturedContext);
            return mapTimeServerConnectionResult(resp.getTimeFromServer2);
        }
    }
}