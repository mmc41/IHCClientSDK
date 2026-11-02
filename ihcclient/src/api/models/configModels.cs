using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Ihc {
    /// <summary>
    /// Identity and status of the controller: uptime, clock, serial number and the firmware and hardware versions of
    /// the controller and its modules.
    /// </summary>
    public record SystemInfo {

        /// <summary>
        /// Milliseconds since the controller last started; it counts from zero again after every restart.
        /// </summary>
        public long Uptime { get; init; }

        /// <summary>
        /// The controller's clock as a UTC instant; that clock can be far off, so treat it as the controller's view.
        /// </summary>
        public DateTimeOffset Realtimeclock { get; init; }

        /// <summary>
        /// Controller serial number, also printed on a label on the controller.
        /// </summary>
        public string? SerialNumber { get; init; }

        /// <summary>
        /// Production year and week as text, in the form <c>YYYY/WW</c>.
        /// </summary>
        public string? ProductionDate { get; init; }

        /// <summary>
        /// Brand name reported by the controller, such as <c>LK</c>.
        /// </summary>
        public string? Brand { get; init; }

        /// <summary>
        /// Controller firmware version as dot-separated numbers, such as <c>3.3.44</c>.
        /// </summary>
        public string? Version { get; init; }

        /// <summary>
        /// Hardware (board) revision of the controller, such as <c>7.1</c>.
        /// </summary>
        public string? HWRevision { get; init; }

        /// <summary>
        /// Release date of the controller firmware, as a UTC instant.
        /// </summary>
        public DateTimeOffset SWDate { get; init; }

        /// <summary>
        /// Firmware version of the controller's I/O (dataline) board, such as <c>IOB.B.03.02.03</c>.
        /// </summary>
        public string? DatalineVersion { get; init; }

        /// <summary>
        /// Firmware version of the controller's radio (RF) module for IHC Wireless.
        /// </summary>
        public string? RFModuleSoftwareVersion { get; init; }

        /// <summary>
        /// Serial number of the controller's radio (RF) module for IHC Wireless.
        /// </summary>
        public string? RFModuleSerialNumber { get; init; }

        /// <summary>
        /// <c>true</c> on a controller without the Viewer function.
        /// </summary>
        public bool ApplicationIsWithoutViewer { get; init; }

        /// <summary>
        /// SMS modem software version, probably that of the modem firmware the controller carries rather than of a
        /// fitted modem: it is reported even with no modem fitted.
        /// </summary>
        public string? SmsModemSoftwareVersion { get; init; }

        /// <summary>
        /// Version of the LED Dimmer 2 firmware image stored in the controller, which it pushes to the dimmers when
        /// they are upgraded; not the version an attached dimmer runs.
        /// </summary>
        public string? LedDimmerSoftwareVersion { get; init; }

        public override string ToString()
        {
          return $"SystemInfo(Uptime={Uptime}, Realtimeclock={Realtimeclock}, SerialNumber={SerialNumber}, ProductionDate={ProductionDate}, Brand={Brand}, Version={Version}, HWRevision={HWRevision}, SWDate={SWDate}, DatalineVersion={DatalineVersion}, RFModuleSoftwareVersion={RFModuleSoftwareVersion}, RFModuleSerialNumber={RFModuleSerialNumber}, ApplicationIsWithoutViewer={ApplicationIsWithoutViewer}, SmsModemSoftwareVersion={SmsModemSoftwareVersion}, LedDimmerSoftwareVersion={LedDimmerSoftwareVersion})";
        }
    }

    /// <summary>
    /// Text read from the controller's user log: the controller's own record of events, warnings and faults, not of
    /// user input.
    /// </summary>
    public record UserLogFileText {
        /// <summary>
        /// Name the controller gives the returned text, which serves as the cursor for the next read.
        /// </summary>
        public string? FileName { get; init; }

        /// <summary>
        /// The log text: one line per event, <c>yyyy.MM.dd HH:mm:ss</c>, a tab, the severity (such as <c>Fejl</c>),
        /// a tab and the message, newest first. Time stamps follow the controller's local clock.
        /// </summary>
        public string? Content { get; init; }

        public override string ToString()
        {
          return $"UserLogFileText(FileName={FileName}, Content=string[{Content?.Length ?? 0}])";
        }
    }

    /// <summary>
    /// The controller's wired network settings: a static IPv4 address with mask and gateway, and its HTTP and HTTPS
    /// ports. There is no DHCP or host-name setting.
    /// </summary>
    public record NetworkSettings {
        /// <summary>
        /// Static IPv4 address of the controller's wired network interface, dotted-decimal.
        /// </summary>
        [StringLength(15, ErrorMessage = "IpAddress length can't be more than 15.")]
        [Required(ErrorMessage = "IpAddress is required")]
        public string? IpAddress { get; init; }

        /// <summary>
        /// Subnet mask, dotted-decimal. With <see cref="IpAddress"/> it decides which clients count as the local
        /// network in <see cref="WebAccessControl"/>.
        /// </summary>
        [StringLength(15, ErrorMessage = "Netmask length can't be more than 15.")]
        [Required(ErrorMessage = "Netmask is required")]
        public string? Netmask { get; init; }

        /// <summary>
        /// Default gateway, dotted-decimal.
        /// </summary>
        [StringLength(15, ErrorMessage = "Gateway length can't be more than 15.")]
        [Required(ErrorMessage = "Gateway is required")]
        public string? Gateway { get; init; }

        /// <summary>
        /// Plain HTTP (web) port, 80 by default.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "HttpPort must be between 0 and 65535.")]
        public int HttpPort { get; init; }

        /// <summary>
        /// HTTPS (secure) port, 443 by default, which the vendor says should normally not be changed.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "HttpsPort must be between 0 and 65535.")]
        public int HttpsPort { get; init; }

        public override string ToString()
        {
          return $"NetworkSettings(IpAddress={IpAddress}, Netmask={Netmask}, Gateway={Gateway}, HttpPort={HttpPort}, HttpsPort={HttpsPort})";
        }
    }

    /// <summary>
    /// Settings of the controller's WiFi client: a USB WiFi dongle that joins an existing wireless network as a second
    /// network interface, unrelated to IHC Wireless. Its address is static; there is no DHCP.
    /// </summary>
    public record WLanSettings {
        /// <summary>
        /// Indicates whether the WiFi client is enabled.
        /// </summary>
        public bool Enabled { get; init; }

        /// <summary>
        /// SSID (network name) of the wireless network to join. IEEE 802.11 allows up to 32 octets; the vendor advises
        /// plain ASCII, avoiding characters such as æ, ø, å, '/', '-' and '_'.
        /// </summary>
        [StringLength(32, ErrorMessage = "Ssid length can't be more than 32.")]
        public string? Ssid { get; init; }

        /// <summary>
        /// Wireless network password/key. A WPA2-PSK passphrase is 8–63 characters (64 would be the raw hex PSK),
        /// so the limit is 63 — anything shorter refuses passphrases a real access point hands out.
        /// </summary>
        [StringLength(63, ErrorMessage = "Key length can't be more than 63.")]
        [SensitiveData]
        public string? Key { get; init; }

        /// <summary>
        /// Security type: <c>NONE</c>, <c>WEP</c>, <c>WPA-PSK</c> or <c>WPA2-PSK</c>.
        /// </summary>
        public string? SecurityType { get; init; }

        /// <summary>
        /// Key format for <c>WEP</c> (<c>Ascii</c> or <c>Hex</c>) or cipher for WPA (<c>TKIP</c> or <c>CCMP</c>);
        /// <c>null</c> with <c>NONE</c>.
        /// </summary>
        public string? EncryptionType { get; init; }

        /// <summary>
        /// Static IPv4 address of the WiFi interface, dotted-decimal.
        /// </summary>
        [StringLength(15, ErrorMessage = "IpAddress length can't be more than 15.")]
        [Required(ErrorMessage = "IpAddress is required")]        
        public string? IpAddress { get; init; }

        /// <summary>
        /// Subnet mask of the WiFi interface, dotted-decimal.
        /// </summary>
        [StringLength(15, ErrorMessage = "Netmask length can't be more than 15.")]
        [Required(ErrorMessage = "Netmask is required")]   
        public string? Netmask { get; init; }

        /// <summary>
        /// Default gateway of the WiFi interface, dotted-decimal.
        /// </summary>
        [StringLength(15, ErrorMessage = "Gateway length can't be more than 15.")]
        [Required(ErrorMessage = "Gateway is required")]   
        public string? Gateway { get; init; }

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
    /// A wireless network found by a scan of the controller's WiFi interface; scans report no signal level or channel.
    /// </summary>
    public record WLanCell {
        /// <summary>
        /// SSID of the detected wireless network.
        /// </summary>
        public string? Ssid { get; init; }

        /// <summary>
        /// Indicates whether the network has encryption enabled.
        /// </summary>
        public bool HasEncryption { get; init; }

        /// <summary>
        /// Security type of the network, in the form <see cref="WLanSettings.SecurityType"/> takes.
        /// </summary>
        public string? SecurityType { get; init; }

        /// <summary>
        /// Cipher or key format of the network, in the form <see cref="WLanSettings.EncryptionType"/> takes.
        /// </summary>
        public string? EncryptionType { get; init; }

        public override string ToString()
        {
          return $"WLanCell(Ssid={Ssid}, HasEncryption={HasEncryption}, SecurityType={SecurityType}, EncryptionType={EncryptionType})";
        }
    }

  /// <summary>
  /// Status of the controller's WiFi client interface, a USB WiFi dongle unrelated to IHC Wireless.
  /// </summary>
  public record WLanInterface
  {
    /// <summary>
    /// Whether the interface is associated with a wireless network.
    /// </summary>
    public bool Connected { get; init; }

    /// <summary>
    /// Name of the WiFi interface; empty or <c>null</c> when no WiFi dongle is fitted.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// SSID of the currently connected network.
    /// </summary>
    public string? Ssid { get; init; }

    /// <summary>
    /// Link quality as text.
    /// </summary>
    public string? Quality { get; init; }

    public override string ToString()
    {
      return $"WLanInterface(Connected={Connected}, Name={Name}, Ssid={Ssid}, Quality={Quality})";
    }
  }

    /// <summary>
    /// Which client applications may log in to the controller over each route: USB, the local network (a client address
    /// inside the controller's own IP subnet) and the Internet (any other address).
    /// 
    /// Restricting USB access can lock out even someone at the controller, so the SDK refuses a write that requires a
    /// USB login (<see cref="UsbLoginRequired"/>) or switches off IHC Administrator over USB
    /// (<see cref="AdministratorUsb"/>).
    /// </summary>
    public record WebAccessControl {
        /// <summary>
        /// Whether logging in over USB requires a user name and password (Danish UI: "Kræver login"), which by default
        /// it does not.
        /// </summary>
        [AllowedValues(false, ErrorMessage = "Usb access should be passwordless")]
        public bool UsbLoginRequired { get; init; }

        /// <summary>
        /// IHC Administrator may log in over USB. Keep this on so that the controller stays reachable with physical
        /// access.
        /// </summary>
        [AllowedValues(true, ErrorMessage = "Usb access to Administrator should not be disabled")]
        public bool AdministratorUsb { get; init; }

        /// <summary>
        /// IHC Administrator may log in from the local network.
        /// </summary>
        public bool AdministratorInternal { get; init; }

        /// <summary>
        /// IHC Administrator may log in from the Internet.
        /// </summary>
        public bool AdministratorExternal { get; init; }

        /// <summary>
        /// IHC ServiceView (application name <c>treeview</c>) may log in over USB.
        /// </summary>
        public bool TreeviewUsb { get; init; }

        /// <summary>
        /// IHC ServiceView (application name <c>treeview</c>) may log in from the local network.
        /// </summary>
        public bool TreeviewInternal { get; init; }

        /// <summary>
        /// IHC ServiceView (application name <c>treeview</c>) may log in from the Internet.
        /// </summary>
        public bool TreeviewExternal { get; init; }

        /// <summary>
        /// IHC SceneView may log in over USB.
        /// </summary>
        public bool SceneviewUsb { get; init; }

        /// <summary>
        /// IHC SceneView may log in from the local network.
        /// </summary>
        public bool SceneviewInternal { get; init; }

        /// <summary>
        /// IHC SceneView may log in from the Internet.
        /// </summary>
        public bool SceneviewExternal { get; init; }

        /// <summary>
        /// IHC SceneDesign may log in over USB.
        /// </summary>
        public bool ScenedesignUsb { get; init; }

        /// <summary>
        /// IHC SceneDesign may log in from the local network.
        /// </summary>
        public bool ScenedesignInternal { get; init; }

        /// <summary>
        /// IHC SceneDesign may log in from the Internet.
        /// </summary>
        public bool ScenedesignExternal { get; init; }

        /// <summary>
        /// Probably whether a controller status web page is open over USB.
        /// </summary>
        public bool ServerstatusUsb { get; init; }

        /// <summary>
        /// Probably whether the controller status web page is open from the local network; see
        /// <see cref="ServerstatusUsb"/>.
        /// </summary>
        public bool ServerstatusInternal { get; init; }

        /// <summary>
        /// Probably whether the controller status web page is open from the Internet; see
        /// <see cref="ServerstatusUsb"/>.
        /// </summary>
        public bool ServerstatusExternal { get; init; }

        /// <summary>
        /// IHC Visual may log in over USB.
        /// </summary>
        public bool IhcvisualUsb { get; init; }

        /// <summary>
        /// IHC Visual may log in from the local network.
        /// </summary>
        public bool IhcvisualInternal { get; init; }

        /// <summary>
        /// IHC Visual may log in from the Internet.
        /// </summary>
        public bool IhcvisualExternal { get; init; }

        /// <summary>
        /// Online reports (Danish UI: "Online rapporter") may be used over USB.
        /// </summary>
        public bool OnlinedocumentationUsb { get; init; }

        /// <summary>
        /// Online reports may be used from the local network.
        /// </summary>
        public bool OnlinedocumentationInternal { get; init; }

        /// <summary>
        /// Online reports may be used from the Internet.
        /// </summary>
        public bool OnlinedocumentationExternal { get; init; }

        /// <summary>
        /// Web SceneView, the web-page version of SceneView, may be used over USB. Firmware 3.3.44 lacks this switch,
        /// so it reads <c>false</c> there.
        /// </summary>
        public bool WebsceneviewUsb { get; init; }

        /// <summary>
        /// Web SceneView may be used from the local network; see <see cref="WebsceneviewUsb"/>.
        /// </summary>
        public bool WebsceneviewInternal { get; init; }

        /// <summary>
        /// Web SceneView may be used from the Internet; see <see cref="WebsceneviewUsb"/>.
        /// </summary>
        public bool WebsceneviewExternal { get; init; }

        /// <summary>
        /// Third-party products on the open interface (Danish UI: "Åben for tredjepart produkter") may log in over USB.
        /// </summary>
        public bool OpenapiUsb { get; init; }

        /// <summary>
        /// Third-party products on the open interface may log in from the local network.
        /// </summary>
        public bool OpenapiInternal { get; init; }

        /// <summary>
        /// Third-party products on the open interface may log in from the Internet.
        /// </summary>
        public bool OpenapiExternal { get; init; }

        /// <summary>
        /// Marks that third-party access has been enabled at some point, and stays set; the access itself is set by
        /// <see cref="OpenapiUsb"/>, <see cref="OpenapiInternal"/> and <see cref="OpenapiExternal"/>.
        /// </summary>
        public bool OpenapiUsed { get; init; }

        public override string ToString()
        {
          return $"WebAccessControl(UsbLoginRequired={UsbLoginRequired}, AdministratorUsb={AdministratorUsb}, AdministratorInternal={AdministratorInternal}, AdministratorExternal={AdministratorExternal}, TreeviewUsb={TreeviewUsb}, TreeviewInternal={TreeviewInternal}, TreeviewExternal={TreeviewExternal}, SceneviewUsb={SceneviewUsb}, SceneviewInternal={SceneviewInternal}, SceneviewExternal={SceneviewExternal}, ScenedesignUsb={ScenedesignUsb}, ScenedesignInternal={ScenedesignInternal}, ScenedesignExternal={ScenedesignExternal}, ServerstatusUsb={ServerstatusUsb}, ServerstatusInternal={ServerstatusInternal}, ServerstatusExternal={ServerstatusExternal}, IhcvisualUsb={IhcvisualUsb}, IhcvisualInternal={IhcvisualInternal}, IhcvisualExternal={IhcvisualExternal}, OnlinedocumentationUsb={OnlinedocumentationUsb}, OnlinedocumentationInternal={OnlinedocumentationInternal}, OnlinedocumentationExternal={OnlinedocumentationExternal}, WebsceneviewUsb={WebsceneviewUsb}, WebsceneviewInternal={WebsceneviewInternal}, WebsceneviewExternal={WebsceneviewExternal}, OpenapiUsb={OpenapiUsb}, OpenapiInternal={OpenapiInternal}, OpenapiExternal={OpenapiExternal}, OpenapiUsed={OpenapiUsed})";
        }
    }

    /// <summary>
    /// The POP3 mailbox the controller polls for command mails (e-mail control), with the poll settings; which mails
    /// trigger which action is set per resource in IHC SceneDesign.
    /// </summary>
    public record EmailControlSettings {
        /// <summary>
        /// POP3 server host name or IP address, despite the property name.
        /// </summary>
        [StringLength(20, ErrorMessage = "ServerIpAddress length can't be more than 20.")]
        public string? ServerIpAddress { get; init; }

        /// <summary>
        /// POP3 server TCP port, typically 110.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "ServerPortNumber must be between 0 and 65535.")]
        public int ServerPortNumber { get; init; }

        /// <summary>
        /// POP3 username for authentication.
        /// </summary>
        [StringLength(10, ErrorMessage = "Pop3Username length can't be more than 10.")]
        public string? Pop3Username { get; init; }

        /// <summary>
        /// POP3 password; the controller returns it in clear when the settings are read.
        /// </summary>
        [SensitiveData]
        [StringLength(10, ErrorMessage = "Pop3Password length can't be more than 10.")]
        public string? Pop3Password { get; init; }

        /// <summary>
        /// Address of the polled mailbox, to which command mails are sent.
        /// </summary>
        [StringLength(20, ErrorMessage = "EmailAddress length can't be more than 20.")]
        public string? EmailAddress { get; init; }

        /// <summary>
        /// Seconds between mailbox polls.
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
    /// The SMTP account the controller sends all its mail through (notifications and IHC SceneDesign e-mail messages),
    /// with the notification switches and recipient. Firmware 3.3.44 also has a switch for LED Dimmer error mails,
    /// which this record does not carry, so the SDK neither reads nor sends it.
    /// </summary>
    public record SMTPSettings {
        /// <summary>
        /// SMTP server host name or IP address.
        /// </summary>
        [StringLength(20, ErrorMessage = "Hostname length can't be more than 20.")]
        public string? Hostname { get; init; }

        /// <summary>
        /// SMTP server TCP port, 25 by default.
        /// </summary>
        [Range(0, 65535, ErrorMessage = "Hostport must be between 0 and 65535.")]
        public int Hostport { get; init; }

        /// <summary>
        /// SMTP username for authentication.
        /// </summary>
        [StringLength(20, ErrorMessage = "Username length can't be more than 20.")]
        public string? Username { get; init; }

        /// <summary>
        /// SMTP password; the controller returns it in clear when the settings are read.
        /// </summary>
        [StringLength(20, ErrorMessage = "Password length can't be more than 20.")]
        [SensitiveData]
        public string? Password { get; init; }

        /// <summary>
        /// Use SSL for the SMTP connection; on port 465 it probably means TLS from connect (SMTPS).
        /// </summary>
        public bool Ssl { get; init; }

        /// <summary>
        /// Send a mail when the battery of a component in the installation runs low.
        /// </summary>
        public bool SendLowBatteryNotification { get; init; }

        /// <summary>
        /// Address the notification mails go to, for LED Dimmer errors as well as low battery.
        /// </summary>
        [StringLength(20, ErrorMessage = "SendLowBatteryNotificationRecipient length can't be more than 20.")]
        public string? SendLowBatteryNotificationRecipient { get; init; }

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
    /// An IPv4 address in the packed-integer form the controller uses for DNS servers.
    /// </summary>
    public record InetAddress {
        /// <summary>
        /// IPv4 address as a signed 32-bit integer with the first octet in the most significant byte, so a first
        /// octet of 128 or more makes it negative.
        /// </summary>
        public int IpAddress { get; init; }

        public override string ToString()
        {
          return $"InetAddress(IpAddress={IpAddress})";
        }
    }

    /// <summary>
    /// The controller's primary and secondary DNS servers, used to resolve server names it is configured with, such as
    /// a time server. The wire carries each as a packed 32-bit integer; the SDK converts it to and from dotted text.
    /// </summary>
    public record DNSServers {
        /// <summary>
        /// Primary DNS server as a dotted-decimal IPv4 address. The SDK sends <c>null</c> or empty as no server, and
        /// throws for a value that is not an IPv4 address.
        /// </summary>
        [StringLength(15, ErrorMessage = "PrimaryDNS length can't be more than 15.")]
        public string? PrimaryDNS { get; init; }

        /// <summary>
        /// Secondary DNS server, in the same form as <see cref="PrimaryDNS"/>.
        /// </summary>
        [StringLength(15, ErrorMessage = "SecondaryDNS length can't be more than 15.")]
        public string? SecondaryDNS { get; init; }

        public override string ToString()
        {
          return $"DNSServers(PrimaryDNS={PrimaryDNS}, SecondaryDNS={SecondaryDNS})";
        }
    }
}