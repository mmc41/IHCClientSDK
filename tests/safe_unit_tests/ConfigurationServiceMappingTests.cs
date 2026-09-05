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
                .Returns(Task.FromResult(new outputMessageName17()));
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

            await h.Service.SetNetworkSettings(sent);
            A.CallTo(() => h.Soap.getNetworkSettingsAsync(A<inputMessageName16>._))
                .Returns(Task.FromResult(new outputMessageName16(h.SentNetworkSettings)));

            Assert.That(await h.Service.GetNetworkSettings(), Is.EqualTo(sent));
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
                SendLowBatteryNotificationRecipient = "drift@example.dk"
            };

            await h.Service.SetSMTPSettings(sent);
            A.CallTo(() => h.Soap.getSMTPSettingsAsync(A<inputMessageName5>._))
                .Returns(Task.FromResult(new outputMessageName5(h.SentSmtpSettings)));

            Assert.That(await h.Service.GetSMTPSettings(), Is.EqualTo(sent));
        }

        [Test]
        public async Task EmailControlSettings_RoundTripThroughTheWire()
        {
            Harness h = NewHarness();
            var sent = new EmailControlSettings
            {
                ServerIpAddress = "10.0.0.5",
                ServerPortNumber = 110,
                Pop3Username = "ihc",
                Pop3Password = "kode",
                EmailAddress = "ihc@example.dk",
                PollInterval = 15,
                RemoveEmailsAfterUsage = true,
                Ssl = true
            };

            await h.Service.SetEmailControlSettings(sent);
            A.CallTo(() => h.Soap.getEmailControlSettingsAsync(A<inputMessageName22>._))
                .Returns(Task.FromResult(new outputMessageName22(h.SentEmailControlSettings)));

            Assert.That(await h.Service.GetEmailControlSettings(), Is.EqualTo(sent));
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

            IReadOnlyList<string> lines = await h.Service.GetUserLog("da");

            Assert.That(lines, Is.EqualTo(new[] { "første linje", "anden linje" }));
        }

        /// <summary>
        /// A blank line the controller actually wrote is content, not a separator artefact - which is why
        /// the terminators are normalised rather than filtered out.
        /// </summary>
        [Test]
        public async Task GetUserLog_KeepsABlankLineTheControllerWrote()
        {
            Harness h = AnsweringUserLog("første linje\r\n\r\nanden linje");

            Assert.That(await h.Service.GetUserLog("da"),
                Is.EqualTo(new[] { "første linje", "", "anden linje" }));
        }

        /// <summary>A harness whose controller answers the user log with the given file content.</summary>
        private static Harness AnsweringUserLog(string content)
        {
            Harness h = NewHarness();
            A.CallTo(() => h.Soap.getUserLogAsync(A<inputMessageName2>._))
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

            Assert.That(await h.Service.GetUserLog("da"), Is.Empty);
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
