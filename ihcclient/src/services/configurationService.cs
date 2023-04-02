using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Configuration;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ConfigurationService without any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public interface IConfigurationService
    {
        public Task<SystemInfo> GetSystemInfo();

        public Task ClearUserLog();
                
        public Task<UserLogFileText> GetUserLog(string lang = "da");

        public Task DelayedRebootAsync(int delay); // TODO: Identify time unit for delay and rename arg.
    }

    /**
    * A highlevel implementation of a client to the IHC AuthenticationService without exposing any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public class ConfigurationService : IConfigurationService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Configuration.ConfigurationService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "ConfigurationService") { }

            public Task<outputMessageName3> clearUserLogAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("clearUserLog", request);
            }

            public Task<outputMessageName1> delayedRebootAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("delayedReboot", request);
            }

            public Task<outputMessageName7> getDNSServersAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("getDNSServers", request);
            }

            public Task<outputMessageName21> getEmailControlEnabledAsync(inputMessageName21 request)
            {
                return soapPost<outputMessageName21, inputMessageName21>("getEmailControlEnabled", request);
            }

            public Task<outputMessageName22> getEmailControlSettingsAsync(inputMessageName22 request)
            {
                return soapPost<outputMessageName22, inputMessageName22>("getEmailControlSettings", request);
            }

            public Task<outputMessageName16> getNetworkSettingsAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("getNetworkSettings", request);
            }

            public Task<outputMessageName5> getSMTPSettingsAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getSMTPSettings", request);
            }

            public Task<outputMessageName6> getSystemInfoAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("getSystemInfo", request);
            }

            public Task<outputMessageName2> getUserLogAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getUserLog", request);
            }

            public Task<outputMessageName18> getWebAccessControlAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("getWebAccessControl", request);
            }

            public Task<outputMessageName12> getWLanInterfaceAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("getWLanInterface", request);
            }

            public Task<outputMessageName13> getWLanScanAsync(inputMessageName13 request)
            {
                return soapPost<outputMessageName13, inputMessageName13>("getWLanScan", request);
            }

            public Task<outputMessageName14> getWLanSettingsAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("getWLanSettings", request);
            }

            public Task<outputMessageName8> setDNSServersAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("setDNSServers", request);
            }

            public Task<outputMessageName20> setEmailControlEnabledAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("setEmailControlEnabled", request);
            }

            public Task<outputMessageName23> setEmailControlSettingsAsync(inputMessageName23 request)
            {
                return soapPost<outputMessageName23, inputMessageName23>("setEmailControlSettings", request);
            }

            public Task<outputMessageName17> setNetworkSettingsAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("setNetworkSettings", request);
            }

            public Task<outputMessageName24> setServerLanguageAsync(inputMessageName24 request)
            {
                return soapPost<outputMessageName24, inputMessageName24>("setServerLanguage", request);
            }

            public Task<outputMessageName4> setSMTPSettingsAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("setSMTPSettings", request);
            }

            public Task<outputMessageName19> setWebAccessControlAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("setWebAccessControl", request);
            }

            public Task<outputMessageName15> setWLanSettingsAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("setWLanSettings", request);
            }

            public Task<outputMessageName11> testSendMessage1Async(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("testSendMessage1", request);
            }

            public Task<outputMessageName10> testSendMessageAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("testSendMessage", request);
            }

            public Task<outputMessageName9> testSettingsNowAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("testSettingsNow", request);
            }
        }

        private readonly SoapImpl impl;

        /**
        * Create an ConfigurationService instance for access to the IHC API related to configuration.
        * <param name="authService">AuthenticationService instance</param>
        */
        public ConfigurationService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        private SystemInfo mapSystemInfo(Ihc.Soap.Configuration.WSSystemInfo info)
        {
            return new SystemInfo()
            {
                Uptime = info.uptime,
                Realtimeclock = info.realtimeclock,
                SerialNumber = info.serialNumber,
                ProductionDate = info.productionDate,
                Brand = info.brand,
                Version = info.version,
                HWRevision = info.hwRevision,
                SWDate = DateTime.SpecifyKind(info.swDate, DateHelper.GetWSDateTimeKind()),
                DatalineVersion = info.datalineVersion,
                RFModuleSoftwareVersion = info.rfModuleSoftwareVersion,
                RFModuleSerialNumber = info.rfModuleSerialNumber,
                ApplicationIsWithoutViewer = info.applicationIsWithoutViewer
            };
        }

        // TODO: Implement remaining high level service.

        private UserLogFileText mapLogFile(Ihc.Soap.Configuration.WSFile e)
        {
            return new UserLogFileText()
            {
                FileName = e!=null && e.filename != null ? e.filename : "",
                Content = e!=null && e.data != null ? System.Text.Encoding.UTF8.GetString(e.data) : ""
            };
        }

        public async Task<SystemInfo> GetSystemInfo()
        {
            var resp = await impl.getSystemInfoAsync(new inputMessageName6() { });
            return resp.getSystemInfo1!=null ? mapSystemInfo(resp.getSystemInfo1) : null;
        }

        public async Task ClearUserLog() {
            await impl.clearUserLogAsync(new inputMessageName3());
        }

        public async Task<UserLogFileText> GetUserLog(string lang = "da") {
            var resp = await impl.getUserLogAsync(new inputMessageName2("", 0, lang));
            return mapLogFile(resp.getUserLog4);
        }

        public async Task DelayedRebootAsync(int delay) {
            // TODO: Find out what time unit delay is in ?
            await impl.delayedRebootAsync(new inputMessageName1(delay) {});
        }
    }
}