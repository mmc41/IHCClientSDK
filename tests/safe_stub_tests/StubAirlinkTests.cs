using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The wireless device management answers from typed values, read through the real
    /// <see cref="AirlinkManagementService"/>: two lists, journaled RF actions, and two queues the waits answer from
    /// by the long-poll rule.
    /// </summary>
    [TestFixture]
    public class StubAirlinkTests
    {
        private static readonly RFDevice Device = new()
        {
            BatteryLevel = 3,
            SignalStrength = -61,
            DeviceType = 4352,
            SerialNumber = 0x0123_4567_89AB,
            Version = 12,
            Detected = true,
        };

        private const string Detections = "detections";

        private const string TestResults = "test results";

        /// <summary>A queue the scenario fills, and the wait that reports from it.</summary>
        private sealed record Queue(Action<StubAirlink, RFDevice> Report, Func<StubbedSdk, int, Task<RFDevice?>> Wait);

        private static readonly Dictionary<string, Queue> QueuesByName = new()
        {
            [Detections] = new((airlink, device) => airlink.Detect(device), (sdk, timeout) => sdk.Airlink.WaitForDeviceDetected(timeout)),
            [TestResults] = new((airlink, device) => airlink.ReportTestResult(device), (sdk, timeout) => sdk.Airlink.WaitForDeviceTestResult(timeout)),
        };

        [TestCase(Detections)]
        [TestCase(TestResults)]
        public async Task AQueuedItem_IsAnsweredAtOnce_OldestFirst_AndOnlyOnce(string name)
        {
            Queue queue = QueuesByName[name];
            var stub = new ControllerStub();
            RFDevice second = Device with { SerialNumber = 42 };
            queue.Report(stub.Airlink, Device);
            queue.Report(stub.Airlink, second);
            using var sdk = StubbedSdk.Start(stub);

            var clock = Stopwatch.StartNew();
            RFDevice? first = await queue.Wait(sdk, 10);
            RFDevice? next = await queue.Wait(sdk, 10);
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)), "nothing was held");
            RFDevice? none = await queue.Wait(sdk, 0);

            Assert.That((first, next, none), Is.EqualTo((Device, second, (RFDevice?)null)));
        }

        [TestCase(Detections)]
        [TestCase(TestResults)]
        public async Task AnIdleWait_HoldsAtMostOneSecond_ThenAnswersNone(string name)
        {
            Queue queue = QueuesByName[name];
            using var sdk = StubbedSdk.Start();

            var clock = Stopwatch.StartNew();
            RFDevice? reported = await queue.Wait(sdk, 15);

            Assert.That(reported, Is.Null);
            Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)).And.LessThan(TimeSpan.FromSeconds(10)));
        }

        [TestCase(Detections)]
        [TestCase(TestResults)]
        public async Task AnItemQueuedWhileAWaitHolds_WakesIt(string name)
        {
            Queue queue = QueuesByName[name];
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            var clock = Stopwatch.StartNew();
            Task<RFDevice?> wait = queue.Wait(sdk, 1);
            await Task.Delay(100);
            queue.Report(stub.Airlink, Device);

            Assert.That(await wait, Is.EqualTo(Device));
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)), "the wait answered when the item arrived");
        }

        /// <summary>The two queues are separate: a detection is no test result.</summary>
        [Test]
        public async Task TheQueues_AreSeparate()
        {
            var stub = new ControllerStub();
            stub.Airlink.PendingDetections = [Device];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Airlink.WaitForDeviceTestResult(0), Is.Null);
            Assert.That(stub.Airlink.PendingDetections, Is.EqualTo(new[] { Device }));
            Assert.That(stub.Airlink.TestResults, Is.Empty);

            RFDevice tested = Device with { SignalStrength = -40 };
            stub.Airlink.TestResults = [tested];
            Assert.That(await sdk.Airlink.WaitForDeviceTestResult(0), Is.EqualTo(tested));
            Assert.That(stub.Airlink.PendingDetections, Is.EqualTo(new[] { Device }), "reporting a test result takes no detection");
        }

        [Test]
        public async Task TheLists_AreTheScenarios()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.Airlink.GetDetectedDeviceList(), Is.Empty);
            Assert.That(await sdk.Airlink.GetDevicesRunningOutOfBattery(), Is.EqualTo(new[] { -1 }), "the captured controller's answer");

            stub.Airlink.DetectedDevices = [Device];
            stub.Airlink.LowBattery = [41001, 41002];
            Assert.That(await sdk.Airlink.GetDetectedDeviceList(), Is.EqualTo(new[] { Device }));
            Assert.That(await sdk.Airlink.GetDevicesRunningOutOfBattery(), Is.EqualTo(new[] { 41001, 41002 }));
        }

        /// <summary>The RF modes keep no flag, so the journal is where a test sees each mode change and each test.</summary>
        [Test]
        public async Task TheRfActions_AreAcknowledged_AndJournaled()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.Airlink.EnterRFConfiguration(), Is.True);
            Assert.That(await sdk.Airlink.EnterRFTest(), Is.True);
            Assert.That(await sdk.Airlink.TestRFActuatorWithSerialNumber(Device.SerialNumber), Is.True);
            Assert.That(await sdk.Airlink.ExitRFTest(), Is.True);
            Assert.That(await sdk.Airlink.ExitRFConfiguration(), Is.True);

            Assert.That(sdk.Stub.Journal.All.Where(call => call.Service == "AirlinkManagementService").Select(call => call.SdkMethod), Is.EqualTo(new[]
            {
                nameof(IAirlinkManagementService.EnterRFConfiguration),
                nameof(IAirlinkManagementService.EnterRFTest),
                nameof(IAirlinkManagementService.TestRFActuatorWithSerialNumber),
                nameof(IAirlinkManagementService.ExitRFTest),
                nameof(IAirlinkManagementService.ExitRFConfiguration),
            }));
            Assert.That(sdk.Stub.Journal.Calls<IAirlinkManagementService>(nameof(IAirlinkManagementService.TestRFActuatorWithSerialNumber)).Single().Payload,
                Is.EqualTo(Device.SerialNumber));
        }
    }
}
