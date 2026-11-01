using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The configuration service's system information, user log, wireless network, test-send outcome and its
    /// settings, one property per get/set pair. The defaults are a real controller's answers, with anything that
    /// names a person or a device replaced.
    /// </summary>
    public sealed class StubConfiguration
    {
        private readonly Lock _gate;

        internal StubConfiguration(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>What a system-info query answers.</summary>
        public SystemInfo SystemInfo
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new()
        {
            Uptime = 86489,
            // The time manager's moment, so a default controller reads one clock whichever service is asked.
            Realtimeclock = new DateTimeOffset(ShapeDefaults.Instant),
            SerialNumber = "CN0000000000",
            ProductionDate = "2020/20",
            Brand = "LK",
            Version = "3.3.44",
            HWRevision = "7.1",
            SWDate = new DateTimeOffset(2021, 6, 23, 10, 0, 0, TimeSpan.Zero),
            DatalineVersion = "IOB.B.03.02.03",
            RFModuleSoftwareVersion = "3.06.d",
            RFModuleSerialNumber = "000000000000",
            ApplicationIsWithoutViewer = false,
            SmsModemSoftwareVersion = "1.2.0",
            LedDimmerSoftwareVersion = "DAP.T.01.01.37",
        };

        /// <summary>The user log's lines, oldest first. Clearing the log empties it.</summary>
        public ImmutableArray<string> UserLog
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } = [];

        /// <summary>The wireless interface, or null for a controller that reports none.</summary>
        public WLanInterface? WLanInterface
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new() { Connected = false, Name = "wlan0", Ssid = "", Quality = "" };

        /// <summary>The wireless networks a scan finds.</summary>
        public ImmutableArray<WLanCell> WLanScan
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } = [new() { Ssid = "stub-wlan", HasEncryption = true, SecurityType = "WPA2-PSK", EncryptionType = "CCMP" }];

        /// <summary>Whether a test message is sent. The default is the captured outcome: a controller whose mail
        /// server is out of reach, so the attempt fails on the network.</summary>
        public bool TestSendSucceeds
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>The wired network settings, which a set replaces whole.</summary>
        public NetworkSettings? NetworkSettings
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new() { IpAddress = "192.168.77.103", Netmask = "255.255.255.0", Gateway = "192.168.77.1", HttpPort = 80, HttpsPort = 443 };

        /// <summary>The DNS servers. The wire carries an address list, so a secondary server with no primary reads
        /// back as the primary.</summary>
        public DNSServers DNSServers
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = new() { PrimaryDNS = "193.162.153.164", SecondaryDNS = "8.8.8.8" };

        /// <summary>The wireless network settings. The default is a controller with wireless switched off.</summary>
        public WLanSettings? WLanSettings
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new() { Enabled = false, Ssid = "", Key = "", SecurityType = "", EncryptionType = "", IpAddress = "", Netmask = "", Gateway = "" };

        /// <summary>The outgoing mail server settings. The default is a controller with none configured.</summary>
        public SMTPSettings? SMTPSettings
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new()
        {
            Hostname = "",
            Hostport = 25,
            Username = "",
            Password = "",
            Ssl = false,
            SendLowBatteryNotification = false,
            SendLowBatteryNotificationRecipient = "",
        };

        /// <summary>The e-mail control settings. The default is a controller with no mailbox configured.</summary>
        public EmailControlSettings? EmailControlSettings
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new() { ServerPortNumber = 110, PollInterval = 180, RemoveEmailsAfterUsage = true, Ssl = false };

        /// <summary>Which web applications are reachable over USB, the internal network and the external one.</summary>
        public WebAccessControl? WebAccessControl
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new()
        {
            UsbLoginRequired = false,
            AdministratorUsb = true,
            AdministratorInternal = true,
            AdministratorExternal = false,
            TreeviewUsb = true,
            TreeviewInternal = true,
            TreeviewExternal = true,
            SceneviewUsb = true,
            SceneviewInternal = true,
            SceneviewExternal = true,
            ScenedesignUsb = true,
            ScenedesignInternal = true,
            ScenedesignExternal = true,
            ServerstatusUsb = false,
            ServerstatusInternal = false,
            ServerstatusExternal = false,
            IhcvisualUsb = true,
            IhcvisualInternal = true,
            IhcvisualExternal = true,
            OnlinedocumentationUsb = true,
            OnlinedocumentationInternal = true,
            OnlinedocumentationExternal = false,
            WebsceneviewUsb = true,
            WebsceneviewInternal = true,
            WebsceneviewExternal = true,
            OpenapiUsb = true,
            OpenapiInternal = true,
            OpenapiExternal = true,
            OpenapiUsed = true,
        };
    }
}
