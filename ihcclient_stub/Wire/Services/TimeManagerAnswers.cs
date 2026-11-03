using Ihc.Soap.Timemanager;

namespace Ihc.Stubbing
{
    /// <summary>The time manager's answers, read from <see cref="StubTime"/>.</summary>
    internal sealed class TimeManagerAnswers(StubTime scenario) : IWireAnswers
    {
        public string Service => "TimeManagerService";

        internal Task<outputMessageName2> getCurrentLocalTimeAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2 { getCurrentLocalTime1 = WireDates.ToWire<WSDate>(scenario.Now, scenario.UtcOffset) });

        internal Task<outputMessageName3> getSettingsAsync(inputMessageName3 request) =>
            Task.FromResult(new outputMessageName3 { getSettings1 = SettingsToWire(scenario.Settings) });

        /// <summary>Answers the scenario's result code, 0 for success by default, and stores the settings, the clock
        /// they carry with them, only on 0.</summary>
        internal Task<outputMessageName4> setSettingsAsync(inputMessageName4 request)
        {
            TimeManagerSettings settings = SettingsFromWire(request.setSettings1
                ?? throw new InvalidOperationException("a settings change carried no settings"));
            AnswerContext.RecordPayload(settings);
            int resultCode = scenario.SetSettingsResultCode;
            if (resultCode == 0)
            {
                scenario.Settings = settings;
            }
            return Task.FromResult(new outputMessageName4 { setSettings2 = resultCode });
        }

        /// <summary>The scenario's outcome, whichever server was asked: the stub reaches no time server. The server
        /// asked is journaled.</summary>
        internal Task<outputMessageName1> getTimeFromServerAsync(inputMessageName1 request)
        {
            AnswerContext.RecordPayload(request.getTimeFromServer1);
            TimeServerConnectionResult result = scenario.ServerResult;
            return Task.FromResult(new outputMessageName1
            {
                getTimeFromServer2 = new WSTimeServerConnectionResult
                {
                    connectionWasSuccessful = result.ConnectionWasSuccessful,
                    // Unix milliseconds, with 0 for no date: the SDK reads anything not above 0 as none.
                    dateFromServer = result.DateFromServer == DateTimeOffset.MinValue ? 0 : result.DateFromServer.ToUnixTimeMilliseconds(),
                    connectionFailedDueToUnknownHost = result.ConnectionFailedDueToUnknownHost,
                    connectionFailedDueToOtherErrors = result.ConnectionFailedDueToOtherErrors,
                },
            });
        }

        private static WSTimeManagerSettings SettingsToWire(TimeManagerSettings settings) => new()
        {
            synchroniseTimeAgainstServer = settings.SynchroniseTimeAgainstServer,
            useDST = settings.UseDST,
            gmtOffsetInHours = settings.GmtOffsetInHours,
            serverName = settings.ServerName,
            syncIntervalInHours = settings.SyncIntervalInHours,
            timeAndDateInUTC = WireDates.ToWire<WSDate>(settings.TimeAndDateInUTC, TimeSpan.Zero),
            online_calendar_update_online = settings.OnlineCalendarUpdateOnline,
            online_calendar_country = settings.OnlineCalendarCountry,
            online_calendar_valid_until = settings.OnlineCalendarValidUntil,
        };

        private static TimeManagerSettings SettingsFromWire(WSTimeManagerSettings wire) => new()
        {
            SynchroniseTimeAgainstServer = wire.synchroniseTimeAgainstServer,
            UseDST = wire.useDST,
            GmtOffsetInHours = wire.gmtOffsetInHours,
            ServerName = wire.serverName,
            SyncIntervalInHours = wire.syncIntervalInHours,
            TimeAndDateInUTC = WireDates.FromWire(wire.timeAndDateInUTC, TimeSpan.Zero),
            OnlineCalendarUpdateOnline = wire.online_calendar_update_online,
            OnlineCalendarCountry = wire.online_calendar_country,
            OnlineCalendarValidUntil = wire.online_calendar_valid_until,
        };
    }
}
