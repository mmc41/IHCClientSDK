using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Authentication;
using System.Text;

namespace Ihc {
    /**
     * High level model of a IHC WS version Info without soap distractions.
     */
    public record FWVersion {
        public int MajorVersion { get; init; }

        public int MinorVersion { get; init; }

        public int BuildVersion { get; init; }
    }

    /**
     * High level model of event package without soap distractions.
     */
    public record EventPackage {
        public ResourceValue[] ResourceValueEvents { get; init; }
        public bool ControllerExecutionRunning { get; init; }
        public int SubscriptionAmount { get; init; }
    }
}