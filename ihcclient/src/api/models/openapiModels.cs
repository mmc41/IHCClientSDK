using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /// <summary>
    /// The controller's firmware version as <see cref="IOpenAPIService.GetFWVersion"/> reports it: major, minor and
    /// build numbers.
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
    /// What one <see cref="IOpenAPIService.WaitForEvents"/> call returns: the subscribed resources' value changes and
    /// two controller status fields.
    /// </summary>
    public record EventPackage {
        /// <summary>
        /// The value changes reported, each with its resource's id in <see cref="ResourceValue.ResourceID"/>; never
        /// <c>null</c>. Entries the controller sent without a value are left out.
        /// </summary>
        public ResourceValue[] ResourceValueEvents { get; init; } = Array.Empty<ResourceValue>();

        /// <summary>
        /// Indicates whether the controller execution is currently running. Also <c>false</c> when the reply carries
        /// no package, which the SDK replaces with an empty one.
        /// </summary>
        public bool ControllerExecutionRunning { get; init; }

        /// <summary>
        /// Number of active subscriptions; 0 when the reply carries no package.
        /// </summary>
        public int SubscriptionAmount { get; init; }

        public override string ToString()
        {
          return $"EventPackage(ResourceValueEvents=ResourceValue[{ResourceValueEvents?.Length ?? 0}], ControllerExecutionRunning={ControllerExecutionRunning}, SubscriptionAmount={SubscriptionAmount})";
        }
    }
}