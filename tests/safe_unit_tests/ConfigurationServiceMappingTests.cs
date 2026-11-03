using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Configuration;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller's CONFIGURATION mapping, in both directions.
    ///
    /// <see cref="WebAccessControl"/> decides which controller services are reachable, and from where -
    /// so an outbound defect here silently opens or closes controller access, and the damage outlives
    /// the session that caused it. That is the Critical tier's definition, and the whole service was at
    /// zero coverage because nothing above the socket could be substituted until it grew a test seam.
    ///
    /// The access-control mapping is a wall of booleans written out twice by hand, which is the one shape
    /// where a transposition can hide from a value comparison: swap two flags in BOTH directions and a
    /// round-trip still passes. Three checks together rule that out - each domain flag lights exactly
    /// one wire flag, each wire flag lights exactly one domain flag, and the round-trip is the identity
    /// - and one verbatim envelope pins the wire NAMES a controller actually reads.
    /// </summary>
    [TestFixture]
    public class ConfigurationServiceMappingTests
    {
        private sealed class Harness
        {
            internal required ConfigurationService Service { get; init; }
            internal required Ihc.Soap.Configuration.ConfigurationService Soap { get; init; }
            internal WSAccessControl? SentAccessControl { get; set; }
            internal WSNetworkSettings? SentNetworkSettings { get; set; }
            internal inputMessageName2? SentUserLogRequest { get; set; }
            internal WSWLanSettings? SentWLanSettings { get; set; }
            internal WSSMTPSettings? SentSmtpSettings { get; set; }
            internal WSEmailControlSettings? SentEmailControlSettings { get; set; }
            internal (WSInetAddress? Primary, WSInetAddress? Secondary) SentDnsServers { get; set; }
        }

        private static Harness NewHarness()
        {
            var soap = A.Fake<Ihc.Soap.Configuration.ConfigurationService>();
            var harness = new Harness { Service = new ConfigurationService(FakeSession.Over(), soap), Soap = soap };

            A.CallTo(() => soap.setWebAccessControlAsync(A<inputMessageName19>._))
                .Invokes((inputMessageName19 m) => harness.SentAccessControl = m.setWebAccessControl1)
                .Returns(Task.FromResult(new outputMessageName19()));
            A.CallTo(() => soap.setNetworkSettingsAsync(A<inputMessageName17>._))
                .Invokes((inputMessageName17 m) => harness.SentNetworkSettings = m.setNetworkSettings1)
                .Returns(Task.FromResult(new outputMessageName17 { setNetworkSettings2 = true }));
            A.CallTo(() => soap.setWLanSettingsAsync(A<inputMessageName15>._))
                .Invokes((inputMessageName15 m) => harness.SentWLanSettings = m.setWLanSettings1)
                .Returns(Task.FromResult(new outputMessageName15()));
            A.CallTo(() => soap.setSMTPSettingsAsync(A<inputMessageName4>._))
                .Invokes((inputMessageName4 m) => harness.SentSmtpSettings = m.setSMTPSettings1)
                .Returns(Task.FromResult(new outputMessageName4()));
            A.CallTo(() => soap.setEmailControlSettingsAsync(A<inputMessageName23>._))
                .Invokes((inputMessageName23 m) => harness.SentEmailControlSettings = m.setEmailControlSettings1)
                .Returns(Task.FromResult(new outputMessageName23(true)));
            A.CallTo(() => soap.setDNSServersAsync(A<inputMessageName8>._))
                .Invokes((inputMessageName8 m) => harness.SentDnsServers = (m.setDNSServers1, m.setDNSServers2))
                .Returns(Task.FromResult(new outputMessageName8()));

            return harness;
        }

        // ---------------------------------------------------------------- WebAccessControl

        private static IReadOnlyList<PropertyInfo> AccessFlags() =>
            typeof(WebAccessControl).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

        private static IReadOnlyList<PropertyInfo> WireAccessFlags() =>
            typeof(WSAccessControl).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool))
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .ToList();

        /// <summary>
        /// Every flag the SDK will actually write. <c>UsbLoginRequired</c> is excluded on purpose: it
        /// carries <c>[AllowedValues(false)]</c>, so no value of it other than the default can be sent
        /// at all, and a case that "covered" it would only be asserting the wire default against the
        /// model default.
        /// </summary>
        private static IEnumerable<TestCaseData> AccessFlagCases() =>
            AccessFlags().Where(p => p.Name != nameof(WebAccessControl.UsbLoginRequired))
                .Select(p => new TestCaseData(p.Name).SetName($"{{m}}({p.Name})"));

        private static IEnumerable<TestCaseData> WireAccessFlagCases() =>
            WireAccessFlags().Select(p => new TestCaseData(p.Name).SetName($"{{m}}({p.Name})"));

        /// <summary>
        /// The least access the SDK will write: everything off but administrator-over-USB, which
        /// <c>[AllowedValues(true)]</c> keeps on so an installer cannot lock themselves out of the
        /// controller they are configuring.
        /// </summary>
        private static WebAccessControl LeastAccess() => new() { AdministratorUsb = true };

        /// <summary>The least access, plus one flag - so what changed is exactly one permission.</summary>
        private static WebAccessControl With(string flagName)
        {
            var value = LeastAccess();
            typeof(WebAccessControl).GetProperty(flagName)!
                .GetSetMethod(nonPublic: true)!.Invoke(value, new object[] { true });
            return value;
        }

        private static int TrueWireFlags(WSAccessControl wire) =>
            WireAccessFlags().Count(p => (bool)p.GetValue(wire)!);

        private static int TrueAccessFlags(WebAccessControl model) =>
            AccessFlags().Count(p => (bool)p.GetValue(model)!);

        /// <summary>
        /// The two flag lists are reflected, and a reflection that finds nothing turns every case above
        /// into a case that cannot fail. This is what stops that: the counts are asserted against each
        /// other rather than against a written-down number, which would go stale the day the vendor's
        /// WSDL grows a permission.
        /// </summary>
        [Test]
        public void TheAccessFlagsAreReflectedFromBothSidesAndMatchInNumber()
        {
            Assert.Multiple(() =>
            {
                Assert.That(AccessFlags(), Is.Not.Empty);
                Assert.That(WireAccessFlags().Count, Is.EqualTo(AccessFlags().Count),
                    "a permission on one side with no counterpart on the other cannot map at all");
            });
        }

        /// <summary>
        /// Outbound injectivity: a flag turned on in the model turns on a wire flag OF ITS OWN. Two
        /// domain flags mapped to one wire field would silently grant whatever the second one guards;
        /// a flag mapped to none would silently drop a permission the installer set. Counted rather
        /// than named, so the check needs no second copy of the pairing to be wrong in the same way.
        /// </summary>
        [TestCaseSource(nameof(AccessFlagCases))]
        public async Task WebAccessControl_EachDomainFlag_ReachesAWireFlagOfItsOwn(string flagName)
        {
            Harness h = NewHarness();
            WebAccessControl granted = With(flagName);

            await h.Service.SetWebAccessControl(granted);

            Assert.That(TrueWireFlags(h.SentAccessControl!), Is.EqualTo(TrueAccessFlags(granted)),
                $"{flagName} must reach a wire flag no other permission already occupies");
        }

        /// <summary>
        /// Inbound injectivity, the same argument in the direction a dialog reads: a single wire flag
        /// must reach a single domain flag, so what the installer is SHOWN matches what the controller
        /// holds.
        /// </summary>
        [TestCaseSource(nameof(WireAccessFlagCases))]
        public async Task WebAccessControl_EachWireFlag_ReachesExactlyOneDomainFlag(string wireFlagName)
        {
            Harness h = NewHarness();
            var wire = new WSAccessControl();
            typeof(WSAccessControl).GetProperty(wireFlagName)!.SetValue(wire, true);
            A.CallTo(() => h.Soap.getWebAccessControlAsync(A<inputMessageName18>._))
                .Returns(Task.FromResult(new outputMessageName18(wire)));

            WebAccessControl? read = await h.Service.GetWebAccessControl();

            Assert.That(TrueAccessFlags(read!), Is.EqualTo(1),
                $"{wireFlagName} must set exactly one domain flag");
        }

        /// <summary>
        /// With both directions injective, an identity round-trip is what rules out a PAIR of flags
        /// swapped consistently in both mappings - the one defect the two counts above cannot see.
        /// </summary>
        [TestCaseSource(nameof(AccessFlagCases))]
        public async Task WebAccessControl_RoundTripsThroughTheWire(string flagName)
        {
            Harness h = NewHarness();
            WebAccessControl sent = With(flagName);

            await h.Service.SetWebAccessControl(sent);
            A.CallTo(() => h.Soap.getWebAccessControlAsync(A<inputMessageName18>._))
                .Returns(Task.FromResult(new outputMessageName18(h.SentAccessControl)));

            Assert.That(await h.Service.GetWebAccessControl(), Is.EqualTo(sent));
        }

        /// <summary>
        /// The wire NAMES, which no round-trip can pin: the controller reads these element names, and a
        /// name that changed would round-trip perfectly while granting nothing. One pattern that is
        /// asymmetric across the usb/internal/external triples, so a whole group shifted by one shows.
        /// </summary>
        [Test]
        public async Task SetWebAccessControl_SerializesTheFlagsUnderTheirWireNames()
        {
            Harness h = NewHarness();

            await h.Service.SetWebAccessControl(new WebAccessControl
            {
                UsbLoginRequired = false,
                AdministratorUsb = true,
                AdministratorInternal = true,
                AdministratorExternal = false,
                TreeviewUsb = true,
                OpenapiExternal = true,
                OpenapiUsed = true
            });

            string xml = SoapRequestText.Of(new inputMessageName19(h.SentAccessControl));

            Assert.Multiple(() =>
            {
                Assert.That(Flag(xml, "m_usbLoginRequired_usb"), Is.False);
                Assert.That(Flag(xml, "m_administrator_usb"), Is.True);
                Assert.That(Flag(xml, "m_administrator_internal"), Is.True);
                Assert.That(Flag(xml, "m_administrator_external"), Is.False);
                Assert.That(Flag(xml, "m_treeview_usb"), Is.True);
                Assert.That(Flag(xml, "m_treeview_internal"), Is.False);
                Assert.That(Flag(xml, "m_openapi_external"), Is.True);
                Assert.That(Flag(xml, "m_openapi_used"), Is.True);
                Assert.That(Flag(xml, "m_openapi_usb"), Is.False);
            });
        }

        private static bool Flag(string xml, string element)
        {
            Match match = Regex.Match(xml, $"<utcs:{Regex.Escape(element)}>(?<v>true|false)</utcs:{Regex.Escape(element)}>");
            Assert.That(match.Success, Is.True, $"the envelope must carry <utcs:{element}>");
            return match.Groups["v"].Value == "true";
        }

        /// <summary>
        /// Two flags the SDK refuses to write at all, because they can lock an installer out of the
        /// controller they are configuring: USB access must stay passwordless, and administrator access
        /// over USB must stay open. Refused BEFORE the SOAP call, so a rejected value never half-lands.
        /// </summary>
        [Test]
        public void SetWebAccessControl_RefusingValuesThatCouldLockTheInstallerOut_NeverReachesTheController()
        {
            Harness usbLogin = NewHarness();
            Harness noAdminUsb = NewHarness();

            Assert.Multiple(() =>
            {
                Assert.CatchAsync(async () => await usbLogin.Service.SetWebAccessControl(
                    new WebAccessControl { AdministratorUsb = true, UsbLoginRequired = true }));
                Assert.That(usbLogin.SentAccessControl, Is.Null);

                Assert.CatchAsync(async () => await noAdminUsb.Service.SetWebAccessControl(
                    new WebAccessControl { AdministratorUsb = false }));
                Assert.That(noAdminUsb.SentAccessControl, Is.Null);
            });
        }

        /// <summary>A controller that answers with nothing has no access control to report.</summary>
        [Test]
        public async Task GetWebAccessControl_WithNoAnswer_IsNull()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getWebAccessControlAsync(A<inputMessageName18>._))
                .Returns(Task.FromResult(new outputMessageName18(null)));

            Assert.That(await h.Service.GetWebAccessControl(), Is.Null);
        }

        // ---------------------------------------------------------------- SystemInfo

        [Test]
        public async Task GetSystemInfo_MapsEveryFieldTheControllerReports()
        {
            Harness h = NewHarness();
            var realtime = new DateTime(2026, 3, 4, 9, 30, 0, DateTimeKind.Utc);
            var swDate = new DateTime(2018, 6, 5, 4, 3, 2);
            A.CallTo(() => h.Soap.getSystemInfoAsync(A<inputMessageName6>._))
                .Returns(Task.FromResult(new outputMessageName6(new WSSystemInfo
                {
                    uptime = 123456789L,
                    realtimeclock = realtime,
                    serialNumber = "SN-1",
                    productionDate = "2018-06-05",
                    brand = "LK",
                    version = "3.3.7",
                    hwRevision = "HW-2",
                    swDate = swDate,
                    datalineVersion = "DL-4",
                    rfModuleSoftwareVersion = "RF-5",
                    rfModuleSerialNumber = "RFSN-6",
                    applicationIsWithoutViewer = true,
                    smsModemSoftwareVersion = "SMS-7",
                    ledDimmerSoftwareVersion = "LED-8"
                })));

            SystemInfo info = await h.Service.GetSystemInfo();

            Assert.Multiple(() =>
            {
                Assert.That(info.Uptime, Is.EqualTo(123456789L));
                Assert.That(info.Realtimeclock, Is.EqualTo(new DateTimeOffset(realtime)));
                Assert.That(info.SerialNumber, Is.EqualTo("SN-1"));
                Assert.That(info.ProductionDate, Is.EqualTo("2018-06-05"));
                Assert.That(info.Brand, Is.EqualTo("LK"));
                Assert.That(info.Version, Is.EqualTo("3.3.7"));
                Assert.That(info.HWRevision, Is.EqualTo("HW-2"));
                Assert.That(info.DatalineVersion, Is.EqualTo("DL-4"));
                Assert.That(info.RFModuleSoftwareVersion, Is.EqualTo("RF-5"));
                Assert.That(info.RFModuleSerialNumber, Is.EqualTo("RFSN-6"));
                Assert.That(info.ApplicationIsWithoutViewer, Is.True);
                Assert.That(info.SmsModemSoftwareVersion, Is.EqualTo("SMS-7"));
                Assert.That(info.LedDimmerSoftwareVersion, Is.EqualTo("LED-8"));
                // The wire carries a bare DateTime for the software date; the SDK reads it as UTC
                // rather than as the reader's local time, so the value does not shift with the host.
                Assert.That(info.SWDate, Is.EqualTo(new DateTimeOffset(swDate, TimeSpan.Zero)));
                Assert.That(info.SWDate.Offset, Is.EqualTo(TimeSpan.Zero));
            });
        }

        /// <summary>
        /// A controller that reports nothing yields an empty record rather than a null: the caller of
        /// <c>GetSystemInfo</c> is handed a non-nullable <see cref="SystemInfo"/>, so "no information"
        /// has to be expressible as a value.
        /// </summary>
        [Test]
        public async Task GetSystemInfo_WithNoAnswer_IsAnEmptyRecordRatherThanNull()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getSystemInfoAsync(A<inputMessageName6>._))
                .Returns(Task.FromResult(new outputMessageName6(null)));

            Assert.That(await h.Service.GetSystemInfo(), Is.EqualTo(new SystemInfo()));
        }

        // ---------------------------------------------------------------- the remaining settings blocks

        [Test]
        public async Task NetworkSettings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();
            var sent = new NetworkSettings
            {
                IpAddress = "192.168.1.10",
                Netmask = "255.255.255.0",
                Gateway = "192.168.1.1",
                HttpPort = 8080,
                HttpsPort = 8443
            };

            Assert.That(await h.Service.SetNetworkSettings(sent), Is.True);
            A.CallTo(() => h.Soap.getNetworkSettingsAsync(A<inputMessageName16>._))
                .Returns(Task.FromResult(new outputMessageName16(h.SentNetworkSettings)));

            Assert.That(await h.Service.GetNetworkSettings(), Is.EqualTo(sent));
        }

        private static readonly NetworkSettings Wired = new()
        {
            IpAddress = "192.168.1.10", Netmask = "255.255.255.0", Gateway = "192.168.1.1", HttpPort = 80, HttpsPort = 443,
        };

        /// <summary>The reply's flag is the controller's answer to the write: true accepted, false declined.</summary>
        [Test]
        public async Task SetNetworkSettings_ADeclinedWrite_ReadsFalse()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setNetworkSettingsAsync(A<inputMessageName17>._))
                .Returns(Task.FromResult(new outputMessageName17 { setNetworkSettings2 = false }));

            Assert.That(await h.Service.SetNetworkSettings(Wired), Is.False);
        }

        /// <summary>A reply with no flag leaves the outcome unknown, which a declined write is not.</summary>
        [Test]
        public void SetNetworkSettings_WithNoFlag_Throws()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setNetworkSettingsAsync(A<inputMessageName17>._))
                .Returns(Task.FromResult(new outputMessageName17()));

            Assert.That(async () => await h.Service.SetNetworkSettings(Wired),
                Throws.InvalidOperationException.With.Message.Contains("unknown"));
        }

        /// <summary>
        /// A save that changes the address makes a controller restart without replying, as both captured saves did: the
        /// connection is cut. That is no proof the write applied or failed, so it is raised as an unconfirmed write,
        /// the transport's error kept inside, telling the caller to reconnect and read the settings back.
        /// </summary>
        [Test]
        public void SetNetworkSettings_WhenTheConnectionIsCut_RaisesAnUnconfirmedWrite()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setNetworkSettingsAsync(A<inputMessageName17>._))
                .ThrowsAsync(new System.Net.Http.HttpRequestException("An error occurred while sending the request."));

            var unconfirmed = Assert.ThrowsAsync<ErrorWithCodeException>(async () => await h.Service.SetNetworkSettings(Wired))!;

            Assert.Multiple(() =>
            {
                Assert.That(unconfirmed.ErrorCode, Is.EqualTo(Errors.CONTROLLER_WRITE_UNCONFIRMED));
                Assert.That(unconfirmed.Message, Does.StartWith(nameof(Errors.CONTROLLER_WRITE_UNCONFIRMED)).And.Contain("read"));
                Assert.That(unconfirmed.InnerException, Is.InstanceOf<System.Net.Http.HttpRequestException>());
            });
        }

        [Test]
        public async Task WLanSettings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();
            var sent = new WLanSettings
            {
                Enabled = true,
                Ssid = "ihc-net",
                Key = "hemmeligt-kodeord",
                SecurityType = "WPA2",
                EncryptionType = "AES",
                IpAddress = "192.168.2.10",
                Netmask = "255.255.255.0",
                Gateway = "192.168.2.1"
            };

            await h.Service.SetWLanSettings(sent);
            A.CallTo(() => h.Soap.getWLanSettingsAsync(A<inputMessageName14>._))
                .Returns(Task.FromResult(new outputMessageName14(h.SentWLanSettings)));

            Assert.That(await h.Service.GetWLanSettings(), Is.EqualTo(sent));
        }

        /// <summary>
        /// The reply's flag is returned as the controller sent it, and an absent one as null: its meaning is not
        /// established, so neither value is read as an acceptance or a refusal, and no outcome throws.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        [TestCase(null)]
        public async Task SetWLanSettings_ReturnsTheReplyFlagAsSent(bool? flag)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setWLanSettingsAsync(A<inputMessageName15>._))
                .Returns(Task.FromResult(new outputMessageName15 { setWLanSettings2 = flag }));

            Assert.That(await h.Service.SetWLanSettings(new WLanSettings
            {
                Enabled = false, Ssid = "", Key = "", SecurityType = "", EncryptionType = "",
                IpAddress = "192.168.2.10", Netmask = "255.255.255.0", Gateway = "192.168.2.1",
            }), Is.EqualTo(flag));
        }

        /// <summary>
        /// IHC Administrator's mail fields are 20 columns wide but check no length, and the WSDL declares plain
        /// strings, so a longer value is sent whole: here a 21-character SMTP host and its account address, and a POP3
        /// user name written as the full mailbox address, as mail providers ask for.
        /// </summary>
        [Test]
        public async Task MailSettings_LongerThanTheVendorsFieldWidths_AreSentWhole()
        {
            Harness h = NewHarness();
            const string host = "smtp-mail.outlook.com";
            const string account = "installation.alarm@outlook.com";

            await h.Service.SetSMTPSettings(new SMTPSettings
            {
                Hostname = host, Hostport = 587, Username = account, Password = "a passphrase longer than twenty",
                SendLowBatteryNotificationRecipient = "service.department@installer.example",
            });
            await h.Service.SetEmailControlSettings(new EmailControlSettings
            {
                ServerAddress = "outlook.office365.com", ServerPortNumber = 995, Pop3Username = account,
                Pop3Password = "another long passphrase", EmailAddress = account, Ssl = true,
            });

            Assert.That((h.SentSmtpSettings!.hostname, h.SentSmtpSettings.username), Is.EqualTo((host, account)));
            Assert.That(host, Has.Length.EqualTo(21));
            Assert.That((h.SentEmailControlSettings!.serverIPAddress, h.SentEmailControlSettings.pop3Username, h.SentEmailControlSettings.emailAddress),
                Is.EqualTo(("outlook.office365.com", account, account)));
        }

        [Test]
        public async Task SmtpSettings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();
            var sent = new SMTPSettings
            {
                Hostname = "smtp.example.dk",
                Hostport = 587,
                Username = "postkasse",
                Password = "hemmeligt",
                Ssl = true,
                SendLowBatteryNotification = true,
                SendLowBatteryNotificationRecipient = "drift@example.dk",
                SendLEDDimmerErrorNotification = true
            };

            await h.Service.SetSMTPSettings(sent);
            A.CallTo(() => h.Soap.getSMTPSettingsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(h.SentSmtpSettings)));

            Assert.That(await h.Service.GetSMTPSettings(), Is.EqualTo(sent));
        }

        /// <summary>
        /// The LED Dimmer error-mail switch is the last element of the settings a 3.3.44 controller's served WSDL
        /// declares, after the recipient; on the wire it is <c>sendLEDDimmerErrorNotification</c>.
        /// </summary>
        [Test]
        public async Task SmtpSettings_SendTheLedDimmerErrorSwitch_LastOnTheWire()
        {
            Harness h = NewHarness();

            await h.Service.SetSMTPSettings(new SMTPSettings
            {
                Hostname = "smtp.example.dk", Hostport = 25, SendLowBatteryNotificationRecipient = "drift@example.dk",
                SendLEDDimmerErrorNotification = true,
            });

            Assert.That(SoapRequestText.Normalized(SoapRequestText.Of(new inputMessageName4(h.SentSmtpSettings))), Does.Contain(
                "<utcs:sendLowBatteryNotificationRecepient>drift@example.dk</utcs:sendLowBatteryNotificationRecepient>" +
                "<utcs:sendLEDDimmerErrorNotification>true</utcs:sendLEDDimmerErrorNotification></utcs:setSMTPSettings1>"));
        }

        private static readonly EmailControlSettings Pop3 = new()
        {
            ServerAddress = "10.0.0.5",
            ServerPortNumber = 110,
            Pop3Username = "ihc",
            Pop3Password = "kode",
            EmailAddress = "ihc@example.dk",
            PollInterval = 15,
            RemoveEmailsAfterUsage = true,
            Ssl = true
        };

        /// <summary>Reads back e-mail control settings the harness captured on the wire, through the SDK's own
        /// read mapping, so a test compares what was sent with the record it came from.</summary>
        private static async Task<EmailControlSettings?> ReadBack(Harness h, WSEmailControlSettings? wire)
        {
            A.CallTo(() => h.Soap.getEmailControlSettingsAsync(A<inputMessageName22>._))
                .Returns(Task.FromResult(new outputMessageName22(wire)));
            return await h.Service.GetEmailControlSettings();
        }

        [Test]
        public async Task EmailControlSettings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();

            await h.Service.SetEmailControlSettings(Pop3);

            Assert.That(await ReadBack(h, h.SentEmailControlSettings), Is.EqualTo(Pop3));
        }

        /// <summary>
        /// The vendor's settings test sends the dialog's unsaved POP3 settings and shows the outcome the controller
        /// answers with: the settings go out whole, and the four status flags come back as they were sent.
        /// </summary>
        [TestCase(true, false, false, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, false, false, true)]
        public async Task TestSettingsNow_SendsTheSettings_AndReturnsTheStatus(bool succeeded, bool authentication, bool network, bool invalid)
        {
            Harness h = NewHarness();
            WSEmailControlSettings? tested = null;
            A.CallTo(() => h.Soap.testSettingsNowAsync(A<inputMessageName9>._))
                .Invokes((inputMessageName9 m) => tested = m.testSettingsNow1)
                .Returns(Task.FromResult(new outputMessageName9(new WSEmailControlConnectionStatus
                {
                    lastConnectionAttemptSucceeded = succeeded,
                    attemptFailedDueToAuthentication = authentication,
                    attemptFailedDueToNetworkProblems = network,
                    attemptFailedDueToInvalidSettings = invalid,
                })));

            EmailControlConnectionStatus? status = await h.Service.TestSettingsNow(Pop3);

            Assert.That(status, Is.EqualTo(new EmailControlConnectionStatus
            {
                LastConnectionAttemptSucceeded = succeeded,
                AttemptFailedDueToAuthentication = authentication,
                AttemptFailedDueToNetworkProblems = network,
                AttemptFailedDueToInvalidSettings = invalid,
            }));
            Assert.That(await ReadBack(h, tested), Is.EqualTo(Pop3), "the tested settings go out whole");
        }

        private static readonly SMTPSettings Smtp = new()
        {
            Hostname = "smtp.example.dk", Hostport = 587, Username = "ihc@example.dk", Password = "kode", Ssl = true,
        };

        /// <summary>A harness whose controller answers a test mail with <paramref name="result"/>, keeping what it
        /// was sent.</summary>
        private static (Harness Harness, Func<inputMessageName10?> Sent) AnsweringTestMail(WSNotificationMessageSendResult? result)
        {
            Harness h = NewHarness();
            inputMessageName10? sent = null;
            A.CallTo(() => h.Soap.testSendMessageAsync(A<inputMessageName10>._))
                .Invokes((inputMessageName10 m) => sent = m)
                .Returns(Task.FromResult(new outputMessageName10(result)));
            return (h, () => sent);
        }

        /// <summary>
        /// The test mail goes out as IHC Administrator sends it, through the six-part overload: the message carries
        /// the subject, body, recipient and the controller's sender, an e-mail type, a date of zeros and not
        /// delivered; the loose parts are the SMTP account to test, password included.
        /// </summary>
        [Test]
        public async Task TestSendMessage_SendsTheVendorsSixPartRequest()
        {
            var (h, sent) = AnsweringTestMail(new WSNotificationMessageSendResult { attemptSucceeded = true });

            await h.Service.TestSendMessage(Smtp, "drift@example.dk", "Emne", "Tekst");

            Assert.That(sent()!.testSendMessage1.sender, Is.EqualTo("LK IHC® controller<ihc@example.dk>"));
            // The normalised text has no whitespace at all, so the sender's spaces are gone from it.
            Assert.That(SoapRequestText.Normalized(SoapRequestText.Of(sent()!)), Does.Contain(
                "<utcs:testSendMessage1>" +
                "<utcs:date><utcs:day>0</utcs:day><utcs:hours>0</utcs:hours><utcs:minutes>0</utcs:minutes>" +
                "<utcs:seconds>0</utcs:seconds><utcs:year>0</utcs:year><utcs:monthWithJanuaryAsOne>0</utcs:monthWithJanuaryAsOne></utcs:date>" +
                "<utcs:notificationType>text.notification.email_type</utcs:notificationType>" +
                "<utcs:recipient>drift@example.dk</utcs:recipient>" +
                "<utcs:sender>LKIHC®controller&lt;ihc@example.dk&gt;</utcs:sender>" +
                "<utcs:subject>Emne</utcs:subject><utcs:body>Tekst</utcs:body><utcs:delivered>false</utcs:delivered>" +
                "</utcs:testSendMessage1>" +
                "<utcs:testSendMessage2>smtp.example.dk</utcs:testSendMessage2><utcs:testSendMessage3>587</utcs:testSendMessage3>" +
                "<utcs:testSendMessage4>ihc@example.dk</utcs:testSendMessage4><utcs:testSendMessage5>kode</utcs:testSendMessage5>" +
                "<utcs:testSendMessage6>true</utcs:testSendMessage6>"));
        }

        /// <summary>Each of the reply's eight flags reaches its own member, the wire's misspelt "attemp" included.</summary>
        [Test]
        public async Task TestSendMessage_ReturnsEveryFlagOfTheResult()
        {
            (string Wire, string Sdk)[] flags =
            [
                ("attemptSucceeded", nameof(NotificationSendResult.AttemptSucceeded)),
                ("attemptFailedDueToAuthentication", nameof(NotificationSendResult.AttemptFailedDueToAuthentication)),
                ("attemptFailedDueToNetworkProblems", nameof(NotificationSendResult.AttemptFailedDueToNetworkProblems)),
                ("attemptFailedDueToInvalidSettings", nameof(NotificationSendResult.AttemptFailedDueToInvalidSettings)),
                ("attemptFailedDueToInternalErrors", nameof(NotificationSendResult.AttemptFailedDueToInternalErrors)),
                ("attemptFailedDueToConnectTimeout", nameof(NotificationSendResult.AttemptFailedDueToConnectTimeout)),
                ("attempFailedDueToConnectionRefused", nameof(NotificationSendResult.AttemptFailedDueToConnectionRefused)),
                ("attempFailedDueToIOTimeout", nameof(NotificationSendResult.AttemptFailedDueToIOTimeout)),
            ];
            foreach (var (wireName, sdkName) in flags)
            {
                var wire = new WSNotificationMessageSendResult();
                typeof(WSNotificationMessageSendResult).GetProperty(wireName)!.SetValue(wire, true);
                var (h, _) = AnsweringTestMail(wire);

                NotificationSendResult? result = await h.Service.TestSendMessage(Smtp, "drift@example.dk", "Emne", "Tekst");

                string[] set = [.. typeof(NotificationSendResult).GetProperties()
                    .Where(property => property.PropertyType == typeof(bool) && (bool)property.GetValue(result)!)
                    .Select(property => property.Name)];
                Assert.That(set, Is.EqualTo(new[] { sdkName }), wireName);
            }
        }

        [Test]
        public async Task TestSendMessage_WithNoResult_IsNull()
        {
            var (h, _) = AnsweringTestMail(null);

            Assert.That(await h.Service.TestSendMessage(Smtp, "drift@example.dk", "Emne", "Tekst"), Is.Null);
        }

        /// <summary>The SMTP account is validated as a write of it is, so a port no server could use is refused
        /// before anything is sent.</summary>
        [Test]
        public void TestSendMessage_RefusesWhatAnSmtpWriteRefuses_AndSendsNothing()
        {
            var (h, sent) = AnsweringTestMail(null);

            Assert.That(async () => await h.Service.TestSendMessage(Smtp with { Hostport = 70000 }, "drift@example.dk", "Emne", "Tekst"),
                Throws.ArgumentException);
            Assert.That(sent(), Is.Null);
        }

        [Test]
        public async Task TestSettingsNow_WithNoStatus_IsNull()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.testSettingsNowAsync(A<inputMessageName9>._)).Returns(Task.FromResult(new outputMessageName9(null)));

            Assert.That(await h.Service.TestSettingsNow(Pop3), Is.Null);
        }

        /// <summary>The settings are validated as a write of them is, so a port no controller could use is refused
        /// before anything is sent.</summary>
        /// <summary>IHC Administrator requires a POP3 port above 0 and a poll interval of at least 0; the SDK refuses
        /// the same before sending, naming the field.</summary>
        [TestCase(0, 15, "ServerPortNumber")]
        [TestCase(65536, 15, "ServerPortNumber")]
        [TestCase(110, -1, "PollInterval")]
        public void SetEmailControlSettings_RefusesWhatTheAdministratorRefuses_AndSendsNothing(int port, int poll, string named)
        {
            Harness h = NewHarness();

            Assert.That(async () => await h.Service.SetEmailControlSettings(Pop3 with { ServerPortNumber = port, PollInterval = poll }),
                Throws.ArgumentException.With.Message.Contains(named));
            Assert.That(h.SentEmailControlSettings, Is.Null);
        }

        [TestCase(1, 0)]
        [TestCase(65535, int.MaxValue)]
        public async Task SetEmailControlSettings_AcceptsTheRangesEnds(int port, int poll)
        {
            Harness h = NewHarness();

            await h.Service.SetEmailControlSettings(Pop3 with { ServerPortNumber = port, PollInterval = poll });

            Assert.That((h.SentEmailControlSettings!.serverPortNumber, h.SentEmailControlSettings.pollInterval), Is.EqualTo((port, poll)));
        }

        [Test]
        public void TestSettingsNow_RefusesWhatAWriteRefuses_AndSendsNothing()
        {
            Harness h = NewHarness();

            Assert.That(async () => await h.Service.TestSettingsNow(Pop3 with { ServerPortNumber = 70000 }), Throws.ArgumentException);
            A.CallTo(() => h.Soap.testSettingsNowAsync(A<inputMessageName9>._)).MustNotHaveHappened();
        }

        /// <summary>
        /// The controller answers a POP3 settings write with an acceptance flag, which IHC Administrator reads: true
        /// closes its dialog, false leaves it open as a failure. The flag is returned as sent.
        /// </summary>
        [TestCase(true)]
        [TestCase(false)]
        public async Task SetEmailControlSettings_ReturnsTheControllersAcceptanceFlag(bool accepted)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setEmailControlSettingsAsync(A<inputMessageName23>._))
                .Returns(Task.FromResult(new outputMessageName23(accepted)));

            Assert.That(await h.Service.SetEmailControlSettings(new EmailControlSettings { ServerPortNumber = 110 }), Is.EqualTo(accepted));
        }

        /// <summary>A reply with no flag is not a declined write: the settings were sent and the answer did not say
        /// what became of them, so it throws rather than read as either outcome.</summary>
        [Test]
        public void SetEmailControlSettings_WithNoAcceptanceFlag_Throws()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.setEmailControlSettingsAsync(A<inputMessageName23>._))
                .Returns(Task.FromResult(new outputMessageName23(null)));

            Assert.That(async () => await h.Service.SetEmailControlSettings(new EmailControlSettings { ServerPortNumber = 110 }),
                Throws.InvalidOperationException.With.Message.Contains("unknown"));
            A.CallTo(() => h.Soap.setEmailControlSettingsAsync(A<inputMessageName23>._)).MustHaveHappenedOnceExactly();
        }

        /// <summary>
        /// A controller with no mailbox set answers its four strings nil, each with a line break inside: the shape
        /// captured from a real controller's reply, with synthetic values in the members it did fill. Read through the
        /// SDK's deserializer, the four read as null, never as the line break, also where the nil attribute is written
        /// with the whitespace XML allows around its <c>=</c>.
        /// </summary>
        [TestCase("xsi:nil=\"true\"", TestName = "EmailControlSettings_WithNilStrings_ReadThemAsNull(as captured)")]
        [TestCase("xsi:nil = \"true\"", TestName = "EmailControlSettings_WithNilStrings_ReadThemAsNull(spaced around the equals sign)")]
        public async Task EmailControlSettings_WithNilStrings_ReadThemAsNull(string nil)
        {
            string reply = """
                <SOAP-ENV:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
                  <SOAP-ENV:Body>
                    <ns1:getEmailControlSettings1 xmlns:ns1="utcs" xsi:type="ns1:WSEmailControlSettings">
                      <ns1:serverIPAddress xsi:type="xsd:string" xsi:nil="true">
                </ns1:serverIPAddress>
                      <ns1:serverPortNumber xsi:type="xsd:int">110</ns1:serverPortNumber>
                      <ns1:pop3Username xsi:type="xsd:string" xsi:nil="true">
                </ns1:pop3Username>
                      <ns1:pop3Password xsi:type="xsd:string" xsi:nil="true">
                </ns1:pop3Password>
                      <ns1:emailAddress xsi:type="xsd:string" xsi:nil="true">
                </ns1:emailAddress>
                      <ns1:pollInterval xsi:type="xsd:int">5</ns1:pollInterval>
                      <ns1:removeEmailsAfterUsage xsi:type="xsd:boolean">false</ns1:removeEmailsAfterUsage>
                      <ns1:ssl xsi:type="xsd:boolean">false</ns1:ssl>
                    </ns1:getEmailControlSettings1>
                  </SOAP-ENV:Body>
                </SOAP-ENV:Envelope>
                """.Replace("xsi:nil=\"true\"", nil, StringComparison.Ordinal);
            Harness h = NewHarness();
            var answer = Serialization.DeserializeXml<Ihc.Envelope.ResponseEnvelope<outputMessageName22>>(reply)!.Body;
            A.CallTo(() => h.Soap.getEmailControlSettingsAsync(A<inputMessageName22>._)).Returns(Task.FromResult(answer));

            EmailControlSettings? settings = await h.Service.GetEmailControlSettings();

            Assert.That((settings!.ServerAddress, settings.Pop3Username, settings.Pop3Password, settings.EmailAddress),
                Is.EqualTo(((string?)null, (string?)null, (string?)null, (string?)null)));
            Assert.That((settings.ServerPortNumber, settings.PollInterval), Is.EqualTo((110, 5)));
        }

        /// <summary>
        /// DNS is the one settings block whose wire form is not a mirror of the model: two addresses
        /// carried POSITIONALLY as packed 32-bit integers. So the round-trip has to survive the packing
        /// as well as the ordering, and a secondary server that was never set must stay unset rather
        /// than arriving as address zero.
        /// </summary>
        [Test]
        public async Task DnsServers_RoundTripThroughTheirPackedWireForm()
        {
            Harness h = NewHarness();
            var sent = new DNSServers { PrimaryDNS = "8.8.8.8", SecondaryDNS = "1.1.1.1" };

            await h.Service.SetDNSServers(sent);
            A.CallTo(() => h.Soap.getDNSServersAsync(A<inputMessageName7>._))
                .Returns(Task.FromResult(new outputMessageName7(
                    new[] { h.SentDnsServers.Primary!, h.SentDnsServers.Secondary! })));

            Assert.That(await h.Service.GetDNSServers(), Is.EqualTo(sent));
        }

        /// <summary>
        /// A server typed with leading zeros goes out as the address meant, its groups read as decimal: the packed
        /// int is 192.168.1.10 in network byte order (0xC0A8010A), never the octal 192.168.1.8 the platform parser
        /// would make of it. The expected ints are literals, independent of the conversion under test.
        /// </summary>
        [Test]
        public async Task DnsServers_TypedWithLeadingZeros_GoOutAsTheDecimalAddress()
        {
            Harness h = NewHarness();

            await h.Service.SetDNSServers(new DNSServers { PrimaryDNS = "192.168.001.010", SecondaryDNS = "08.8.8.8" });

            Assert.That((h.SentDnsServers.Primary!.ipAddress, h.SentDnsServers.Secondary!.ipAddress),
                Is.EqualTo((-1062731510, 0x08080808)));
        }

        /// <summary>A legacy form the platform parser accepts never reaches the wire.</summary>
        [Test]
        public void DnsServers_InALegacyForm_AreRefusedBeforeTheRequestIsSent()
        {
            Harness h = NewHarness();

            Assert.ThrowsAsync<ArgumentException>(() => h.Service.SetDNSServers(new DNSServers { PrimaryDNS = "10.1", SecondaryDNS = "8.8.8.8" }));
            A.CallTo(() => h.Soap.setDNSServersAsync(A<inputMessageName8>._)).MustNotHaveHappened();
        }

        [Test]
        public async Task DnsServers_WithNoSecondary_SendNoSecondaryAddress()
        {
            Harness h = NewHarness();

            await h.Service.SetDNSServers(new DNSServers { PrimaryDNS = "8.8.8.8", SecondaryDNS = null });

            Assert.Multiple(() =>
            {
                Assert.That(h.SentDnsServers.Primary, Is.Not.Null);
                Assert.That(h.SentDnsServers.Secondary, Is.Null,
                    "an unset secondary must not be sent as address 0.0.0.0");
            });
        }

        [Test]
        public async Task GetDnsServers_WithNoAddressesReported_IsEmptyRatherThanZeroed()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getDNSServersAsync(A<inputMessageName7>._))
                .Returns(Task.FromResult(new outputMessageName7(Array.Empty<WSInetAddress>())));

            Assert.That(await h.Service.GetDNSServers(), Is.EqualTo(new DNSServers()));
        }

        /// <summary>
        /// A 3.3.44 controller answers an empty list as an array holding one nil item, which deserializes to an array
        /// of one null. The DNS list's item is nillable, so that is how a controller with no DNS server would answer;
        /// it reads as no servers, not as a null dereference.
        /// </summary>
        [Test]
        public async Task GetDnsServers_WithTheLoneNilItem_IsEmpty()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getDNSServersAsync(A<inputMessageName7>._))
                .Returns(Task.FromResult(new outputMessageName7([null!])));

            Assert.That(await h.Service.GetDNSServers(), Is.EqualTo(new DNSServers()));
        }

        // ---------------------------------------------------------------- the remaining operations

        /// <summary>
        /// The user log arrives as a file of bytes, and the SDK hands back its lines. The terminator is
        /// the controller's to choose and the SDK's to absorb: a CRLF is ONE line break, so a log
        /// written that way must not read back with a blank line between every real one.
        /// </summary>
        [TestCase("\n", TestName = "GetUserLog_DecodesTheFileAsUtf8Lines(LF)")]
        [TestCase("\r\n", TestName = "GetUserLog_DecodesTheFileAsUtf8Lines(CRLF)")]
        [TestCase("\r", TestName = "GetUserLog_DecodesTheFileAsUtf8Lines(CR)")]
        public async Task GetUserLog_DecodesTheFileAsUtf8Lines(string terminator)
        {
            Harness h = AnsweringUserLog($"første linje{terminator}anden linje");

            UserLogFileText log = await h.Service.GetUserLog("da", "");

            Assert.That(log.Lines, Is.EqualTo(new[] { "første linje", "anden linje" }));
        }

        /// <summary>
        /// The request's first part is a cursor: empty for a fresh read, afterwards the file name the previous reply
        /// carried, which is the next cursor. The second part is always 0 and the third the language.
        /// </summary>
        [Test]
        public async Task GetUserLog_SendsTheCursor_AndReturnsTheNextOne()
        {
            Harness h = AnsweringUserLog("første linje");

            UserLogFileText log = await h.Service.GetUserLog("en", "userlog.7");

            Assert.Multiple(() =>
            {
                inputMessageName2 sent = h.SentUserLogRequest!;
                Assert.That((sent.getUserLog1, sent.getUserLog2, sent.getUserLog3), Is.EqualTo(("userlog.7", (int?)0, "en")));
                Assert.That(log.FileName, Is.EqualTo("user.log"), "the reply's file name is the next cursor");
            });
        }

        /// <summary>Called without arguments through the interface, a read is a fresh one in Danish.</summary>
        [Test]
        public async Task GetUserLog_ByDefault_IsAFreshReadInDanish()
        {
            Harness h = AnsweringUserLog("første linje");
            IConfigurationService service = h.Service;

            await service.GetUserLog();

            Assert.That((h.SentUserLogRequest!.getUserLog1, h.SentUserLogRequest.getUserLog3), Is.EqualTo(("", "da")));
        }

        /// <summary>
        /// A blank line the controller actually wrote is content, not a separator artefact - which is why
        /// the terminators are normalised rather than filtered out.
        /// </summary>
        [Test]
        public async Task GetUserLog_KeepsABlankLineTheControllerWrote()
        {
            Harness h = AnsweringUserLog("første linje\r\n\r\nanden linje");

            Assert.That((await h.Service.GetUserLog("da", "")).Lines,
                Is.EqualTo(new[] { "første linje", "", "anden linje" }));
        }

        /// <summary>A harness whose controller answers the user log with the given file content.</summary>
        private static Harness AnsweringUserLog(string content)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getUserLogAsync(A<inputMessageName2>._))
                .Invokes((inputMessageName2 m) => h.SentUserLogRequest = m)
                .Returns(Task.FromResult(new outputMessageName2(new WSFile
                {
                    filename = "user.log",
                    data = Encoding.UTF8.GetBytes(content)
                })));
            return h;
        }

        [Test]
        public async Task GetUserLog_WithNoFile_IsEmpty()
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getUserLogAsync(A<inputMessageName2>._))
                .Returns(Task.FromResult(new outputMessageName2(null)));

            UserLogFileText log = await h.Service.GetUserLog("da", "");

            Assert.That((log.FileName, log.Lines), Is.EqualTo(((string?)null, Array.Empty<string>())));
        }

        /// <summary>
        /// The plain pass-through operations, asserted together: each must reach the controller with
        /// the argument it was given. They carry no mapping, so what is at stake is only that the right
        /// value reaches the right call.
        /// </summary>
        [Test]
        public async Task PassThroughOperations_ReachTheControllerWithTheirArgument()
        {
            Harness h = NewHarness();
            int? rebootDelay = null;
            string? language = null;
            bool? emailControlEnabled = null;
            A.CallTo(() => h.Soap.delayedRebootAsync(A<inputMessageName1>._))
                .Invokes((inputMessageName1 m) => rebootDelay = m.delayedReboot1)
                .Returns(Task.FromResult(new outputMessageName1()));
            A.CallTo(() => h.Soap.setServerLanguageAsync(A<inputMessageName24>._))
                .Invokes((inputMessageName24 m) => language = m.setServerLanguage1)
                .Returns(Task.FromResult(new outputMessageName24()));
            A.CallTo(() => h.Soap.setEmailControlEnabledAsync(A<inputMessageName20>._))
                .Invokes((inputMessageName20 m) => emailControlEnabled = m.setEmailControlEnabled1)
                .Returns(Task.FromResult(new outputMessageName20()));
            A.CallTo(() => h.Soap.clearUserLogAsync(A<inputMessageName3>._))
                .Returns(Task.FromResult(new outputMessageName3()));

            await h.Service.DelayedReboot(30);
            await h.Service.SetServerLanguage("da");
            await h.Service.SetEmailControlEnabled(true);
            await h.Service.ClearUserLog();

            Assert.Multiple(() =>
            {
                Assert.That(rebootDelay, Is.EqualTo(30));
                Assert.That(language, Is.EqualTo("da"));
                Assert.That(emailControlEnabled, Is.True);
                A.CallTo(() => h.Soap.clearUserLogAsync(A<inputMessageName3>._)).MustHaveHappenedOnceExactly();
            });
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(null, false)]
        public async Task GetEmailControlEnabled_TreatsSilenceAsDisabled(bool? answered, bool expected)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getEmailControlEnabledAsync(A<inputMessageName21>._))
                .Returns(Task.FromResult(new outputMessageName21(answered)));

            Assert.That(await h.Service.GetEmailControlEnabled(), Is.EqualTo(expected));
        }
    }
}
