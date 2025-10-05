using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Ihc.Soap.Controller;
using System.IO;

namespace Ihc {
    /**
    * A highlevel client interface for the IHC ControllerService without any of the soap distractions.
    *
    * Status: Incomplete.
    */
    public interface IControllerService
    {
        public Task<bool> IsIHCProjectAvailable();
        public Task<bool> IsSDCardReady();
        public Task<SDInfo> GetSDCardInfo();

        public Task<string> GetControllerState();
        public Task<string> WaitForControllerStateChange(string waitState, int waitSec);

        public Task<ProjectInfo> GetProjectInfo();
        public Task<ProjectFile> GetProject();

        /// <summary>
        /// Store a new project on the controller, checking preconditions and handling the project change mode.
        /// Calls internal methods to enter and exit project change mode.
        /// Fails with InvalidOperationException if controller is not ready. 
        /// Nb. Unlike IHC Visual, this method does not
        /// * handle re-setting runtime values after the project change.
        /// * robooting controller. Call delayedReboot(100) manully for this
        /// </summary>
        /// <param name="project"></param>
        /// <returns></returns>
        public Task<bool> StoreProject(ProjectFile project);

        public Task<BackupFile> GetBackup();

        public Task<int> Restore();

        public Task ResetS0Values();
        public Task<float> GetS0MeterValue();
        public Task SetS0Consumption(float consumption, bool flag);
        public Task SetS0FiscalYearStart(sbyte month, sbyte day);
    }

    /**
    * A highlevel implementation of a client to the IHC ControllerService without exposing any of the soap distractions.
    *
    * TODO: Add remaining operations.
    */
    public class ControllerService : IControllerService
    {
        private readonly ILogger logger;
        private readonly IAuthenticationService authService;

        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Controller.ControllerService
        {
            public SoapImpl(ILogger logger, ICookieHandler cookieHandler, string endpoint) : base(logger, cookieHandler, endpoint, "ControllerService") { }

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

        /**
        * Create an ControllerService instance for access to the IHC API related to the controller itself.
        * <param name="authService">AuthenticationService instance</param>
        */
        public ControllerService(IAuthenticationService authService)
        {
            this.logger = authService.Logger;
            this.authService = authService;
            this.impl = new SoapImpl(logger, authService.GetCookieHandler(), authService.Endpoint);
        }

        // TODO: Implement remaining high level service.

        private SDInfo mapSDCardData(WSSdCardData e)
        {
            return new SDInfo()
            {
                Size = e.size,
                Free = e.free
            };
        }

        private BackupFile mapBackup(Ihc.Soap.Controller.WSFile backupFile)
        {
            return new BackupFile()
            {
                Filename = backupFile?.filename,
                Data = backupFile?.data // Hmm. Can't identify the file format. Binary?
            };
        }

        private DateTimeOffset mapDate(WSDate v)
        {
            return new DateTimeOffset(v.year, v.monthWithJanuaryAsOne, v.day, v.hours, v.minutes, v.seconds, DateHelper.GetWSTimeOffset());
        }

        private ProjectInfo mapProjectInfo(Ihc.Soap.Controller.WSProjectInfo projectInfo)
        {
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
                return await reader.ReadToEndAsync();
            }
        }

        private async Task<byte[]> compress(string data)
        {
            using (MemoryStream mscompressed = new MemoryStream())
            {
                using (Stream outStream = new System.IO.Compression.GZipStream(mscompressed, System.IO.Compression.CompressionMode.Compress))
                using (StreamWriter writer = new StreamWriter(outStream, ProjectFile.Encoding))
                {
                    await writer.WriteAsync(data);
                }
                return mscompressed.ToArray();
            }
        }

        private async Task<ProjectFile> mapProjectFile(Ihc.Soap.Controller.WSFile wsFile)
        {
            return new ProjectFile()
            {
                Data = await decompress(wsFile.data),
                Filename = wsFile.filename
            };
        }

        private async Task<Ihc.Soap.Controller.WSFile> mapProjectFile(ProjectFile projectFile)
        {
            return new Ihc.Soap.Controller.WSFile()
            {
                data = await compress(projectFile.Data),
                filename = projectFile.Filename
            };
        }

/*
        private ControllerState mapControllerState(Ihc.Soap.Controller.WSControllerState state)
        {
            switch (state?.state)
            {
                case "text.ctrl.state.ready":
                    return ControllerState.Ready;
                default:
                    return ControllerState.Unknown;
            }
       }*/

        public async Task<bool> IsIHCProjectAvailable()
        {
            var result = await impl.isIHCProjectAvailableAsync(new inputMessageName14() { });
            return result.isIHCProjectAvailable1.HasValue ? result.isIHCProjectAvailable1.Value : false;
        }

        public async Task<bool> IsSDCardReady()
        {
            var result = await impl.isSDCardReadyAsync(new inputMessageName9() { });
            return result.isSDCardReady1.HasValue ? result.isSDCardReady1.Value : false;
        }

