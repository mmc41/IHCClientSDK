using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ihc.App
{
    /// <summary>
    /// Contains administrator-related editable information from the IHC controller
    /// which, can be set/read via the AdminService.
    /// Reuses existing models from Ihc namespace.
    /// Note that this is a mutable model unlike most other Ihc models.
    /// </summary>
    public record MutableAdminModel
    {
        /// <summary>
        /// Serialization metadata.
        /// </summary>
        public ModelMetadata ModelMetadata { get; set; }

        /// <summary>
        /// List of all users registered on the IHC controller.
        /// </summary>
        public ISet<IhcUser> Users { get; set; }

        /// <summary>
        /// Email control settings for receiving control commands via email (POP3 configuration).
        /// </summary>
        public EmailControlSettings EmailControl { get; set; }

        /// <summary>
        /// SMTP settings for sending email notifications from the controller.
        /// </summary>
        public SMTPSettings SmtpSettings { get; set; }

        /// <summary>
        /// DNS server configuration for the IHC controller network.
        /// </summary>
        public DNSServers DnsServers { get; set; }

        /// <summary>
        /// Network settings including IP address, netmask, gateway, and HTTP/HTTPS ports.
        /// </summary>
        public NetworkSettings NetworkSettings { get; set; }

        /// <summary>
        /// Web access control settings defining which applications can be accessed from different network locations.
        /// </summary>
        public WebAccessControl WebAccess { get; set; }

        /// <summary>
        /// Wireless LAN settings for the IHC controller network connectivity.
        /// </summary>
        public WLanSettings WLanSettings { get; set; }

        /// <summary>
        /// This default ToString method should not be used! Use alternative with bool parameter.
        /// </summary>
        public override string ToString()
        {
            return this.ToString(true); // Unsecure - will output sensitive data
        }

        /// <summary>
        /// Safely convert to string. Only convert sensitive data if LogSensitiveData set to true.
        /// </summary>
        public string ToString(bool LogSensitiveData)
        {
            var usersInfo = Users != null
                ? $"[{string.Join(", ", Users.Select(u => u.ToString(LogSensitiveData)))}]"
                : "null";
            var emailControlInfo = EmailControl?.ToString(LogSensitiveData) ?? "null";
            var smtpInfo = SmtpSettings?.ToString(LogSensitiveData) ?? "null";
            var wlanInfo = WLanSettings?.ToString(LogSensitiveData) ?? "null";

            return $"AdminModel(Users={usersInfo}, EmailControl={emailControlInfo}, SmtpSettings={smtpInfo}, DnsServers={DnsServers}, NetworkSettings={NetworkSettings}, WebAccess={WebAccess}, WLanSettings={wlanInfo})";
        }

        /// <summary>
        /// Creates a shallow copy of the current AdminModel instance (only top-level properties as they are all immutable).
        /// </summary>
        /// <returns>A copy</returns>
        public MutableAdminModel Copy()
        {
            return new MutableAdminModel
            {
                ModelMetadata = this.ModelMetadata,
                Users = this.Users != null ? new HashSet<IhcUser>(this.Users) : null,
                EmailControl = this.EmailControl,
                SmtpSettings = this.SmtpSettings,
                DnsServers = this.DnsServers,
                NetworkSettings = this.NetworkSettings,
                WebAccess = this.WebAccess,
                WLanSettings = this.WLanSettings
            };
        }
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