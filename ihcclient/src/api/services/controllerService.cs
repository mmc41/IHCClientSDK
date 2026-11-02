using System.Threading.Tasks;
using System;
using System.Linq;
using Ihc.Soap.Controller;
using System.IO;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Ihc {
    /// <summary>
    /// The controller's own functions: its run state with a long poll on it, the SD card, the stored project (fetch,
    /// send in project change mode, restore from the SD card) and the single S0 energy meter.
    /// </summary>
    public interface IControllerService : IIHCApiService
    {
        /// <summary>
        /// Checks whether the controller holds an IHC project.
        /// </summary>
        /// <returns><c>true</c> when the controller holds a project; <c>false</c> also when the reply carries no
        /// value.</returns>
        public Task<bool> IsIHCProjectAvailable();

        /// <summary>
        /// Checks whether the SD card is ready and accessible. Without a ready card a project send is not backed up to
        /// it, so <see cref="Restore"/> cannot bring the project back.
        /// </summary>
        /// <returns><c>true</c> when the card is ready; <c>false</c> also when the reply carries no value.</returns>
        public Task<bool> IsSDCardReady();

        /// <summary>
        /// Gets the size and free space of the SD card.
        /// </summary>
        /// <returns>The card information, or <c>null</c> when the reply carries none.</returns>
        public Task<SDInfo?> GetSDCardInfo();

        /// <summary>
        /// Reads the controller's current run state; a state key this SDK does not know, or none, gives
        /// <see cref="ControllerState.Unknown"/>. Follow later changes with <see cref="WaitForControllerStateChange"/>.
        /// </summary>
        public Task<ControllerState> GetControllerState();

        /// <summary>
        /// Long-polls the run state: the controller holds the call while its state equals
        /// <paramref name="waitState"/> and answers with the new state as soon as it changes, or with the unchanged
        /// state when the hold runs out.
        /// </summary>
        /// <param name="waitState">The state the caller believes current, normally the last one read; not a target
        /// state. <see cref="ControllerState.Unknown"/> is refused with an <see cref="ArgumentException"/>.</param>
        /// <param name="waitSec">Longest time, in seconds, the controller holds the call.</param>
        /// <returns>The state the controller answered with; <see cref="ControllerState.Unknown"/> when the reply names
        /// none.</returns>
        public Task<ControllerState> WaitForControllerStateChange(ControllerState waitState, int waitSec);

        /// <summary>
        /// Reads the metadata of the stored project, taken from the stored file itself: its identity pair, which
        /// segment downloads pass back, its format version, number, customer, installer and last-save stamp.
        /// </summary>
        /// <returns>The information, or <c>null</c> when the reply carries none.</returns>
        public Task<ProjectInfo?> GetProjectInfo();

        /// <summary>
        /// Downloads the whole stored project in one call and returns its XML, which the SDK decompresses. A
        /// segmented download exists too (<see cref="GetIHCProjectSegment"/>), but see its known defect.
        /// </summary>
        /// <returns>The project, or <c>null</c> when the reply carries no file.</returns>
        public Task<ProjectFile?> GetProject();

        /// <summary>
        /// Uploads and stores a new project, sending it whole in one call. It checks that the controller is ready,
        /// enters and leaves project change mode with <see cref="EnterProjectChangeMode"/> and
        /// <see cref="ExitProjectChangeMode"/>, and waits for the state changes. It does not reset runtime values or
        /// restart the controller; call <see cref="IConfigurationService.DelayedReboot"/> if needed.
        /// </summary>
        /// <param name="project">Project file to store on the controller.</param>
        /// <returns>The controller's answer to the store; a reply that carries none throws instead.</returns>
        /// <remarks>Before entering change mode it refuses a controller that holds no project, and project text
        /// outside ISO-8859-1.</remarks>
        /// <exception cref="InvalidOperationException">The controller is not ready or its SD card is
        /// unavailable.</exception>
        public Task<bool> StoreProject(ProjectFile project);

        /// <summary>
        /// Gets a backup file (name and raw bytes) from the controller.
        /// </summary>
        /// <returns>The backup, or <c>null</c> when the reply carries no file.</returns>
        public Task<BackupFile?> GetBackup();

        /// <summary>
        /// Restores the project kept on the controller's SD card, wireless links included; every project stored on
        /// the controller overwrites that copy. It is not a factory reset.
        /// </summary>
        /// <returns>The controller's reply value; 0 when the reply carries no value.</returns>
        public Task<int> Restore();

        /// <summary>
        /// Downloads one segment of the stored project: a slice of the controller's own gzip stream of it; joined in
        /// index order, the segments decompress as a whole.
        /// </summary>
        /// <param name="index">Segment index, 0-based, below <see cref="GetIHCProjectNumberOfSegments"/>.</param>
        /// <param name="major">First half of the stored project's identity:
        /// <see cref="ProjectInfo.ProjectMajorRevision"/> from <see cref="GetProjectInfo"/>.</param>
        /// <param name="minor">Second half of the stored project's identity:
        /// <see cref="ProjectInfo.ProjectMinorRevision"/> from <see cref="GetProjectInfo"/>.</param>
        /// <returns>The segment, or <c>null</c> when the reply carries no file.</returns>
        /// <remarks>Known defect: this method decompresses each segment on its own, but only segment 0 starts with the
        /// gzip header, so it cannot decode any later segment. Download the whole project with
        /// <see cref="GetProject"/> instead.</remarks>
        public Task<ProjectFile?> GetIHCProjectSegment(int index, int major, int minor);

        /// <summary>
        /// Uploads one segment of a project. A project is sent as segments of
        /// <see cref="GetIHCProjectSegmentationSize"/> bytes, in index order between
        /// <see cref="EnterProjectChangeMode"/> and <see cref="ExitProjectChangeMode"/>; the reply to the last one can
        /// take many seconds.
        /// </summary>
        /// <param name="segment">The segment's text.</param>
        /// <param name="index">Segment index, 0-based.</param>
        /// <param name="major">The total number of segments, the same in every call; it is not a version.</param>
        /// <returns><c>true</c> when the controller accepted the segment; send the next only after an accepted one.
        /// <c>false</c> also when the reply carries no value.</returns>
        /// <remarks>Known defect: this method gzip-compresses each segment on its own, while the controller's segments
        /// are slices of one gzip stream of the whole project, only the first carrying the gzip header. Whether a
        /// controller accepts a project sent this way in more than one segment is not known; send a project whole
        /// with <see cref="StoreProject"/> instead.</remarks>
        public Task<bool> StoreIHCProjectSegment(ProjectFile segment, int index, int major);

        /// <summary>
        /// Gets the size of project segments in bytes of gzip-compressed data: the slice size an upload is cut into,
        /// and the size of the slices a download returns (7,500 on firmware 3.3.44).
        /// </summary>
        /// <returns>The size in bytes; 0 when the reply carries no value.</returns>
        public Task<int> GetIHCProjectSegmentationSize();

        /// <summary>
        /// Gets the number of segments a download of the stored project takes: the size of the controller's own gzip
        /// stream of it over the segment size, rounded up. Segment indexes run from 0 to this count minus one.
        /// </summary>
        /// <returns>The segment count; 0 when the reply carries no value.</returns>
        public Task<int> GetIHCProjectNumberOfSegments();

        /// <summary>
        /// Resets figures of the controller's single S0 energy meter.
        /// </summary>
        /// <remarks>Known defect: the controller's operation takes five flags, one per period (hour, day, week, month
        /// and year), and this method sends none of them, so which figures it resets is not known.</remarks>
        public Task ResetS0Values();

        /// <summary>
        /// Reads the total of the controller's single S0 energy meter (accumulated consumption in kWh).
        /// </summary>
        /// <returns>The meter total; 0 when the reply carries no value.</returns>
        public Task<float> GetS0MeterValue();

        /// <summary>
        /// Sets the total of the controller's single S0 energy meter, for example to match the physical meter's
        /// reading.
        /// </summary>
        /// <param name="consumption">The new meter total in kWh.</param>
        /// <param name="flag">A reset flag, normally <c>false</c>; probably also resets the period figures.</param>
        public Task SetS0Consumption(float consumption, bool flag);

        /// <summary>
        /// Sets the start of the S0 meter's fiscal (invoicing) year, as a day and month.
        /// </summary>
        /// <param name="month">The day of the month, despite the name (see the remarks); the vendor client sends it
        /// 0-based, so day 1 is 0.</param>
        /// <param name="day">The month, despite the name (see the remarks); the vendor client sends it 0-based, so
        /// January is 0.</param>
        /// <remarks>Known defect: the parameter names are swapped relative to the controller, which reads the first
        /// argument as the day of the month and the second as the month.</remarks>
        public Task SetS0FiscalYearStart(sbyte month, sbyte day);

        /// <summary>
        /// Puts the controller in project change mode, which wraps every project send; the controller reports
        /// <see cref="ControllerState.Initialize"/> from this call until <see cref="ExitProjectChangeMode"/>
        /// completes.
        /// </summary>
        /// <returns><c>true</c> when the controller entered the mode; <c>false</c> when it refused or the reply
        /// carries no value.</returns>
        /// <remarks>Always follow it with <see cref="ExitProjectChangeMode"/>, also after a failed step.</remarks>
        public Task<bool> EnterProjectChangeMode();

        /// <summary>
        /// Takes the controller out of project change mode after project updates; after a send the controller reports
        /// <see cref="ControllerState.Ready"/> again as this call returns.
        /// </summary>
        /// <returns><c>true</c> when the controller left the mode; <c>false</c> when it refused or the reply carries
        /// no value.</returns>
        public Task<bool> ExitProjectChangeMode();
    }

    /// <summary>
    /// A highlevel implementation of a client to the IHC ControllerService without exposing any of the soap distractions.
    /// </summary>
    public class ControllerService : ServiceBase, IControllerService
    {
        private class SoapImpl : ServiceBaseImpl, Ihc.Soap.Controller.ControllerService
        {
            public SoapImpl(ICookieHandler cookieHandler, IhcSettings settings) : base(cookieHandler, settings, "ControllerService") { }

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

        private readonly Ihc.Soap.Controller.ControllerService impl;

        /// <summary>
        /// Create an ControllerService instance for access to the IHC API related to the controller itself.
        /// </summary>
        /// <param name="authService">AuthenticationService instance</param>
        public ControllerService(IAuthenticationService authService)
            : base(SettingsOf(authService))
        {
            this.impl = new SoapImpl(authService.GetCookieHandler(), settings);
        }

        /// <summary>Test seam: inject a fake SOAP layer (used by unit tests only).</summary>
        internal ControllerService(IAuthenticationService authService, Ihc.Soap.Controller.ControllerService impl)
            : base(SettingsOf(authService))
        {
            this.impl = impl;
        }

        private static SDInfo? mapSDCardData(WSSdCardData? e)
        {
            if (e == null)
                return null;

            return new SDInfo()
            {
                Size = e.size,
                Free = e.free
            };
        }

        private static BackupFile? mapBackup(Ihc.Soap.Controller.WSFile? backupFile)
        {
            if (backupFile == null)
                return null;

            if (backupFile.data == null)
            {
                // BinaryFile.Data is a non-null contract the Lab relies on when it writes the bytes out.
                // The generated layer promises nothing, so the check has to be here - name the real problem
                // rather than hand a null on to a caller that has been told it cannot be one.
                throw new InvalidOperationException(
                    $"The controller returned a backup entry ('{backupFile.filename}') carrying no data.");
            }

            return new BackupFile(
                Filename: backupFile.filename,
                Data: backupFile.data // Hmm. Can't identify the file format. Binary?
            );
        }

        /// <summary>
        /// One wire date, through the shared conversion and the shared absent-value answer.
        /// <para>It used to build the <see cref="DateTimeOffset"/> itself, which put a second opinion on what a
        /// corrupt date means beside <see cref="DateHelper.CreateDateTimeOffset"/>'s: that one answers with the
        /// documented sentinel, this one threw. Two sibling services carried a byte-identical copy of the same
        /// helper, so the divergence was written three times.</para>
        /// </summary>
        private static DateTimeOffset mapDate(WSDate? v, string field) =>
            DateHelper.OrAbsentSentinel(v?.ToDateTimeOffset(), field);

        private static ProjectInfo? mapProjectInfo(Ihc.Soap.Controller.WSProjectInfo? projectInfo)
        {
            if (projectInfo == null)
                return null;

            return new ProjectInfo()
            {
                VisualMajorVersion = projectInfo.visualMajorVersion,
                VisualMinorVersion = projectInfo.visualMinorVersion,
                ProjectMajorRevision = projectInfo.projectMajorRevision,
                ProjectMinorRevision = projectInfo.projectMinorRevision,
                Lastmodified = mapDate(projectInfo.lastmodified, nameof(ProjectInfo.Lastmodified)),
                ProjectNumber = projectInfo.projectNumber,
                CustomerName = projectInfo.customerName,
                InstallerName = projectInfo.installerName
            };
        }

        private async Task<string> decompress(byte[] fileData)
        {
            using (MemoryStream mscompressed = new MemoryStream(fileData))
            using (Stream inStream = new System.IO.Compression.GZipStream(mscompressed, System.IO.Compression.CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(inStream, ProjectFile.Encoding, detectEncodingFromByteOrderMarks: false))
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
                    using (StreamWriter writer = new StreamWriter(outStream, ProjectFile.StrictEncoding, bufferSize: 10*1024, leaveOpen: true))
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

        private async Task<ProjectFile?> mapProjectFile(Ihc.Soap.Controller.WSFile? wsFile)
        {
            if (wsFile == null)
                return null;

            if (wsFile.data == null)
            {
                // decompress would otherwise surface this as a bare ArgumentNullException out of MemoryStream.
                throw new InvalidOperationException(
                    $"The controller returned a project entry ('{wsFile.filename}') carrying no data.");
            }

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

            private static ControllerState mapControllerState(Ihc.Soap.Controller.WSControllerState? state)
        {
            // An answer that names no state is not the Uninitialized state: that is a state the controller
            // genuinely reports, by name, in the switch below - and it is also the enum's ZERO member, so
            // folding an unreadable answer onto it made "never assigned", "genuinely uninitialized" and "the
            // answer did not say" one value. Unknown is the member that already means the last of those.
            if (state == null || String.IsNullOrEmpty(state.state))
                return ControllerState.Unknown;

            switch (state.state)
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
                case "text.ctrl.state.uninitialized":
                    return ControllerState.Uninitialized;                    
                default:
                    return ControllerState.Unknown; // Unknown state - add to enum if we find it.
            }
        }


        private static Ihc.Soap.Controller.WSControllerState mapControllerState(ControllerState state)
        {
            string stateStr;
            switch (state)
            {
                case ControllerState.Failed: stateStr = "text.ctrl.state.failed"; break;
                case ControllerState.Ready: stateStr = "text.ctrl.state.ready"; break;
                case ControllerState.Initialize: stateStr = "text.ctrl.state.initialize"; break;
                case ControllerState.RfConfiguration: stateStr = "text.ctrl.state.rfconfiguration"; break;
                case ControllerState.Simulation: stateStr = "text.ctrl.state.simulation"; break;
                case ControllerState.Uninitialized: stateStr = "text.ctrl.state.uninitialized"; break;
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

        public async Task<SDInfo?> GetSDCardInfo()
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
                    if (!result.getS0MeterValue1.HasValue)
                    {
                        // Zero is a REAL meter reading - a meter that has counted nothing - so the substitution
                        // is indistinguishable from one. The value stays; the warning says it was substituted.
                        activity.AddWarning(
                            "The controller answered no S0 meter value; substituting 0.",
                            ("type", "AbsentWireValue"),
                            ("field", nameof(GetS0MeterValue)));
                    }
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

        public async Task<BackupFile?> GetBackup()
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

        public async Task<ProjectInfo?> GetProjectInfo()
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

        public async Task<ProjectFile?> GetProject()
        {
            using (var activity = StartActivity(nameof(GetProject)))
            {
                try
                {
                    var result = await impl.getIHCProjectAsync(new inputMessageName3() { }).ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    var retv = await mapProjectFile(result.getIHCProject1).ConfigureAwait(settings.AsyncContinueOnCapturedContext);

                    // Metadata only — the full project XML does not belong in telemetry tags.
                    activity?.SetReturnValue(retv == null ? null : $"{retv.Filename} ({retv.Data?.Length ?? 0} chars)");
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
                    // Metadata only — the full project XML does not belong in telemetry tags.
                    activity?.SetParameters(
                        ("project.filename", project?.Filename),
                        ("project.data.length", project?.Data?.Length ?? 0)
                    );

                    ValidationHelper.ValidateDataAnnotations(project, nameof(project));
                    if (project.Data is null)
                    {
                        // Data has no [Required], so a null slips past ValidateDataAnnotations and would
                        // surface as a bare ArgumentNullException from GetBytes — name the real problem.
                        throw new InvalidOperationException(
                            $"Project '{project.Filename}' has no Data to store; nothing was sent to the controller.");
                    }

                    // Fail before entering change mode: an out-of-repertoire character would otherwise either
                    // abort mid-change-mode or (with a replacement fallback) be silently stored as '?'.
                    try
                    {
                        ProjectFile.StrictEncoding.GetBytes(project.Data);
                    }
                    catch (EncoderFallbackException ex)
                    {
                        string offender = ex.OffendingCodePointLabel();
                        throw new InvalidOperationException(
                            $"Project '{project.Filename}' contains character {offender} at index {ex.Index}, " +
                            "outside the ISO-8859-1 repertoire required by the IHC controller. Load the file with " +
                            $"ISO-8859-1 ({nameof(ProjectFile)}.{nameof(ProjectFile.Encoding)}), not UTF-8.", ex);
                    }

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
                        throw new InvalidOperationException(
                            "Controller reports no existing project (IsIHCProjectAvailable=false). Storing onto an " +
                            "empty controller is unsupported by this SDK because the firmware behavior is unverified; " +
                            "restore a project via IHC Visual first.");

                    bool inChange = await EnterProjectChangeMode().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                    activity?.SetTag("progress.inChange.in", inChange);
                    if (!inChange)
                        throw new InvalidOperationException("Controller could not enter change mode to prepare for project change");

                    var state = await WaitForControllerStateChange(ControllerState.Initialize, 10).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // TODO: Retry x times.
                    activity?.SetTag("progress.state.in", state);                    
                    if (state != ControllerState.Initialize)
                        throw new InvalidOperationException("Controller state did not enter init state to prepare for project change");

                    outputMessageName4? result = null;
                    Exception? primary = null;
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
                    catch (Exception storeEx)
                    {
                        // Do not throw yet: the change-mode cleanup below must run first, and a cleanup failure
                        // must never replace this root cause (an exception leaving a finally block would).
                        primary = storeEx;
                    }
                    try
                    {
                        inChange = await ExitProjectChangeMode().ConfigureAwait(settings.AsyncContinueOnCapturedContext);
                        activity?.SetTag("progress.inChange.out", inChange);
                        if (!inChange && primary == null)
                            throw new InvalidOperationException("Controller could not exit change mode after project change");

                        state = await WaitForControllerStateChange(ControllerState.Ready, 10).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // TODO: Retry x times.
                        activity?.SetTag("progress.state.out", state);
                        if (state != ControllerState.Ready && primary == null)
                            throw new InvalidOperationException($"Controller did not return to {nameof(ControllerState.Ready)} state after project change (observed state: {state})");
                    }
                    catch (Exception cleanupEx) when (primary != null)
                    {
                        activity?.SetTag("cleanup.error", cleanupEx.ToString());
                    }
                    if (primary != null)
                        ExceptionDispatchInfo.Capture(primary).Throw();

                    await Task.Delay(100).ConfigureAwait(settings.AsyncContinueOnCapturedContext); // Wait a little to let controller settle.

                    // An absent acknowledgement element is not a declined store: the bytes were sent, and the
                    // answer simply did not say what became of them. Folded to `false` it is indistinguishable
                    // from a controller that refused, which is the one thing a caller would act on differently.
                    if (result?.storeIHCProject2 is null)
                        throw new InvalidOperationException("Controller did not acknowledge the project store: the response carried no result. The project bytes were sent, so the controller's state is unknown.");

                    var retv = result.storeIHCProject2 == true;

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

        public async Task<ProjectFile?> GetIHCProjectSegment(int index, int major, int minor)
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

                    ValidationHelper.ValidateDataAnnotations(segment, nameof(segment));

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