using System;

public record LogEventEntry {
        public DateTimeOffset Date { get; init; } 
        public string ControlType { get; init;}
        
        public int LogEntryType { get; init;}
        
        public string SenderAddress { get; init; }   
        public string SenderAddressDescription { get; init; }   

        public string TriggerString { get; init; }
        
        public string AuthenticationTypeAsString { get; init; }
        
        public string ActionTypeAsString { get; init; }
}        
