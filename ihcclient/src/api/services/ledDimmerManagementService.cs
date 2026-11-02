using System.Threading.Tasks;
using System;
using Ihc.Soap.Leddimmermanagement;
using System.Diagnostics;
using System.Collections.Generic;

namespace Ihc
{
    /// <summary>
    /// Manages the RS-485 LED Dimmer 2 (a wired two-channel dimmer) through the controller: linking a dimmer's
    /// channels, listing the dimmers and upgrading their firmware.
    /// </summary>
    public interface ILedDimmerManagementService : IIHCApiService
    {
        /// <summary>
        /// Puts the controller's LED-dimmer handling in configuration mode, as needed around linking a dimmer and
        /// around a firmware upgrade. Always end it with <see cref="ExitConfiguration"/>, also after a refusal.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> EnterConfiguration();

        /// <summary>
        /// Leaves the LED-dimmer configuration mode entered with <see cref="EnterConfiguration"/>.
        /// </summary>
        /// <returns><c>true</c> if the controller accepted; <c>false</c> if it refused or the reply carries no
        /// value.</returns>
        Task<bool> ExitConfiguration();

        /// <summary>
        /// Long-polls in configuration mode: the controller holds the call until a dimmer announces itself (both its
        /// buttons held until they blink, then one pressed) or the hold runs out. Repeat the call to keep listening.
        /// </summary>
        /// <param name="timeoutSeconds">Longest time, in seconds, the controller holds the call.</param>
        /// <returns>The announcing dimmer, or <c>null</c> when the reply carries none.</returns>
        Task<LedDimmerInfo?> WaitForDeviceDetected(int timeoutSeconds);

        /// <summary>
        /// Scans for LED dimmers that are already configured. Unverified: no vendor client calls it.
        /// </summary>
        /// <param name="scanParameter1">Passed to the controller as-is; its meaning is not documented.</param>
        /// <param name="scanParameter2">Passed to the controller as-is; its meaning is not documented.</param>
        Task<IReadOnlyList<LedDimmerInfo>> ScanConfiguredDevices(int scanParameter1, int scanParameter2);

        /// <summary>
        /// Assigns a bus id to one channel of the dimmer with the given serial number; 0 is the unlink value. Link with
        /// one call per channel, in configuration mode.
        /// </summary>
        /// <param name="serialNumber">The dimmer's serial number, as <see cref="WaitForDeviceDetected"/> reports
        /// it.</param>
        /// <param name="channel">Channel index within the dimmer, 0 or 1.</param>
        /// <param name="channelID">Bus id for the channel, 0x80-0xBF (-128 to -65 as a signed byte); 0 unlinks.</param>
        /// <returns><c>true</c> on success; <c>false</c> for a failed link, also when the reply carries no
        /// value.</returns>
        Task<bool> AssignID(string serialNumber, sbyte channel, sbyte channelID);

        /// <summary>
        /// Gets the number of LED Dimmer 2 entries the controller manages.
        /// </summary>
        /// <returns>The count; 0 also when the reply carries no value.</returns>
        Task<int> GetDeviceCount();

        /// <summary>
        /// Gets the current light level and fault flags of one LED-dimmer channel.
        /// </summary>
        /// <param name="channel">Channel to read the light level from; probably its bus id
        /// (<see cref="LedDimmerInfo.ChannelID"/>).</param>
        /// <returns>The level and fault flags, or <c>null</c> when the reply carries none.</returns>
        Task<LedDimmerLevel?> GetLightLevel(sbyte channel);

        /// <summary>
        /// Gets the controller's LED Dimmer 2 entries with identity, versions, link state, light level and fault flags.
        /// There is one entry per channel, a dimmer's two channels as consecutive entries.
        /// </summary>
        /// <returns>The entries in the controller's order; empty when it lists none.</returns>
        Task<IReadOnlyList<LedDimmerInfo>> GetDeviceList();

        /// <summary>
        /// Starts pushing the controller's stored LED Dimmer 2 firmware image to one dimmer; the call carries no image.
        /// Follow the upgrade with <see cref="GetFirmwareUpgradeProgress"/>.
        /// </summary>
        /// <param name="serialNumber">Serial number of the dimmer to upgrade, as <see cref="GetDeviceList"/> reports
        /// it.</param>
        /// <remarks>
        /// A full upgrade runs <see cref="EnterConfiguration"/>, this call, progress polls while the status is
        /// <c>running</c>, and <see cref="ExitConfiguration"/>. Known defect: the controller upgrades all dimmers in
        /// one call when the serial is <c>null</c>, but the parameter is declared non-nullable, so passing <c>null</c>
        /// draws a nullable warning.
        /// </remarks>
        Task StartFirmwareUpgrade(string serialNumber);

        /// <summary>
        /// Gets the state of the current or last LED-dimmer firmware upgrade: status, progress against a maximum, the
        /// serial being upgraded and a message. It also shows an upgrade that was already running, so check it before
        /// <see cref="StartFirmwareUpgrade"/>.
        /// </summary>
        /// <returns>The progress record, or <c>null</c> when the reply carries none.</returns>
        Task<LedDimmerProgress?> GetFirmwareUpgradeProgress();
    }

    public class LedDimmerManagementService : ServiceBase, ILedDimmerManagementService
    {
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

        private readonly Ihc.Soap.Leddimmermanagement.LEDDimmerManagementService impl;

        /// <summary>
        /// Create a LedDimmerManagementService instance for access to the IHC API related to LED dimmer management.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public LedDimmerManagementService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal LedDimmerManagementService(IAuthenticationService authService, Ihc.Soap.Leddimmermanagement.LEDDimmerManagementService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        // Map methods for translating between SOAP models and high-level models

        /// <summary>
        /// Maps a device list the controller may omit entirely, dropping any entry that carried no
        /// device. An absent list is an empty one, never null; a list SHORTER than the one that arrived is
        /// warned about, because that one is a partly-unreadable answer rather than a shorter installation.
        /// </summary>
        /// <param name="devices">The wire list, or null when the response carried none.</param>
        /// <param name="activity">The calling operation's span.</param>
        /// <param name="field">The calling operation's name, for the warning's <c>field</c> tag.</param>
        private IReadOnlyList<LedDimmerInfo> MapInfoList(WSLEDDimmerInfo[]? devices, Activity? activity, string field)
            => devices is { } present
                ? WireList.MapPresent(present, MapInfo, activity, field)
                : Array.Empty<LedDimmerInfo>();

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

        private static LedDimmerLevel? MapLevel(WSLEDDimmerLevel? ws)
        {
            if (ws == null)
                return null;

            return new LedDimmerLevel
            {
                Level = ws.level,
                ErrorFlags = ws.errorFlags
            };
        }

        private static LedDimmerProgress? MapProgress(WSLEDDimmerProgress? ws)
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
                    IReadOnlyList<LedDimmerInfo> retv = MapInfoList(result.scanConfiguredDevices3, activity, nameof(ScanConfiguredDevices));

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
                    IReadOnlyList<LedDimmerInfo> retv = MapInfoList(result.getDeviceList1, activity, nameof(GetDeviceList));

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
