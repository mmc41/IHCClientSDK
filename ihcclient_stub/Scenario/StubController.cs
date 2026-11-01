namespace Ihc.Stubbing
{
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

        /// <summary>How many segments the current segmented upload has stored; any other way of replacing the
        /// project ends that upload.</summary>
        private int _segmentsStored;

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

        /// <summary>The S0 meter reading. Resetting the S0 values sets it back to zero.</summary>
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

        /// <summary>What a restore answers. Nothing on record shows what a real controller's restore returns, so the
        /// default is zero, the same as an unset number.</summary>
        public int RestoreResult
        {
            get => Gated.Read(_gate, ref field);
            set => Gated.Write(_gate, ref field, value);
        }

        /// <summary>The size, in characters, of each segment of the segmented view of <see cref="Project"/>. A
        /// project's characters are its ISO-8859-1 bytes, so this is also its size in bytes.</summary>
        public int SegmentationSize
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                Gated.Write(_gate, ref field, value);
            }
        } = 8192;

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

        /// <summary>How many segments the stored project splits into: at least one, so an empty project still has
        /// a segment to download; none while no project is stored.</summary>
        internal int NumberOfSegments()
        {
            lock (_gate)
            {
                return _project is null ? 0 : Math.Max(1, (_project.Data.Length + SegmentationSize - 1) / SegmentationSize);
            }
        }

        /// <summary>Segment <paramref name="index"/> of the stored project, or null when there is no such segment.</summary>
        internal ProjectFile? Segment(int index)
        {
            lock (_gate)
            {
                if (_project is null || index < 0 || index >= NumberOfSegments())
                {
                    return null;
                }
                int start = index * SegmentationSize;
                return _project with { Data = _project.Data.Substring(start, Math.Min(SegmentationSize, _project.Data.Length - start)) };
            }
        }

        /// <summary>
        /// Stores segment <paramref name="index"/> of an upload. Index 0 starts a new upload and replaces the stored
        /// project at once; the next index appends; any other index changes nothing and answers false. A segment
        /// carries no total and no last flag, so the stored project is always the segments received so far. While
        /// stores are declined every segment is, as a whole store would be.
        /// </summary>
        internal bool StoreSegment(ProjectFile segment, int index)
        {
            lock (_gate)
            {
                if (!StoreProjectAccepted)
                {
                    return false;
                }
                if (index == 0)
                {
                    _project = segment;
                    _segmentsStored = 1;
                    return true;
                }
                if (_project is null || index != _segmentsStored)
                {
                    return false;
                }
                _project = _project with { Data = _project.Data + segment.Data };
                _segmentsStored++;
                return true;
            }
        }
    }
}
