namespace Ihc.Stubbing
{
    /// <summary>The five period flags an S0 reset carries, in wire order, as the journal records them.</summary>
    public sealed record S0ResetFlags(bool Hour, bool Day, bool Week, bool Month, bool Year);

    /// <summary>
    /// The controller's state and its project. Entering project change mode moves the state to
    /// <see cref="ControllerState.Initialize"/> and leaving it moves it back to <see cref="ControllerState.Ready"/>,
    /// at once, so a state wait always finds the state it waits for already reached.
    /// </summary>
    public sealed class StubController
    {
        private readonly Lock _gate;

        /// <summary>The one stored project, shared by every view of it, so it is an explicit field that one hold of
        /// the gate covers across a whole read-modify-write.</summary>
        private ProjectFile? _project = DefaultScenario.Project;

        /// <summary>The stored project's download slices, for the project and size they were cut for.</summary>
        private (ProjectFile Project, int Size, byte[][] Slices)? _slices;

        /// <summary>How many segments the current segmented upload has received; any other way of replacing the
        /// project ends that upload.</summary>
        private int _segmentsStored;

        /// <summary>The current upload's slices joined so far, the segment count its first slice announced, and the
        /// name it arrived under.</summary>
        private readonly List<byte> _upload = [];
        private int _uploadCount;
        private string _uploadName = "";

        internal StubController(Lock gate)
        {
            _gate = gate;
        }

        /// <summary>The state every state query and state wait answers.</summary>
        public ControllerState State
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = ControllerState.Ready;

        /// <summary>Whether the SD card is ready.</summary>
        public bool SdCardReady
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = true;

        /// <summary>
        /// The stored project, or null for a controller with none. A stored project is kept as it arrived, so
        /// line endings and ISO-8859-1 characters come back unchanged.
        /// </summary>
        public ProjectFile? Project
        {
            get => Gated.Read(_gate, ref _project);
            set
            {
                lock (_gate)
                {
                    _project = value;
                    _segmentsStored = 0;
                }
            }
        }

        /// <summary>The SD card's size and free space, or null for a controller that reports none. The default is a
        /// real controller's answer.</summary>
        public SDInfo? SdCard
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = new() { Size = 479690752, Free = 468685824 };

        /// <summary>The S0 meter reading. Resetting the S0 values sets it back to zero whichever period flags the reset
        /// carries, a stand-in: what a controller resets per flag is not known.</summary>
        public float S0MeterValue
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>The S0 consumption last set, with the flag it was set with.</summary>
        public (float Consumption, bool Flag) S0Consumption
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>The start of the S0 fiscal year last set, read as a controller reads the two parts: the day of the
        /// month first, the month second, as sent (the vendor client sends both 0-based); <c>null</c> until set.</summary>
        public (sbyte Day, sbyte Month)? S0FiscalYearStart
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>What a restore answers. Nothing on record shows what a real controller's restore returns, so the
        /// default is zero, the same as an unset number.</summary>
        public int RestoreResult
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>
        /// The size, in bytes of gzip data, of each segment a download serves: the project's gzip stream is cut into
        /// slices of this size, the last one shorter. A 3.3.44 controller answers 7,500. An upload's segments are
        /// whatever the caller sends.
        /// </summary>
        public int SegmentationSize
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                Gated.Write(_gate, ref field, value);
            }
        } = 7500;

        /// <summary>What a project-info query answers while a project is stored.</summary>
        public ProjectInfo ProjectInfo
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = DefaultScenario.ProjectInfo;

        /// <summary>Whether a project store is accepted, whole or by segment. A declined store answers "declined" and
        /// leaves the stored project as it was.</summary>
        public bool StoreProjectAccepted
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        } = true;

        /// <summary>The project info, or null while no project is stored.</summary>
        internal ProjectInfo? StoredProjectInfo()
        {
            lock (_gate)
            {
                return _project is null ? null : ProjectInfo;
            }
        }

        /// <summary>Stores <paramref name="project"/> when stores are accepted, and says whether it did.</summary>
        internal bool Store(ProjectFile project)
        {
            lock (_gate)
            {
                if (!StoreProjectAccepted)
                {
                    return false;
                }
                _project = project;
                _segmentsStored = 0;
                return true;
            }
        }

        /// <summary>How many segments a download of the stored project takes: its gzip stream's size over
        /// <see cref="SegmentationSize"/>, rounded up, as a controller counts them; none while no project is stored.
        /// A gzip stream is never empty, so a stored project always has a segment.</summary>
        internal int NumberOfSegments()
        {
            lock (_gate)
            {
                return StoredSlices().Length;
            }
        }

        /// <summary>Segment <paramref name="index"/> of a download: that slice of the stored project's gzip stream,
        /// raw, so only the first starts with the gzip header; null when there is no such segment.</summary>
        internal byte[]? Segment(int index)
        {
            lock (_gate)
            {
                return StoredSlices().ElementAtOrDefault(index);
            }
        }

        /// <summary>The stored project's gzip stream cut at <see cref="SegmentationSize"/>, the last slice shorter;
        /// none while no project is stored. Cut once per project and size, so every slice of a download comes from
        /// the same stream. Called under the gate.</summary>
        private byte[][] StoredSlices()
        {
            if (_project is null)
            {
                return [];
            }
            if (_slices is not { } cut || !ReferenceEquals(cut.Project, _project) || cut.Size != SegmentationSize)
            {
                _slices = cut = (_project, SegmentationSize, [.. ProjectGzip.Of(_project).Chunk(SegmentationSize)]);
            }
            return cut.Slices;
        }

        /// <summary>
        /// Receives slice <paramref name="index"/> of an upload of <paramref name="segmentCount"/> slices of one gzip
        /// stream of the project. Index 0 starts a new upload; the next index, with the same count, appends; anything
        /// else changes nothing and answers false. The project is replaced only when the last slice arrives and the
        /// joined slices decompress, the way a controller unpacks an upload; joined slices that do not answer false
        /// to the last one and leave the project as it was. While stores are declined every slice is, as a whole store
        /// would be.
        /// </summary>
        internal bool StoreSegment(byte[] slice, string filename, int index, int segmentCount)
        {
            lock (_gate)
            {
                if (!StoreProjectAccepted || segmentCount < 1 || index < 0 || index >= segmentCount)
                {
                    return false;
                }
                if (index == 0)
                {
                    _upload.Clear();
                    _uploadCount = segmentCount;
                    _uploadName = filename;
                }
                else if (index != _segmentsStored || segmentCount != _uploadCount)
                {
                    return false;
                }
                _upload.AddRange(slice);
                _segmentsStored = index + 1;
                if (_segmentsStored < segmentCount)
                {
                    return true;
                }
                _segmentsStored = 0;
                try
                {
                    _project = ProjectGzip.ToProject(_uploadName, [.. _upload]);
                    return true;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
            }
        }
    }
}
