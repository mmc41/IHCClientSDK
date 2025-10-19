using System;

namespace Ihc
{
    public record NotificationMessage
    {
        public DateTimeOffset Date { get; init; }

        public string NotificationType;

        public string Recipient;

        public string Sender;

        public string Subject;

        public string Body;

        public bool Delivered;

        public override string ToString()
        {
            return $"NotificationMessage(Date={Date}, NotificationType={NotificationType}, Recipient={Recipient}, Sender={Sender}, Subject={Subject}, Body={Body}, Delivered={Delivered})";
        }
    }
}        
