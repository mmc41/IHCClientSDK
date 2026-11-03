using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ihc
{
    /// <summary>
    /// The controller's time configuration and clock. A write refuses what IHC Administrator refuses: an offset
    /// outside -12 to +14 and, with synchronisation on, an interval under one hour or no server name.
    /// </summary>
    /// <remarks>
    /// Two of the Administrator's checks need the controller and are not made here: it refuses a server that is not a
    /// dotted IPv4 address when the controller has no DNS server, and it proves the server with a time fetch before
    /// saving.
    /// </remarks>
    public record TimeManagerSettings : IValidatableObject
    {
        /// <summary>
        /// <c>true</c>: the controller synchronizes its clock from <see cref="ServerName"/> every
        /// <see cref="SyncIntervalInHours"/>; <c>false</c>: the clock is set by hand.
        /// </summary>
        public bool SynchroniseTimeAgainstServer { get; init; }

        /// <summary>
        /// Indicates whether the controller switches automatically between winter and summer time, one hour apart.
        /// </summary>
        public bool UseDST { get; init; }

        /// <summary>
        /// Time-zone offset in whole hours east of UTC, -12 to +14, summer time excluded (Denmark: 1).
        /// </summary>
        [Range(-12, 14, ErrorMessage = "GmtOffsetInHours must be between -12 and 14.")]
        public int GmtOffsetInHours { get; init; }

        /// <summary>
        /// Host name or dotted IPv4 address of the time server to synchronize with, kept while sync is off too; a host
        /// name needs a DNS server on the controller.
        /// </summary>
        /// <remarks>
        /// No length limit is known: the WSDL declares the name an unrestricted string, the vendor's default
        /// <c>0.schneider.pool.ntp.org</c> has 24 characters, and IHC Administrator's 20 columns are only its field's
        /// width.
        /// </remarks>
        public string? ServerName { get; init; }

        /// <summary>
        /// Interval in hours between time synchronization attempts; at least 1 with sync on.
        /// </summary>
        public int SyncIntervalInHours { get; init; }

        /// <summary>
        /// Current time and date in UTC; on a write with sync off, the clock to set. <c>DateTimeOffset.MinValue</c>
        /// when the reply carries no readable date.
        /// </summary>
        /// <remarks>
        /// The wire carries a bare clock face, which is the controller's UTC clock: a read gives that instant at
        /// offset 0, the same instant <see cref="SystemInfo.Realtimeclock"/> reports, and a write sends the instant's
        /// UTC clock face, whatever offset it is stated at.
        /// </remarks>
        public DateTimeOffset TimeAndDateInUTC { get; init; }

        /// <summary>
        /// Indicates whether the holiday calendar that programs can use is updated online.
        /// </summary>
        public bool OnlineCalendarUpdateOnline { get; init; }

        /// <summary>
        /// Country code for the holiday calendar, such as <c>dk</c>.
        /// </summary>
        public string? OnlineCalendarCountry { get; init; }

        /// <summary>
        /// Until when the stored holiday calendar is valid, as a year such as 2027 rather than a timestamp.
        /// </summary>
        public int OnlineCalendarValidUntil { get; init; }

        /// <summary>
        /// With synchronisation on, the interval must be at least one hour and a server must be named, as IHC
        /// Administrator requires; with it off, both are kept but not used.
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!SynchroniseTimeAgainstServer)
                yield break;
            if (SyncIntervalInHours < 1)
                yield return new ValidationResult("SyncIntervalInHours must be at least 1 when SynchroniseTimeAgainstServer is set.",
                    [nameof(SyncIntervalInHours)]);
            if (string.IsNullOrWhiteSpace(ServerName))
                yield return new ValidationResult("ServerName is required when SynchroniseTimeAgainstServer is set.",
                    [nameof(ServerName)]);
        }

        public override string ToString()
        {
            return $"TimeManagerSettings(SynchroniseTimeAgainstServer={SynchroniseTimeAgainstServer}, UseDST={UseDST}, GmtOffsetInHours={GmtOffsetInHours}, ServerName={ServerName}, SyncIntervalInHours={SyncIntervalInHours}, TimeAndDateInUTC={TimeAndDateInUTC}, OnlineCalendarUpdateOnline={OnlineCalendarUpdateOnline}, OnlineCalendarCountry={OnlineCalendarCountry}, OnlineCalendarValidUntil={OnlineCalendarValidUntil})";
        }
    }

    /// <summary>
    /// The outcome of asking the controller to fetch the time from a time server.
    /// </summary>
    public record TimeServerConnectionResult
    {
        /// <summary>
        /// Indicates whether the controller got a time from the time server.
        /// </summary>
        public bool ConnectionWasSuccessful { get; init; }

        /// <summary>
        /// Date and time received from the time server: the wire value read as Unix milliseconds at offset zero;
        /// <c>DateTimeOffset.MinValue</c> when the controller sends 0 or less.
        /// </summary>
        public DateTimeOffset DateFromServer { get; init; }

        /// <summary>
        /// Indicates whether the server's name could not be resolved, as when the controller has no working DNS server.
        /// </summary>
        public bool ConnectionFailedDueToUnknownHost { get; init; }

        /// <summary>
        /// Indicates whether the connection failed due to other errors.
        /// </summary>
        public bool ConnectionFailedDueToOtherErrors { get; init; }

        public override string ToString()
        {
            return $"TimeServerConnectionResult(ConnectionWasSuccessful={ConnectionWasSuccessful}, DateFromServer={DateFromServer}, ConnectionFailedDueToUnknownHost={ConnectionFailedDueToUnknownHost}, ConnectionFailedDueToOtherErrors={ConnectionFailedDueToOtherErrors})";
        }
    }
}
