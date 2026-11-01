using System.Collections.Immutable;

namespace Ihc.Stubbing
{
    /// <summary>
    /// The one resource store, and what the controller logs and positions around it. A set through the SDK, or
    /// <see cref="Change"/>, writes the value and queues it as a change for the next wait of each API that waits for
    /// changes: the resource interaction service's and OpenAPI's.
    /// </summary>
    public sealed class StubResources
    {
        private readonly Lock _gate;

        /// <summary>The store, explicit because a set writes it and queues a change in one hold of the gate.</summary>
        private ImmutableDictionary<int, ResourceValue> _values;

        internal StubResources(Lock gate)
        {
            _gate = gate;
            Changes = new PendingQueue<ResourceValue>(gate);
            Events = new PendingQueue<ResourceValue>(gate);
            _values = DatalineInputs.ToImmutableDictionary(input => input.ResourceID, input => ResourceValue.CreateBooleanRuntimeInput(input.ResourceID, false) with
            {
                ValueTime = ShapeDefaults.Instant,
            });
        }

        /// <summary>The stored values by resource id. Replacing them queues no change. A read of an id with no stored
        /// value answers the boolean false.</summary>
        public ImmutableDictionary<int, ResourceValue> Values
        {
            get => Gated.Read(_gate, ref _values);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref _values, value);
            }
        }

        /// <summary>The dataline inputs. The default is the inputs a real controller listed, each with its value off.</summary>
        public ImmutableArray<DatalineResource> DatalineInputs
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                TypedValues.RequireInitialized(value);
                Gated.Write(_gate, ref field, value);
            }
        } =
        [
            new() { DatalineNumber = 1, ResourceID = 23386 },
            new() { DatalineNumber = 9, ResourceID = 23898 },
            new() { DatalineNumber = 17, ResourceID = 28762 },
            new() { DatalineNumber = 32, ResourceID = 23642 },
            new() { DatalineNumber = 128, ResourceID = 25946 },
        ];

        /// <summary>The logged data of each resource that has any.</summary>
        public ImmutableDictionary<int, ImmutableArray<LoggedData>> LoggedData
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = ImmutableDictionary<int, ImmutableArray<LoggedData>>.Empty;

        /// <summary>The scene resources and positions of each scene group that has any.</summary>
        public ImmutableDictionary<int, ImmutableArray<SceneResourceIdAndLocation>> SceneGroupPositions
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = ImmutableDictionary<int, ImmutableArray<SceneResourceIdAndLocation>>.Empty;

        /// <summary>The positions of each scene value resource that has them. Any other resource answers its own id
        /// with empty positions, as a real controller does.</summary>
        public ImmutableDictionary<int, SceneResourceIdAndLocation> ScenePositions
        {
            get => Gated.Read(_gate, ref field);
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Gated.Write(_gate, ref field, value);
            }
        } = ImmutableDictionary<int, SceneResourceIdAndLocation>.Empty;

        /// <summary>Writes <paramref name="value"/> and queues it for the next wait, as a change on the controller
        /// itself would be.</summary>
        public void Change(ResourceValue value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Write([value]);
        }

        /// <summary>The value of each id, in the order asked; an id with nothing stored reads as the boolean false.</summary>
        internal ImmutableArray<ResourceValue> Read(IEnumerable<int> ids)
        {
            lock (_gate)
            {
                return [.. ids.Select(id => _values.GetValueOrDefault(id) ?? Unset(id))];
            }
        }

        internal void Write(IReadOnlyCollection<ResourceValue> values)
        {
            lock (_gate)
            {
                foreach (ResourceValue value in values)
                {
                    _values = _values.SetItem(value.ResourceID, value);
                }
                Changes.Add(values);
                Events.Add(values);
            }
        }

        /// <summary>
        /// Writes bare values, as an API that carries no resource type sends them: each keeps the type and the
        /// runtime flag its resource already has, and a resource with nothing stored becomes a runtime value of no
        /// stated type. Answers what was written.
        /// </summary>
        internal ImmutableArray<ResourceValue> WriteBare(IEnumerable<(int Id, ResourceValue.UnionValue Value)> values)
        {
            lock (_gate)
            {
                ImmutableArray<ResourceValue> written =
                [
                    .. values.Select(bare => _values.GetValueOrDefault(bare.Id) is { } stored
                        ? stored with { Value = bare.Value, ValueTime = ShapeDefaults.Instant }
                        : new ResourceValue(ShapeDefaults.Instant) { ResourceID = bare.Id, IsValueRuntime = true, Value = bare.Value }),
                ];
                Write(written);
                return written;
            }
        }

        /// <summary>Changes no resource-interaction wait has reported yet.</summary>
        internal PendingQueue<ResourceValue> Changes { get; }

        /// <summary>Changes no OpenAPI event wait has reported yet. A queue of its own, so a wait through one API
        /// never takes a change the other has yet to report.</summary>
        internal PendingQueue<ResourceValue> Events { get; }

        private static ResourceValue Unset(int resourceId) =>
            new(ShapeDefaults.Instant)
            {
                ResourceID = resourceId,
                Value = new ResourceValue.UnionValue { ValueKind = ResourceValue.ValueKind.BOOL, BoolValue = false },
            };
    }
}
