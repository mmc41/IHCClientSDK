using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Ihc.Stubbing;
using NUnit.Framework;

namespace Ihc.Tests
{
    /// <summary>
    /// The LED dimmer management service answers from typed values, read through the real
    /// <see cref="LedDimmerManagementService"/>: a device list, a detection queue a wait answers from by the
    /// long-poll rule, a journaled ID assignment, and a firmware upgrade that finishes as it starts.
    /// </summary>
    [TestFixture]
    public class StubLedDimmerTests
    {
        private static readonly LedDimmerInfo Detected = new()
        {
            Location = "Køkken",
            Channel = 1,
            BootloaderVersion = "1.0",
            ApplicationVersion = "DAP.T.01.01.37",
            ApplicationStatus = 1,
            HardwareVersion = "2",
            SerialNumber = "LD-0001",
            Level = 80,
            ErrorFlags = 4,
            ChannelID = 7,
        };

        [Test]
        public async Task TheDeviceList_AndItsCount_AreTheScenarios()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.LedDimmers.GetDeviceList(), Is.EqualTo(stub.LedDimmers.Devices));
            Assert.That(await sdk.LedDimmers.GetDeviceCount(), Is.EqualTo(2));

            stub.LedDimmers.Devices = [Detected];
            Assert.That(await sdk.LedDimmers.GetDeviceList(), Is.EqualTo(new[] { Detected }));
            Assert.That(await sdk.LedDimmers.GetDeviceCount(), Is.EqualTo(1));
        }

        [Test]
        public async Task Configuration_IsEnteredAndExited()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.LedDimmers.EnterConfiguration(), Is.True);
            Assert.That(await sdk.LedDimmers.ExitConfiguration(), Is.True);
        }

        [Test]
        public async Task AQueuedDetection_IsAnsweredAtOnce_AndOnlyOnce()
        {
            var stub = new ControllerStub();
            stub.LedDimmers.Detect(Detected);
            using var sdk = StubbedSdk.Start(stub);

            var clock = Stopwatch.StartNew();
            Assert.That(await sdk.LedDimmers.WaitForDeviceDetected(10), Is.EqualTo(Detected));
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)));
            Assert.That(await sdk.LedDimmers.WaitForDeviceDetected(0), Is.Null);
            Assert.That(stub.LedDimmers.PendingDetections, Is.Empty);
        }

        [Test]
        public async Task QueuedDetections_AreAnsweredOldestFirst()
        {
            var stub = new ControllerStub();
            LedDimmerInfo second = Detected with { SerialNumber = "LD-0002" };
            stub.LedDimmers.PendingDetections = [Detected, second];
            using var sdk = StubbedSdk.Start(stub);

            Assert.That(await sdk.LedDimmers.WaitForDeviceDetected(1), Is.EqualTo(Detected));
            Assert.That(await sdk.LedDimmers.WaitForDeviceDetected(1), Is.EqualTo(second));
        }

        /// <summary>An idle wait holds for the client's timeout capped at one second, then answers none.</summary>
        [Test]
        public async Task AnIdleWait_HoldsAtMostOneSecond_ThenAnswersNone()
        {
            using var sdk = StubbedSdk.Start();

            var clock = Stopwatch.StartNew();
            LedDimmerInfo? detected = await sdk.LedDimmers.WaitForDeviceDetected(15);

            Assert.That(detected, Is.Null);
            Assert.That(clock.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900)).And.LessThan(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public async Task ADetectionWhileAWaitHolds_WakesIt()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);

            var clock = Stopwatch.StartNew();
            Task<LedDimmerInfo?> wait = sdk.LedDimmers.WaitForDeviceDetected(1);
            await Task.Delay(100);
            stub.LedDimmers.Detect(Detected);

            Assert.That(await wait, Is.EqualTo(Detected));
            Assert.That(clock.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(900)), "the wait answered when the detection arrived");
        }

        [Test]
        public async Task AssignID_IsAcknowledged_AndJournaled()
        {
            using var sdk = StubbedSdk.Start();

            Assert.That(await sdk.LedDimmers.AssignID("LD-0001", 1, 7), Is.True);

            Assert.That(sdk.Stub.Journal.Calls<ILedDimmerManagementService>(nameof(ILedDimmerManagementService.AssignID)).Single().Payload,
                Is.EqualTo((SerialNumber: "LD-0001", Channel: (sbyte?)1, ChannelID: (sbyte?)7)));
        }

        /// <summary>A real client polls the progress while it is running, so an upgrade that finishes as it starts
        /// ends a positive flow on its first poll.</summary>
        [Test]
        public async Task AFirmwareUpgrade_IsJournaled_AndFinishesAtOnce()
        {
            var stub = new ControllerStub();
            using var sdk = StubbedSdk.Start(stub);
            Assert.That(await sdk.LedDimmers.GetFirmwareUpgradeProgress(), Is.Null, "no upgrade has run");

            await sdk.LedDimmers.StartFirmwareUpgrade("LD-0001");

            LedDimmerProgress? progress = await sdk.LedDimmers.GetFirmwareUpgradeProgress();
            Assert.That(progress, Is.EqualTo(stub.LedDimmers.FirmwareProgress));
            Assert.That((progress!.SerialNumber, progress.Status, progress.Progress, progress.Maximum),
                Is.EqualTo(("LD-0001", progress.Finished, 100, 100)));
            Assert.That((progress.Running, progress.Finished, progress.Failed), Is.EqualTo(("RUNNING", "FINISHED", "FAILED")));
            Assert.That(sdk.Stub.Journal.Calls<ILedDimmerManagementService>(nameof(ILedDimmerManagementService.StartFirmwareUpgrade)).Single().Payload,
                Is.EqualTo("LD-0001"));
        }

        /// <summary>No serial means every dimmer, as the vendor's Administrator sends it: the part goes out nil and
        /// reaches the controller as no serial.</summary>
        [Test]
        public async Task AFirmwareUpgradeOfEveryDimmer_SendsNoSerial()
        {
            using var sdk = StubbedSdk.Start();

            await sdk.LedDimmers.StartFirmwareUpgrade(null);

            StubCall call = sdk.Stub.Journal.Calls<ILedDimmerManagementService>(nameof(ILedDimmerManagementService.StartFirmwareUpgrade)).Single();
            Assert.That((call.Outcome, call.Payload), Is.EqualTo((StubOutcome.Typed, (object?)null)));
        }

        [Test]
        public void AnUninitialisedDeviceList_IsRefused()
        {
            var stub = new ControllerStub();

            Assert.That(() => stub.LedDimmers.Devices = default(ImmutableArray<LedDimmerInfo>), Throws.ArgumentException);
        }
    }
}
