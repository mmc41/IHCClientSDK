using Ihc.Soap.Smsmodem;

namespace Ihc.Stubbing
{
    /// <summary>The SMS modem's answers, read from <see cref="StubSmsModem"/>. Every member name follows the
    /// <see cref="NamedMembers"/> rule, the settings' behind an <c>m_</c> prefix.</summary>
    internal sealed class SmsModemAnswers(StubSmsModem scenario) : IWireAnswers
    {
        private const string SettingsPrefix = "m_";

        /// <summary>The SDK names the recipient-slot mask for what it is; the wire calls it a power-down number.</summary>
        private static readonly IReadOnlyDictionary<string, string> SettingsRenamed = new Dictionary<string, string>
        {
            ["m_powerdownNumber"] = nameof(SmsModemSettings.RecipientSlots),
        };

        public string Service => "SMSModemService";

        internal Task<outputMessageName4> getSMSModemInfoAsync(inputMessageName4 request) =>
            Task.FromResult(new outputMessageName4 { getSMSModemInfo1 = NamedMembers.ToWire<WSSMSModemInfo>(scenario.Info) });

        internal Task<outputMessageName2> getSMSModemSettingsAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2
            {
                getSMSModemSettings1 = NamedMembers.ToWire<WSSMSModemSettings>(scenario.Settings, SettingsPrefix, SettingsRenamed),
            });

        internal Task<outputMessageName1> setSMSModemSettingsAsync(inputMessageName1 request)
        {
            SmsModemSettings settings = NamedMembers.FromWire<SmsModemSettings>(request.setSMSModemSettings1
                ?? throw new InvalidOperationException("a settings change carried no settings"), SettingsPrefix, SettingsRenamed);
            AnswerContext.RecordPayload(settings);
            scenario.Settings = settings;
            return Task.FromResult(new outputMessageName1());
        }

        internal Task<outputMessageName3> getSMSModemStatusAsync(inputMessageName3 request) =>
            Task.FromResult(new outputMessageName3 { getSMSModemStatus1 = NamedMembers.ToWire<WSSMSModemStatus>(scenario.Status) });

        /// <summary>A reset changes nothing the stub answers from: a test sees it in the journal, which records
        /// every exchange.</summary>
        internal static Task<outputMessageName5> resetSMSModemAsync(inputMessageName5 request) =>
            Task.FromResult(new outputMessageName5());
    }
}
