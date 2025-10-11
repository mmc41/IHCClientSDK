using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Configuration;
using System.Diagnostics;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ConfigurationService without any of the soap distractions.
    */
    public interface IConfigurationService : IIHCService
    {
        /**
        * Get system information including uptime, version, serial number, and hardware details.
        */
        public Task<SystemInfo> GetSystemInfo();

        /**
        * Clear the user log on the controller.
        */
        public Task ClearUserLog();

        /**
        * Get user log entries from the controller.
        * @param lang Language code (default: "da" for Danish)
        */
        public Task<string[]> GetUserLog(string lang = "da");

        /**
        * Reboot the IHC controller after a delay.
        * @param delayUnknownUnit Delay before reboot (time unit unknown - requires testing)
        */
        public Task DelayedReboot(int delayUnknownUnit);

        /**
        * Get network settings including IP address, netmask, gateway, and ports.
        */
        public Task<NetworkSettings> GetNetworkSettings();

        /**
        * Set network settings for the controller.
        */
        public Task SetNetworkSettings(NetworkSettings settings);

        /**
        * Get DNS server addresses configured on the controller.
        */
        public Task<DNSServers> GetDNSServers();

        /**
        * Set DNS server addresses (primary and secondary).
        */
        public Task SetDNSServers(DNSServers dnsServers);

        /**
        * Get wireless LAN settings.
        */
        public Task<WLanSettings> GetWLanSettings();

        /**
        * Set wireless LAN settings including SSID, security, and network configuration.
        */
        public Task SetWLanSettings(WLanSettings settings);

        /**
        * Get wireless interface information including connection status and signal quality.
        */
        public Task<WLanInterface> GetWLanInterface();

        /**
        * Scan for available wireless networks.
        */
        public Task<WLanCell[]> GetWLanScan();

        /**
        * Get SMTP server settings for email notifications.
        */
        public Task<SMTPSettings> GetSMTPSettings();

        /**
        * Set SMTP server settings for email notifications.
        */
        public Task SetSMTPSettings(SMTPSettings settings);

        /**
        * Test SMTP settings by attempting to connect to the mail server.
        */
        public Task TestSettingsNow();

        /**
        * Send a test email message to verify SMTP configuration.
        */
        public Task<bool> TestSendMessage(string recipient, string subject, string message);

        /**
        * Check if email control feature is enabled.
        */
        public Task<bool> GetEmailControlEnabled();

        /**
        * Enable or disable the email control feature.
        */
        public Task SetEmailControlEnabled(bool enabled);

        /**
        * Get email control settings for remote controller access via email.
        */
        public Task<EmailControlSettings> GetEmailControlSettings();

        /**
        * Set email control settings for remote controller access via email.
        */
        public Task SetEmailControlSettings(EmailControlSettings settings);

        /**
        * Get web access control settings for different applications and access types.
        */
        public Task<WebAccessControl> GetWebAccessControl();

        /**
        * Set web access control settings for different applications and access types.
        */
        public Task SetWebAccessControl(WebAccessControl accessControl);

        /**
        * Set the server language for the controller interface.
        */
        public Task SetServerLanguage(string language);
    }

    /**
    * A highlevel implementation of a client to the IHC ConfigurationService without exposing any of the soap distractions.
    */
    public class ConfigurationService : ServiceBase, IConfigurationService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Configuration.ConfigurationService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "ConfigurationService", logSensitiveData, asyncContinueOnCapturedContext) { }

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
        * <param name="logSensitiveData">If true, log sensitive data. If false (default), redact sensitive values in logs.</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public ConfigurationService(IAuthenticationService authService, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, logSensitiveData, asyncContinueOnCapturedContext);
        }

        private SystemInfo mapSystemInfo(Ihc.Soap.Configuration.WSSystemInfo info)
        {
            // Return empty SystemInfo if input is null
            if (info == null)
            {
                return new SystemInfo();
            }

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
            if (e == null)
                return Array.Empty<string>();

            string logs = e.data != null ? System.Text.Encoding.UTF8.GetString(e.data) : "";
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
            if (settings == null)
                return null;

            return new Ihc.Soap.Configuration.WSNetworkSettings()
            {
                ipAddress = settings.IpAddress,
                netmask = settings.Netmask,
                gateway = settings.Gateway,
                httpPort = settings.HttpPort,
                httpsPort = settings.HttpsPort
            };
        }

        private DNSServers mapDNSServers(Ihc.Soap.Configuration.WSInetAddress[] dnsAddresses)
        {
            // Assume max length is 2 (primary and secondary DNS)
            return new DNSServers()
            {
                PrimaryDNS = dnsAddresses != null && dnsAddresses.Length > 0 ? NetworkHelper.ConvertIntToIPAddress(dnsAddresses[0].ipAddress) : null,
                SecondaryDNS = dnsAddresses != null && dnsAddresses.Length > 1 ? NetworkHelper.ConvertIntToIPAddress(dnsAddresses[1].ipAddress) : null
            };
        }

        private (Ihc.Soap.Configuration.WSInetAddress, Ihc.Soap.Configuration.WSInetAddress) unmapDNSServers(DNSServers dnsServers)
        {
            var dns1 = !string.IsNullOrEmpty(dnsServers?.PrimaryDNS)
                ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = NetworkHelper.ConvertIPAddressToInt(dnsServers.PrimaryDNS) }
                : null;
            var dns2 = !string.IsNullOrEmpty(dnsServers?.SecondaryDNS)
                ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = NetworkHelper.ConvertIPAddressToInt(dnsServers.SecondaryDNS) }
                : null;
            return (dns1, dns2);
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
            if (settings == null)
                return null;

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
            if (settings == null)
                return null;

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
            if (settings == null)
                return null;

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
            if (ac == null)
                return null;

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
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getSystemInfoAsync(new inputMessageName6() { }).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSystemInfo(resp.getSystemInfo1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task ClearUserLog() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            await impl.clearUserLogAsync(new inputMessageName3()).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<string[]> GetUserLog(string lang) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("lang", lang));

            var resp = await impl.getUserLogAsync(new inputMessageName2("", 0, lang)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapLogFile(resp.getUserLog4);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task DelayedReboot(int delayUnknownUnit) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("delayUnknownUnit", delayUnknownUnit));

            await impl.delayedRebootAsync(new inputMessageName1(delayUnknownUnit) {}).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        // Network operations

        public async Task<NetworkSettings> GetNetworkSettings() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getNetworkSettingsAsync(new inputMessageName16()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapNetworkSettings(resp.getNetworkSettings1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetNetworkSettings(NetworkSettings settings) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("settings", settings));

            await impl.setNetworkSettingsAsync(new inputMessageName17(unmapNetworkSettings(settings))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<DNSServers> GetDNSServers() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getDNSServersAsync(new inputMessageName7()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapDNSServers(resp.getDNSServers1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetDNSServers(DNSServers dnsServers) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("dnsServers", dnsServers));

            var (dns1, dns2) = unmapDNSServers(dnsServers);
            await impl.setDNSServersAsync(new inputMessageName8(dns1, dns2)).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        // WiFi operations

        public async Task<WLanSettings> GetWLanSettings() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getWLanSettingsAsync(new inputMessageName14()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapWLanSettings(resp.getWLanSettings1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetWLanSettings(WLanSettings settings) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("settings", settings));

            await impl.setWLanSettingsAsync(new inputMessageName15(unmapWLanSettings(settings))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<WLanInterface> GetWLanInterface() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getWLanInterfaceAsync(new inputMessageName12()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapWLanInterface(resp.getWLanInterface1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<WLanCell[]> GetWLanScan() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getWLanScanAsync(new inputMessageName13()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getWLanScan1?.Select(cell => mapWLanCell(cell)).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        // SMTP operations

        public async Task<SMTPSettings> GetSMTPSettings() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getSMTPSettingsAsync(new inputMessageName5()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapSMTPSettings(resp.getSMTPSettings1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetSMTPSettings(SMTPSettings settings) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("settings", settings));

            await impl.setSMTPSettingsAsync(new inputMessageName4(unmapSMTPSettings(settings))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task TestSettingsNow() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            await impl.testSettingsNowAsync(new inputMessageName9(null)).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<bool> TestSendMessage(string recipient, string subject, string message) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("recipient", recipient), ("subject", subject), ("message", message));

            // Using testSendMessage1Async which takes recipient, subject, and message as strings
            var notificationMessage = new Ihc.Soap.Configuration.WSNotificationMessage() {
                recipient = recipient
            };
            var resp = await impl.testSendMessage1Async(new inputMessageName11(notificationMessage, subject, message, null)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.testSendMessage12 != null && resp.testSendMessage12.attemptSucceeded;

            activity?.SetReturnValue(retv);
            return retv;
        }

        // Email Control operations

        public async Task<bool> GetEmailControlEnabled() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getEmailControlEnabledAsync(new inputMessageName21()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = resp.getEmailControlEnabled1.HasValue ? resp.getEmailControlEnabled1.Value : false;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetEmailControlEnabled(bool enabled) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("enabled", enabled));

            await impl.setEmailControlEnabledAsync(new inputMessageName20(enabled)).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        public async Task<EmailControlSettings> GetEmailControlSettings() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getEmailControlSettingsAsync(new inputMessageName22()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapEmailControlSettings(resp.getEmailControlSettings1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetEmailControlSettings(EmailControlSettings settings) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("settings", settings));

            await impl.setEmailControlSettingsAsync(new inputMessageName23(unmapEmailControlSettings(settings))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        // Web Access Control operations

        public async Task<WebAccessControl> GetWebAccessControl() {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var resp = await impl.getWebAccessControlAsync(new inputMessageName18()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapWebAccessControl(resp.getWebAccessControl1);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task SetWebAccessControl(WebAccessControl accessControl) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("accessControl", accessControl));

            await impl.setWebAccessControlAsync(new inputMessageName19(unmapWebAccessControl(accessControl))).ConfigureAwait(asyncContinueOnCapturedContext);
        }

        // Language operations

        public async Task SetServerLanguage(string language) {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("language", language));

            await impl.setServerLanguageAsync(new inputMessageName24(language)).ConfigureAwait(asyncContinueOnCapturedContext);
        }
    }
}