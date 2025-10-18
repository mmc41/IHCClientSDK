using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    public record SystemInfo {

        public long Uptime { get; init; }

        public DateTimeOffset Realtimeclock { get; init; }

        public string SerialNumber { get; init; }

        public string ProductionDate { get; init; }

        public string Brand { get; init; }

        public string Version { get; init; }

        public string HWRevision { get; init; }

        public DateTimeOffset SWDate { get; init; }

        public string DatalineVersion { get; init; }

        public string RFModuleSoftwareVersion { get; init; }

        public string RFModuleSerialNumber { get; init; }

        public bool ApplicationIsWithoutViewer { get; init; }

        public override string ToString()
        {
          return $"SystemInfo(Uptime={Uptime}, Realtimeclock={Realtimeclock}, SerialNumber={SerialNumber}, ProductionDate={ProductionDate}, Brand={Brand}, Version={Version}, HWRevision={HWRevision}, SWDate={SWDate}, DatalineVersion={DatalineVersion}, RFModuleSoftwareVersion={RFModuleSoftwareVersion}, RFModuleSerialNumber={RFModuleSerialNumber}, ApplicationIsWithoutViewer={ApplicationIsWithoutViewer})";
        }
    }

    public record UserLogFileText {
        public string FileName { get; init; }
        public string Content { get; init; }

        public override string ToString()
        {
          return $"UserLogFileText(FileName={FileName}, Content=string[{Content?.Length ?? 0}])";
        }
    }

    public record NetworkSettings {
        public string IpAddress { get; init; }
        public string Netmask { get; init; }
        public string Gateway { get; init; }
        public int HttpPort { get; init; }
        public int HttpsPort { get; init; }

        public override string ToString()
        {
          return $"NetworkSettings(IpAddress={IpAddress}, Netmask={Netmask}, Gateway={Gateway}, HttpPort={HttpPort}, HttpsPort={HttpsPort})";
        }
    }

    public record WLanSettings {
        public bool Enabled { get; init; }
        public string Ssid { get; init; }
        public string Key { get; init; }
        public string SecurityType { get; init; }
        public string EncryptionType { get; init; }
        public string IpAddress { get; init; }
        public string Netmask { get; init; }
        public string Gateway { get; init; }

        public override string ToString()
        {
          return $"WLanSettings(Enabled={Enabled}, Ssid={Ssid}, Key={Key}, SecurityType={SecurityType}, EncryptionType={EncryptionType}, IpAddress={IpAddress}, Netmask={Netmask}, Gateway={Gateway})";
        }
    }

    public record WLanCell {
        public string Ssid { get; init; }
        public bool HasEncryption { get; init; }
        public string SecurityType { get; init; }
        public string EncryptionType { get; init; }

        public override string ToString()
        {
          return $"WLanCell(Ssid={Ssid}, HasEncryption={HasEncryption}, SecurityType={SecurityType}, EncryptionType={EncryptionType})";
        }
    }

    public record WLanInterface {
        public bool Connected { get; init; }
        public string Name { get; init; }
        public string Ssid { get; init; }
        public string Quality { get; init; }

        public override string ToString()
        {
          return $"WLanInterface(Connected={Connected}, Name={Name}, Ssid={Ssid}, Quality={Quality})";
        }
    }

    public record WebAccessControl {
        public bool UsbLoginRequired { get; init; }
        public bool AdministratorUsb { get; init; }
        public bool AdministratorInternal { get; init; }
        public bool AdministratorExternal { get; init; }
        public bool TreeviewUsb { get; init; }
        public bool TreeviewInternal { get; init; }
        public bool TreeviewExternal { get; init; }
        public bool SceneviewUsb { get; init; }
        public bool SceneviewInternal { get; init; }
        public bool SceneviewExternal { get; init; }
        public bool ScenedesignUsb { get; init; }
        public bool ScenedesignInternal { get; init; }
        public bool ScenedesignExternal { get; init; }
        public bool ServerstatusUsb { get; init; }
        public bool ServerstatusInternal { get; init; }
        public bool ServerstatusExternal { get; init; }
        public bool IhcvisualUsb { get; init; }
        public bool IhcvisualInternal { get; init; }
        public bool IhcvisualExternal { get; init; }
        public bool OnlinedocumentationUsb { get; init; }
        public bool OnlinedocumentationInternal { get; init; }
        public bool OnlinedocumentationExternal { get; init; }
        public bool WebsceneviewUsb { get; init; }
        public bool WebsceneviewInternal { get; init; }
        public bool WebsceneviewExternal { get; init; }
        public bool OpenapiUsb { get; init; }
        public bool OpenapiInternal { get; init; }
        public bool OpenapiExternal { get; init; }
        public bool OpenapiUsed { get; init; }

        public override string ToString()
        {
          return $"WebAccessControl(UsbLoginRequired={UsbLoginRequired}, AdministratorUsb={AdministratorUsb}, AdministratorInternal={AdministratorInternal}, AdministratorExternal={AdministratorExternal}, TreeviewUsb={TreeviewUsb}, TreeviewInternal={TreeviewInternal}, TreeviewExternal={TreeviewExternal}, SceneviewUsb={SceneviewUsb}, SceneviewInternal={SceneviewInternal}, SceneviewExternal={SceneviewExternal}, ScenedesignUsb={ScenedesignUsb}, ScenedesignInternal={ScenedesignInternal}, ScenedesignExternal={ScenedesignExternal}, ServerstatusUsb={ServerstatusUsb}, ServerstatusInternal={ServerstatusInternal}, ServerstatusExternal={ServerstatusExternal}, IhcvisualUsb={IhcvisualUsb}, IhcvisualInternal={IhcvisualInternal}, IhcvisualExternal={IhcvisualExternal}, OnlinedocumentationUsb={OnlinedocumentationUsb}, OnlinedocumentationInternal={OnlinedocumentationInternal}, OnlinedocumentationExternal={OnlinedocumentationExternal}, WebsceneviewUsb={WebsceneviewUsb}, WebsceneviewInternal={WebsceneviewInternal}, WebsceneviewExternal={WebsceneviewExternal}, OpenapiUsb={OpenapiUsb}, OpenapiInternal={OpenapiInternal}, OpenapiExternal={OpenapiExternal}, OpenapiUsed={OpenapiUsed})";
        }
    }

    public record EmailControlSettings {
        public string ServerIpAddress { get; init; }
        public int ServerPortNumber { get; init; }
        public string Pop3Username { get; init; }
        public string Pop3Password { get; init; }
        public string EmailAddress { get; init; }
        public int PollInterval { get; init; }
        public bool RemoveEmailsAfterUsage { get; init; }
        public bool Ssl { get; init; }

        public override string ToString()
        {
          return $"EmailControlSettings(ServerIpAddress={ServerIpAddress}, ServerPortNumber={ServerPortNumber}, Pop3Username={Pop3Username}, Pop3Password={Pop3Password}, EmailAddress={EmailAddress}, PollInterval={PollInterval}, RemoveEmailsAfterUsage={RemoveEmailsAfterUsage}, Ssl={Ssl})";
        }
    }

    public record SMTPSettings {
        public string Hostname { get; init; }
        public int Hostport { get; init; }
        public string Username { get; init; }
        public string Password { get; init; }
        public bool Ssl { get; init; }
        public bool SendLowBatteryNotification { get; init; }
        public string SendLowBatteryNotificationRecipient { get; init; }

        public override string ToString()
        {
          return $"SMTPSettings(Hostname={Hostname}, Hostport={Hostport}, Username={Username}, Password={Password}, Ssl={Ssl}, SendLowBatteryNotification={SendLowBatteryNotification}, SendLowBatteryNotificationRecipient={SendLowBatteryNotificationRecipient})";
        }
    }

    public record InetAddress {
        public int IpAddress { get; init; }

        public override string ToString()
        {
          return $"InetAddress(IpAddress={IpAddress})";
        }
    }

    public record DNSServers {
        public string PrimaryDNS { get; init; }
        public string SecondaryDNS { get; init; }

        public override string ToString()
        {
          return $"DNSServers(PrimaryDNS={PrimaryDNS}, SecondaryDNS={SecondaryDNS})";
        }
    }
}