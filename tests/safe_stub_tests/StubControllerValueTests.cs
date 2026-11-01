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

        [Test]
        public async Task TheS0MeterValue_RoundTrips_AndAResetZeroesIt()
        {
            var stub = new ControllerStub();
            stub.Controller.S0MeterValue = 42.5f;
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Controller.GetS0MeterValue(), Is.EqualTo(42.5f));
            await sdk.Controller.ResetS0Values();
            Assert.That(await sdk.Controller.GetS0MeterValue(), Is.Zero);
        }

        [Test]
        public async Task SettingTheS0Consumption_IsKeptAsSent()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.Controller.SetS0Consumption(11.0f, true);

            Assert.That(sdk.Stub.Controller.S0Consumption, Is.EqualTo((11.0f, true)));
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
