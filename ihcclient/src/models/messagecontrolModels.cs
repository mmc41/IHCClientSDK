using System;

namespace Ihc
{
    public record LogEventEntry
    {
        public DateTimeOffset Date { get; init; }
        public string ControlType { get; init; }

        public int LogEntryType { get; init; }

        public string SenderAddress { get; init; }
        public string SenderAddressDescription { get; init; }

        public string TriggerString { get; init; }

        public string AuthenticationTypeAsString { get; init; }

        public string ActionTypeAsString { get; init; }

        public override string ToString()
        {
            return $"LogEventEntry(Date={Date}, ControlType={ControlType}, LogEntryType={LogEntryType}, SenderAddress={SenderAddress}, SenderAddressDescription={SenderAddressDescription}, TriggerString={TriggerString}, AuthenticationTypeAsString={AuthenticationTypeAsString}, ActionTypeAsString={ActionTypeAsString})";
        }
    }
}        
