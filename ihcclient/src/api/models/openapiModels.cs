using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /// <summary>
    /// High level model of IHC firmware version information without soap distractions.
    /// </summary>
    public record FWVersion {
        /// <summary>
        /// Major version number.
        /// </summary>
        public int MajorVersion { get; init; }

        /// <summary>
        /// Minor version number.
        /// </summary>
        public int MinorVersion { get; init; }

        /// <summary>
        /// Build version number.
        /// </summary>
        public int BuildVersion { get; init; }

        public override string ToString()
        {
          return $"FWVersion(MajorVersion={MajorVersion}, MinorVersion={MinorVersion}, BuildVersion={BuildVersion})";
        }
    }

    /// <summary>
    /// High level model of an event package containing resource value changes without soap distractions.
    /// </summary>
    public record EventPackage {
        /// <summary>
        /// Array of resource value change events.
        /// </summary>
        public ResourceValue[] ResourceValueEvents { get; init; }

        /// <summary>
        /// Indicates whether the controller execution is currently running.
        /// </summary>
        public bool ControllerExecutionRunning { get; init; }

        /// <summary>
        /// Number of active subscriptions.
        /// </summary>
        public int SubscriptionAmount { get; init; }

        public override string ToString()
        {
          return $"EventPackage(ResourceValueEvents=ResourceValue[{ResourceValueEvents?.Length ?? 0}], ControllerExecutionRunning={ControllerExecutionRunning}, SubscriptionAmount={SubscriptionAmount})";
        }
    }
}