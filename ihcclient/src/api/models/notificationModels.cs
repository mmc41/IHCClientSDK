using System;

namespace Ihc
{
    /// <summary>
    /// One entry of the controller's notification log: an e-mail or SMS message the controller has sent.
    /// </summary>
    public record NotificationMessage
    {
        /// <summary>
        /// Time of the message. The SDK reads it at a fixed +01:00 offset, so the instant is right only for a time
        /// stamped at UTC+1; <c>DateTimeOffset.MinValue</c> when the reply carries no readable date.
        /// </summary>
        public DateTimeOffset Date { get; init; }

        /// <summary>
        /// Message type, reported as <c>text.notification.email_type</c> or <c>text.notification.sms_type</c>; read any
        /// value other than the SMS one as e-mail.
        /// </summary>
        public string? NotificationType { get; init; }

        /// <summary>
        /// Recipient address or phone number.
        /// </summary>
        public string? Recipient { get; init; }

        /// <summary>
        /// Sender address or identifier.
        /// </summary>
        public string? Sender { get; init; }

        /// <summary>
        /// Subject line of the notification.
        /// </summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Body content of the notification message.
        /// </summary>
        public string? Body { get; init; }

        /// <summary>
        /// Indicates whether the notification has been successfully delivered.
        /// </summary>
        public bool Delivered { get; init; }

        public override string ToString()
        {
            return $"NotificationMessage(Date={Date}, NotificationType={NotificationType}, Recipient={Recipient}, Sender={Sender}, Subject={Subject}, Body={Body}, Delivered={Delivered})";
        }
    }

    /// <summary>
    /// The outcome of a test mail: whether the controller sent it, or which failure stopped it, as the controller
    /// reports it.
    /// </summary>
    public record NotificationSendResult
    {
        /// <summary>The controller handed the mail to the SMTP server.</summary>
        public bool AttemptSucceeded { get; init; }

        /// <summary>The SMTP server refused the user name or password.</summary>
        public bool AttemptFailedDueToAuthentication { get; init; }

        /// <summary>The SMTP server could not be reached.</summary>
        public bool AttemptFailedDueToNetworkProblems { get; init; }

        /// <summary>The SMTP settings could not be used.</summary>
        public bool AttemptFailedDueToInvalidSettings { get; init; }

        /// <summary>The controller failed internally.</summary>
        public bool AttemptFailedDueToInternalErrors { get; init; }

        /// <summary>The connection to the SMTP server timed out.</summary>
        public bool AttemptFailedDueToConnectTimeout { get; init; }

        /// <summary>The SMTP server refused the connection. The wire spells it <c>attempFailedDueToConnectionRefused</c>.</summary>
        public bool AttemptFailedDueToConnectionRefused { get; init; }

        /// <summary>Reading from or writing to the SMTP server timed out. The wire spells it
        /// <c>attempFailedDueToIOTimeout</c>.</summary>
        public bool AttemptFailedDueToIOTimeout { get; init; }

        public override string ToString()
        {
            return $"NotificationSendResult(AttemptSucceeded={AttemptSucceeded}, AttemptFailedDueToAuthentication={AttemptFailedDueToAuthentication}, AttemptFailedDueToNetworkProblems={AttemptFailedDueToNetworkProblems}, AttemptFailedDueToInvalidSettings={AttemptFailedDueToInvalidSettings}, AttemptFailedDueToInternalErrors={AttemptFailedDueToInternalErrors}, AttemptFailedDueToConnectTimeout={AttemptFailedDueToConnectTimeout}, AttemptFailedDueToConnectionRefused={AttemptFailedDueToConnectionRefused}, AttemptFailedDueToIOTimeout={AttemptFailedDueToIOTimeout})";
        }
    }
}        
