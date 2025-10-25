using System;
using System.Collections;
using System.Collections.Generic;

namespace Ihc.App
{
    /// <summary>
    /// Represents administrator-related editable information from the IHC controller.
    /// This model captures user management, email control, and SMTP notification settings.
    /// Reuses existing models from Ihc namespace (IhcUser, EmailControlSettings, SMTPSettings).
    /// </summary>
    public record AdminModel
    {
        /// <summary>
        /// List of all users registered on the IHC controller.
        /// Each user contains username, password, email, firstname, lastname, phone, group, etc.
        /// Maps to existing IhcUser model array.
        /// </summary>
        public ISet<IhcUser> Users { get; set; }

        /// <summary>
        /// Email control settings for receiving control commands via email (POP3 configuration).
        /// Maps to existing EmailControlSettings model.
        /// </summary>
        public EmailControlSettings EmailControl { get; set; }

        /// <summary>
        /// SMTP settings for sending email notifications from the controller.
        /// Maps to existing SMTPSettings model.
        /// </summary>
        public SMTPSettings SmtpSettings { get; set; }

        /// <summary>
        /// DNS server configuration for the IHC controller network.
        /// Maps to existing DNSServers model.
        /// </summary>
        public DNSServers DnsServers { get; set; }

        /// <summary>
        /// Network settings including IP address, netmask, gateway, and HTTP/HTTPS ports.
        /// Maps to existing NetworkSettings model.
        /// </summary>
        public NetworkSettings NetworkSettings { get; set; }

        /// <summary>
        /// Web access control settings defining which applications can be accessed from different network locations.
        /// Maps to existing WebAccessControl model.
        /// </summary>
        public WebAccessControl WebAccess { get; set; }

        /// <summary>
        /// Wireless LAN settings for the IHC controller network connectivity.
        /// Maps to existing WLanSettings model.
        /// </summary>
        public WLanSettings WLanSettings { get; set; }
    }

     /// <summary>
    /// Internal enumeration representing types of administrative changes.
    /// Not exposed to clients - used internally by AdminService for change tracking.
    /// </summary>
    internal enum AdminChangeType
    {
        /// <summary>
        /// A new user was added to the system.
        /// </summary>
        UserAdded,

        /// <summary>
        /// An existing user's properties were modified.
        /// </summary>
        UserUpdated,

        /// <summary>
        /// A user was removed from the system.
        /// </summary>
        UserDeleted,

        /// <summary>
        /// Email control settings (POP3 configuration) were changed.
        /// </summary>
        EmailControlChanged,

        /// <summary>
        /// SMTP notification settings were changed.
        /// </summary>
        SmtpSettingsChanged,

        /// <summary>
        /// DNS server configuration was changed.
        /// </summary>
        DnsServersChanged,

        /// <summary>
        /// Network settings (IP address, netmask, gateway, ports) were changed.
        /// </summary>
        NetworkSettingsChanged,

        /// <summary>
        /// Web access control settings were changed.
        /// </summary>
        WebAccessChanged,

        /// <summary>
        /// Wireless LAN settings were changed.
        /// </summary>
        WLanSettingsChanged
    }

    /// <summary>
    /// Internal record representing a single administrative change.
    /// Not exposed to clients - used internally by AdminService for change tracking.
    /// </summary>
    internal record AdminChange
    {
        /// <summary>
        /// Type of change that occurred.
        /// </summary>
        public AdminChangeType ChangeType { get; init; }

        /// <summary>
        /// Payload containing the changed data (IhcUser, EmailControlSettings, or SMTPSettings).
        /// </summary>
        public object Payload { get; init; }
    }
}