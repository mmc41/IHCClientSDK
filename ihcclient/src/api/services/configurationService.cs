using System.Threading.Tasks;
using System;
using Ihc.Soap.Configuration;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ihc {
    /// <summary>
    /// Controller-wide settings and housekeeping: system information, wired network, DNS, WiFi, access control,
    /// outgoing mail (SMTP), e-mail control (POP3), the user log, the server language and restart.
    /// </summary>
    public interface IConfigurationService : IIHCApiService
    {
        /// <summary>
        /// Reads the controller's identity and status: uptime, clock, serial number, production date, and firmware,
        /// hardware and module versions.
        /// </summary>
        public Task<SystemInfo> GetSystemInfo();

        /// <summary>
        /// Deletes the user log stored on the controller (see <see cref="GetUserLog"/>).
        /// </summary>
        public Task ClearUserLog();

        /// <summary>
        /// Reads the controller's user log: its own record of events, warnings and faults, not of user input. Each call
        /// sends an empty cursor, so it returns stored lines rather than only those added since an earlier read.
        /// </summary>
        /// <param name="lang">Two-letter language code, such as <c>da</c> or <c>en</c>; probably selects the language
        /// of the log text.</param>
        /// <returns>
        /// One entry per line, newest stored first, which is not time-stamp order once the controller's clock has
        /// gone back. Each is a <c>yyyy.MM.dd HH:mm:ss</c> time stamp by the controller's clock, a tab, the severity
        /// (such as <c>Fejl</c>), a tab and the message. The text ends in a line feed, so the last entry is empty.
        /// </returns>
        public Task<IReadOnlyList<string>> GetUserLog(string lang = "da");

        /// <summary>
        /// Restarts the controller after a delay. The controller replies first and then restarts, which ends the
        /// session, so log in again once it is back.
        /// </summary>
        /// <param name="delay">Delay before the restart, in milliseconds.</param>
        public Task DelayedReboot(int delay);

        /// <summary>
        /// Reads the wired-LAN settings: IPv4 address, netmask and gateway as dotted strings, and the web (HTTP) and
        /// secure (HTTPS) ports. There is no DHCP or host-name setting.
        /// </summary>
        /// <returns>The settings, or <c>null</c> when the reply carries none.</returns>
        public Task<NetworkSettings?> GetNetworkSettings();

        /// <summary>
        /// Writes all five wired-LAN values as one record, not a delta: read them with
        /// <see cref="GetNetworkSettings"/>, change what is needed and send them all back.
        /// </summary>
        /// <param name="settings">The complete wired-LAN settings to store.</param>
        /// <remarks>
        /// A save that changes the IP address makes the controller restart without replying, so expect the call to fail
        /// although the new values are kept.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c> or breaks a limit declared on
        /// <see cref="NetworkSettings"/>; nothing is sent.</exception>
        public Task SetNetworkSettings(NetworkSettings settings);

        /// <summary>
        /// Reads the primary and secondary DNS servers as dotted IPv4 strings; any further servers the controller lists
        /// are ignored.
        /// </summary>
        public Task<DNSServers> GetDNSServers();

        /// <summary>
        /// Writes the primary and secondary DNS servers; only IPv4 addresses can be sent.
        /// </summary>
        /// <param name="dnsServers">Both servers as dotted IPv4 strings; a <c>null</c> or empty one is sent as no
        /// server.</param>
        /// <exception cref="ArgumentException"><paramref name="dnsServers"/> is <c>null</c> or breaks a limit declared
        /// on <see cref="DNSServers"/>; nothing is sent.</exception>
        public Task SetDNSServers(DNSServers dnsServers);

        /// <summary>
        /// Reads the stored configuration of the controller's WiFi client interface, a USB dongle unrelated to IHC
        /// Wireless: on/off, network, key, security and encryption type, and its static IPv4 settings.
        /// </summary>
        /// <returns>The settings, or <c>null</c> when the reply carries none.</returns>
        public Task<WLanSettings?> GetWLanSettings();

        /// <summary>
        /// Writes the complete WiFi client configuration, not a delta: read it with <see cref="GetWLanSettings"/>,
        /// change it and send it back.
        /// </summary>
        /// <param name="settings">The complete WiFi client settings to store.</param>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c> or breaks a limit declared on
        /// <see cref="WLanSettings"/>; nothing is sent.</exception>
        public Task SetWLanSettings(WLanSettings settings);

        /// <summary>
        /// Reports whether the controller has a WiFi interface (a USB dongle, unrelated to IHC Wireless) and whether
        /// it is associated with a network; an empty interface name means no dongle.
        /// </summary>
        /// <returns>The interface status, or <c>null</c> when the reply carries none.</returns>
        public Task<WLanInterface?> GetWLanInterface();

        /// <summary>
        /// Lists the WiFi networks the controller's WiFi interface can see, with their security and encryption
        /// types; no signal level or channel is reported.
        /// </summary>
        public Task<IReadOnlyList<WLanCell>> GetWLanScan();

        /// <summary>
        /// Reads the SMTP account the controller sends all its mail through, with the low-battery notification
        /// switch and the notification recipient. The reply includes the stored password.
        /// </summary>
        /// <returns>The settings, or <c>null</c> when the reply carries none.</returns>
        public Task<SMTPSettings?> GetSMTPSettings();

        /// <summary>
        /// Writes the SMTP account and notification settings as one complete record, not a delta: read it with
        /// <see cref="GetSMTPSettings"/>, change it and send it back.
        /// </summary>
        /// <param name="settings">The complete SMTP settings to store.</param>
        /// <remarks>
        /// The record lacks the LED Dimmer error-mail switch that firmware 3.3.44 also holds, so this method never
        /// sends it; what a write does to that switch is not known.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c> or breaks a limit declared on
        /// <see cref="SMTPSettings"/>; nothing is sent.</exception>
        public Task SetSMTPSettings(SMTPSettings settings);

        /// <summary>
        /// Asks the controller to test the e-mail control (POP3 mailbox) settings now, without saving them; it does not
        /// test SMTP.
        /// </summary>
        /// <remarks>
        /// Known defect: this method sends no settings and discards the connection status the controller returns, so
        /// what it tests and whether the test passed are not known.
        /// </remarks>
        public Task TestSettingsNow();

        /// <summary>
        /// Asks the controller to send a test mail to <paramref name="recipient"/>. Unverified: no vendor client calls
        /// it, and the controller may not use <paramref name="subject"/> and <paramref name="message"/> as the mail's
        /// subject and body.
        /// </summary>
        /// <param name="recipient">Recipient address.</param>
        /// <param name="subject">Intended subject of the test mail.</param>
        /// <param name="message">Intended body of the test mail.</param>
        /// <returns><c>true</c> when the controller reports success; <c>false</c> otherwise, also when the reply
        /// carries no result. Failure reasons are not returned.</returns>
        public Task<bool> TestSendMessage(string recipient, string subject, string message);

        /// <summary>
        /// Reads the controller's e-mail control enabled flag. Unverified: no vendor client calls it.
        /// </summary>
        /// <returns>The flag; <c>false</c> also when the reply carries no value.</returns>
        public Task<bool> GetEmailControlEnabled();

        /// <summary>
        /// Sets the controller's e-mail control enabled flag. Unverified: no vendor client calls it.
        /// </summary>
        /// <param name="enabled"><c>true</c> to enable, <c>false</c> to disable.</param>
        public Task SetEmailControlEnabled(bool enabled);

        /// <summary>
        /// Reads the e-mail control settings: the POP3 mailbox the controller polls for command mails (whose
        /// triggers are set up in IHC SceneDesign), the poll interval and whether fetched mails are deleted. The reply
        /// includes the stored password.
        /// </summary>
        /// <returns>The settings, or <c>null</c> when the reply carries none.</returns>
        public Task<EmailControlSettings?> GetEmailControlSettings();

        /// <summary>
        /// Writes the e-mail control mailbox and poll settings. The controller answers with an acceptance flag, which
        /// this method does not return.
        /// </summary>
        /// <param name="settings">Mailbox, credentials and poll settings to store.</param>
        /// <exception cref="ArgumentException"><paramref name="settings"/> is <c>null</c> or breaks a limit declared on
        /// <see cref="EmailControlSettings"/>; nothing is sent.</exception>
        public Task SetEmailControlSettings(EmailControlSettings settings);

        /// <summary>
        /// Reads the access-control matrix: for each client application, whether it may log in over USB, from the
        /// controller's own IP subnet (internal) or from any other address (external).
        /// </summary>
        /// <returns>The matrix, or <c>null</c> when the reply carries none.</returns>
        public Task<WebAccessControl?> GetWebAccessControl();

        /// <summary>
        /// Writes the whole access-control matrix, not a delta: read it with <see cref="GetWebAccessControl"/>, change
        /// it and send it back. A client whose application is not allowed on its route is refused at login.
        /// </summary>
        /// <param name="accessControl">The complete matrix to store.</param>
        /// <exception cref="ArgumentException"><paramref name="accessControl"/> is <c>null</c> or breaks a limit
        /// declared on <see cref="WebAccessControl"/>, such as switching off IHC Administrator over USB; nothing is
        /// sent.</exception>
        public Task SetWebAccessControl(WebAccessControl accessControl);

        /// <summary>
        /// Sets the controller's language; there is no operation to read it back. The change applies after a
        /// controller restart and a new login; which texts it changes is not known.
        /// </summary>
        /// <param name="language">Lower-case ISO 639 language code, such as <c>da</c> or <c>en</c>.</param>
        public Task SetServerLanguage(string language);
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ConfigurationService without exposing any of the soap distractions.
    /// </summary>
    public class ConfigurationService : ServiceBase, IConfigurationService
    {
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

        private readonly Ihc.Soap.Configuration.ConfigurationService impl;

        /// <summary>
        /// Create an ConfigurationService instance for access to the IHC API related to configuration.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ConfigurationService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal ConfigurationService(IAuthenticationService authService, Ihc.Soap.Configuration.ConfigurationService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        private static SystemInfo mapSystemInfo(Ihc.Soap.Configuration.WSSystemInfo? info)
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
                ApplicationIsWithoutViewer = info.applicationIsWithoutViewer,
                SmsModemSoftwareVersion = info.smsModemSoftwareVersion,
                LedDimmerSoftwareVersion = info.ledDimmerSoftwareVersion
            };
        }

        private static string[] mapLogFile(Ihc.Soap.Configuration.WSFile? e)
        {
            if (e == null)
                return Array.Empty<string>();

            string logs = e.data != null ? System.Text.Encoding.UTF8.GetString(e.data) : "";
            // Separators are NORMALISED, not split on individually: splitting on both characters turns each
            // CRLF into a pair of separators and so into an empty line between every real one. Genuinely
            // blank lines in the log survive, because only the terminators are collapsed.
            return logs.ReplaceLineEndings("\n").Split('\n');
        }

        private static NetworkSettings? mapNetworkSettings(Ihc.Soap.Configuration.WSNetworkSettings? settings)
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

        private static Ihc.Soap.Configuration.WSNetworkSettings? unmapNetworkSettings(NetworkSettings? settings)
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

        private static DNSServers mapDNSServers(Ihc.Soap.Configuration.WSInetAddress[] dnsAddresses)
        {
            // Assume max length is 2 (primary and secondary DNS)
            return new DNSServers()
            {
                PrimaryDNS = dnsAddresses != null && dnsAddresses.Length > 0 ? NetworkHelper.ConvertIntToIPAddress(dnsAddresses[0].ipAddress) : null,
                SecondaryDNS = dnsAddresses != null && dnsAddresses.Length > 1 ? NetworkHelper.ConvertIntToIPAddress(dnsAddresses[1].ipAddress) : null
            };
        }

        private static (Ihc.Soap.Configuration.WSInetAddress?, Ihc.Soap.Configuration.WSInetAddress?) unmapDNSServers(DNSServers? dnsServers)
        {
            var dns1 = !string.IsNullOrEmpty(dnsServers?.PrimaryDNS)
                ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = NetworkHelper.ConvertIPAddressToInt(dnsServers.PrimaryDNS) }
                : null;
            var dns2 = !string.IsNullOrEmpty(dnsServers?.SecondaryDNS)
                ? new Ihc.Soap.Configuration.WSInetAddress() { ipAddress = NetworkHelper.ConvertIPAddressToInt(dnsServers.SecondaryDNS) }
                : null;
            return (dns1, dns2);
        }

        private static WLanSettings? mapWLanSettings(Ihc.Soap.Configuration.WSWLanSettings? settings)
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

        private static Ihc.Soap.Configuration.WSWLanSettings? unmapWLanSettings(WLanSettings? settings)
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

        private static WLanInterface? mapWLanInterface(Ihc.Soap.Configuration.WSWLanInterface? iface)
        {
            return iface != null ? new WLanInterface()
            {
                Connected = iface.connected,
                Name = iface.name,
                Ssid = iface.ssid,
                Quality = iface.quality
            } : null;
        }

        private WLanCell? mapWLanCell(Ihc.Soap.Configuration.WSWLanCell? cell)
        {
            return cell != null ? new WLanCell()
            {
                Ssid = cell.ssid,
                HasEncryption = cell.hasEncryption,
                SecurityType = cell.securityType,
                EncryptionType = cell.encryptionType
            } : null;
        }

        private static SMTPSettings? mapSMTPSettings(Ihc.Soap.Configuration.WSSMTPSettings? settings)
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

        private static Ihc.Soap.Configuration.WSSMTPSettings? unmapSMTPSettings(SMTPSettings? settings)
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

        private static EmailControlSettings? mapEmailControlSettings(Ihc.Soap.Configuration.WSEmailControlSettings? settings)
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

        private static Ihc.Soap.Configuration.WSEmailControlSettings? unmapEmailControlSettings(EmailControlSettings? settings)
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

        private static WebAccessControl? mapWebAccessControl(Ihc.Soap.Configuration.WSAccessControl? ac)
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

        private static Ihc.Soap.Configuration.WSAccessControl? unmapWebAccessControl(WebAccessControl? ac)
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

        public async Task<IReadOnlyList<string>> GetUserLog(string lang) {
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

        public async Task DelayedReboot(int delay) {
            using (var activity = StartActivity(nameof(DelayedReboot)))
            {
                try
                {
                    activity?.SetParameters((nameof(delay), delay));

                    await impl.delayedRebootAsync(new inputMessageName1(delay) {}).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        // Network operations

        public async Task<NetworkSettings?> GetNetworkSettings() {
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

                    ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

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

                    ValidationHelper.ValidateDataAnnotations(dnsServers, nameof(dnsServers));

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

        public async Task<WLanSettings?> GetWLanSettings() {
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

                    ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

                    await impl.setWLanSettingsAsync(new inputMessageName15(unmapWLanSettings(settings))).ConfigureAwait(this.settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<WLanInterface?> GetWLanInterface() {
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

        public async Task<IReadOnlyList<WLanCell>> GetWLanScan() {
            using (var activity = StartActivity(nameof(GetWLanScan)))
            {
                try
                {
                    var resp = await impl.getWLanScanAsync(new inputMessageName13()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<WLanCell> retv = resp.getWLanScan1 is { } cells
                        ? WireList.MapPresent(cells, mapWLanCell, activity, nameof(GetWLanScan))
                        : Array.Empty<WLanCell>();

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

        public async Task<SMTPSettings?> GetSMTPSettings() {
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

                    ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

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

        public async Task<EmailControlSettings?> GetEmailControlSettings() {
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

                    ValidationHelper.ValidateDataAnnotations(settings, nameof(settings));

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

        public async Task<WebAccessControl?> GetWebAccessControl() {
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

                    ValidationHelper.ValidateDataAnnotations(accessControl, nameof(accessControl));

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