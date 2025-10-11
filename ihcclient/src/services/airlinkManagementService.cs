using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.AirlinkManagement;
using System.Diagnostics;

namespace Ihc
{
    /// <summary>
    /// High-level interface for IHC Wireless (Airlink Management) operations.
    /// Used for managing wireless devices in the IHC system.
    /// </summary>
    public interface IAirlinkManagementService : IIHCService
    {
        /// <summary>
        /// Enter RF configuration mode to allow device pairing and configuration.
        /// </summary>
        Task<bool> EnterRFConfiguration();

        /// <summary>
        /// Exit RF configuration mode.
        /// </summary>
        Task<bool> ExitRFConfiguration();

        /// <summary>
        /// Enter RF test mode for testing device communication.
        /// </summary>
        Task<bool> EnterRFTest();

        /// <summary>
        /// Exit RF test mode.
        /// </summary>
        Task<bool> ExitRFTest();

        /// <summary>
        /// Test an RF actuator with a specific serial number.
        /// </summary>
        Task<bool> TestRFActuatorWithSerialNumber(long serialNumber);

        /// <summary>
        /// Get list of resource IDs for devices running out of battery.
        /// </summary>
        Task<int[]> GetDevicesRunningOutOfBattery();

        /// <summary>
        /// Wait for a new device to be detected during configuration.
        /// </summary>
        Task<RFDevice> WaitForDeviceDetected(int timeoutSeconds);

        /// <summary>
        /// Wait for device test result.
        /// </summary>
        Task<RFDevice> WaitForDeviceTestResult(int timeoutSeconds);

        /// <summary>
        /// Get list of all detected RF devices.
        /// </summary>
        Task<RFDevice[]> GetDetectedDeviceList();

        /// <summary>
        /// Get battery level for a specific resource ID.
        /// </summary>
        Task<int> GetBatteryLevel(int resourceId);
    }

    public class AirlinkManagementService : ServiceBase, IAirlinkManagementService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.AirlinkManagement.AirlinkManagementService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint, bool logSensitiveData, bool asyncContinueOnCapturedContext) : base(logger, cookieHandler, endpoint, "AirlinkManagementService", logSensitiveData, asyncContinueOnCapturedContext) { }

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

        private readonly SoapImpl impl;

        /**
        * Create an AirlinkManagementService instance for access to the IHC API related to Airlink RF device management.
        * <param name="authService">AuthenticationService instance</param>
        * <param name="logSensitiveData">If true, log sensitive data. If false (default), redact sensitive values in logs.</param>
        * <param name="asyncContinueOnCapturedContext">If true, continue on captured context after await. If false (default), use ConfigureAwait(false) for better library performance.</param>
        */
        public AirlinkManagementService(IAuthenticationService authService, bool logSensitiveData = false, bool asyncContinueOnCapturedContext = false)
            : base(authService.Logger, logSensitiveData, asyncContinueOnCapturedContext)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint, logSensitiveData, asyncContinueOnCapturedContext);
        }

        private RFDevice mapRFDevice(WSRFDevice device)
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
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.enterRFConfigurationAsync(new inputMessageName1()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.enterRFConfiguration1.HasValue && result.enterRFConfiguration1.Value;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> ExitRFConfiguration()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.exitRFConfigurationAsync(new inputMessageName2()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.exitRFConfiguration1.HasValue && result.exitRFConfiguration1.Value;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> EnterRFTest()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.enterRFTestAsync(new inputMessageName8()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.enterRFTest1.HasValue && result.enterRFTest1.Value;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> ExitRFTest()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.exitRFTestAsync(new inputMessageName9()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.exitRFTest1.HasValue && result.exitRFTest1.Value;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<bool> TestRFActuatorWithSerialNumber(long serialNumber)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("serialNumber", serialNumber));

            var result = await impl.testRFActuatorWithSerialNumberAsync(new inputMessageName3(serialNumber)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.testRFActuatorWithSerialNumber2.HasValue && result.testRFActuatorWithSerialNumber2.Value;

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int[]> GetDevicesRunningOutOfBattery()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getDevicesRunningOutOfBatteryAsync(new inputMessageName4()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.getDevicesRunningOutOfBattery1 != null ? result.getDevicesRunningOutOfBattery1 : Array.Empty<int>();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<RFDevice> WaitForDeviceDetected(int timeoutSeconds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("timeoutSeconds", timeoutSeconds));

            var result = await impl.waitForDeviceDetectedAsync(new inputMessageName5(timeoutSeconds)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapRFDevice(result.waitForDeviceDetected2);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<RFDevice> WaitForDeviceTestResult(int timeoutSeconds)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("timeoutSeconds", timeoutSeconds));

            var result = await impl.waitForDeviceTestResultAsync(new inputMessageName6(timeoutSeconds)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = mapRFDevice(result.waitForDeviceTestResult2);

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<RFDevice[]> GetDetectedDeviceList()
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);

            var result = await impl.getDetectedDeviceListAsync(new inputMessageName7()).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.getDetectedDeviceList1 == null ? Array.Empty<RFDevice>() : result.getDetectedDeviceList1.Select(mapRFDevice).ToArray();

            activity?.SetReturnValue(retv);
            return retv;
        }

        public async Task<int> GetBatteryLevel(int resourceId)
        {
            using var activity = Telemetry.ActivitySource.StartActivity(ActivityKind.Internal);
            activity?.SetParameters(("resourceId", resourceId));

            var result = await impl.getBatteryLevelAsync(new inputMessageName10(resourceId)).ConfigureAwait(asyncContinueOnCapturedContext);
            var retv = result.getBatteryLevel2.HasValue ? result.getBatteryLevel2.Value : 0;

            activity?.SetReturnValue(retv);
            return retv;
        }
    }
}