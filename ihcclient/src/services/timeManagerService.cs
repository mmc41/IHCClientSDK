using System.Threading.Tasks;
using System;
using Ihc.Soap.Timemanager;
using Microsoft.Extensions.Logging;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC TimeManagerService without any of the soap distractions.
    *
    * Status: Incomplete.
    */
    public interface ITimeManagerService
    {
        public Task<DateTimeOffset> GetCurrentLocalTime();
        public Task<TimeSpan> GetUptime();
    }

    /**
    * A highlevel implementation of a client to the IHC TimeManagerService without exposing any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public class TimeManagerService : ITimeManagerService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Timemanager.TimeManagerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "TimeManagerService") { }

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
        */
        public TimeManagerService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Add remaining API.

        public async Task<DateTimeOffset> GetCurrentLocalTime()
        {
            var resp = await impl.getCurrentLocalTimeAsync(new inputMessageName2());
            var result = resp.getCurrentLocalTime1;
            if (result != null)
            {
                return result.ToDateTimeOffset();
            }
            else return DateTimeOffset.MinValue;
        }

        public async Task<TimeSpan> GetUptime()
        {
            var resp = await impl.getUptimeAsync(new inputMessageName5());
            var result = resp.getUptime1;
            return result.HasValue ? TimeSpan.FromMilliseconds(result.Value) : TimeSpan.Zero;
        }
    }
}