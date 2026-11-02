using System;

namespace Ihc
{
    /// <summary>
    /// One entry of the controller's control log: an e-mail or SMS control message the controller has received.
    /// </summary>
    public record LogEventEntry
    {
        /// <summary>
        /// Time of the entry. The SDK reads it at a fixed +01:00 offset, so the instant is right only for a time
        /// stamped at UTC+1; <c>DateTimeOffset.MinValue</c> when the reply carries no readable date.
        /// </summary>
        public DateTimeOffset Date { get; init; }

        /// <summary>
        /// Control channel, reported as <c>text.control.email_type</c> or <c>text.control.sms_type</c>; read any value
        /// other than the SMS one as e-mail.
        /// </summary>
        public string? ControlType { get; init; }

        /// <summary>
        /// Status of the control: 1 awaiting confirmation, 2 aborted, 3 unauthorized, 4 executed; any other value
        /// unknown.
        /// </summary>
        public int LogEntryType { get; init; }

        /// <summary>
        /// The sender's address: an e-mail address, or for an SMS control the sender's phone number; <c>null</c> when
        /// the entry names no sender.
        /// </summary>
        public string? SenderAddress { get; init; }

        /// <summary>
        /// Human-readable description of the sender address; <c>null</c> when the entry names no sender.
        /// </summary>
        public string? SenderAddressDescription { get; init; }

        /// <summary>
        /// The control's trigger text that the received message carried, as defined for the control in IHC SceneDesign.
        /// </summary>
        public string? TriggerString { get; init; }

        /// <summary>
        /// The control's authentication method: direct control, sender-based or three-way handshake.
        /// </summary>
        public string? AuthenticationTypeAsString { get; init; }

        /// <summary>
        /// The control's action on its resource; for an input: on, off or pulse.
        /// </summary>
        public string? ActionTypeAsString { get; init; }

        public override string ToString()
        {
            return $"LogEventEntry(Date={Date}, ControlType={ControlType}, LogEntryType={LogEntryType}, SenderAddress={SenderAddress}, SenderAddressDescription={SenderAddressDescription}, TriggerString={TriggerString}, AuthenticationTypeAsString={AuthenticationTypeAsString}, ActionTypeAsString={ActionTypeAsString})";
        }
    }
}        
