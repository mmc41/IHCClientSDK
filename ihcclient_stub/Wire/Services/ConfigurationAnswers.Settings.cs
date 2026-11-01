using System.Buffers.Binary;
using System.Net;
using Ihc.Soap.Configuration;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The configuration service's get/set pairs: a get reads one typed property and a set replaces it whole. A set
    /// journals what it stored, as the SDK model.
    /// </summary>
    internal sealed partial class ConfigurationAnswers
    {
        internal Task<outputMessageName16> getNetworkSettingsAsync(inputMessageName16 request) =>
            Task.FromResult(new outputMessageName16 { getNetworkSettings1 = NetworkToWire(scenario.NetworkSettings) });

        internal Task<outputMessageName17> setNetworkSettingsAsync(inputMessageName17 request)
        {
            scenario.NetworkSettings = Stored(NetworkFromWire(request.setNetworkSettings1));
            return Task.FromResult(new outputMessageName17 { setNetworkSettings2 = true });
        }

        internal Task<outputMessageName7> getDNSServersAsync(inputMessageName7 request) =>
            Task.FromResult(new outputMessageName7 { getDNSServers1 = DnsToWire(scenario.DNSServers) });

        internal Task<outputMessageName8> setDNSServersAsync(inputMessageName8 request)
        {
            scenario.DNSServers = Stored(new DNSServers
            {
                PrimaryDNS = AddressFromWire(request.setDNSServers1),
                SecondaryDNS = AddressFromWire(request.setDNSServers2),
            });
            return Task.FromResult(new outputMessageName8());
        }

        internal Task<outputMessageName14> getWLanSettingsAsync(inputMessageName14 request) =>
            Task.FromResult(new outputMessageName14 { getWLanSettings1 = WLanToWire(scenario.WLanSettings) });

        internal Task<outputMessageName15> setWLanSettingsAsync(inputMessageName15 request)
        {
            scenario.WLanSettings = Stored(WLanFromWire(request.setWLanSettings1));
            return Task.FromResult(new outputMessageName15 { setWLanSettings2 = true });
        }

        internal Task<outputMessageName5> getSMTPSettingsAsync(inputMessageName5 request) =>
            Task.FromResult(new outputMessageName5 { getSMTPSettings1 = SmtpToWire(scenario.SMTPSettings) });

        internal Task<outputMessageName4> setSMTPSettingsAsync(inputMessageName4 request)
        {
            scenario.SMTPSettings = Stored(SmtpFromWire(request.setSMTPSettings1));
            return Task.FromResult(new outputMessageName4());
        }

        internal Task<outputMessageName22> getEmailControlSettingsAsync(inputMessageName22 request) =>
            Task.FromResult(new outputMessageName22 { getEmailControlSettings1 = EmailControlToWire(scenario.EmailControlSettings) });

        internal Task<outputMessageName23> setEmailControlSettingsAsync(inputMessageName23 request)
        {
            scenario.EmailControlSettings = Stored(EmailControlFromWire(request.setEmailControlSettings1));
            return Task.FromResult(new outputMessageName23 { setEmailControlSettings2 = true });
        }

        internal Task<outputMessageName18> getWebAccessControlAsync(inputMessageName18 request) =>
            Task.FromResult(new outputMessageName18 { getWebAccessControl1 = AccessToWire(scenario.WebAccessControl) });

        internal Task<outputMessageName19> setWebAccessControlAsync(inputMessageName19 request)
        {
            scenario.WebAccessControl = Stored(AccessFromWire(request.setWebAccessControl1));
            return Task.FromResult(new outputMessageName19());
        }

        private static T Stored<T>(T value)
        {
            AnswerContext.RecordPayload(value);
            return value;
        }

        private static WSNetworkSettings? NetworkToWire(NetworkSettings? network) => network is null ? null : new()
        {
            ipAddress = network.IpAddress,
            netmask = network.Netmask,
            gateway = network.Gateway,
            httpPort = network.HttpPort,
            httpsPort = network.HttpsPort,
        };

        private static NetworkSettings? NetworkFromWire(WSNetworkSettings? wire) => wire is null ? null : new()
        {
            IpAddress = wire.ipAddress,
            Netmask = wire.netmask,
            Gateway = wire.gateway,
            HttpPort = wire.httpPort,
            HttpsPort = wire.httpsPort,
        };

        /// <summary>The servers as the controller lists them: the present ones in order, each an IPv4 address packed
        /// big-endian into one signed 32-bit number.</summary>
        private static WSInetAddress[] DnsToWire(DNSServers dns) =>
            [.. new[] { dns.PrimaryDNS, dns.SecondaryDNS }
                .Where(address => !string.IsNullOrEmpty(address))
                .Select(address => new WSInetAddress { ipAddress = BinaryPrimitives.ReadInt32BigEndian(IPAddress.Parse(address!).GetAddressBytes()) })];

        private static string? AddressFromWire(WSInetAddress? wire)
        {
            if (wire is null)
            {
                return null;
            }
            var bytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(bytes, wire.ipAddress);
            return new IPAddress(bytes).ToString();
        }

        private static WSWLanSettings? WLanToWire(WLanSettings? wlan) => wlan is null ? null : new()
        {
            enabled = wlan.Enabled,
            ssid = wlan.Ssid,
            key = wlan.Key,
            securityType = wlan.SecurityType,
            encryptionType = wlan.EncryptionType,
            ipAddress = wlan.IpAddress,
            netmask = wlan.Netmask,
            gateway = wlan.Gateway,
        };

        private static WLanSettings? WLanFromWire(WSWLanSettings? wire) => wire is null ? null : new()
        {
            Enabled = wire.enabled,
            Ssid = wire.ssid,
            Key = wire.key,
            SecurityType = wire.securityType,
            EncryptionType = wire.encryptionType,
            IpAddress = wire.ipAddress,
            Netmask = wire.netmask,
            Gateway = wire.gateway,
        };

        private static WSSMTPSettings? SmtpToWire(SMTPSettings? smtp) => smtp is null ? null : new()
        {
            hostname = smtp.Hostname,
            hostport = smtp.Hostport,
            username = smtp.Username,
            password = smtp.Password,
            ssl = smtp.Ssl,
            sendLowBatteryNotification = smtp.SendLowBatteryNotification,
            sendLowBatteryNotificationRecepient = smtp.SendLowBatteryNotificationRecipient,
        };

        private static SMTPSettings? SmtpFromWire(WSSMTPSettings? wire) => wire is null ? null : new()
        {
            Hostname = wire.hostname,
            Hostport = wire.hostport,
            Username = wire.username,
            Password = wire.password,
            Ssl = wire.ssl,
            SendLowBatteryNotification = wire.sendLowBatteryNotification,
            SendLowBatteryNotificationRecipient = wire.sendLowBatteryNotificationRecepient,
        };

        private static WSEmailControlSettings? EmailControlToWire(EmailControlSettings? mail) => mail is null ? null : new()
        {
            serverIPAddress = mail.ServerIpAddress,
            serverPortNumber = mail.ServerPortNumber,
            pop3Username = mail.Pop3Username,
            pop3Password = mail.Pop3Password,
            emailAddress = mail.EmailAddress,
            pollInterval = mail.PollInterval,
            removeEmailsAfterUsage = mail.RemoveEmailsAfterUsage,
            ssl = mail.Ssl,
        };

        private static EmailControlSettings? EmailControlFromWire(WSEmailControlSettings? wire) => wire is null ? null : new()
        {
            ServerIpAddress = wire.serverIPAddress,
            ServerPortNumber = wire.serverPortNumber,
            Pop3Username = wire.pop3Username,
            Pop3Password = wire.pop3Password,
            EmailAddress = wire.emailAddress,
            PollInterval = wire.pollInterval,
            RemoveEmailsAfterUsage = wire.removeEmailsAfterUsage,
            Ssl = wire.ssl,
        };

        private static WSAccessControl? AccessToWire(WebAccessControl? access) => access is null ? null : new()
        {
            m_usbLoginRequired_usb = access.UsbLoginRequired,
            m_administrator_usb = access.AdministratorUsb,
            m_administrator_internal = access.AdministratorInternal,
            m_administrator_external = access.AdministratorExternal,
            m_treeview_usb = access.TreeviewUsb,
            m_treeview_internal = access.TreeviewInternal,
            m_treeview_external = access.TreeviewExternal,
            m_sceneview_usb = access.SceneviewUsb,
            m_sceneview_internal = access.SceneviewInternal,
            m_sceneview_external = access.SceneviewExternal,
            m_scenedesign_usb = access.ScenedesignUsb,
            m_scenedesign_internal = access.ScenedesignInternal,
            m_scenedesign_external = access.ScenedesignExternal,
            m_serverstatus_usb = access.ServerstatusUsb,
            m_serverstatus_internal = access.ServerstatusInternal,
            m_serverstatus_external = access.ServerstatusExternal,
            m_ihcvisual_usb = access.IhcvisualUsb,
            m_ihcvisual_internal = access.IhcvisualInternal,
            m_ihcvisual_external = access.IhcvisualExternal,
            m_onlinedocumentation_usb = access.OnlinedocumentationUsb,
            m_onlinedocumentation_internal = access.OnlinedocumentationInternal,
            m_onlinedocumentation_external = access.OnlinedocumentationExternal,
            m_websceneview_usb = access.WebsceneviewUsb,
            m_websceneview_internal = access.WebsceneviewInternal,
            m_websceneview_external = access.WebsceneviewExternal,
            m_openapi_usb = access.OpenapiUsb,
            m_openapi_internal = access.OpenapiInternal,
            m_openapi_external = access.OpenapiExternal,
            m_openapi_used = access.OpenapiUsed,
        };

        private static WebAccessControl? AccessFromWire(WSAccessControl? wire) => wire is null ? null : new()
        {
            UsbLoginRequired = wire.m_usbLoginRequired_usb,
            AdministratorUsb = wire.m_administrator_usb,
            AdministratorInternal = wire.m_administrator_internal,
            AdministratorExternal = wire.m_administrator_external,
            TreeviewUsb = wire.m_treeview_usb,
            TreeviewInternal = wire.m_treeview_internal,
            TreeviewExternal = wire.m_treeview_external,
            SceneviewUsb = wire.m_sceneview_usb,
            SceneviewInternal = wire.m_sceneview_internal,
            SceneviewExternal = wire.m_sceneview_external,
            ScenedesignUsb = wire.m_scenedesign_usb,
            ScenedesignInternal = wire.m_scenedesign_internal,
            ScenedesignExternal = wire.m_scenedesign_external,
            ServerstatusUsb = wire.m_serverstatus_usb,
            ServerstatusInternal = wire.m_serverstatus_internal,
            ServerstatusExternal = wire.m_serverstatus_external,
            IhcvisualUsb = wire.m_ihcvisual_usb,
            IhcvisualInternal = wire.m_ihcvisual_internal,
            IhcvisualExternal = wire.m_ihcvisual_external,
            OnlinedocumentationUsb = wire.m_onlinedocumentation_usb,
            OnlinedocumentationInternal = wire.m_onlinedocumentation_internal,
            OnlinedocumentationExternal = wire.m_onlinedocumentation_external,
            WebsceneviewUsb = wire.m_websceneview_usb,
            WebsceneviewInternal = wire.m_websceneview_internal,
            WebsceneviewExternal = wire.m_websceneview_external,
            OpenapiUsb = wire.m_openapi_usb,
            OpenapiInternal = wire.m_openapi_internal,
            OpenapiExternal = wire.m_openapi_external,
            OpenapiUsed = wire.m_openapi_used,
        };
    }
}
