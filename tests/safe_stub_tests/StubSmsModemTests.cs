using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The SMS modem answers from typed values, read through the real <see cref="SmsModemService"/>: its
    /// information and status, one settings property a set replaces, and a reset seen only in the journal.
    /// </summary>
    [TestFixture]
    public class StubSmsModemTests
    {
        [Test]
        public async Task TheInfoAndStatus_AreTheScenarios()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.SmsModem.GetSmsModemInfo(), Is.EqualTo(stub.SmsModem.Info));
            Assert.That(await sdk.SmsModem.GetSmsModemStatus(), Is.EqualTo(stub.SmsModem.Status));
        }

        [Test]
        public async Task InfoAndStatusSetOnTheScenario_AreWhatTheSdkReads()
        {
            var stub = new ControllerStub();
            var info = new SmsModemInfo
            {
                FirmwareVersion = "1.2.0",
                GSMChipVersion = "SIM800",
                HardwareRevision = "3",
                ProductionDate = "2021/07",
                Detected = false,
                SerialNumber = "SN-1",
                IMEINumber = "IMEI-1",
            };
            var status = new SmsModemStatus
            {
                AntennaCoverage = "text.sms_modem.antenna_coverage.low",
                MobileOperator = "Operator",
                ModemStatus = "text.sms_modem.no_sim",
                MobileNumber = "11111111",
            };
            stub.SmsModem.Info = info;
            stub.SmsModem.Status = status;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.SmsModem.GetSmsModemInfo(), Is.EqualTo(info));
            Assert.That(await sdk.SmsModem.GetSmsModemStatus(), Is.EqualTo(status));
        }

        [Test]
        public async Task SetSettings_ReadsBack_AndIsJournaled()
        {
            using var sdk = StubbedSdk.Start();
            var changed = new SmsModemSettings
            {
                PowerupMessage = "Strøm tilbage",
                PowerdownMessage = "Strøm væk",
                RecipientSlots = "000000000000000000000001000000",
                RelaySMS = true,
                ForceStandAloneMode = true,
                SendLowBatteryNotification = true,
                SendLowBatteryNotificationLanguage = true,
                SendLEDDimmerErrorNotification = true,
            };

            await sdk.SmsModem.SetSmsModemSettings(changed);

            Assert.That(await sdk.SmsModem.GetSmsModemSettings(), Is.EqualTo(changed));
            Assert.That(sdk.Stub.Journal.Calls<ISmsModemService>(nameof(ISmsModemService.SetSmsModemSettings)).Single().Payload, Is.EqualTo(changed));
        }

        [Test]
        public async Task TheDefaultSettings_AreTheScenarios()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.SmsModem.GetSmsModemSettings(), Is.EqualTo(stub.SmsModem.Settings));
        }

        /// <summary>A reset changes nothing the stub answers from, so the journal is where a test sees it.</summary>
        [Test]
        public async Task AReset_IsJournaled_AndChangesNothing()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);
            SmsModemSettings before = stub.SmsModem.Settings;

            await sdk.SmsModem.ResetSmsModem();

            Assert.That(sdk.Stub.Journal.Calls<ISmsModemService>(nameof(ISmsModemService.ResetSmsModem)), Has.Count.EqualTo(1));
            Assert.That(await sdk.SmsModem.GetSmsModemSettings(), Is.EqualTo(before));
        }
    }
}
