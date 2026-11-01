using Ihc.Soap.Airlinkmanagement;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The wireless device management answers, read from <see cref="StubAirlink"/>. The RF modes keep no flag:
    /// nothing the stub answers depends on them, and a test sees each mode change in the journal.
    /// </summary>
    internal sealed class AirlinkManagementAnswers(StubAirlink scenario) : IWireAnswers
    {
        public string Service => "AirlinkManagementService";

        internal static Task<outputMessageName1> enterRFConfigurationAsync(inputMessageName1 request) =>
            Task.FromResult(new outputMessageName1 { enterRFConfiguration1 = true });

        internal static Task<outputMessageName2> exitRFConfigurationAsync(inputMessageName2 request) =>
            Task.FromResult(new outputMessageName2 { exitRFConfiguration1 = true });

        internal static Task<outputMessageName8> enterRFTestAsync(inputMessageName8 request) =>
            Task.FromResult(new outputMessageName8 { enterRFTest1 = true });

        internal static Task<outputMessageName9> exitRFTestAsync(inputMessageName9 request) =>
            Task.FromResult(new outputMessageName9 { exitRFTest1 = true });

        /// <summary>Acknowledged and journaled with the serial number tested; the result a test expects is queued
        /// on the scenario.</summary>
        internal static Task<outputMessageName3> testRFActuatorWithSerialNumberAsync(inputMessageName3 request)
        {
            AnswerContext.RecordPayload(request.testRFActuatorWithSerialNumber1);
            return Task.FromResult(new outputMessageName3 { testRFActuatorWithSerialNumber2 = true });
        }

        internal Task<outputMessageName4> getDevicesRunningOutOfBatteryAsync(inputMessageName4 request) =>
            Task.FromResult(new outputMessageName4 { getDevicesRunningOutOfBattery1 = [.. scenario.LowBattery] });

        internal Task<outputMessageName7> getDetectedDeviceListAsync(inputMessageName7 request) =>
            Task.FromResult(new outputMessageName7 { getDetectedDeviceList1 = [.. scenario.DetectedDevices.Select(device => NamedMembers.ToWire<WSRFDevice>(device))] });

        /// <summary>The oldest queued detection, held for by the <see cref="LongPoll"/> rule; none answers nil.</summary>
        internal async Task<outputMessageName5> waitForDeviceDetectedAsync(inputMessageName5 request) =>
            new() { waitForDeviceDetected2 = ToWire(await LongPoll.TakeOne(scenario.Detections, request.waitForDeviceDetected1).ConfigureAwait(false)) };

        /// <summary>The oldest queued test result, held for by the <see cref="LongPoll"/> rule; none answers nil.</summary>
        internal async Task<outputMessageName6> waitForDeviceTestResultAsync(inputMessageName6 request) =>
            new() { waitForDeviceTestResult2 = ToWire(await LongPoll.TakeOne(scenario.TestResultQueue, request.waitForDeviceTestResult1).ConfigureAwait(false)) };

        private static WSRFDevice? ToWire(RFDevice? device) => device is null ? null : NamedMembers.ToWire<WSRFDevice>(device);
    }
}
