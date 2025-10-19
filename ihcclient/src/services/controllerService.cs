using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Controller;
using System.IO;
using System.Diagnostics;

namespace Ihc {
    /// <summary>
    /// A highlevel client interface for the IHC ControllerService without any of the soap distractions.
    /// </summary>
    public interface IControllerService : IIHCService
    {
        /// <summary>
        /// Check if an IHC project is available on the controller.
        /// </summary>
        public Task<bool> IsIHCProjectAvailable();

        /// <summary>
        /// Check if the SD card is ready and accessible.
        /// </summary>
        public Task<bool> IsSDCardReady();

        /// <summary>
        /// Get SD card information including size and free space.
        /// </summary>
        public Task<SDInfo> GetSDCardInfo();

        /// <summary>
        /// Get current controller state 
        /// </summary>
        public Task<ControllerState> GetControllerState();

        /// <summary>
        /// Wait for controller to transition to a specific state.
        /// Observation: It seems the controller only waits until timeout IF the controller is already in the specified state??
        /// </summary>
        /// <param name="waitState">Target state to wait for</param>
        /// <param name="waitSec">Timeout in seconds</param>
        public Task<ControllerState> WaitForControllerStateChange(ControllerState waitState, int waitSec);

        /// <summary>
        /// Get project information including version, customer name, and last modified date.
        /// </summary>
        public Task<ProjectInfo> GetProjectInfo();

        /// <summary>
        /// Download the complete IHC project file from the controller.
        /// </summary>
        public Task<ProjectFile> GetProject();

        /// <summary>
        /// Upload and store a new project on the controller safely.
        /// Automatically checks controller readiness and handles project change mode entry/exit and state transitions
        /// by calling EnterProjectChangeMode/ExitProjectChangeMode and waiting for state changes.
        /// Note: Does not reset runtime values or reboot controller - call DelayedReboot manually if needed.
        /// </summary>
        /// <param name="project">Project file to store on the controller</param>
        /// <exception cref="InvalidOperationException">Thrown if controller is not ready or SD card unavailable</exception>
        public Task<bool> StoreProject(ProjectFile project);

        /// <summary>
        /// Get a backup file from the controller.
        /// </summary>
        public Task<BackupFile> GetBackup();

        /// <summary>
        /// Restore controller from backup.
        /// </summary>
        public Task<int> Restore();

        /// <summary>
        /// Get a specific segment of the IHC project (for large projects split into parts).
        /// </summary>
        /// <param name="index">Segment index</param>
        /// <param name="major">Major version number</param>
        /// <param name="minor">Minor version number</param>
        public Task<ProjectFile> GetIHCProjectSegment(int index, int major, int minor);

        /// <summary>
        /// Store a specific segment of the IHC project.
        /// </summary>
        /// <param name="segment">Project segment to store</param>
        /// <param name="index">Segment index</param>
        /// <param name="major">Major version number</param>
        public Task<bool> StoreIHCProjectSegment(ProjectFile segment, int index, int major);

        /// <summary>
        /// Get the size of project segments in bytes.
        /// </summary>
        public Task<int> GetIHCProjectSegmentationSize();

        /// <summary>
        /// Get the total number of segments in the current project.
        /// </summary>
        public Task<int> GetIHCProjectNumberOfSegments();

        /// <summary>
        /// Reset S0 energy meter values to zero.
        /// </summary>
        public Task ResetS0Values();

        /// <summary>
        /// Get current S0 meter value.
        /// </summary>
        public Task<float> GetS0MeterValue();

        /// <summary>
        /// Set S0 consumption configuration.
        /// </summary>
        /// <param name="consumption">Consumption value to set</param>
        /// <param name="flag">Configuration flag</param>
        public Task SetS0Consumption(float consumption, bool flag);

        /// <summary>
        /// Set the start date for S0 fiscal year tracking.
        /// </summary>
        /// <param name="month">Month (1-12)</param>
        /// <param name="day">Day of month</param>
        public Task SetS0FiscalYearStart(sbyte month, sbyte day);

