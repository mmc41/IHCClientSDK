using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The controller service's SD card, S0 and restore operations answer from typed values, read and written
    /// through the real <see cref="ControllerService"/>.
    /// </summary>
    [TestFixture]
    public class StubControllerValueTests
    {
        [Test]
        public async Task TheDefaults_AreARealControllersAnswers()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Controller.GetSDCardInfo(), Is.EqualTo(new SDInfo { Size = 479690752, Free = 468685824 }));
            Assert.That(await sdk.Controller.GetS0MeterValue(), Is.Zero);
            Assert.That(await sdk.Controller.Restore(), Is.Zero);
        }

        [Test]
        public async Task TheSdCard_RoundTripsThroughTheRealWrapper()
        {
            var card = new SDInfo { Size = 2_000_000_000L * 4, Free = 12345 };
            var stub = new ControllerStub();
            stub.Controller.SdCard = card;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetSDCardInfo(), Is.EqualTo(card));
        }

        [Test]
        public async Task AControllerReportingNoSdCard_ReadsAsNone()
        {
            var stub = new ControllerStub();
            stub.Controller.SdCard = null;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetSDCardInfo(), Is.Null);
        }

        /// <summary>The reset's five period flags reach the stub as sent, hour first, and are journalled. Zeroing the
        /// reading is the stub's stand-in effect: what a controller resets per flag is not known.</summary>
        [Test]
        public async Task TheS0MeterValue_RoundTrips_AndAResetZeroesIt_JournallingItsFlags()
        {
            var stub = new ControllerStub();
            stub.Controller.S0MeterValue = 42.5f;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetS0MeterValue(), Is.EqualTo(42.5f));
            await sdk.Controller.ResetS0Values(hour: true, day: false, week: false, month: true, year: false);
            Assert.That(await sdk.Controller.GetS0MeterValue(), Is.Zero);
            Assert.That(sdk.Stub.Journal.Calls<IControllerService>(nameof(IControllerService.ResetS0Values)).Single().Payload,
                Is.EqualTo(new S0ResetFlags(Hour: true, Day: false, Week: false, Month: true, Year: false)));
        }

        [Test]
        public async Task SettingTheS0Consumption_IsKeptAsSent()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Controller.SetS0Consumption(11.0f, true);

            Assert.That(sdk.Stub.Controller.S0Consumption, Is.EqualTo((11.0f, true)));
        }

        /// <summary>The stub reads the parts as a controller does, the day first: a fiscal year from 1 March goes out
        /// as day 1, month 3, through the real wrapper and serializer.</summary>
        [Test]
        public async Task SettingTheS0FiscalYearStart_SendsTheDayFirst()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            await sdk.Controller.SetS0FiscalYearStart(month: 3, day: 1);

            Assert.That(stub.Controller.S0FiscalYearStart, Is.EqualTo(((sbyte)1, (sbyte)3)));
        }

        [Test]
        public async Task ARestore_AnswersTheTypedResult_AndIsJournalled()
        {
            var stub = new ControllerStub();
            stub.Controller.RestoreResult = 7;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.Restore(), Is.EqualTo(7));
            Assert.That(sdk.Stub.Journal.Calls<IControllerService>(nameof(IControllerService.Restore)).Select(call => (call.SoapAction, call.Outcome)),
                Is.EqualTo(new[] { ("restore", StubOutcome.Typed) }));
        }
    }
}
