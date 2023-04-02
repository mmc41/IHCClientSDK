using System;

public record NotificationMessage {
        public DateTimeOffset Date { get; init; }  
       
        public string NotificationType;
        
        public string Recipient;
        
        public string Sender;
        
        public string Subject;
        
        public string Body;
        
        public bool Delivered;
}        
