using Ihc.Soap.Controller;

namespace Ihc.Stubbing
{
    /// <summary>The controller service's answers, read from and written to <see cref="StubController"/>.</summary>
    internal sealed class ControllerAnswers(StubController scenario) : IWireAnswers
    {
        public string Service => "ControllerService";

        internal Task<outputMessageName1> getStateAsync(inputMessageName1 request) =>
            Task.FromResult(new outputMessageName1 { getState1 = StateToWire(scenario.State) });

        /// <summary>Answers the current state at once. The stub's transitions are synchronous, so the state a
        /// caller waits for has always been reached already; held like a long poll, each wait would only add
        /// delay to an upload.</summary>
        internal Task<outputMessageName19> waitForControllerStateChangeAsync(inputMessageName19 request) =>
            Task.FromResult(new outputMessageName19 { waitForControllerStateChange3 = StateToWire(scenario.State) });

        internal Task<outputMessageName9> isSDCardReadyAsync(inputMessageName9 request) =>
            Task.FromResult(new outputMessageName9 { isSDCardReady1 = scenario.SdCardReady });

        internal Task<outputMessageName14> isIHCProjectAvailableAsync(inputMessageName14 request) =>
            Task.FromResult(new outputMessageName14 { isIHCProjectAvailable1 = scenario.Project is not null });

        internal Task<outputMessageName8> getProjectInfoAsync(inputMessageName8 request) =>
            Task.FromResult(new outputMessageName8 { getProjectInfo1 = ProjectInfoToWire(scenario.StoredProjectInfo()) });

        internal Task<outputMessageName12> enterProjectChangeModeAsync(inputMessageName12 request)
        {
            scenario.State = ControllerState.Initialize;
            return Task.FromResult(new outputMessageName12 { enterProjectChangeMode1 = true });
        }

        internal Task<outputMessageName13> exitProjectChangeModeAsync(inputMessageName13 request)
        {
            scenario.State = ControllerState.Ready;
            return Task.FromResult(new outputMessageName13 { exitProjectChangeMode1 = true });
        }

        internal Task<outputMessageName4> storeIHCProjectAsync(inputMessageName4 request)
        {
            ProjectFile project = ProjectFromWire(request.storeIHCProject1);
            AnswerContext.RecordPayload(project);
            return Task.FromResult(new outputMessageName4 { storeIHCProject2 = scenario.Store(project) });
        }

        internal Task<outputMessageName3> getIHCProjectAsync(inputMessageName3 request) =>
            Task.FromResult(new outputMessageName3 { getIHCProject1 = ProjectToWire(scenario.Project) });

        internal Task<outputMessageName17> getIHCProjectSegmentationSizeAsync(inputMessageName17 request) =>
            Task.FromResult(new outputMessageName17 { getIHCProjectSegmentationSize1 = scenario.SegmentationSize });

        internal Task<outputMessageName18> getIHCProjectNumberOfSegmentsAsync(inputMessageName18 request) =>
            Task.FromResult(new outputMessageName18 { getIHCProjectNumberOfSegments1 = scenario.NumberOfSegments() });

        internal Task<outputMessageName15> getIHCProjectSegmentAsync(inputMessageName15 request) =>
            Task.FromResult(new outputMessageName15 { getIHCProjectSegment4 = SegmentToWire(scenario.Segment(request.getIHCProjectSegment1 ?? -1)) });

        /// <summary>A slice is kept raw: part 2 is its index and part 3 the upload's segment count, and the slices are
        /// decoded only once all of them have arrived.</summary>
        internal Task<outputMessageName16> storeIHCProjectSegmentAsync(inputMessageName16 request)
        {
            WSFile file = request.storeIHCProjectSegment1
                ?? throw new InvalidOperationException("a segment store carried no file");
            var segment = new ProjectSegment { Data = file.data ?? [], Filename = file.filename };
            AnswerContext.RecordPayload(segment);
            bool accepted = scenario.StoreSegment(segment.Data, file.filename ?? "", request.storeIHCProjectSegment2 ?? -1, request.storeIHCProjectSegment3 ?? 0);
            return Task.FromResult(new outputMessageName16 { storeIHCProjectSegment4 = accepted });
        }

        internal Task<outputMessageName5> getSdCardInfoAsync(inputMessageName5 request) =>
            Task.FromResult(new outputMessageName5 { getSdCardInfo1 = SdCardToWire(scenario.SdCard) });

