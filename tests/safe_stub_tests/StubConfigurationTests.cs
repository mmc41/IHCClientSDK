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
            Assert.That(await sdk.Configuration.TestSendMessage("stub@stub.invalid", "Emne", "Tekst"), Is.False,
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

            Assert.That(await sdk.Configuration.GetUserLog("da"), Is.EqualTo(stub.Configuration.UserLog));
            await sdk.Configuration.ClearUserLog();

            Assert.That(stub.Configuration.UserLog, Is.Empty);
            Assert.That(string.Concat(await sdk.Configuration.GetUserLog("da")), Is.Empty, "the log the SDK reads has no entries");
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
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Configuration.TestSendMessage("stub@stub.invalid", "Emne", "Tekst"), Is.True);

            StubCall call = sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.TestSendMessage)).Single();
            Assert.That(call.SoapAction, Is.EqualTo("testSendMessage1"));
            Assert.That(call.Payload, Is.EqualTo(new NotificationMessage { Recipient = "stub@stub.invalid", Subject = "Emne", Body = "Tekst" }));
        }

        [Test]
        public async Task ADelayedReboot_IsJournalledWithItsDelay()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.DelayedReboot(1000);

            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.DelayedReboot)).Select(call => call.Payload),
                Is.EqualTo(new object[] { 1000 }));
        }

        [Test]
        public async Task TestingTheSettingsNow_AnswersTyped()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Configuration.TestSettingsNow();

            Assert.That(sdk.Stub.Journal.Calls<IConfigurationService>(nameof(IConfigurationService.TestSettingsNow)).Single().Outcome,
                Is.EqualTo(StubOutcome.Typed));
        }

        [Test]
        public void TheTestSendMessageOverload_StaysAShapeRow()
        {
            Assert.That(SdkOperations.Find("ConfigurationService", "testSendMessage")!.AnswerKind, Is.EqualTo(StubAnswerKind.Shape));
            Assert.That(SdkOperations.Find("ConfigurationService", "testSendMessage1")!.AnswerKind, Is.EqualTo(StubAnswerKind.Typed));
        }

        [Test]
        public void AnUninitializedList_IsRefused()
        {
            var stub = new ControllerStub();

            Assert.That(() => stub.Configuration.UserLog = default, Throws.ArgumentException);
        }
    }
}
