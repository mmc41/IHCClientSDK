using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The LED dimmers: the device list, detections a wait has not reported yet, and the firmware upgrade's
    /// progress. The defaults are a real controller's answers.
    /// </summary>
    public sealed class StubLedDimmers
    {
        /// <summary>The upgrade's status names, which a progress reply carries beside its status so a client can
        /// compare against them. A client polls while the status is the running one.</summary>
        private const string Running = "RUNNING";

        private const string Finished = "FINISHED";

        private const string Failed = "FAILED";

        private readonly Lock _gate;

        internal StubLedDimmers(Lock gate)
        {
            _gate = gate;
            Detections = new PendingQueue<LedDimmerInfo>(gate);
        }

        /// <summary>The dimmers the controller knows. The default is the captured controller's two unconfigured
        /// channels, which report no versions.</summary>
        public ImmutableArray<LedDimmerInfo> Devices
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } = [Unconfigured(0), Unconfigured(1)];

        /// <summary>Detections no wait has reported yet, oldest first. Setting it wakes a wait that is holding.</summary>
        public ImmutableList<LedDimmerInfo> PendingDetections
        {
            get => Detections.Items;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Detections.Replace(value);
            }
        }

        /// <summary>The upgrade's progress, or null for none, as the captured controller answers before any upgrade.
        /// Starting an upgrade sets it to finished.</summary>
        public LedDimmerProgress? FirmwareProgress
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>Queues <paramref name="device"/> as detected, for the next wait to report.</summary>
        public void Detect(LedDimmerInfo device)
        {
            ArgumentNullException.ThrowIfNull(device);
            Detections.Add([device]);
        }

        /// <summary>Detections no wait has reported yet.</summary>
        internal PendingQueue<LedDimmerInfo> Detections { get; }

        /// <summary>Completes an upgrade at once, so a client's poll sees it finished on its first read.</summary>
        internal void CompleteUpgrade(string? serialNumber) => FirmwareProgress = new LedDimmerProgress
        {
            Message = "",
            SerialNumber = serialNumber,
            Status = Finished,
            Progress = 100,
            Maximum = 100,
            Running = Running,
            Finished = Finished,
            Failed = Failed,
        };

        private static LedDimmerInfo Unconfigured(sbyte channel) => new()
        {
            Location = "",
            Channel = channel,
            BootloaderVersion = "?",
            ApplicationVersion = "?",
            ApplicationStatus = -1,
            HardwareVersion = "?",
            SerialNumber = "",
        };
    }
}
