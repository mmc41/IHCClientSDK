using System;

namespace Ihc.App
{
    /// <summary>
    /// Represents non-editable information from the IHC controller administrator interface.
    /// This model captures read-only system information including network, DNS, time, and access control settings.
    /// Reuses existing models from Ihc namespace (NetworkSettings, DNSServers, TimeManagerSettings, WebAccessControl).
    /// </summary>
    public record InformationModel
    {
        /// <summary>
        /// Network configuration information (IP address, ports, subnet mask, gateway).
        /// Maps to existing NetworkSettings model.
        /// </summary>
        public NetworkSettings Network { get; init; }

        /// <summary>
        /// DNS server configuration information (primary and secondary DNS).
        /// Maps to existing DNSServers model.
        /// </summary>
        public DNSServers Dns { get; init; }

        /// <summary>
        /// Time and date configuration information (timezone, DST, time sync, country, validity).
        /// Maps to existing TimeManagerSettings model.
        /// </summary>
        public TimeManagerSettings Time { get; init; }

        /// <summary>
        /// Access control settings for different connection types (USB, internal network, external network).
        /// Maps to existing WebAccessControl model.
        /// </summary>
        public WebAccessControl AccessControl { get; init; }

        public override string ToString()
        {
            return $"InformationModel(Network={Network}, Dns={Dns}, Time={Time}, AccessControl={AccessControl})";
        }
    }
}