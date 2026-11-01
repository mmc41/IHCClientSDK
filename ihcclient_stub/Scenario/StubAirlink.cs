using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The wireless (RF) devices: the detected-device list, the devices low on battery, and the detections and test
    /// results a wait has not reported yet. Each is the scenario's to set: reporting a detection or a test result
    /// changes no list, so a test states every answer it expects.
    /// </summary>
    public sealed class StubAirlink
    {
        private readonly Lock _gate;

        internal StubAirlink(Lock gate)
        {
            _gate = gate;
            Detections = new PendingQueue<RFDevice>(gate);
            TestResultQueue = new PendingQueue<RFDevice>(gate);
        }

        /// <summary>The devices the controller has detected; none by default.</summary>
        public ImmutableArray<RFDevice> DetectedDevices
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } = [];

        /// <summary>The resource ids of the devices low on battery. The default is the captured controller's answer
        /// with no RF device installed, a single -1.</summary>
        public ImmutableArray<int> LowBattery
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } = [-1];

        /// <summary>Detections no wait has reported yet, oldest first. Setting it wakes a wait that is holding.</summary>
        public ImmutableList<RFDevice> PendingDetections
        {
            get => Detections.Items;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Detections.Replace(value);
            }
        }

        /// <summary>Test results no wait has reported yet, oldest first. Setting it wakes a wait that is holding.</summary>
        public ImmutableList<RFDevice> TestResults
        {
            get => TestResultQueue.Items;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                TestResultQueue.Replace(value);
            }
        }

        /// <summary>Queues <paramref name="device"/> as detected, for the next detection wait to report.</summary>
        public void Detect(RFDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            Detections.Add([device]);
        }

        /// <summary>Queues <paramref name="device"/> as a test result, for the next test-result wait to report.</summary>
        public void ReportTestResult(RFDevice device)
        {
            ArgumentNullException.ThrowIfNull(device);
            TestResultQueue.Add([device]);
        }

        /// <summary>Detections no wait has reported yet.</summary>
        internal PendingQueue<RFDevice> Detections { get; }

        /// <summary>Test results no wait has reported yet.</summary>
        internal PendingQueue<RFDevice> TestResultQueue { get; }
    }
}
