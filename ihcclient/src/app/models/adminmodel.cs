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

        /*
        public DNSServers DnsServers { get; set; }
        public InetAddress IPAddress { get; set; }

        public WebAccessControl WebAccess { get; set; }

        public WLanSettings WLanSettings { get; set; }
        */
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
        SmtpSettingsChanged
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