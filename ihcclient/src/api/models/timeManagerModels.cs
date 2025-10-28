using System;
using System.ComponentModel.DataAnnotations;

namespace Ihc
{
    /// <summary>
    /// High level model of time manager settings without soap distractions.
    /// </summary>
    public record TimeManagerSettings
    {
        /// <summary>
        /// Indicates whether time should be synchronized against a time server.
        /// </summary>
        public bool SynchroniseTimeAgainstServer { get; init; }

        /// <summary>
        /// Indicates whether daylight saving time (DST) should be used.
        /// </summary>
        public bool UseDST { get; init; }

        /// <summary>
        /// GMT/UTC offset in hours.
        /// </summary>
        public int GmtOffsetInHours { get; init; }

        /// <summary>
        /// Name or address of the time server to synchronize with.
        /// </summary>
        [StringLength(20, ErrorMessage = "ServerName length can't be more than 20.")]
        public string ServerName { get; init; }

        /// <summary>
        /// Interval in hours between time synchronization attempts.
        /// </summary>
        public int SyncIntervalInHours { get; init; }

        /// <summary>
        /// Current time and date in UTC.
        /// </summary>
        public DateTimeOffset TimeAndDateInUTC { get; init; }

        /// <summary>
        /// Indicates whether online calendar should update automatically.
        /// </summary>
        public bool OnlineCalendarUpdateOnline { get; init; }

        /// <summary>
        /// Country code for online calendar.
        /// </summary>
        public string OnlineCalendarCountry { get; init; }

        /// <summary>
        /// Timestamp indicating until when the online calendar is valid.
        /// </summary>
        public int OnlineCalendarValidUntil { get; init; }

        public override string ToString()
        {
            return $"TimeManagerSettings(SynchroniseTimeAgainstServer={SynchroniseTimeAgainstServer}, UseDST={UseDST}, GmtOffsetInHours={GmtOffsetInHours}, ServerName={ServerName}, SyncIntervalInHours={SyncIntervalInHours}, TimeAndDateInUTC={TimeAndDateInUTC}, OnlineCalendarUpdateOnline={OnlineCalendarUpdateOnline}, OnlineCalendarCountry={OnlineCalendarCountry}, OnlineCalendarValidUntil={OnlineCalendarValidUntil})";
        }
    }

    /// <summary>
    /// High level model of a time server connection result without soap distractions.
    /// </summary>
    public record TimeServerConnectionResult
    {
        /// <summary>
        /// Indicates whether the connection to the time server was successful.
        /// </summary>
        public bool ConnectionWasSuccessful { get; init; }

        /// <summary>
        /// Date and time received from the time server.
        /// </summary>
        public DateTimeOffset DateFromServer { get; init; }

        /// <summary>
        /// Indicates whether the connection failed due to unknown host.
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
