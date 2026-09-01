using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Leddimmermanagement;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ihc
{
    /// <summary>
    /// High-level interface for IHC LED dimmer management operations.
    /// Used for detecting, configuring and firmware-upgrading LED dimmer devices on the datalines.
    /// </summary>
    public interface ILedDimmerManagementService : IIHCApiService
    {
        /// <summary>
        /// Enter LED dimmer configuration mode to allow detection and assignment of devices.
        /// </summary>
        Task<bool> EnterConfiguration();

        /// <summary>
        /// Exit LED dimmer configuration mode.
        /// </summary>
        Task<bool> ExitConfiguration();

        /// <summary>
        /// Wait for a new LED dimmer device to be detected during configuration.
        /// </summary>
        /// <param name="timeoutSeconds">Time in seconds to wait for a device to be detected.</param>
        Task<LedDimmerInfo?> WaitForDeviceDetected(int timeoutSeconds);

        /// <summary>
        /// Scan for already-configured LED dimmer devices.
        /// </summary>
        /// <remarks>
        /// The two integer parameters are passed through to the controller as-is; their exact
        /// meaning is not documented in the service WSDL.
        /// </remarks>
        Task<IReadOnlyList<LedDimmerInfo>> ScanConfiguredDevices(int scanParameter1, int scanParameter2);

        /// <summary>
        /// Assign a channel ID to an LED dimmer device identified by its serial number.
        /// </summary>
        /// <param name="serialNumber">Serial number of the device to assign.</param>
        /// <param name="channel">Dataline channel the device is connected to.</param>
        /// <param name="channelID">Channel ID to assign to the device.</param>
        Task<bool> AssignID(string serialNumber, sbyte channel, sbyte channelID);

        /// <summary>
        /// Get the number of detected LED dimmer devices.
        /// </summary>
        Task<int> GetDeviceCount();

        /// <summary>
        /// Get the current light level of the LED dimmer on the given channel.
        /// </summary>
        /// <param name="channel">Channel to read the light level from.</param>
        Task<LedDimmerLevel?> GetLightLevel(sbyte channel);

        /// <summary>
        /// Get the list of all detected LED dimmer devices.
        /// </summary>
        Task<IReadOnlyList<LedDimmerInfo>> GetDeviceList();

        /// <summary>
        /// Start a firmware upgrade for the LED dimmer device identified by its serial number.
        /// </summary>
        /// <param name="serialNumber">Serial number of the device to upgrade.</param>
        Task StartFirmwareUpgrade(string serialNumber);

        /// <summary>
        /// Get the progress of an ongoing LED dimmer firmware upgrade.
        /// </summary>
        Task<LedDimmerProgress?> GetFirmwareUpgradeProgress();
    }

    public class LedDimmerManagementService : ServiceBase, ILedDimmerManagementService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Leddimmermanagement.LEDDimmerManagementService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "LEDDimmerManagementService") { }

            public Task<outputMessageName1> enterConfigurationAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("enterConfiguration", request);
            }

            public Task<outputMessageName2> exitConfigurationAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("exitConfiguration", request);
            }

            public Task<outputMessageName3> waitForDeviceDetectedAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("waitForDeviceDetected", request);
            }

            public Task<outputMessageName4> scanConfiguredDevicesAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("scanConfiguredDevices", request);
            }

            public Task<outputMessageName5> assignIDAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("assignID", request);
            }

            public Task<outputMessageName6> getDeviceCountAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("getDeviceCount", request);
            }

            public Task<outputMessageName7> getLightLevelAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("getLightLevel", request);
            }

            public Task<outputMessageName8> getDeviceListAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("getDeviceList", request);
            }

            public Task<outputMessageName9> startFirmwareUpgradeAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("startFirmwareUpgrade", request);
            }

            public Task<outputMessageName10> getFirmwareUpgradeProgressAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("getFirmwareUpgradeProgress", request);
            }
        }

        private readonly SoapImpl impl;

        /// <summary>
        /// Create a LedDimmerManagementService instance for access to the IHC API related to LED dimmer management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public LedDimmerManagementService(IAuthenticationService authService)
            : base(authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        // Map methods for translating between SOAP models and high-level models

        /// <summary>
        /// Maps a device list the controller may omit entirely, dropping any entry that carried no
        /// device. An absent list is an empty one, never null.
        /// </summary>
        private IReadOnlyList<LedDimmerInfo> MapInfoList(WSLEDDimmerInfo[]? devices)
            => devices == null
                ? Array.Empty<LedDimmerInfo>()
                : devices.Select(MapInfo).OfType<LedDimmerInfo>().ToList();

        private LedDimmerInfo? MapInfo(WSLEDDimmerInfo? ws)
        {
            if (ws == null)
                return null;

            return new LedDimmerInfo
            {
                Location = ws.location,
                Channel = ws.channel,
                BootloaderVersion = ws.bootloaderVersion,
                ApplicationVersion = ws.applicationVersion,
                ApplicationStatus = ws.applicationStatus,
                HardwareVersion = ws.hardwareVersion,
                SerialNumber = ws.serialNumber,
                Level = ws.level,
                ErrorFlags = ws.errorFlags,
                ChannelID = ws.channelID
            };
        }

        private LedDimmerLevel? MapLevel(WSLEDDimmerLevel? ws)
        {
            if (ws == null)
                return null;

            return new LedDimmerLevel
            {
                Level = ws.level,
                ErrorFlags = ws.errorFlags
            };
        }

        private LedDimmerProgress? MapProgress(WSLEDDimmerProgress? ws)
        {
            if (ws == null)
                return null;

            return new LedDimmerProgress
            {
                Message = ws.message,
                SerialNumber = ws.serialNumber,
                Status = ws.status,
                Progress = ws.progress,
                Maximum = ws.maximum,
                Running = ws.RUNNING,
                Finished = ws.FINISHED,
                Failed = ws.FAILED
            };
        }

        public async Task<bool> EnterConfiguration()
        {
            using (var activity = StartActivity(nameof(EnterConfiguration)))
            {
                try
                {
                    var result = await impl.enterConfigurationAsync(new inputMessageName1()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.enterConfiguration1.HasValue && result.enterConfiguration1.Value;

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

        public async Task<bool> ExitConfiguration()
        {
            using (var activity = StartActivity(nameof(ExitConfiguration)))
            {
                try
                {
                    var result = await impl.exitConfigurationAsync(new inputMessageName2()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.exitConfiguration1.HasValue && result.exitConfiguration1.Value;

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

        public async Task<LedDimmerInfo?> WaitForDeviceDetected(int timeoutSeconds)
        {
            using (var activity = StartActivity(nameof(WaitForDeviceDetected)))
            {
                try
                {
                    activity?.SetParameters((nameof(timeoutSeconds), timeoutSeconds));

                    var result = await impl.waitForDeviceDetectedAsync(new inputMessageName3(timeoutSeconds)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapInfo(result.waitForDeviceDetected2);

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

        public async Task<IReadOnlyList<LedDimmerInfo>> ScanConfiguredDevices(int scanParameter1, int scanParameter2)
        {
            using (var activity = StartActivity(nameof(ScanConfiguredDevices)))
            {
                try
                {
                    activity?.SetParameters((nameof(scanParameter1), scanParameter1), (nameof(scanParameter2), scanParameter2));

                    var result = await impl.scanConfiguredDevicesAsync(new inputMessageName4(scanParameter1, scanParameter2)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<LedDimmerInfo> retv = MapInfoList(result.scanConfiguredDevices3);

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

        public async Task<bool> AssignID(string serialNumber, sbyte channel, sbyte channelID)
        {
            using (var activity = StartActivity(nameof(AssignID)))
            {
                try
                {
                    activity?.SetParameters((nameof(serialNumber), serialNumber), (nameof(channel), channel), (nameof(channelID), channelID));

                    var result = await impl.assignIDAsync(new inputMessageName5(serialNumber, channel, channelID)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.assignID4.HasValue && result.assignID4.Value;

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

        public async Task<int> GetDeviceCount()
        {
            using (var activity = StartActivity(nameof(GetDeviceCount)))
            {
                try
                {
                    var result = await impl.getDeviceCountAsync(new inputMessageName6()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getDeviceCount1.HasValue ? result.getDeviceCount1.Value : 0;

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

        public async Task<LedDimmerLevel?> GetLightLevel(sbyte channel)
        {
            using (var activity = StartActivity(nameof(GetLightLevel)))
            {
                try
                {
                    activity?.SetParameters((nameof(channel), channel));

                    var result = await impl.getLightLevelAsync(new inputMessageName7(channel)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapLevel(result.getLightLevel2);

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

        public async Task<IReadOnlyList<LedDimmerInfo>> GetDeviceList()
        {
            using (var activity = StartActivity(nameof(GetDeviceList)))
            {
                try
                {
                    var result = await impl.getDeviceListAsync(new inputMessageName8()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    IReadOnlyList<LedDimmerInfo> retv = MapInfoList(result.getDeviceList1);

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

        public async Task StartFirmwareUpgrade(string serialNumber)
        {
            using (var activity = StartActivity(nameof(StartFirmwareUpgrade)))
            {
                try
                {
                    activity?.SetParameters((nameof(serialNumber), serialNumber));

                    await impl.startFirmwareUpgradeAsync(new inputMessageName9(serialNumber)).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<LedDimmerProgress?> GetFirmwareUpgradeProgress()
        {
            using (var activity = StartActivity(nameof(GetFirmwareUpgradeProgress)))
            {
                try
                {
                    var result = await impl.getFirmwareUpgradeProgressAsync(new inputMessageName10()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = MapProgress(result.getFirmwareUpgradeProgress1);

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
