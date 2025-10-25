using System;

namespace Ihc
{
    /// <summary>
    /// High level model of a message control log event entry without soap distractions.
    /// </summary>
    public record LogEventEntry
    {
        /// <summary>
        /// Date and time of the log event.
        /// </summary>
        public DateTimeOffset Date { get; init; }

        /// <summary>
        /// Type of control (e.g., email, SMS).
        /// </summary>
        public string ControlType { get; init; }

        /// <summary>
        /// Numeric log entry type identifier.
        /// </summary>
        public int LogEntryType { get; init; }

        /// <summary>
        /// Sender's address (email or phone number).
        /// </summary>
        public string SenderAddress { get; init; }

        /// <summary>
        /// Human-readable description of the sender address.
        /// </summary>
        public string SenderAddressDescription { get; init; }

        /// <summary>
        /// The trigger string that initiated the event.
        /// </summary>
        public string TriggerString { get; init; }

        /// <summary>
        /// Authentication type as a string.
        /// </summary>
        public string AuthenticationTypeAsString { get; init; }

        /// <summary>
        /// Action type as a string describing what action was taken.
        /// </summary>
        public string ActionTypeAsString { get; init; }

        public override string ToString()
        {
            return $"LogEventEntry(Date={Date}, ControlType={ControlType}, LogEntryType={LogEntryType}, SenderAddress={SenderAddress}, SenderAddressDescription={SenderAddressDescription}, TriggerString={TriggerString}, AuthenticationTypeAsString={AuthenticationTypeAsString}, ActionTypeAsString={ActionTypeAsString})";
        }
    }
}        
