using System;

namespace Ihc
{
    /// <summary>
    /// High level model of a notification message without soap distractions.
    /// </summary>
    public record NotificationMessage
    {
        /// <summary>
        /// Date and time when the notification was created.
        /// </summary>
        public DateTimeOffset Date { get; init; }

        /// <summary>
        /// Type of notification (e.g., email, SMS).
        /// </summary>
        public string NotificationType;

        /// <summary>
        /// Recipient address or phone number.
        /// </summary>
        public string Recipient;

        /// <summary>
        /// Sender address or identifier.
        /// </summary>
        public string Sender;

        /// <summary>
        /// Subject line of the notification.
        /// </summary>
        public string Subject;

        /// <summary>
        /// Body content of the notification message.
        /// </summary>
        public string Body;

        /// <summary>
        /// Indicates whether the notification has been successfully delivered.
        /// </summary>
        public bool Delivered;

        public override string ToString()
        {
            return $"NotificationMessage(Date={Date}, NotificationType={NotificationType}, Recipient={Recipient}, Sender={Sender}, Subject={Subject}, Body={Body}, Delivered={Delivered})";
        }
    }
}        