        /// <summary>
        /// Make the controller enter project change mode to allow project updates.
        /// </summary>
        public Task<bool> EnterProjectChangeMode();

        /// <summary>
        /// Make the controller exit project change mode after project updates.
        /// </summary>
        public Task<bool> ExitProjectChangeMode();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ControllerService without exposing any of the soap distractions.
    /// </summary>
    public class ControllerService : ServiceBase, IControllerService
    {
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Controller.ControllerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, IhcSettings settings) : base(logger, cookieHandler, settings, "ControllerService") { }

            public Task<outputMessageName12> enterProjectChangeModeAsync(inputMessageName12 request)
            {
                return soapPost<outputMessageName12, inputMessageName12>("enterProjectChangeMode", request);
            }

            public Task<outputMessageName13> exitProjectChangeModeAsync(inputMessageName13 request)
            {
                return soapPost<outputMessageName13, inputMessageName13>("exitProjectChangeMode", request);
            }

            public Task<outputMessageName2> getBackupAsync(inputMessageName2 request)
            {
                return soapPost<outputMessageName2, inputMessageName2>("getBackup", request);
            }

            public Task<outputMessageName3> getIHCProjectAsync(inputMessageName3 request)
            {
                return soapPost<outputMessageName3, inputMessageName3>("getIHCProject", request);
            }

            public Task<outputMessageName18> getIHCProjectNumberOfSegmentsAsync(inputMessageName18 request)
            {
                return soapPost<outputMessageName18, inputMessageName18>("getIHCProjectNumberOfSegments", request);
            }

            public Task<outputMessageName15> getIHCProjectSegmentAsync(inputMessageName15 request)
            {
                return soapPost<outputMessageName15, inputMessageName15>("getIHCProjectSegment", request);
            }

            public Task<outputMessageName17> getIHCProjectSegmentationSizeAsync(inputMessageName17 request)
            {
                return soapPost<outputMessageName17, inputMessageName17>("getIHCProjectSegmentationSize", request);
            }

            public Task<outputMessageName8> getProjectInfoAsync(inputMessageName8 request)
            {
                return soapPost<outputMessageName8, inputMessageName8>("getProjectInfo", request);
            }

            public Task<outputMessageName11> getS0MeterValueAsync(inputMessageName11 request)
            {
                return soapPost<outputMessageName11, inputMessageName11>("getS0MeterValue", request);
            }

            public Task<outputMessageName5> getSdCardInfoAsync(inputMessageName5 request)
            {
                return soapPost<outputMessageName5, inputMessageName5>("getSdCardInfo", request);
            }

            public Task<outputMessageName1> getStateAsync(inputMessageName1 request)
            {
                return soapPost<outputMessageName1, inputMessageName1>("getState", request);
            }

            public Task<outputMessageName14> isIHCProjectAvailableAsync(inputMessageName14 request)
            {
                return soapPost<outputMessageName14, inputMessageName14>("isIHCProjectAvailable", request);
            }

            public Task<outputMessageName9> isSDCardReadyAsync(inputMessageName9 request)
            {
                return soapPost<outputMessageName9, inputMessageName9>("isSDCardReady", request);
            }

            public Task<outputMessageName10> resetS0ValuesAsync(inputMessageName10 request)
            {
                return soapPost<outputMessageName10, inputMessageName10>("resetS0Values", request);
            }

            public Task<outputMessageName7> restoreAsync(inputMessageName7 request)
            {
                return soapPost<outputMessageName7, inputMessageName7>("restore", request);
            }

            public Task<outputMessageName6> setS0ConsumptionAsync(inputMessageName6 request)
            {
                return soapPost<outputMessageName6, inputMessageName6>("setS0Consumption", request);
            }

            public Task<outputMessageName20> setS0FiscalYearStartAsync(inputMessageName20 request)
            {
                return soapPost<outputMessageName20, inputMessageName20>("setS0FiscalYearStart", request);
            }

