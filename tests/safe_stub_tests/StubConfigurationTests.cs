using System;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The configuration service's single operations answer from typed values, read and written through the real
    /// <see cref="ConfigurationService"/>.
    /// </summary>
    [TestFixture]
    public class StubConfigurationTests
    {
        [Test]
        public async Task TheDefaults_AreARealControllersAnswers()
        {
            using var sdk = StubbedSdk.Start();

            SystemInfo info = await sdk.Configuration.GetSystemInfo();

            Assert.That(info, Is.EqualTo(sdk.Stub.Configuration.SystemInfo));
            Assert.That((info.Brand, info.Version, info.SWDate),
                Is.EqualTo(("LK", "3.3.44", new DateTimeOffset(2021, 6, 23, 10, 0, 0, TimeSpan.Zero))));
            Assert.That(await sdk.Configuration.GetWLanInterface(), Is.EqualTo(new WLanInterface { Name = "wlan0", Ssid = "", Quality = "" }));
            Assert.That(await sdk.Configuration.GetWLanScan(), Is.EqualTo(sdk.Stub.Configuration.WLanScan));
            Assert.That(await sdk.Configuration.TestSendMessage(sdk.Stub.Configuration.SMTPSettings!, "stub@stub.invalid", "Emne", "Tekst"),
                Is.EqualTo(new NotificationSendResult { AttemptFailedDueToNetworkProblems = true }),
                "the captured attempt failed on the network");
        }

        [Test]
        public async Task TheSystemInfo_RoundTripsThroughTheRealWrapper()
        {
            var info = new SystemInfo
            {
                Uptime = 123456789,
                Realtimeclock = new DateTimeOffset(2024, 2, 29, 23, 59, 58, TimeSpan.FromHours(2)),
                SerialNumber = "SN-1",
                ProductionDate = "2024/09",
                Brand = "Schneider",
                Version = "9.9.9",
                HWRevision = "8.0",
                SWDate = new DateTimeOffset(2023, 12, 31, 22, 30, 0, TimeSpan.Zero),
                DatalineVersion = "D-2",
                RFModuleSoftwareVersion = "R-3",
                RFModuleSerialNumber = "RS-4",
                ApplicationIsWithoutViewer = true,
                SmsModemSoftwareVersion = "S-5",
                LedDimmerSoftwareVersion = "L-6",
            };
            var stub = new ControllerStub();
            stub.Configuration.SystemInfo = info;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.GetSystemInfo(), Is.EqualTo(info));
        }

        [Test]
        public async Task TheUserLog_RoundTrips_AndClearingEmptiesIt()
        {
            var stub = new ControllerStub();
            stub.Configuration.UserLog = ["30-10-2026 14:04 Login: stub", "30-10-2026 14:05 Æblegrød øl å"];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That((await sdk.Configuration.GetUserLog("da")).Lines, Is.EqualTo(stub.Configuration.UserLog));
            await sdk.Configuration.ClearUserLog();

            Assert.That(stub.Configuration.UserLog, Is.Empty);
            Assert.That(string.Concat((await sdk.Configuration.GetUserLog("da")).Lines), Is.Empty, "the log the SDK reads has no entries");
        }

        /// <summary>The cursor a read sends reaches the controller, and the cursor arranged for the reply comes back
        /// as the next one.</summary>
        [Test]
        public async Task TheUserLogCursor_IsSent_AndTheArrangedNextOneReturned()
        {
            var stub = new ControllerStub();
            stub.Configuration.UserLog = ["30-10-2026 14:04 Login: stub"];
            stub.Configuration.UserLogCursor = "userlog.42";
            using var sdk = StubbedSdk.Start(stub);

            UserLogFileText log = await sdk.Configuration.GetUserLog("da", "userlog.41");

            Assert.Multiple(() =>
            {
                Assert.That(log.FileName, Is.EqualTo("userlog.42"));
                Assert.That(log.Lines, Is.EqualTo(stub.Configuration.UserLog));
                Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.GetUserLog)).Single().Payload,
                    Is.EqualTo("userlog.41"));
            });
        }

        [Test]
        public async Task TheWLanValues_RoundTripThroughTheRealWrapper()
        {
            var wlan = new WLanInterface { Connected = true, Name = "wlan1", Ssid = "net", Quality = "70/70" };
            WLanCell[] cells =
            [
                new() { Ssid = "a", HasEncryption = false, SecurityType = "", EncryptionType = "" },
                new() { Ssid = "b", HasEncryption = true, SecurityType = "WPA-PSK", EncryptionType = "TKIP" },
            ];
            var stub = new ControllerStub();
            stub.Configuration.WLanInterface = wlan;
            stub.Configuration.WLanScan = [.. cells];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.GetWLanInterface(), Is.EqualTo(wlan));
            Assert.That(await sdk.Configuration.GetWLanScan(), Is.EqualTo(cells));
        }

        [Test]
        public async Task NoWLan_ReadsAsNone()
        {
            var stub = new ControllerStub();
            stub.Configuration.WLanInterface = null;
            stub.Configuration.WLanScan = [];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.GetWLanInterface(), Is.Null);
            Assert.That(await sdk.Configuration.GetWLanScan(), Is.Empty);
        }

        [Test]
        public async Task ATestSend_AnswersTheTypedOutcome_AndIsJournalledWithWhatWasSent()
        {
            var stub = new ControllerStub();
            stub.Configuration.TestSendSucceeds = true;
            var smtp = new SMTPSettings { Hostname = "smtp.stub.invalid", Hostport = 587, Username = "stub", Password = "stub-only", Ssl = true };
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.TestSendMessage(smtp, "stub@stub.invalid", "Emne", "Tekst"),
                Is.EqualTo(new NotificationSendResult { AttemptSucceeded = true }));

            StubCall call = sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.TestSendMessage)).Single();
            Assert.That(call.SoapAction, Is.EqualTo("testSendMessage"));
            Assert.That(call.Payload, Is.EqualTo(new StubTestMail(
                new NotificationMessage
                {
                    NotificationType = "text.notification.email_type", Recipient = "stub@stub.invalid",
                    Sender = "LK IHC® controller<stub>", Subject = "Emne", Body = "Tekst", Delivered = false,
                },
                smtp with { Password = null })), "the account is journalled without its password");
            Assert.That(stub.Configuration.SMTPSettings, Is.Not.EqualTo(smtp), "a test stores nothing");
        }

        [Test]
        public async Task ADelayedReboot_IsJournalledWithItsDelay()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.DelayedReboot(1000);

            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.DelayedReboot)).Select(call => call.Payload),
                Is.EqualTo(new object[] { 1000 }));
        }

        /// <summary>The settings to test reach the controller and the arranged status comes back, while the stored
        /// settings stay as they were.</summary>
        [Test]
        public async Task TestingTheSettingsNow_SendsThem_AndAnswersTheArrangedStatus()
        {
            var stub = new ControllerStub();
            stub.Configuration.EmailControlTestStatus = new EmailControlConnectionStatus { AttemptFailedDueToAuthentication = true };
            var tested = new EmailControlSettings
            {
                ServerAddress = "pop3.stub.invalid", ServerPortNumber = 995, Pop3Username = "stub", Pop3Password = "stub-only",
                EmailAddress = "stub@stub.invalid", PollInterval = 60, RemoveEmailsAfterUsage = false, Ssl = true,
            };
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.TestSettingsNow(tested), Is.EqualTo(stub.Configuration.EmailControlTestStatus));

            StubCall call = sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.TestSettingsNow)).Single();
            Assert.That((call.Outcome, call.Payload), Is.EqualTo((StubOutcome.Typed, (object?)(tested with { Pop3Password = null }))),
                "the tested settings are journalled without their password");
            Assert.That(stub.Configuration.EmailControlSettings, Is.Not.EqualTo(tested), "a test stores nothing");
        }

        [Test]
        public void TheUnsentTestSendMessageOverload_StaysAShapeRow()
        {
            Assert.That(SdkOperations.Find("ConfigurationService", "testSendMessage")!.AnswerKind, Is.EqualTo(StubAnswerKind.Typed));
            Assert.That(SdkOperations.Find("ConfigurationService", "testSendMessage1")!.AnswerKind, Is.EqualTo(StubAnswerKind.Shape));
        }

        [Test]
        public void AnUninitializedList_IsRefused()
        {
            var stub = new ControllerStub();

            Assert.That(() => stub.Configuration.UserLog = default, Throws.ArgumentException);
        }
    }
}