        internal Task<outputMessageName11> getS0MeterValueAsync(inputMessageName11 request) =>
            Task.FromResult(new outputMessageName11 { getS0MeterValue1 = scenario.S0MeterValue });

        /// <summary>Journals the five period flags, hour first, as sent. Zeroing the reading whatever the flags is a
        /// stand-in: what a controller resets per flag is not known.</summary>
        internal Task<outputMessageName10> resetS0ValuesAsync(inputMessageName10 request)
        {
            AnswerContext.RecordPayload(new S0ResetFlags(request.resetS0Values1 ?? false, request.resetS0Values2 ?? false,
                request.resetS0Values3 ?? false, request.resetS0Values4 ?? false, request.resetS0Values5 ?? false));
            scenario.S0MeterValue = 0;
            return Task.FromResult(new outputMessageName10());
        }

        internal Task<outputMessageName6> setS0ConsumptionAsync(inputMessageName6 request)
        {
            scenario.S0Consumption = (request.setS0Consumption1 ?? 0, request.setS0Consumption2 ?? false);
            return Task.FromResult(new outputMessageName6());
        }

        /// <summary>Part 1 is the day of the month and part 2 the month, as IHC ServiceView sends them.</summary>
        internal Task<outputMessageName20> setS0FiscalYearStartAsync(inputMessageName20 request)
        {
            scenario.S0FiscalYearStart = (request.setS0FiscalYearStart1 ?? 0, request.setS0FiscalYearStart2 ?? 0);
            return Task.FromResult(new outputMessageName20());
        }

        internal Task<outputMessageName7> restoreAsync(inputMessageName7 request) =>
            Task.FromResult(new outputMessageName7 { restore1 = scenario.RestoreResult });

        private static WSSdCardData? SdCardToWire(SDInfo? card) =>
            card is null ? null : new WSSdCardData { size = card.Size, free = card.Free };

        private static WSControllerState StateToWire(ControllerState state) => new()
        {
            state = state switch
            {
                ControllerState.Failed => "text.ctrl.state.failed",
                ControllerState.Ready => "text.ctrl.state.ready",
                ControllerState.Initialize => "text.ctrl.state.initialize",
                ControllerState.RfConfiguration => "text.ctrl.state.rfconfiguration",
                ControllerState.Simulation => "text.ctrl.state.simulation",
                ControllerState.Uninitialized => "text.ctrl.state.uninitialized",
                // A name the SDK reads as no state at all, which is what Unknown means to it.
                _ => string.Empty,
            },
        };

        internal static WSProjectInfo? ProjectInfoToWire(ProjectInfo? info) => info is null ? null : new WSProjectInfo
        {
            visualMajorVersion = info.VisualMajorVersion,
            visualMinorVersion = info.VisualMinorVersion,
            projectMajorRevision = info.ProjectMajorRevision,
            projectMinorRevision = info.ProjectMinorRevision,
            lastmodified = WireDates.ToWire<WSDate>(info.Lastmodified),
            projectNumber = info.ProjectNumber,
            customerName = info.CustomerName,
            installerName = info.InstallerName,
        };

        /// <summary>The project as the controller sends it: its ISO-8859-1 bytes, gzip-compressed.</summary>
        internal static WSFile? ProjectToWire(ProjectFile? project) => project is null ? null : new WSFile
        {
            filename = project.Filename,
            data = ProjectGzip.Of(project),
        };

        /// <summary>The name a 3.3.44 controller gives every project segment it serves, whatever the project's own
        /// name; scene project segments are named differently.</summary>
        internal const string SegmentFilename = "unknown.bin";

        /// <summary>A segment as the controller sends it: a raw slice of its gzip stream of the project, under the
        /// name it gives every segment.</summary>
        internal static WSFile? SegmentToWire(byte[]? slice) => slice is null ? null : new WSFile
        {
            filename = SegmentFilename,
            data = slice,
        };

        /// <summary>The project a store carried, kept opaque: its gzip decoded to ISO-8859-1, which maps every byte to
        /// one character, so line endings and letters come back exactly as they were sent.</summary>
        private static ProjectFile ProjectFromWire(WSFile? file)
        {
            ArgumentNullException.ThrowIfNull(file);
            return ProjectGzip.ToProject(file.filename, file.data);
        }
    }
}