            public Task<outputMessageName4> storeIHCProjectAsync(inputMessageName4 request)
            {
                return soapPost<outputMessageName4, inputMessageName4>("storeIHCProject", request);
            }

            public Task<outputMessageName16> storeIHCProjectSegmentAsync(inputMessageName16 request)
            {
                return soapPost<outputMessageName16, inputMessageName16>("storeIHCProjectSegment", request);
            }

            public Task<outputMessageName19> waitForControllerStateChangeAsync(inputMessageName19 request)
            {
                return soapPost<outputMessageName19, inputMessageName19>("waitForControllerStateChange", request);
            }
        }

        private readonly SoapImpl impl;

        /// <summary>
        /// Create an ControllerService instance for access to the IHC API related to the controller itself.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ControllerService(IAuthenticationService authService)
            : base(authService.Logger, authService.IhcSettings)
        {
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), settings);
        }

        private SDInfo mapSDCardData(WSSdCardData e)
        {
            if (e == null)
                return null;

            return new SDInfo()
            {
                Size = e.size,
                Free = e.free
            };
        }

        private BackupFile mapBackup(Ihc.Soap.Controller.WSFile backupFile)
        {
            if (backupFile == null)
                return null;

            return new BackupFile(
                Filename: backupFile.filename,
                Data: backupFile.data // Hmm. Can't identify the file format. Binary?
            );
        }

        private DateTimeOffset mapDate(WSDate v)
        {
            if (v == null)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        private ProjectInfo mapProjectInfo(Ihc.Soap.Controller.WSProjectInfo projectInfo)
        {
            if (projectInfo == null)
                return null;

            return new ProjectInfo()
            {
                VisualMajorVersion = projectInfo.visualMajorVersion,
                VisualMinorVersion = projectInfo.visualMinorVersion,
                ProjectMajorRevision = projectInfo.projectMajorRevision,
                ProjectMinorRevision = projectInfo.projectMinorRevision,
                Lastmodified = mapDate(projectInfo.lastmodified),
                ProjectNumber = projectInfo.projectNumber,
                CustomerName = projectInfo.customerName,
                InstallerName = projectInfo.installerName
            };
        }

        private async Task<string> decompress(byte[] fileData)
        {
            using (MemoryStream mscompressed = new MemoryStream(fileData))
            using (Stream inStream = new System.IO.Compression.GZipStream(mscompressed, System.IO.Compression.CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(inStream, ProjectFile.Encoding))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
            }
        }

        private async Task<byte[]> compress(string data)
        {
            using (MemoryStream mscompressed = new MemoryStream())
            {
                // Explicit scope ensures GZipStream is fully flushed and disposed before ToArray()
                using (Stream outStream = new System.IO.Compression.GZipStream(mscompressed, System.IO.Compression.CompressionMode.Compress))
                {
                    using (StreamWriter writer = new StreamWriter(outStream, ProjectFile.Encoding, bufferSize: 10*1024, leaveOpen: true))
                    {
                        await writer.WriteAsync(data).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        await writer.FlushAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    }
                    // Explicitly flush the GZipStream to ensure all compressed data is written
                    await outStream.FlushAsync().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                // GZipStream is now fully disposed and all data is written to mscompressed
                return mscompressed.ToArray();
            }
        }

        private async Task<ProjectFile> mapProjectFile(Ihc.Soap.Controller.WSFile wsFile)
        {
            if (wsFile == null)
                return null;

            return new ProjectFile(
                Filename: wsFile.filename,
                Data: await decompress(wsFile.data).ConfigureAwait(settings.AsyncContinueOnCapturedContext)
            );
        }

        private async Task<Ihc.Soap.Controller.WSFile> mapProjectFile(ProjectFile projectFile)
        {
            return new Ihc.Soap.Controller.WSFile()
            {
                data = await compress(projectFile.Data).ConfigureAwait(settings.AsyncContinueOnCapturedContext),
                filename = projectFile.Filename
            };
        }

            private ControllerState mapControllerState(Ihc.Soap.Controller.WSControllerState state)
        {
            if (state == null || String.IsNullOrEmpty(state.state))
                return ControllerState.Uninitialized;

            switch (state?.state)
            {
                case "text.ctrl.state.failed":
                    return ControllerState.Failed;
                case "text.ctrl.state.ready":
                    return ControllerState.Ready;
                case "text.ctrl.state.initialize":
                    return ControllerState.Initialize;
                case "text.ctrl.state.rfconfiguration":
                    return ControllerState.RfConfiguration;
                case "text.ctrl.state.simulation":
                    return ControllerState.Simulation;
                default:
                    return ControllerState.Unknown; // Unknown state - add to enum if we find it.
            }
        }


        private Ihc.Soap.Controller.WSControllerState mapControllerState(ControllerState state)
        {
            string stateStr;
            switch (state)
            {
                case ControllerState.Failed: stateStr = "text.ctrl.state.failed"; break;
                case ControllerState.Ready: stateStr = "text.ctrl.state.ready"; break;
                case ControllerState.Initialize: stateStr = "text.ctrl.state.initialize"; break;
                case ControllerState.RfConfiguration: stateStr = "text.ctrl.state.rfconfiguration"; break;
                case ControllerState.Simulation: stateStr = "text.ctrl.state.simulation"; break;
                case ControllerState.Uninitialized:
                case ControllerState.Unknown: stateStr = ""; break;
                default: throw new ArgumentException("Unknown state " + state);
            }

            return new WSControllerState() { state = stateStr };
        }

        public async Task<bool> IsIHCProjectAvailable()
        {
            using (var activity = StartActivity(nameof(IsIHCProjectAvailable)))
            {
                try
                {
                    var result = await impl.isIHCProjectAvailableAsync(new inputMessageName14() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;

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

        public async Task<bool> IsSDCardReady()
        {
            using (var activity = StartActivity(nameof(IsSDCardReady)))
            {
                try
                {
                    var result = await impl.isSDCardReadyAsync(new inputMessageName9() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.isSDCardReady1.HasValue ? result.isSDCardReady1.Value : false;

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

        public async Task<SDInfo> GetSDCardInfo()
        {
            using (var activity = StartActivity(nameof(GetSDCardInfo)))
            {
                try
                {
                    var result = await impl.getSdCardInfoAsync(new inputMessageName5() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getSdCardInfo1 != null ? mapSDCardData(result.getSdCardInfo1) : null;

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

        public async Task<ControllerState> GetControllerState()
        {
            using (var activity = StartActivity(nameof(GetControllerState)))
            {
                try
                {
                    var result = await impl.getStateAsync(new inputMessageName1() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapControllerState(result.getState1);

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

        /*
        public async Task<string> WaitForControllerStateChangeRaw(string waitState, int waitSec)
        {
            using (var activity = StartActivity(nameof(WaitForControllerStateChange)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(waitState), waitState),
                        (nameof(waitSec), waitSec)
                    );

                    var result = await impl.waitForControllerStateChangeAsync(new inputMessageName19(new Ihc.Soap.Controller.WSControllerState { state = waitState }, waitSec) { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result?.waitForControllerStateChange3.state;

                    activity?.SetReturnValue(retv);
                    return retv;
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }*/

        
        public async Task<ControllerState> WaitForControllerStateChange(ControllerState waitState, int waitSec)
        {
            using (var activity = StartActivity(nameof(WaitForControllerStateChange)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(waitState), waitState),
                        (nameof(waitSec), waitSec)
                    );

                    var result = await impl.waitForControllerStateChangeAsync(new inputMessageName19(mapControllerState(waitState), waitSec) { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapControllerState(result?.waitForControllerStateChange3);

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

        public async Task<float> GetS0MeterValue()
        {
            using (var activity = StartActivity(nameof(GetS0MeterValue)))
            {
                try
                {
                    var result = await impl.getS0MeterValueAsync(new inputMessageName11() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getS0MeterValue1.HasValue ? result.getS0MeterValue1.Value : 0.0f;

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
        
        public async Task ResetS0Values() {
            using (var activity = StartActivity(nameof(ResetS0Values)))
            {
                try
                {
                    await impl.resetS0ValuesAsync(new inputMessageName10() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task SetS0Consumption(float consumption, bool flag)
        {
            using (var activity = StartActivity(nameof(SetS0Consumption)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(consumption), consumption),
                        (nameof(flag), flag)
                    );

                    await impl.setS0ConsumptionAsync(new inputMessageName6(consumption, flag) { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task SetS0FiscalYearStart(sbyte month, sbyte day)
        {
            using (var activity = StartActivity(nameof(SetS0FiscalYearStart)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(month), month),
                        (nameof(day), day)
                    );

                    await impl.setS0FiscalYearStartAsync(new inputMessageName20(month, day) { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                }
                catch (Exception ex)
                {
                    activity?.SetError(ex);
                    throw;
                }
            }
        }

        public async Task<int> Restore()
        {
            using (var activity = StartActivity(nameof(Restore)))
            {
                try
                {
                    var result = await impl.restoreAsync(new inputMessageName7() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.restore1.HasValue ? result.restore1.Value : 0;

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

        public async Task<BackupFile> GetBackup()
        {
            using (var activity = StartActivity(nameof(GetBackup)))
            {
                try
                {
                    var result = await impl.getBackupAsync(new inputMessageName2() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapBackup(result.getBackup1);

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

        public async Task<ProjectInfo> GetProjectInfo()
        {
            using (var activity = StartActivity(nameof(GetProjectInfo)))
            {
                try
                {
                    var result = await impl.getProjectInfoAsync(new inputMessageName8() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = mapProjectInfo(result.getProjectInfo1);

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

        public async Task<ProjectFile> GetProject()
        {
            using (var activity = StartActivity(nameof(GetProject)))
            {
                try
                {
                    var result = await impl.getIHCProjectAsync(new inputMessageName3() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = await mapProjectFile(result.getIHCProject1).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

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

        public async Task<bool> EnterProjectChangeMode()
        {
            using (var activity = StartActivity(nameof(EnterProjectChangeMode)))
            {
                try
                {
                    var result = await impl.enterProjectChangeModeAsync(new inputMessageName12() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result?.enterProjectChangeMode1 != null && result.enterProjectChangeMode1.Value;

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

        public async Task<bool> ExitProjectChangeMode()
        {
            using (var activity = StartActivity(nameof(ExitProjectChangeMode)))
            {
                try
                {
                    var result = await impl.exitProjectChangeModeAsync(new inputMessageName13() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result?.exitProjectChangeMode1 != null && result.exitProjectChangeMode1.Value;

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

        public async Task<bool> StoreProject(ProjectFile project)
        {
            using (var activity = StartActivity(nameof(StoreProject)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(project), project)
                    );

                    // First perform some safty checks similar to what the IHC Visual App does:
                    bool controllerReady = (await GetControllerState().ConfigureAwait(settings.AsyncContinueOnCapturedContext)) == ControllerState.Ready;
                    activity?.SetTag("progress.controllerReady", controllerReady);
                    if (!controllerReady)
                        throw new InvalidOperationException("Controller not in ready state");

                    bool sdCardReady = await IsSDCardReady().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetTag("progress.sdCardReady", sdCardReady);        
                    if (!sdCardReady)
                        throw new InvalidOperationException("Controller SD Card not ready");

                    bool projectAvailable = await IsIHCProjectAvailable().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetTag("progress.projectAvailable", projectAvailable);
                    if (!projectAvailable)
                        throw new InvalidOperationException("Controller has no project available");

                    bool inChange = await EnterProjectChangeMode().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetTag("progress.inChange.in", inChange);
                    if (!inChange)
                        throw new InvalidOperationException("Controller could not enter change mode to prepare for project change");

                    var state = await WaitForControllerStateChange(ControllerState.Initialize, 10).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // TODO: Retry x times.
                    activity?.SetTag("progress.state.in", state);                    
                    if (state != ControllerState.Initialize)
                        throw new InvalidOperationException("Controller state did not enter init state to prepare for project change");

                    outputMessageName4 result;
                    try
                    {
                        // Call the actual store project operation

                        inputMessageName4 request = new inputMessageName4()
                        {
                            storeIHCProject1 = await mapProjectFile(project).ConfigureAwait(settings.AsyncContinueOnCapturedContext)
                        };

                        result = await impl.storeIHCProjectAsync(request).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                        state = await WaitForControllerStateChange(ControllerState.Initialize, 10).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // TODO: Retry x times.
                        if (state != ControllerState.Initialize)
                            throw new InvalidOperationException("Controller state does not remain in init state after project change");

                    }
                    finally
                    {
                        inChange = await ExitProjectChangeMode().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        activity?.SetTag("progress.inChange.out", inChange);                           
                        if (!inChange)
                            throw new InvalidOperationException("Controller could not exit change mode after project change");

                        state = await WaitForControllerStateChange(ControllerState.Ready, 10).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // TODO: Retry x times.
                        activity?.SetTag("progress.state.out", state);  
                        if (state != ControllerState.Ready)
                            throw new InvalidOperationException("Controller state did not enter init state to prepare for project change");
                    }

                    await Task.Delay(100).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // Wait a little to let controller settle.

                    var retv = result.storeIHCProject2 != null && result.storeIHCProject2.Value;

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

        public async Task<ProjectFile> GetIHCProjectSegment(int index, int major, int minor)
        {
            using (var activity = StartActivity(nameof(GetIHCProjectSegment)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(index), index),
                        (nameof(major), major),
                        (nameof(minor), minor)
                    );

                    var result = await impl.getIHCProjectSegmentAsync(new inputMessageName15()
                    {
                        getIHCProjectSegment1 = index,
                        getIHCProjectSegment2 = major,
                        getIHCProjectSegment3 = minor
                    }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = await mapProjectFile(result.getIHCProjectSegment4).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

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

        public async Task<bool> StoreIHCProjectSegment(ProjectFile segment, int index, int major)
        {
            using (var activity = StartActivity(nameof(StoreIHCProjectSegment)))
            {
                try
                {
                    activity?.SetParameters(
                        (nameof(segment), segment),
                        (nameof(index), index),
                        (nameof(major), major)
                    );

                    var result = await impl.storeIHCProjectSegmentAsync(new inputMessageName16()
                    {
                        storeIHCProjectSegment1 = await mapProjectFile(segment).ConfigureAwait(settings.AsyncContinueOnCapturedContext),
                        storeIHCProjectSegment2 = index,
                        storeIHCProjectSegment3 = major
                    }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.storeIHCProjectSegment4.HasValue ? result.storeIHCProjectSegment4.Value : false;

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

        public async Task<int> GetIHCProjectSegmentationSize()
        {
            using (var activity = StartActivity(nameof(GetIHCProjectSegmentationSize)))
            {
                try
                {
                    var result = await impl.getIHCProjectSegmentationSizeAsync(new inputMessageName17()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getIHCProjectSegmentationSize1.HasValue ? result.getIHCProjectSegmentationSize1.Value : 0;

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

        public async Task<int> GetIHCProjectNumberOfSegments()
        {
            using (var activity = StartActivity(nameof(GetIHCProjectNumberOfSegments)))
            {
                try
                {
                    var result = await impl.getIHCProjectNumberOfSegmentsAsync(new inputMessageName18()).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = result.getIHCProjectNumberOfSegments1.HasValue ? result.getIHCProjectNumberOfSegments1.Value : 0;

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