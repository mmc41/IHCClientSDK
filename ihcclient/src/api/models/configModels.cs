using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Ihc {
    /// <summary>
    /// High level model of IHC system information without soap distractions.
    /// </summary>
    public record SystemInfo {

        /// <summary>
        /// System uptime in milliseconds.
        /// </summary>
        public long Uptime { get; init; }

        /// <summary>
        /// Current realtime clock value from the controller.
        /// </summary>
        public DateTimeOffset Realtimeclock { get; init; }

        /// <summary>
        /// Controller serial number.
        /// </summary>
        public string SerialNumber { get; init; }

        /// <summary>
        /// Production date of the controller.
        /// </summary>
        public string ProductionDate { get; init; }

        /// <summary>
        /// Brand name of the controller (e.g., LK, Schneider Electric).
        /// </summary>
        public string Brand { get; init; }

        /// <summary>
        /// Software version running on the controller.
        /// </summary>
        public string Version { get; init; }

        /// <summary>
        /// Hardware revision of the controller.
        /// </summary>
        public string HWRevision { get; init; }

        /// <summary>
        /// Software build date.
        /// </summary>
        public DateTimeOffset SWDate { get; init; }

        /// <summary>
        /// Dataline protocol version.
        /// </summary>
        public string DatalineVersion { get; init; }

        /// <summary>
        /// RF module software version if present.
        /// </summary>
        public string RFModuleSoftwareVersion { get; init; }

        /// <summary>
        /// RF module serial number if present.
        /// </summary>
        public string RFModuleSerialNumber { get; init; }

        /// <summary>
        /// Indicates whether the application is running without the IHC viewer.
        /// </summary>
        public bool ApplicationIsWithoutViewer { get; init; }

        /// <summary>
        /// Get SMS Modem Software Version
        /// </summary>
        public string SmsModemSoftwareVersion { get; init; }

        /// <summary>
        /// Get LED Dimmer Software Version
        /// </summary>
        public string LedDimmerSoftwareVersion { get; init; }

        public override string ToString()
        {
          return $"SystemInfo(Uptime={Uptime}, Realtimeclock={Realtimeclock}, SerialNumber={SerialNumber}, ProductionDate={ProductionDate}, Brand={Brand}, Version={Version}, HWRevision={HWRevision}, SWDate={SWDate}, DatalineVersion={DatalineVersion}, RFModuleSoftwareVersion={RFModuleSoftwareVersion}, RFModuleSerialNumber={RFModuleSerialNumber}, ApplicationIsWithoutViewer={ApplicationIsWithoutViewer}, SmsModemSoftwareVersion={SmsModemSoftwareVersion}, LedDimmerSoftwareVersion={LedDimmerSoftwareVersion})";
        }
    }

    /// <summary>
    /// High level model of a user log file without soap distractions.
    /// </summary>
    public record UserLogFileText {
        /// <summary>
        /// Name of the log file.
        /// </summary>
        public string FileName { get; init; }

        /// <summary>
        /// Text content of the log file.
        /// </summary>
        public string Content { get; init; }

        public override string ToString()
        {
          return $"UserLogFileText(FileName={FileName}, Content=string[{Content?.Length ?? 0}])";
        }
    }

    /// <summary>
    /// High level model of IHC network settings without soap distractions.
    /// </summary>
    public record NetworkSettings {
        /// <summary>
        /// IP address of the controller.
        /// </summary>
        [StringLength(15, ErrorMessage = "IpAddress length can't be more than 15.")]
        [Required(ErrorMessage = "IpAddress is required")]
        public string IpAddress { get; init; }

        /// <summary>
        /// Network mask.
        /// </summary>
        [StringLength(15, ErrorMessage = "Netmask length can't be more than 15.")]
        [Required(ErrorMessage = "Netmask is required")]
        public string Netmask { get; init; }

        /// <summary>
        /// Gateway IP address.
        /// </summary>
        [StringLength(15, ErrorMessage = "Gateway length can't be more than 15.")]
        [Required(ErrorMessage = "Gateway is required")]
        public string Gateway { get; init; }

        /// <summary>
        /// HTTP port number.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "HttpPort must be between 0 and 65535.")]
        public int HttpPort { get; init; }

        /// <summary>
        /// HTTPS port number.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "HttpsPort must be between 0 and 65535.")]
        public int HttpsPort { get; init; }

        public override string ToString()
        {
          return $"NetworkSettings(IpAddress={IpAddress}, Netmask={Netmask}, Gateway={Gateway}, HttpPort={HttpPort}, HttpsPort={HttpsPort})";
        }
    }

    /// <summary>
    /// High level model of IHC WLAN settings without soap distractions.
    /// </summary>
    public record WLanSettings {
        /// <summary>
        /// Indicates whether WLAN is enabled.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// SSID (network name) of the wireless network.
        /// </summary>
        [StringLength(16, ErrorMessage = "Ssid length can't be more than 16.")]
        public string Ssid { get; init; }

        /// <summary>
        /// Wireless network password/key.
        /// </summary>
        [StringLength(16, ErrorMessage = "Key length can't be more than 16.")]
        [SensitiveData]
        public string Key { get; init; }

        /// <summary>
        /// Security type (e.g., WPA2, WEP).
        /// </summary>
        public string SecurityType { get; init; }

        /// <summary>
        /// Encryption type used.
        /// </summary>
        public string EncryptionType { get; init; }

        /// <summary>
        /// IP address for WLAN interface.
        /// </summary>
        [StringLength(15, ErrorMessage = "IpAddress length can't be more than 15.")]
        [Required(ErrorMessage = "IpAddress is required")]        
        public string IpAddress { get; init; }

        /// <summary>
        /// Network mask for WLAN interface.
        /// </summary>
        [StringLength(15, ErrorMessage = "Netmask length can't be more than 15.")]
        [Required(ErrorMessage = "Netmask is required")]   
        public string Netmask { get; init; }

        /// <summary>
        /// Gateway for WLAN interface.
        /// </summary>
        [StringLength(15, ErrorMessage = "Gateway length can't be more than 15.")]
        [Required(ErrorMessage = "Gateway is required")]   
        public string Gateway { get; init; }

        /// <summary>
        /// This default ToString method should not be used! Use alternative with bool parameter.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
          return this.ToString(true); // Unsecure - will output key
        }

        /// <summary>
        /// Safely convert to string. Only convert key if LogSensitiveData set to true.
        /// </summary>
        /// <returns></returns>
        public string ToString(bool LogSensitiveData)
        {
          return $"WLanSettings(Enabled={Enabled}, Ssid={Ssid}, Key={(LogSensitiveData ? Key : UserConstants.REDACTED_PASSWORD)}, SecurityType={SecurityType}, EncryptionType={EncryptionType}, IpAddress={IpAddress}, Netmask={Netmask}, Gateway={Gateway})";
        }
    }

    /// <summary>
    /// High level model of a detected WLAN cell (access point) without soap distractions.
    /// </summary>
    public record WLanCell {
        /// <summary>
        /// SSID of the detected wireless network.
        /// </summary>
        public string Ssid { get; init; }

        /// <summary>
        /// Indicates whether the network has encryption enabled.
        /// </summary>
        public bool HasEncryption { get; init; }

        /// <summary>
        /// Security type of the network.
        /// </summary>
        public string SecurityType { get; init; }

        /// <summary>
        /// Encryption type used by the network.
        /// </summary>
        public string EncryptionType { get; init; }

        public override string ToString()
        {
          return $"WLanCell(Ssid={Ssid}, HasEncryption={HasEncryption}, SecurityType={SecurityType}, EncryptionType={EncryptionType})";
        }
    }

  /// <summary>
  /// High level model of a WLAN interface status without soap distractions.
  /// </summary>
  public record WLanInterface
  {
    /// <summary>
    /// Indicates whether the interface is connected.
    /// </summary>
    public bool Connected { get; init; }

    /// <summary>
    /// Name of the WLAN interface.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// SSID of the currently connected network.
    /// </summary>
    public string Ssid { get; init; }

    /// <summary>
    /// Signal quality indicator.
    /// </summary>
    public string Quality { get; init; }

    public override string ToString()
    {
      return $"WLanInterface(Connected={Connected}, Name={Name}, Ssid={Ssid}, Quality={Quality})";
    }
  }

    /// <summary>
    /// High level model of web access control settings without soap distractions.
    /// Controls which applications can be accessed from different network locations (USB, Internal, External).
    /// 
    /// The API appear to allow manipulating USB access which can be dangerous. Thus
    /// validation attributes are added to disallow non-standard USB access.
    /// </summary>
    public record WebAccessControl {
        /// <summary>
        /// Indicates whether USB login is required.
        /// </summary>
        [AllowedValues(false, ErrorMessage = "Usb access should be passwordless")]
        public bool UsbLoginRequired { get; init; }

        /// <summary>
        /// Administrator application access via USB.
        /// </summary>
        [AllowedValues(true, ErrorMessage = "Usb access to Administrator should not be disabled")]
        public bool AdministratorUsb { get; init; }

        /// <summary>
        /// Administrator application access from internal network.
        /// </summary>
        public bool AdministratorInternal { get; init; }

        /// <summary>
        /// Administrator application access from external network.
        /// </summary>
        public bool AdministratorExternal { get; init; }

        /// <summary>
        /// Treeview application access via USB.
        /// </summary>
        public bool TreeviewUsb { get; init; }

        /// <summary>
        /// Treeview application access from internal network.
        /// </summary>
        public bool TreeviewInternal { get; init; }

        /// <summary>
        /// Treeview application access from external network.
        /// </summary>
        public bool TreeviewExternal { get; init; }

        /// <summary>
        /// Sceneview application access via USB.
        /// </summary>
        public bool SceneviewUsb { get; init; }

        /// <summary>
        /// Sceneview application access from internal network.
        /// </summary>
        public bool SceneviewInternal { get; init; }

        /// <summary>
        /// Sceneview application access from external network.
        /// </summary>
        public bool SceneviewExternal { get; init; }

        /// <summary>
        /// Scenedesign application access via USB.
        /// </summary>
        public bool ScenedesignUsb { get; init; }

        /// <summary>
        /// Scenedesign application access from internal network.
        /// </summary>
        public bool ScenedesignInternal { get; init; }

        /// <summary>
        /// Scenedesign application access from external network.
        /// </summary>
        public bool ScenedesignExternal { get; init; }

        /// <summary>
        /// Serverstatus application access via USB.
        /// </summary>
        public bool ServerstatusUsb { get; init; }

        /// <summary>
        /// Serverstatus application access from internal network.
        /// </summary>
        public bool ServerstatusInternal { get; init; }

        /// <summary>
        /// Serverstatus application access from external network.
        /// </summary>
        public bool ServerstatusExternal { get; init; }

        /// <summary>
        /// IHC Visual application access via USB.
        /// </summary>
        public bool IhcvisualUsb { get; init; }

        /// <summary>
        /// IHC Visual application access from internal network.
        /// </summary>
        public bool IhcvisualInternal { get; init; }

        /// <summary>
        /// IHC Visual application access from external network.
        /// </summary>
        public bool IhcvisualExternal { get; init; }

        /// <summary>
        /// Online reports access via USB.
        /// </summary>
        public bool OnlinedocumentationUsb { get; init; }

        /// <summary>
        /// Online reports access from internal network.
        /// </summary>
        public bool OnlinedocumentationInternal { get; init; }

        /// <summary>
        /// Online reports access from external network.
        /// </summary>
        public bool OnlinedocumentationExternal { get; init; }

        /// <summary>
        /// Sceneview access via USB.
        /// </summary>
        public bool WebsceneviewUsb { get; init; }

        /// <summary>
        /// Sceneview access from internal network.
        /// </summary>
        public bool WebsceneviewInternal { get; init; }

        /// <summary>
        /// Sceneview access from external network.
        /// </summary>
        public bool WebsceneviewExternal { get; init; }

        /// <summary>
        /// API access via USB for 3rd-party use.
        /// </summary>
        public bool OpenapiUsb { get; init; }

        /// <summary>
        /// API access from internal network for 3rd-party use.
        /// </summary>
        public bool OpenapiInternal { get; init; }

        /// <summary>
        /// API access from external network for 3rd-party use.
        /// </summary>
        public bool OpenapiExternal { get; init; }

        /// <summary>
        /// Indicates whether APIs are enabled for 3rd-party use.
        /// </summary>
        public bool OpenapiUsed { get; init; }

        public override string ToString()
        {
          return $"WebAccessControl(UsbLoginRequired={UsbLoginRequired}, AdministratorUsb={AdministratorUsb}, AdministratorInternal={AdministratorInternal}, AdministratorExternal={AdministratorExternal}, TreeviewUsb={TreeviewUsb}, TreeviewInternal={TreeviewInternal}, TreeviewExternal={TreeviewExternal}, SceneviewUsb={SceneviewUsb}, SceneviewInternal={SceneviewInternal}, SceneviewExternal={SceneviewExternal}, ScenedesignUsb={ScenedesignUsb}, ScenedesignInternal={ScenedesignInternal}, ScenedesignExternal={ScenedesignExternal}, ServerstatusUsb={ServerstatusUsb}, ServerstatusInternal={ServerstatusInternal}, ServerstatusExternal={ServerstatusExternal}, IhcvisualUsb={IhcvisualUsb}, IhcvisualInternal={IhcvisualInternal}, IhcvisualExternal={IhcvisualExternal}, OnlinedocumentationUsb={OnlinedocumentationUsb}, OnlinedocumentationInternal={OnlinedocumentationInternal}, OnlinedocumentationExternal={OnlinedocumentationExternal}, WebsceneviewUsb={WebsceneviewUsb}, WebsceneviewInternal={WebsceneviewInternal}, WebsceneviewExternal={WebsceneviewExternal}, OpenapiUsb={OpenapiUsb}, OpenapiInternal={OpenapiInternal}, OpenapiExternal={OpenapiExternal}, OpenapiUsed={OpenapiUsed})";
        }
    }

    /// <summary>
    /// High level model of email control settings for receiving control commands via email without soap distractions.
    /// </summary>
    public record EmailControlSettings {
        /// <summary>
        /// POP3 server IP address.
        /// </summary>
        [StringLength(20, ErrorMessage = "ServerIpAddress length can't be more than 20.")]
        public string ServerIpAddress { get; init; }

        /// <summary>
        /// POP3 server port number.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "ServerPortNumber must be between 0 and 65535.")]
        public int ServerPortNumber { get; init; }

        /// <summary>
        /// POP3 username for authentication.
        /// </summary>
        [StringLength(10, ErrorMessage = "Pop3Username length can't be more than 10.")]
        public string Pop3Username { get; init; }

        /// <summary>
        /// POP3 password for authentication.
        /// </summary>
        [SensitiveData]
        [StringLength(10, ErrorMessage = "Pop3Password length can't be more than 10.")]
        public string Pop3Password { get; init; }

        /// <summary>
        /// Email address to check for control commands.
        /// </summary>
        [StringLength(20, ErrorMessage = "EmailAddress length can't be more than 20.")]
        public string EmailAddress { get; init; }

        /// <summary>
        /// Interval in minutes between email checks.
        /// </summary>
        public int PollInterval { get; init; }

        /// <summary>
        /// Indicates whether emails should be removed after processing.
        /// </summary>
        public bool RemoveEmailsAfterUsage { get; init; }

        /// <summary>
        /// Indicates whether SSL should be used for POP3 connection.
        /// </summary>
        public bool Ssl { get; init; }

        /// <summary>
        /// This default ToString method should not be used! Use alternative with bool parameter.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
          return this.ToString(true); // Unsecure - will output password
        }

        /// <summary>
        /// Safely convert to string. Only convert password if LogSensitiveData set to true.
        /// </summary>
        /// <returns></returns>
        public string ToString(bool LogSensitiveData)
        {
          return $"EmailControlSettings(ServerIpAddress={ServerIpAddress}, ServerPortNumber={ServerPortNumber}, Pop3Username={Pop3Username}, Pop3Password={(LogSensitiveData ? Pop3Password : UserConstants.REDACTED_PASSWORD)}, EmailAddress={EmailAddress}, PollInterval={PollInterval}, RemoveEmailsAfterUsage={RemoveEmailsAfterUsage}, Ssl={Ssl})";
        }
    }

    /// <summary>
    /// High level model of SMTP settings for sending notifications without soap distractions.
    /// </summary>
    public record SMTPSettings {
        /// <summary>
        /// SMTP server hostname.
        /// </summary>
        [StringLength(20, ErrorMessage = "Hostname length can't be more than 20.")]
        public string Hostname { get; init; }

        /// <summary>
        /// SMTP server port number.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "Hostport must be between 0 and 65535.")]
        public int Hostport { get; init; }

        /// <summary>
        /// SMTP username for authentication.
        /// </summary>
        [StringLength(20, ErrorMessage = "Username length can't be more than 20.")]
        public string Username { get; init; }

        /// <summary>
        /// SMTP password for authentication.
        /// </summary>
        [StringLength(20, ErrorMessage = "Password length can't be more than 20.")]
        [SensitiveData]
        public string Password { get; init; }

        /// <summary>
        /// Indicates whether SSL should be used for SMTP connection.
        /// </summary>
        public bool Ssl { get; init; }

        /// <summary>
        /// Indicates whether low battery notifications should be sent.
        /// </summary>
        public bool SendLowBatteryNotification { get; init; }

        /// <summary>
        /// Email address to receive low battery notifications.
        /// </summary>
        [StringLength(20, ErrorMessage = "SendLowBatteryNotificationRecipient length can't be more than 20.")]
        public string SendLowBatteryNotificationRecipient { get; init; }

        /// <summary>
        /// This default ToString method should not be used! Use alternative with bool parameter.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
          return this.ToString(true); // Unsecure - will output password
        }

        /// <summary>
        /// Safely convert to string. Only convert password if LogSensitiveData set to true.
        /// </summary>
        /// <returns></returns>
        public string ToString(bool LogSensitiveData)
        {
          return $"SMTPSettings(Hostname={Hostname}, Hostport={Hostport}, Username={Username}, Password={(LogSensitiveData ? Password : UserConstants.REDACTED_PASSWORD)}, Ssl={Ssl}, SendLowBatteryNotification={SendLowBatteryNotification}, SendLowBatteryNotificationRecipient={SendLowBatteryNotificationRecipient})";
        }
    }

    /// <summary>
    /// High level model of an internet address without soap distractions.
    /// </summary>
    public record InetAddress {
        /// <summary>
        /// IP address represented as an integer.
        /// </summary>
        public int IpAddress { get; init; }

        public override string ToString()
        {
          return $"InetAddress(IpAddress={IpAddress})";
        }
    }

    /// <summary>
    /// High level model of DNS server configuration without soap distractions.
    /// </summary>
    public record DNSServers {
        /// <summary>
        /// Primary DNS server address.
        /// </summary>
        [StringLength(15, ErrorMessage = "PrimaryDNS length can't be more than 15.")]
        public string PrimaryDNS { get; init; }

        /// <summary>
        /// Secondary DNS server address.
        /// </summary>
        [StringLength(15, ErrorMessage = "SecondaryDNS length can't be more than 15.")]
        public string SecondaryDNS { get; init; }

        public override string ToString()
        {
          return $"DNSServers(PrimaryDNS={PrimaryDNS}, SecondaryDNS={SecondaryDNS})";
        }
    }
}