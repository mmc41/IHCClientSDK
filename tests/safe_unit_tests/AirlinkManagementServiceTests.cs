using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Ihc.Soap.Airlinkmanagement;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// <see cref="AirlinkManagementService.GetDevicesRunningOutOfBattery"/> against a faked SOAP layer. The controller's
    /// list of low-battery devices is the one reply array whose items are not nillable, so it cannot answer "none" with
    /// the lone nil item the other empty lists use: it answers a list holding only -1, captured twice from a controller
    /// with no low device and read as "no device" by ServiceView.
    /// </summary>
    [TestFixture]
    public class AirlinkManagementServiceTests
    {
        private static Task<IReadOnlyList<int>> LowBatteryAnswering(int[]? ids)
        {
            var soap = A.Fake<Ihc.Soap.Airlinkmanagement.AirlinkManagementService>();
            A.CallTo(() => soap.getDevicesRunningOutOfBatteryAsync(A<inputMessageName4>._))
                .Returns(Task.FromResult(new outputMessageName4(ids)));
            return new AirlinkManagementService(FakeSession.Over(), soap).GetDevicesRunningOutOfBattery();
        }

        [Test]
        public async Task TheControllersNoneSentinel_ReadsAsAnEmptyList()
        {
            Assert.That(await LowBatteryAnswering([-1]), Is.Empty);
        }

        [TestCase(new[] { 5 })]
        [TestCase(new[] { 41001, 41002 })]
        public async Task RealIds_AreReturnedAsSent(int[] ids)
        {
            Assert.That(await LowBatteryAnswering(ids), Is.EqualTo(ids));
        }

        [Test]
        public async Task NoListAtAll_ReadsAsAnEmptyList()
        {
            Assert.That(await LowBatteryAnswering(null), Is.Empty);
        }
    }
}
