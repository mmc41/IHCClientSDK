using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// Each of the configuration service's get/set pairs reads and writes one typed property: a value set through
    /// the real wrapper reads back through its get.
    /// </summary>
    [TestFixture]
    public class StubConfigurationSettingsTests
    {
        [Test]
        public async Task TheDefaults_AreARealControllersAnswers()
        {
            using var sdk = StubbedSdk.Start();
            StubConfiguration typed = sdk.Stub.Configuration;

            Assert.That(await sdk.Configuration.GetNetworkSettings(), Is.EqualTo(typed.NetworkSettings));
            Assert.That(await sdk.Configuration.GetDNSServers(), Is.EqualTo(new DNSServers { PrimaryDNS = "193.162.153.164", SecondaryDNS = "8.8.8.8" }));
            Assert.That(await sdk.Configuration.GetWLanSettings(), Is.EqualTo(typed.WLanSettings));
            Assert.That(await sdk.Configuration.GetSMTPSettings(), Is.EqualTo(typed.SMTPSettings));
            Assert.That(await sdk.Configuration.GetEmailControlSettings(), Is.EqualTo(typed.EmailControlSettings));
            Assert.That(await sdk.Configuration.GetWebAccessControl(), Is.EqualTo(typed.WebAccessControl));
        }

        [Test]
        public async Task NetworkSettings_SetThroughTheWrapper_ReadBack()
        {
            var network = new NetworkSettings { IpAddress = "10.0.0.5", Netmask = "255.0.0.0", Gateway = "10.0.0.1", HttpPort = 8080, HttpsPort = 8443 };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetNetworkSettings(network);

            Assert.That(await sdk.Configuration.GetNetworkSettings(), Is.EqualTo(network));
        }

        [TestCase("10.20.30.40", "255.254.253.252")]
        [TestCase("1.1.1.1", null)]
        public async Task DnsServers_SetThroughTheWrapper_ReadBack(string primary, string? secondary)
        {
            var dns = new DNSServers { PrimaryDNS = primary, SecondaryDNS = secondary };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetDNSServers(dns);

            Assert.That(await sdk.Configuration.GetDNSServers(), Is.EqualTo(dns));
            Assert.That(sdk.Stub.Configuration.DNSServers, Is.EqualTo(dns));
        }

        /// <summary>The stored settings keep the network key the get must answer; the journal keeps none.</summary>
        [Test]
        public async Task WLanSettings_SetThroughTheWrapper_ReadBack_AndJournalNoKey()
        {
            var wlan = new WLanSettings
            {
                Enabled = true,
                Ssid = "net",
                Key = "stub-key",
                SecurityType = "WPA2-PSK",
                EncryptionType = "CCMP",
                IpAddress = "192.168.1.9",
                Netmask = "255.255.255.0",
                Gateway = "192.168.1.1",
            };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetWLanSettings(wlan);

            Assert.That(await sdk.Configuration.GetWLanSettings(), Is.EqualTo(wlan));
            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.SetWLanSettings)).Single().Payload,
                Is.EqualTo(wlan with { Key = null }));
        }

        /// <summary>The stored settings keep the password the get must answer; the journal keeps none.</summary>
        [Test]
        public async Task SmtpSettings_SetThroughTheWrapper_ReadBack_AndJournalNoPassword()
        {
            var smtp = new SMTPSettings
            {
                Hostname = "smtp.stub.invalid",
                Hostport = 587,
                Username = "stub",
                Password = "stub-only",
                Ssl = true,
                SendLowBatteryNotification = true,
                SendLowBatteryNotificationRecipient = "stub@stub.invalid",
            };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetSMTPSettings(smtp);

            Assert.That(await sdk.Configuration.GetSMTPSettings(), Is.EqualTo(smtp));
            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.SetSMTPSettings)).Single().Payload,
                Is.EqualTo(smtp with { Password = null }));
        }

        [Test]
        public async Task EmailControlSettings_SetThroughTheWrapper_ReadBack_AndJournalNoPassword()
        {
            var mail = new EmailControlSettings
            {
                ServerIpAddress = "pop.stub.invalid",
                ServerPortNumber = 995,
                Pop3Username = "stub",
                Pop3Password = "stub-only",
                EmailAddress = "stub@stub.invalid",
                PollInterval = 60,
                RemoveEmailsAfterUsage = false,
                Ssl = true,
            };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetEmailControlSettings(mail);

            Assert.That(await sdk.Configuration.GetEmailControlSettings(), Is.EqualTo(mail));
            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.SetEmailControlSettings)).Single().Payload,
                Is.EqualTo(mail with { Pop3Password = null }));
        }

        /// <summary>Every flag the opposite of the default, so a mapper that swapped two flags or read one twice
        /// cannot pass.</summary>
        private static readonly WebAccessControl EveryFlagFlipped = new()
        {
            UsbLoginRequired = true,
            AdministratorUsb = false,
            AdministratorInternal = false,
            AdministratorExternal = true,
            TreeviewUsb = false,
            TreeviewInternal = false,
            TreeviewExternal = false,
            SceneviewUsb = false,
            SceneviewInternal = false,
            SceneviewExternal = false,
            ScenedesignUsb = false,
            ScenedesignInternal = false,
            ScenedesignExternal = false,
            ServerstatusUsb = true,
            ServerstatusInternal = true,
            ServerstatusExternal = true,
            IhcvisualUsb = false,
            IhcvisualInternal = false,
            IhcvisualExternal = false,
            OnlinedocumentationUsb = false,
            OnlinedocumentationInternal = false,
            OnlinedocumentationExternal = true,
            WebsceneviewUsb = false,
            WebsceneviewInternal = false,
            WebsceneviewExternal = false,
            OpenapiUsb = false,
            OpenapiInternal = false,
            OpenapiExternal = false,
            OpenapiUsed = false,
        };

        [Test]
        public async Task WebAccessControl_EveryFlag_ReadsThroughTheRealWrapper()
        {
            var stub = new ControllerStub();
            stub.Configuration.WebAccessControl = EveryFlagFlipped;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.GetWebAccessControl(), Is.EqualTo(EveryFlagFlipped));
        }

        /// <summary>A mixed pattern, with the two USB flags at the only values the SDK lets a caller set.</summary>
        [Test]
        public async Task WebAccessControl_SetThroughTheWrapper_ReadBack()
        {
            var access = new WebAccessControl
            {
                UsbLoginRequired = false,
                AdministratorUsb = true,
                AdministratorInternal = false,
                AdministratorExternal = true,
                TreeviewUsb = false,
                TreeviewInternal = true,
                TreeviewExternal = false,
                SceneviewUsb = true,
                SceneviewInternal = false,
                SceneviewExternal = true,
                ScenedesignUsb = false,
                ScenedesignInternal = true,
                ScenedesignExternal = false,
                ServerstatusUsb = true,
                ServerstatusInternal = false,
                ServerstatusExternal = true,
                IhcvisualUsb = false,
                IhcvisualInternal = true,
                IhcvisualExternal = false,
                OnlinedocumentationUsb = true,
                OnlinedocumentationInternal = false,
                OnlinedocumentationExternal = true,
                WebsceneviewUsb = false,
                WebsceneviewInternal = true,
                WebsceneviewExternal = false,
                OpenapiUsb = true,
                OpenapiInternal = false,
                OpenapiExternal = true,
                OpenapiUsed = false,
            };
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.SetWebAccessControl(access);

            Assert.That(await sdk.Configuration.GetWebAccessControl(), Is.EqualTo(access));
        }

        [Test]
        public async Task SettingsSetToNone_ReadAsNone()
        {
            var stub = new ControllerStub();
            stub.Configuration.NetworkSettings = null;
            stub.Configuration.WLanSettings = null;
            stub.Configuration.SMTPSettings = null;
            stub.Configuration.EmailControlSettings = null;
            stub.Configuration.WebAccessControl = null;
            stub.Configuration.DNSServers = new DNSServers();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.GetNetworkSettings(), Is.Null);
            Assert.That(await sdk.Configuration.GetWLanSettings(), Is.Null);
            Assert.That(await sdk.Configuration.GetSMTPSettings(), Is.Null);
            Assert.That(await sdk.Configuration.GetEmailControlSettings(), Is.Null);
            Assert.That(await sdk.Configuration.GetWebAccessControl(), Is.Null);
            Assert.That(await sdk.Configuration.GetDNSServers(), Is.EqualTo(new DNSServers()));
        }
    }
}
