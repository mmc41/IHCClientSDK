using System;
using System.ComponentModel.DataAnnotations;

namespace Ihc
{
    /// <summary>
    /// The controller's time configuration and clock.
    /// </summary>
    public record TimeManagerSettings
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
        public int GmtOffsetInHours { get; init; }

        /// <summary>
        /// Host name or dotted IPv4 address of the time server to synchronize with, kept while sync is off too; a host
        /// name needs a DNS server on the controller.
        /// </summary>
        /// <remarks>
        /// Known defect: the SDK refuses a name longer than 20 characters, although the vendor's default
        /// <c>0.schneider.pool.ntp.org</c> has 24, so <see cref="ITimeManagerService.SetSettings"/> throws
        /// <see cref="ArgumentException"/> for settings read from a controller that uses it.
        /// </remarks>
        [StringLength(20, ErrorMessage = "ServerName length can't be more than 20.")]
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
        /// Known defect: the SDK labels the UTC wire time +01:00 instead of +00:00, in both directions. An instant read
        /// from here is therefore one hour early, and a correct instant written sets the controller's clock one hour
        /// ahead; a value read and written back is unchanged.
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
