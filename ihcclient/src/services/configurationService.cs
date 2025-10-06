using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Configuration;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ConfigurationService without any of the soap distractions.
    */
    public interface IConfigurationService
    {
        public Task<SystemInfo> GetSystemInfo();

        public Task ClearUserLog();

        public Task<string[]> GetUserLog(string lang = "da");

        /// <summary>
        /// Reboot the IHC controller after a delay.
        /// </summary>
        /// <param name="delayUnknownUnit">Delay before reboot (time unit unknown - requires testing to determine)</param>
        public Task DelayedReboot(int delayUnknownUnit);

        // Network operations
        public Task<NetworkSettings> GetNetworkSettings();
        public Task SetNetworkSettings(NetworkSettings settings);
        public Task<string[]> GetDNSServers();
        public Task SetDNSServers(string[] dnsServers);

        // WiFi operations
        public Task<WLanSettings> GetWLanSettings();
        public Task SetWLanSettings(WLanSettings settings);
        public Task<WLanInterface> GetWLanInterface();
        public Task<WLanCell[]> GetWLanScan();

        // SMTP operations
        public Task<SMTPSettings> GetSMTPSettings();
        public Task SetSMTPSettings(SMTPSettings settings);
        public Task TestSettingsNow();
        public Task<bool> TestSendMessage(string recipient, string subject, string message);

        // Email Control operations
        public Task<bool> GetEmailControlEnabled();
        public Task SetEmailControlEnabled(bool enabled);
        public Task<EmailControlSettings> GetEmailControlSettings();
        public Task SetEmailControlSettings(EmailControlSettings settings);

        // Web Access Control operations
        public Task<WebAccessControl> GetWebAccessControl();
        public Task SetWebAccessControl(WebAccessControl accessControl);

        // Language operations
        public Task SetServerLanguage(string language);
    }

    /**
    * A highlevel implementation of a client to the IHC ConfigurationService without exposing any of the soap distractions.
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

        private string[] mapLogFile(Ihc.Soap.Configuration.WSFile e)
        {
            string logs = e!=null && e.data != null ? System.Text.Encoding.UTF8.GetString(e.data) : "";
            return logs.Split('\n', '\r');
        }

        private NetworkSettings mapNetworkSettings(Ihc.Soap.Configuration.WSNetworkSettings settings)
        {
            return settings != null ? new NetworkSettings()
            {
                IpAddress = settings.ipAddress,
                Netmask = settings.netmask,
                Gateway = settings.gateway,
                HttpPort = settings.httpPort,
                HttpsPort = settings.httpsPort
            } : null;
        }

        private Ihc.Soap.Configuration.WSNetworkSettings unmapNetworkSettings(NetworkSettings settings)
        {
            return new Ihc.Soap.Configuration.WSNetworkSettings()
            {
                ipAddress = settings.IpAddress,
                netmask = settings.Netmask,
                gateway = settings.Gateway,
                httpPort = settings.HttpPort,
                httpsPort = settings.HttpsPort
            };
        }

        private WLanSettings mapWLanSettings(Ihc.Soap.Configuration.WSWLanSettings settings)
        {
            return settings != null ? new WLanSettings()
            {
                Enabled = settings.enabled,
                Ssid = settings.ssid,
                Key = settings.key,
                SecurityType = settings.securityType,
                EncryptionType = settings.encryptionType,
                IpAddress = settings.ipAddress,
                Netmask = settings.netmask,
                Gateway = settings.gateway
            } : null;
        }

        private Ihc.Soap.Configuration.WSWLanSettings unmapWLanSettings(WLanSettings settings)
        {
            return new Ihc.Soap.Configuration.WSWLanSettings()
            {
                enabled = settings.Enabled,
                ssid = settings.Ssid,
                key = settings.Key,
                securityType = settings.SecurityType,
                encryptionType = settings.EncryptionType,
                ipAddress = settings.IpAddress,
                netmask = settings.Netmask,
                gateway = settings.Gateway
            };
        }

        private WLanInterface mapWLanInterface(Ihc.Soap.Configuration.WSWLanInterface iface)
        {
            return iface != null ? new WLanInterface()
            {
                Connected = iface.connected,
                Name = iface.name,
                Ssid = iface.ssid,
                Quality = iface.quality
            } : null;
        }

        private WLanCell mapWLanCell(Ihc.Soap.Configuration.WSWLanCell cell)
        {
            return cell != null ? new WLanCell()
            {
                Ssid = cell.ssid,
                HasEncryption = cell.hasEncryption,
                SecurityType = cell.securityType,
                EncryptionType = cell.encryptionType
            } : null;
        }

        private SMTPSettings mapSMTPSettings(Ihc.Soap.Configuration.WSSMTPSettings settings)
        {
            return settings != null ? new SMTPSettings()
            {
                Hostname = settings.hostname,
                Hostport = settings.hostport,
                Username = settings.username,
                Password = settings.password,
                Ssl = settings.ssl,
                SendLowBatteryNotification = settings.sendLowBatteryNotification,
                SendLowBatteryNotificationRecipient = settings.sendLowBatteryNotificationRecepient
            } : null;
        }

        private Ihc.Soap.Configuration.WSSMTPSettings unmapSMTPSettings(SMTPSettings settings)
        {
            return new Ihc.Soap.Configuration.WSSMTPSettings()
            {
                hostname = settings.Hostname,
                hostport = settings.Hostport,
                username = settings.Username,
                password = settings.Password,
                ssl = settings.Ssl,
                sendLowBatteryNotification = settings.SendLowBatteryNotification,
                sendLowBatteryNotificationRecepient = settings.SendLowBatteryNotificationRecipient
            };
        }

        private EmailControlSettings mapEmailControlSettings(Ihc.Soap.Configuration.WSEmailControlSettings settings)
        {
            return settings != null ? new EmailControlSettings()
            {
                ServerIpAddress = settings.serverIPAddress,
                ServerPortNumber = settings.serverPortNumber,
                Pop3Username = settings.pop3Username,
                Pop3Password = settings.pop3Password,
                EmailAddress = settings.emailAddress,
                PollInterval = settings.pollInterval,
                RemoveEmailsAfterUsage = settings.removeEmailsAfterUsage,
                Ssl = settings.ssl
            } : null;
        }

        private Ihc.Soap.Configuration.WSEmailControlSettings unmapEmailControlSettings(EmailControlSettings settings)
        {
            return new Ihc.Soap.Configuration.WSEmailControlSettings()
            {
                serverIPAddress = settings.ServerIpAddress,
                serverPortNumber = settings.ServerPortNumber,
                pop3Username = settings.Pop3Username,
                pop3Password = settings.Pop3Password,
                emailAddress = settings.EmailAddress,
                pollInterval = settings.PollInterval,
                removeEmailsAfterUsage = settings.RemoveEmailsAfterUsage,
                ssl = settings.Ssl
            };
        }

        private WebAccessControl mapWebAccessControl(Ihc.Soap.Configuration.WSAccessControl ac)
        {
            return ac != null ? new WebAccessControl()
            {
                UsbLoginRequired = ac.m_usbLoginRequired_usb,
                AdministratorUsb = ac.m_administrator_usb,
                AdministratorInternal = ac.m_administrator_internal,
                AdministratorExternal = ac.m_administrator_external,
                TreeviewUsb = ac.m_treeview_usb,
                TreeviewInternal = ac.m_treeview_internal,
                TreeviewExternal = ac.m_treeview_external,
                SceneviewUsb = ac.m_sceneview_usb,
                SceneviewInternal = ac.m_sceneview_internal,
                SceneviewExternal = ac.m_sceneview_external,
                ScenedesignUsb = ac.m_scenedesign_usb,
                ScenedesignInternal = ac.m_scenedesign_internal,
                ScenedesignExternal = ac.m_scenedesign_external,
                ServerstatusUsb = ac.m_serverstatus_usb,
                ServerstatusInternal = ac.m_serverstatus_internal,
                ServerstatusExternal = ac.m_serverstatus_external,
                IhcvisualUsb = ac.m_ihcvisual_usb,
                IhcvisualInternal = ac.m_ihcvisual_internal,
                IhcvisualExternal = ac.m_ihcvisual_external,
                OnlinedocumentationUsb = ac.m_onlinedocumentation_usb,
                OnlinedocumentationInternal = ac.m_onlinedocumentation_internal,
                OnlinedocumentationExternal = ac.m_onlinedocumentation_external,
                WebsceneviewUsb = ac.m_websceneview_usb,
                WebsceneviewInternal = ac.m_websceneview_internal,
                WebsceneviewExternal = ac.m_websceneview_external,
                OpenapiUsb = ac.m_openapi_usb,
                OpenapiInternal = ac.m_openapi_internal,
                OpenapiExternal = ac.m_openapi_external,
                OpenapiUsed = ac.m_openapi_used
            } : null;
        }

        private Ihc.Soap.Configuration.WSAccessControl unmapWebAccessControl(WebAccessControl ac)
        {
            return new Ihc.Soap.Configuration.WSAccessControl()
            {
                m_usbLoginRequired_usb = ac.UsbLoginRequired,
                m_administrator_usb = ac.AdministratorUsb,
                m_administrator_internal = ac.AdministratorInternal,
                m_administrator_external = ac.AdministratorExternal,
                m_treeview_usb = ac.TreeviewUsb,
                m_treeview_internal = ac.TreeviewInternal,
                m_treeview_external = ac.TreeviewExternal,
                m_sceneview_usb = ac.SceneviewUsb,
                m_sceneview_internal = ac.SceneviewInternal,
                m_sceneview_external = ac.SceneviewExternal,
                m_scenedesign_usb = ac.ScenedesignUsb,
                m_scenedesign_internal = ac.ScenedesignInternal,
                m_scenedesign_external = ac.ScenedesignExternal,
                m_serverstatus_usb = ac.ServerstatusUsb,
                m_serverstatus_internal = ac.ServerstatusInternal,
                m_serverstatus_external = ac.ServerstatusExternal,
                m_ihcvisual_usb = ac.IhcvisualUsb,
                m_ihcvisual_internal = ac.IhcvisualInternal,
                m_ihcvisual_external = ac.IhcvisualExternal,
                m_onlinedocumentation_usb = ac.OnlinedocumentationUsb,
                m_onlinedocumentation_internal = ac.OnlinedocumentationInternal,
                m_onlinedocumentation_external = ac.OnlinedocumentationExternal,
                m_websceneview_usb = ac.WebsceneviewUsb,
                m_websceneview_internal = ac.WebsceneviewInternal,
                m_websceneview_external = ac.WebsceneviewExternal,
                m_openapi_usb = ac.OpenapiUsb,
                m_openapi_internal = ac.OpenapiInternal,
                m_openapi_external = ac.OpenapiExternal,
                m_openapi_used = ac.OpenapiUsed
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

        public async Task<string[]> GetUserLog(string lang) {
            var resp = await impl.getUserLogAsync(new inputMessageName2("", 0, lang));
            return mapLogFile(resp.getUserLog4);
        }

        public async Task DelayedReboot(int delayUnknownUnit) {
            await impl.delayedRebootAsync(new inputMessageName1(delayUnknownUnit) {});
        }

        // Network operations

        public async Task<NetworkSettings> GetNetworkSettings() {
            var resp = await impl.getNetworkSettingsAsync(new inputMessageName16());
            return mapNetworkSettings(resp.getNetworkSettings1);
        }

        public async Task SetNetworkSettings(NetworkSettings settings) {
            await impl.setNetworkSettingsAsync(new inputMessageName17(unmapNetworkSettings(settings)));
        }

        public async Task<string[]> GetDNSServers() {
            var resp = await impl.getDNSServersAsync(new inputMessageName7());
            // WSInetAddress has ipAddress as int - need to convert to string
            // For now, return null if not available, or implement IP int to string conversion if needed
            return resp.getDNSServers1?.Select(addr => addr.ipAddress.ToString()).ToArray();
        }

        public async Task SetDNSServers(string[] dnsServers) {
            // Note: DNS servers API takes only 2 servers (primary and secondary)
            var dns1 = dnsServers != null && dnsServers.Length > 0 ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = int.Parse(dnsServers[0]) } : null;
            var dns2 = dnsServers != null && dnsServers.Length > 1 ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = int.Parse(dnsServers[1]) } : null;
            await impl.setDNSServersAsync(new inputMessageName8(dns1, dns2));
        }

        // WiFi operations

        public async Task<WLanSettings> GetWLanSettings() {
            var resp = await impl.getWLanSettingsAsync(new inputMessageName14());
            return mapWLanSettings(resp.getWLanSettings1);
        }

        public async Task SetWLanSettings(WLanSettings settings) {
            await impl.setWLanSettingsAsync(new inputMessageName15(unmapWLanSettings(settings)));
        }

        public async Task<WLanInterface> GetWLanInterface() {
            var resp = await impl.getWLanInterfaceAsync(new inputMessageName12());
            return mapWLanInterface(resp.getWLanInterface1);
        }

        public async Task<WLanCell[]> GetWLanScan() {
            var resp = await impl.getWLanScanAsync(new inputMessageName13());
            return resp.getWLanScan1?.Select(cell => mapWLanCell(cell)).ToArray();
        }

        // SMTP operations

        public async Task<SMTPSettings> GetSMTPSettings() {
            var resp = await impl.getSMTPSettingsAsync(new inputMessageName5());
            return mapSMTPSettings(resp.getSMTPSettings1);
        }

        public async Task SetSMTPSettings(SMTPSettings settings) {
            await impl.setSMTPSettingsAsync(new inputMessageName4(unmapSMTPSettings(settings)));
        }

        public async Task TestSettingsNow() {
            await impl.testSettingsNowAsync(new inputMessageName9(null));
        }

        public async Task<bool> TestSendMessage(string recipient, string subject, string message) {
            // Using testSendMessage1Async which takes recipient, subject, and message as strings
            var notificationMessage = new Ihc.Soap.Configuration.WSNotificationMessage() {
                recipient = recipient
            };
            var resp = await impl.testSendMessage1Async(new inputMessageName11(notificationMessage, subject, message, null));
            return resp.testSendMessage12?.attemptSucceeded ?? false;
        }

        // Email Control operations

        public async Task<bool> GetEmailControlEnabled() {
            var resp = await impl.getEmailControlEnabledAsync(new inputMessageName21());
            return resp.getEmailControlEnabled1 ?? false;
        }

        public async Task SetEmailControlEnabled(bool enabled) {
            await impl.setEmailControlEnabledAsync(new inputMessageName20(enabled));
        }

        public async Task<EmailControlSettings> GetEmailControlSettings() {
            var resp = await impl.getEmailControlSettingsAsync(new inputMessageName22());
            return mapEmailControlSettings(resp.getEmailControlSettings1);
        }

        public async Task SetEmailControlSettings(EmailControlSettings settings) {
            await impl.setEmailControlSettingsAsync(new inputMessageName23(unmapEmailControlSettings(settings)));
        }

        // Web Access Control operations

        public async Task<WebAccessControl> GetWebAccessControl() {
            var resp = await impl.getWebAccessControlAsync(new inputMessageName18());
            return mapWebAccessControl(resp.getWebAccessControl1);
        }

        public async Task SetWebAccessControl(WebAccessControl accessControl) {
            await impl.setWebAccessControlAsync(new inputMessageName19(unmapWebAccessControl(accessControl)));
        }

        // Language operations

        public async Task SetServerLanguage(string language) {
            await impl.setServerLanguageAsync(new inputMessageName24(language));
        }
    }
}