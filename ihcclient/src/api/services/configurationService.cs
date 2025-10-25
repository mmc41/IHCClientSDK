using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Configuration;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC ConfigurationService without any of the soap distractions.
    /// </summary>
    public interface IConfigurationService : IIHCApiService
    {
        /// <summary>
        /// Get system information including uptime, version, serial number, and hardware details.
        /// </summary>
        public Task<SystemInfo> GetSystemInfo();

        /// <summary>
        /// Clear the user log on the controller.
        /// </summary>
        public Task ClearUserLog();

        /// <summary>
        /// Get user log entries from the controller.
        /// </summary>
        /// <param name="lang">Language code (default: "da" for Danish)</param>
        public Task<string[]> GetUserLog(string lang = "da");

        /// <summary>
        /// Reboot the IHC controller after a delay.
        /// </summary>
        /// <param name="delayUnknownUnit">Delay before reboot (time unit unknown - requires testing)</param>
        public Task DelayedReboot(int delayUnknownUnit);

        /// <summary>
        /// Get network settings including IP address, netmask, gateway, and ports.
        /// </summary>
        public Task<NetworkSettings> GetNetworkSettings();

        /// <summary>
        /// Set network settings for the controller.
        /// </summary>
        /// <param name="settings">Network settings to configure</param>
        public Task SetNetworkSettings(NetworkSettings settings);

        /// <summary>
        /// Get DNS server addresses configured on the controller.
        /// </summary>
        public Task<DNSServers> GetDNSServers();

        /// <summary>
        /// Set DNS server addresses (primary and secondary).
        /// </summary>
        /// <param name="dnsServers">DNS server configuration</param>
        public Task SetDNSServers(DNSServers dnsServers);

        /// <summary>
        /// Get wireless LAN settings.
        /// </summary>
        public Task<WLanSettings> GetWLanSettings();

        /// <summary>
        /// Set wireless LAN settings including SSID, security, and network configuration.
        /// </summary>
        /// <param name="settings">Wireless LAN settings to configure</param>
        public Task SetWLanSettings(WLanSettings settings);

        /// <summary>
        /// Get wireless interface information including connection status and signal quality.
        /// </summary>
        public Task<WLanInterface> GetWLanInterface();

        /// <summary>
        /// Scan for available wireless networks.
        /// </summary>
        public Task<WLanCell[]> GetWLanScan();

        /// <summary>
        /// Get SMTP server settings for email notifications.
        /// </summary>
        public Task<SMTPSettings> GetSMTPSettings();

        /// <summary>
        /// Set SMTP server settings for email notifications.
        /// </summary>
        /// <param name="settings">SMTP settings to configure</param>
        public Task SetSMTPSettings(SMTPSettings settings);

        /// <summary>
        /// Test SMTP settings by attempting to connect to the mail server.
        /// </summary>
        public Task TestSettingsNow();

        /// <summary>
        /// Send a test email message to verify SMTP configuration.
        /// </summary>
        /// <param name="recipient">Email recipient address</param>
        /// <param name="subject">Email subject</param>
        /// <param name="message">Email message body</param>
        public Task<bool> TestSendMessage(string recipient, string subject, string message);

        /// <summary>
        /// Check if email control feature is enabled.
        /// </summary>
        public Task<bool> GetEmailControlEnabled();

        /// <summary>
        /// Enable or disable the email control feature.
        /// </summary>
        /// <param name="enabled">True to enable, false to disable</param>
        public Task SetEmailControlEnabled(bool enabled);

        /// <summary>
        /// Get email control settings for remote controller access via email.
        /// </summary>
        public Task<EmailControlSettings> GetEmailControlSettings();

        /// <summary>
        /// Set email control settings for remote controller access via email.
        /// </summary>
        /// <param name="settings">Email control settings to configure</param>
        public Task SetEmailControlSettings(EmailControlSettings settings);

        /// <summary>
        /// Get web access control settings for different applications and access types.
        /// </summary>
        public Task<WebAccessControl> GetWebAccessControl();

        /// <summary>
        /// Set web access control settings for different applications and access types.
        /// </summary>
        /// <param name="accessControl">Web access control settings to configure</param>
        public Task SetWebAccessControl(WebAccessControl accessControl);

        /// <summary>
        /// Set the server language for the controller interface.
        /// </summary>
        /// <param name="language">Language code to set</param>
        public Task SetServerLanguage(string language);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ConfigurationService without exposing any of the soap distractions.
    /// </summary>
    public class ConfigurationService : ServiceBase, IConfigurationService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Configuration.ConfigurationService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "ConfigurationService") { }

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

        /// <summary>
        /// Create an ConfigurationService instance for access to the IHC API related to configuration.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ConfigurationService(IAuthenticationService authService)
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
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
                SWDate = new DateTimeOffset(DateTime.SpecifyKind(info.swDate, DateHelper.GetWSDateTimeKind())),
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
            using (var activity = StartActivity(nameof(GetSystemInfo)))
            {
                try
                {
                    var resp = await impl.getSystemInfoAsync(new inputMessageName6() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSystemInfo(resp.getSystemInfo1);

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

        public async Task ClearUserLog() {
            using (var activity = StartActivity(nameof(ClearUserLog)))
            {
                try
                {
                    await impl.clearUserLogAsync(new inputMessageName3()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<string[]> GetUserLog(string lang) {
            using (var activity = StartActivity(nameof(GetUserLog)))
            {
                try
                {
                    activity?.SetParameters((nameof(lang), lang));

                    var resp = await impl.getUserLogAsync(new inputMessageName2("", 0, lang)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapLogFile(resp.getUserLog4);

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

        public async Task DelayedReboot(int delayUnknownUnit) {
            using (var activity = StartActivity(nameof(DelayedReboot)))
            {
                try
                {
                    activity?.SetParameters((nameof(delayUnknownUnit), delayUnknownUnit));

                    await impl.delayedRebootAsync(new inputMessageName1(delayUnknownUnit) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        // Network operations

        public async Task<NetworkSettings> GetNetworkSettings() {
            using (var activity = StartActivity(nameof(GetNetworkSettings)))
            {
                try
                {
                    var resp = await impl.getNetworkSettingsAsync(new inputMessageName16()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapNetworkSettings(resp.getNetworkSettings1);

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

        public async Task SetNetworkSettings(NetworkSettings settings) {
            using (var activity = StartActivity(nameof(SetNetworkSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    await impl.setNetworkSettingsAsync(new inputMessageName17(unmapNetworkSettings(settings))).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<DNSServers> GetDNSServers() {
            using (var activity = StartActivity(nameof(GetDNSServers)))
            {
                try
                {
                    var resp = await impl.getDNSServersAsync(new inputMessageName7()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapDNSServers(resp.getDNSServers1);

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

        public async Task SetDNSServers(DNSServers dnsServers) {
            using (var activity = StartActivity(nameof(SetDNSServers)))
            {
                try
                {
                    activity?.SetParameters((nameof(dnsServers), dnsServers));

                    var (dns1, dns2) = unmapDNSServers(dnsServers);
                    await impl.setDNSServersAsync(new inputMessageName8(dns1, dns2)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        // WiFi operations

        public async Task<WLanSettings> GetWLanSettings() {
            using (var activity = StartActivity(nameof(GetWLanSettings)))
            {
                try
                {
                    var resp = await impl.getWLanSettingsAsync(new inputMessageName14()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapWLanSettings(resp.getWLanSettings1);

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

        public async Task SetWLanSettings(WLanSettings settings) {
            using (var activity = StartActivity(nameof(SetWLanSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    await impl.setWLanSettingsAsync(new inputMessageName15(unmapWLanSettings(settings))).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<WLanInterface> GetWLanInterface() {
            using (var activity = StartActivity(nameof(GetWLanInterface)))
            {
                try
                {
                    var resp = await impl.getWLanInterfaceAsync(new inputMessageName12()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapWLanInterface(resp.getWLanInterface1);

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

        public async Task<WLanCell[]> GetWLanScan() {
            using (var activity = StartActivity(nameof(GetWLanScan)))
            {
                try
                {
                    var resp = await impl.getWLanScanAsync(new inputMessageName13()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getWLanScan1?.Select(cell => mapWLanCell(cell)).ToArray();

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

        // SMTP operations

        public async Task<SMTPSettings> GetSMTPSettings() {
            using (var activity = StartActivity(nameof(GetSMTPSettings)))
            {
                try
                {
                    var resp = await impl.getSMTPSettingsAsync(new inputMessageName5()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapSMTPSettings(resp.getSMTPSettings1);

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

        public async Task SetSMTPSettings(SMTPSettings settings) {
            using (var activity = StartActivity(nameof(SetSMTPSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    await impl.setSMTPSettingsAsync(new inputMessageName4(unmapSMTPSettings(settings))).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task TestSettingsNow() {
            using (var activity = StartActivity(nameof(TestSettingsNow)))
            {
                try
                {
                    await impl.testSettingsNowAsync(new inputMessageName9(null)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> TestSendMessage(string recipient, string subject, string message) {
            using (var activity = StartActivity(nameof(TestSendMessage)))
            {
                try
                {
                    activity?.SetParameters((nameof(recipient), recipient), (nameof(subject), subject), (nameof(message), message));

                    // Using testSendMessage1Async which takes recipient, subject, and message as strings
                    var notificationMessage = new Ihc.Soap.Configuration.WSNotificationMessage() {
                        recipient = recipient
                    };
                    var resp = await impl.testSendMessage1Async(new inputMessageName11(notificationMessage, subject, message, null)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.testSendMessage12 != null && resp.testSendMessage12.attemptSucceeded;

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

        // Email Control operations

        public async Task<bool> GetEmailControlEnabled() {
            using (var activity = StartActivity(nameof(GetEmailControlEnabled)))
            {
                try
                {
                    var resp = await impl.getEmailControlEnabledAsync(new inputMessageName21()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = resp.getEmailControlEnabled1.HasValue ? resp.getEmailControlEnabled1.Value : false;

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

        public async Task SetEmailControlEnabled(bool enabled) {
            using (var activity = StartActivity(nameof(SetEmailControlEnabled)))
            {
                try
                {
                    activity?.SetParameters((nameof(enabled), enabled));

                    await impl.setEmailControlEnabledAsync(new inputMessageName20(enabled)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<EmailControlSettings> GetEmailControlSettings() {
            using (var activity = StartActivity(nameof(GetEmailControlSettings)))
            {
                try
                {
                    var resp = await impl.getEmailControlSettingsAsync(new inputMessageName22()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapEmailControlSettings(resp.getEmailControlSettings1);

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

        public async Task SetEmailControlSettings(EmailControlSettings settings) {
            using (var activity = StartActivity(nameof(SetEmailControlSettings)))
            {
                try
                {
                    activity?.SetParameters((nameof(settings), settings));

                    await impl.setEmailControlSettingsAsync(new inputMessageName23(unmapEmailControlSettings(settings))).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        // Web Access Control operations

        public async Task<WebAccessControl> GetWebAccessControl() {
            using (var activity = StartActivity(nameof(GetWebAccessControl)))
            {
                try
                {
                    var resp = await impl.getWebAccessControlAsync(new inputMessageName18()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapWebAccessControl(resp.getWebAccessControl1);

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

        public async Task SetWebAccessControl(WebAccessControl accessControl) {
            using (var activity = StartActivity(nameof(SetWebAccessControl)))
            {
                try
                {
                    activity?.SetParameters((nameof(accessControl), accessControl));

                    await impl.setWebAccessControlAsync(new inputMessageName19(unmapWebAccessControl(accessControl))).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        // Language operations

        public async Task SetServerLanguage(string language) {
            using (var activity = StartActivity(nameof(SetServerLanguage)))
            {
                try
                {
                    activity?.SetParameters((nameof(language), language));

                    await impl.setServerLanguageAsync(new inputMessageName24(language)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
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