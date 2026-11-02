using System.Threading.Tasks;
using System;
using Ihc.Soap.Airlinkmanagement;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ihc
{
    /// <summary>
    /// Manages IHC Wireless (Airlink) radio devices through the controller: the configuration mode in which a device is
    /// linked, the test mode for its signal and battery test, the detected-device list and low-battery reports.
    /// No operation links or unlinks. The project records each linked device's serial number, and the controller keeps
    /// its own link register, which its SD-card backup includes and a sent project does not rebuild.
    /// </summary>
    public interface IAirlinkManagementService : IIHCApiService
    {
        /// <summary>
        /// Puts the controller in wireless configuration mode, in which a device that announces itself (when its
        /// buttons are pressed) is reported to <see cref="WaitForDeviceDetected"/>. Always end it with
        /// <see cref="ExitRFConfiguration"/>.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> EnterRFConfiguration();

        /// <summary>
        /// Leaves the wireless configuration mode entered with <see cref="EnterRFConfiguration"/>, once any pending
        /// <see cref="WaitForDeviceDetected"/> has returned.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> ExitRFConfiguration();

        /// <summary>
        /// Puts the controller in wireless test mode, in which test messages from devices are reported to
        /// <see cref="WaitForDeviceTestResult"/>; a signal and battery test runs in it. Always end it with
        /// <see cref="ExitRFTest"/>.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> EnterRFTest();

        /// <summary>
        /// Leaves the wireless test mode entered with <see cref="EnterRFTest"/>.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> ExitRFTest();

        /// <summary>
        /// In test mode, asks the controller to test the mains-powered wireless device (an actuator) with this serial
        /// number. Battery devices need no such request: they report when a button is pressed.
        /// </summary>
        /// <param name="serialNumber">The device's 64-bit serial number, as in
        /// <see cref="RFDevice.SerialNumber"/>.</param>
        /// <returns><c>true</c> if the controller accepted the request; <c>false</c> if it refused or the reply
        /// carries no value.</returns>
        Task<bool> TestRFActuatorWithSerialNumber(long serialNumber);

        /// <summary>
        /// Gets the ids of the devices the controller reports as low on battery, probably the element ids of the
        /// project's wireless products and LED-dimmer channels.
        /// </summary>
        /// <returns>The ids as sent; a controller with no low device answers a list holding only -1.</returns>
        Task<IReadOnlyList<int>> GetDevicesRunningOutOfBattery();

        /// <summary>
        /// Long-polls in wireless configuration mode: the controller holds the call until a device announces itself or
        /// the hold runs out, so call it repeatedly while waiting for a device.
        /// </summary>
        /// <param name="timeoutSeconds">Longest time, in seconds, the controller holds the call.</param>
        /// <returns>The detected device, or <c>null</c> when the reply carries none; a device with serial number 0 is
        /// no detection.</returns>
        Task<RFDevice?> WaitForDeviceDetected(int timeoutSeconds);

        /// <summary>
        /// Long-polls in wireless test mode: the controller holds the call until a device sends a test message or the
        /// hold runs out, so call it repeatedly; a reply may come from another device than the one under test, so check
        /// its serial number.
        /// </summary>
        /// <param name="timeoutSeconds">Longest time, in seconds, the controller holds the call.</param>
        /// <returns>The reporting device with its battery and signal readings, or <c>null</c> when the reply carries
        /// none.</returns>
        Task<RFDevice?> WaitForDeviceTestResult(int timeoutSeconds);

        /// <summary>
        /// Gets the wireless devices the controller lists as detected, with their signal and battery values.
        /// </summary>
        /// <returns>The devices in the controller's order; empty when it lists none.</returns>
        /// <remarks>
        /// The list need not match the running project: it can include devices of no product in it, all with
        /// signal 0.
        /// </remarks>
        Task<IReadOnlyList<RFDevice>> GetDetectedDeviceList();

        /// <summary>
        /// Gets the battery level of one device. Unverified: no vendor client calls it, and nothing documents the
        /// scale of the result.
        /// </summary>
        /// <param name="resourceId">Probably the id of the device's resource; nothing documents the argument.</param>
        /// <returns>The level, or 0 when the reply carries no value.</returns>
        Task<int> GetBatteryLevel(int resourceId);
    }

    public class AirlinkManagementService : ServiceBase, IAirlinkManagementService
    {
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Airlinkmanagement.AirlinkManagementService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "AirlinkManagementService") { }

            public Task<outputMessageName1> enterRFConfigurationAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("enterRFConfiguration", request);
            }

            public Task<outputMessageName2> exitRFConfigurationAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("exitRFConfiguration", request);
            }

            public Task<outputMessageName3> testRFActuatorWithSerialNumberAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("testRFActuatorWithSerialNumber", request);
            }

            public Task<outputMessageName4> getDevicesRunningOutOfBatteryAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("getDevicesRunningOutOfBattery", request);
            }

            public Task<outputMessageName5> waitForDeviceDetectedAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("waitForDeviceDetected", request);
            }

            public Task<outputMessageName6> waitForDeviceTestResultAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("waitForDeviceTestResult", request);
            }

            public Task<outputMessageName7> getDetectedDeviceListAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("getDetectedDeviceList", request);
            }

            public Task<outputMessageName8> enterRFTestAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("enterRFTest", request);
            }

            public Task<outputMessageName9> exitRFTestAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("exitRFTest", request);
            }

            public Task<outputMessageName10> getBatteryLevelAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("getBatteryLevel", request);
            }
        }

        private readonly Ihc.Soap.Airlinkmanagement.AirlinkManagementService impl;

        /// <summary>
        /// Create an AirlinkManagementService instance for access to the IHC API related to Airlink RF device management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public AirlinkManagementService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal AirlinkManagementService(IAuthenticationService authService, Ihc.Soap.Airlinkmanagement.AirlinkManagementService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        private RFDevice? mapRFDevice(WSRFDevice? device)
        {
            if (device == null)
                return null;

            return new RFDevice()
            {
                BatteryLevel = device.batteryLevel,
                SignalStrength = device.signalStrength,
                DeviceType = device.deviceType,
                SerialNumber = device.serialNumber,
                Version = device.version,
                Detected = device.detected
            };
        }

        public async Task<bool> EnterRFConfiguration()
        {
            using (var activity = StartActivity(nameof(EnterRFConfiguration)))
            {
                try
                {
                    var result = await impl.enterRFConfigurationAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.enterRFConfiguration1.HasValue && result.enterRFConfiguration1.Value;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> ExitRFConfiguration()
        {
            using (var activity = StartActivity(nameof(ExitRFConfiguration)))
            {
                try
                {
                    var result = await impl.exitRFConfigurationAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.exitRFConfiguration1.HasValue && result.exitRFConfiguration1.Value;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> EnterRFTest()
        {
            using (var activity = StartActivity(nameof(EnterRFTest)))
            {
                try
                {
                    var result = await impl.enterRFTestAsync(new inputMessageName8()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.enterRFTest1.HasValue && result.enterRFTest1.Value;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> ExitRFTest()
        {
            using (var activity = StartActivity(nameof(ExitRFTest)))
            {
                try
                {
                    var result = await impl.exitRFTestAsync(new inputMessageName9()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.exitRFTest1.HasValue && result.exitRFTest1.Value;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<bool> TestRFActuatorWithSerialNumber(long serialNumber)
        {
            using (var activity = StartActivity(nameof(TestRFActuatorWithSerialNumber)))
            {
                try
                {
                    activity?.SetParameters((nameof(serialNumber), serialNumber));

                    var result = await impl.testRFActuatorWithSerialNumberAsync(new inputMessageName3(serialNumber)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.testRFActuatorWithSerialNumber2.HasValue && result.testRFActuatorWithSerialNumber2.Value;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<int>> GetDevicesRunningOutOfBattery()
        {
            using (var activity = StartActivity(nameof(GetDevicesRunningOutOfBattery)))
            {
                try
                {
                    var result = await impl.getDevicesRunningOutOfBatteryAsync(new inputMessageName4()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<int> retv = result.getDevicesRunningOutOfBattery1 != null ? result.getDevicesRunningOutOfBattery1 : Array.Empty<int>();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<RFDevice?> WaitForDeviceDetected(int timeoutSeconds)
        {
            using (var activity = StartActivity(nameof(WaitForDeviceDetected)))
            {
                try
                {
                    activity?.SetParameters((nameof(timeoutSeconds), timeoutSeconds));

                    var result = await impl.waitForDeviceDetectedAsync(new inputMessageName5(timeoutSeconds)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapRFDevice(result.waitForDeviceDetected2);

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<RFDevice?> WaitForDeviceTestResult(int timeoutSeconds)
        {
            using (var activity = StartActivity(nameof(WaitForDeviceTestResult)))
            {
                try
                {
                    activity?.SetParameters((nameof(timeoutSeconds), timeoutSeconds));

                    var result = await impl.waitForDeviceTestResultAsync(new inputMessageName6(timeoutSeconds)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapRFDevice(result.waitForDeviceTestResult2);

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<IReadOnlyList<RFDevice>> GetDetectedDeviceList()
        {
            using (var activity = StartActivity(nameof(GetDetectedDeviceList)))
            {
                try
                {
                    var result = await impl.getDetectedDeviceListAsync(new inputMessageName7()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    // A list entry the wire left empty is dropped, and counted: an absent LIST is an empty answer,
                    // but a shorter one than the controller sent is a partly-unreadable answer that used to look
                    // the same.
                    IReadOnlyList<RFDevice> retv = result.getDetectedDeviceList1 is { } devices
                        ? WireList.MapPresent(devices, mapRFDevice, activity, nameof(GetDetectedDeviceList))
                        : Array.Empty<RFDevice>();

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<int> GetBatteryLevel(int resourceId)
        {
            using (var activity = StartActivity(nameof(GetBatteryLevel)))
            {
                try
                {
                    activity?.SetParameters((nameof(resourceId), resourceId));

                    var result = await impl.getBatteryLevelAsync(new inputMessageName10(resourceId)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    if (!result.getBatteryLevel2.HasValue)
                    {
                        // Zero is a REAL battery level, so a caller cannot tell an unanswered read from a flat
                        // battery. The substitution stays - it is the contract - and the warning is what makes
                        // the two distinguishable after the fact.
                        activity.AddWarning(
                            $"The controller answered no battery level for resource {resourceId}; substituting 0.",
                            ("type", "AbsentWireValue"),
                            ("field", nameof(GetBatteryLevel)),
                            ("resourceId", resourceId));
                    }
                    var retv = result.getBatteryLevel2.HasValue ? result.getBatteryLevel2.Value : 0;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }
    }
}