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
}        
