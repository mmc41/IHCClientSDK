using Ihc.Soap.Leddimmermanagement;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The LED dimmer management answers, read from <see cref="StubLedDimmers"/>. Configuration mode keeps no flag:
    /// nothing the stub answers depends on it.
    /// </summary>
    internal sealed class LedDimmerManagementAnswers(StubLedDimmers scenario) : IWireAnswers
    {
        public string Service => "LEDDimmerManagementService";

        internal static Task<outputMessageName1> enterConfigurationAsync(inputMessageName1 request) =>
            Task.FromResult(new outputMessageName1 { enterConfiguration1 = true });

        internal static Task<outputMessageName2> exitConfigurationAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2 { exitConfiguration1 = true });

        /// <summary>The oldest queued detection, held for by the <see cref="LongPoll"/> rule; none answers nil.</summary>
        internal async Task<outputMessageName3> waitForDeviceDetectedAsync(inputMessageName3 request)
        {
            LedDimmerInfo? device = await LongPoll.TakeOne(scenario.Detections, request.waitForDeviceDetected1).ConfigureAwait(false);
            return new outputMessageName3 { waitForDeviceDetected2 = device is null ? null : NamedMembers.ToWire<WSLEDDimmerInfo>(device) };
        }

        /// <summary>Acknowledged and journaled; the device list is the scenario's to change.</summary>
        internal static Task<outputMessageName5> assignIDAsync(inputMessageName5 request)
        {
            AnswerContext.RecordPayload((SerialNumber: request.assignID1, Channel: request.assignID2, ChannelID: request.assignID3));
            return Task.FromResult(new outputMessageName5 { assignID4 = true });
        }

        internal Task<outputMessageName6> getDeviceCountAsync(inputMessageName6 request) =>
            Task.FromResult(new outputMessageName6 { getDeviceCount1 = scenario.Devices.Length });

        internal Task<outputMessageName8> getDeviceListAsync(inputMessageName8 request) =>
            Task.FromResult(new outputMessageName8 { getDeviceList1 = [.. scenario.Devices.Select(device => NamedMembers.ToWire<WSLEDDimmerInfo>(device))] });

        /// <summary>Journaled with the serial number asked for, which is none for every dimmer at once, and finishes
        /// the upgrade at once.</summary>
        internal Task<outputMessageName9> startFirmwareUpgradeAsync(inputMessageName9 request)
        {
            AnswerContext.RecordPayload(request.startFirmwareUpgrade1);
            scenario.CompleteUpgrade(request.startFirmwareUpgrade1);
            return Task.FromResult(new outputMessageName9());
        }

        internal Task<outputMessageName10> getFirmwareUpgradeProgressAsync(inputMessageName10 request) =>
            Task.FromResult(new outputMessageName10
            {
                getFirmwareUpgradeProgress1 = scenario.FirmwareProgress is { } progress ? NamedMembers.ToWire<WSLEDDimmerProgress>(progress) : null,
            });
    }
}