        public async Task<SDInfo> GetSDCardInfo()
        {
            var result = await impl.getSdCardInfoAsync(new inputMessageName5() { });
            return result.getSdCardInfo1 != null ? mapSDCardData(result.getSdCardInfo1) : null;
        }
/*
        public async Task<ControllerState> GetControllerState()
        {
            var result = await impl.getStateAsync(new inputMessageName1() { });
            return mapControllerState(result.getState1);
        }*/

        public async Task<string> GetControllerState()
        {
            var result = await impl.getStateAsync(new inputMessageName1() { });
            return result.getState1.state; // TODO: Convert to ControllerState oncce the enum is more complete.
        }
        
        public async Task<string> WaitForControllerStateChange(string waitState, int waitSec)
        {
            var result = await impl.waitForControllerStateChangeAsync(new inputMessageName19(new Ihc.Soap.Controller.WSControllerState() { state = waitState }, waitSec) { });
            return result?.waitForControllerStateChange3?.state;
        }

        public async Task<float> GetS0MeterValue()
        {
            var result = await impl.getS0MeterValueAsync(new inputMessageName11() { });
            return result.getS0MeterValue1.HasValue ? result.getS0MeterValue1.Value : 0.0f;
        }
        
        public async Task ResetS0Values() {
            await impl.resetS0ValuesAsync(new inputMessageName10() { });
        }

        public async Task SetS0Consumption(float consumption, bool flag)
        {
            await impl.setS0ConsumptionAsync(new inputMessageName6(consumption, flag) { });
        }

        public async Task SetS0FiscalYearStart(sbyte month, sbyte day)
        {
            await impl.setS0FiscalYearStartAsync(new inputMessageName20(month, day) { });
        }

        public async Task<int> Restore()
        {
            var result = await impl.restoreAsync(new inputMessageName7() { });
            return result.restore1.HasValue ? result.restore1.Value : 0;
        }

        public async Task<BackupFile> GetBackup()
        {
            var result = await impl.getBackupAsync(new inputMessageName2() { });
            return mapBackup(result.getBackup1);
        }

        public async Task<ProjectInfo> GetProjectInfo()
        {
            var result = await impl.getProjectInfoAsync(new inputMessageName8() { });
            return mapProjectInfo(result.getProjectInfo1);
        }
        
        public async Task<ProjectFile> GetProject()
        {
            var result = await impl.getIHCProjectAsync(new inputMessageName3() { });
            return await mapProjectFile(result.getIHCProject1);
        }

        protected async Task<bool> EnterProjectChangeMode()
        {
            var result = await impl.enterProjectChangeModeAsync(new inputMessageName12() { });
            return result?.enterProjectChangeMode1 != null && result.enterProjectChangeMode1.Value;
        }

        protected async Task<bool> ExitProjectChangeMode()
        {
            var result = await impl.exitProjectChangeModeAsync(new inputMessageName13() { });
            return result?.exitProjectChangeMode1 != null && result.exitProjectChangeMode1.Value;
        }

        public async Task<bool> StoreProject(ProjectFile project)
        {          
          
            // First perform some safty checks similar to what the IHC Visual App does:
            bool controllerReady = (await GetControllerState()) == ControllerStates.READY;
            if (!controllerReady)
                throw new InvalidOperationException("Controller not in ready state");

            bool sdCardReady = await IsSDCardReady();
            if (!sdCardReady)
                throw new InvalidOperationException("Controller SD Card not ready");

            bool projectAvailable = await IsIHCProjectAvailable();
            if (!projectAvailable)
                throw new InvalidOperationException("Controller has no project available");

            bool inChange = await EnterProjectChangeMode();
            if (!inChange)
                throw new InvalidOperationException("Controller could not enter change mode to prepare for project change");

            var state = await WaitForControllerStateChange(ControllerStates.INIT, 10); // TODO: Retry x times.
            if (state != ControllerStates.INIT)
                throw new InvalidOperationException("Controller state did not enter init state to prepare for project change");

            // TODO: ResourceInteractionService.getRuntimeValues

            outputMessageName4 result;
            try
            {
                // Call the actual store project operation

                inputMessageName4 request = new inputMessageName4()
                {
                    storeIHCProject1 = await mapProjectFile(project)
                };

                result = await impl.storeIHCProjectAsync(request);

                state = await WaitForControllerStateChange(ControllerStates.INIT, 10); // TODO: Retry x times.
                if (state != ControllerStates.INIT)
                    throw new InvalidOperationException("Controller state does not remain in init state after project change");


                // TODO: setResourceValues
            }
            finally
            {
                inChange = await ExitProjectChangeMode();
                if (!inChange)
                    throw new InvalidOperationException("Controller could not exit change mode after project change");

                state = await WaitForControllerStateChange(ControllerStates.READY, 10); // TODO: Retry x times.
                if (state != ControllerStates.READY)
                    throw new InvalidOperationException("Controller state did not enter init state to prepare for project change");
            }

            await Task.Delay(100); // Wait a little to let controller settle.

            return result.storeIHCProject2 != null && result.storeIHCProject2.Value;
        }
    }
}