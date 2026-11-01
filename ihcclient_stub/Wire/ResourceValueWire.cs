using Ihc.Soap.Resourceinteraction;
using static Ihc.ResourceValue;

namespace Ihc.Stubbing
{
    /// <summary>
    /// A resource value as the resource interaction service carries it: an envelope naming the resource around one
    /// value of a kind-specific wire type. A kind with no writable value, such as a scene, travels with no value at
    /// all, which is how a real controller answers for one.
    /// </summary>
    internal static class ResourceValueWire
    {
        internal static WSResourceValueEnvelope ToWire(ResourceValue stored, bool isRuntime) => new()
        {
            resourceID = stored.ResourceID,
            isValueRuntime = isRuntime,
            typeString = stored.TypeString,
            value = ValueToWire(stored.Value),
        };

        /// <summary>A value as written, stamped with the stub's instant rather than the time it arrived, so the store
        /// and the journal hold the same value in every run.</summary>
        internal static ResourceValue FromWire(WSResourceValueEnvelope wire)
        {
            ArgumentNullException.ThrowIfNull(wire);
            return new ResourceValue(ShapeDefaults.Instant)
            {
                ResourceID = wire.resourceID,
                IsValueRuntime = wire.isValueRuntime,
                TypeString = wire.typeString,
                Value = ValueFromWire(wire.value),
            };
        }

        internal static WSResourceValue? ValueToWire(UnionValue value) => value.ValueKind switch
        {
            ValueKind.BOOL => new WSBooleanValue { value = Present(value.BoolValue) },
            ValueKind.DATE => DateToWire(Present(value.DateValue)),
            ValueKind.INT => new WSIntegerValue { integer = Present(value.IntValue) },
            ValueKind.DOUBLE => new WSFloatingPointValue { floatingPointValue = Present(value.DoubleValue) },
            ValueKind.ENUM => EnumToWire(value.EnumValue ?? throw Absent(nameof(value.EnumValue))),
            ValueKind.TIME => TimeToWire(Present(value.TimeValue)),
            ValueKind.TIMER => new WSTimerValue { milliseconds = Present(value.TimerValue) },
            ValueKind.WEEKDAY => new WSWeekdayValue { weekdayNumber = Present(value.WeekdayValue) },
            ValueKind.PhoneNumber => new WSPhoneNumberValue { number = value.PhoneNumberValue },
            ValueKind.SceneDimmer => new WSSceneDimmerValue
            {
                dimmerPercentage = Present(value.DimmerPercentage),
                delayTime = Present(value.DimmerDelayTime),
                rampTime = Present(value.DimmerRampTime),
            },
            ValueKind.SceneRelay => new WSSceneRelayValue { delayTime = Present(value.RelayDelayTime), relayValue = Present(value.RelayValue) },
            ValueKind.SceneShutter => new WSSceneShutterSimpleValue
            {
                shutterPositionIsUp = Present(value.ShutterPositionIsUp),
                delayTime = Present(value.ShutterDelayTime),
            },
            _ => null,
        };

        internal static UnionValue ValueFromWire(WSResourceValue? wire) => wire switch
        {
            WSBooleanValue boolean => new() { ValueKind = ValueKind.BOOL, BoolValue = boolean.value },
            WSDateValue date => new()
            {
                ValueKind = ValueKind.DATE,
                DateValue = new DateTimeOffset(date.year, date.month, date.day, 0, 0, 0, WireDates.Offset),
            },
            WSIntegerValue integer => new() { ValueKind = ValueKind.INT, IntValue = integer.integer },
            WSFloatingPointValue floating => new() { ValueKind = ValueKind.DOUBLE, DoubleValue = floating.floatingPointValue },
            WSEnumValue enumerated => new()
            {
                ValueKind = ValueKind.ENUM,
                EnumValue = new EnumValue
                {
                    DefinitionTypeID = enumerated.definitionTypeID,
                    EnumValueID = enumerated.enumValueID,
                    EnumName = enumerated.enumName,
                },
            },
            WSTimeValue time => new() { ValueKind = ValueKind.TIME, TimeValue = new TimeSpan(time.hours, time.minutes, time.seconds) },
            WSTimerValue timer => new() { ValueKind = ValueKind.TIMER, TimerValue = timer.milliseconds },
            WSWeekdayValue weekday => new() { ValueKind = ValueKind.WEEKDAY, WeekdayValue = weekday.weekdayNumber },
            WSPhoneNumberValue phone => new() { ValueKind = ValueKind.PhoneNumber, PhoneNumberValue = phone.number },
            WSSceneDimmerValue dimmer => new()
            {
                ValueKind = ValueKind.SceneDimmer,
                DimmerPercentage = dimmer.dimmerPercentage,
                DimmerDelayTime = dimmer.delayTime,
                DimmerRampTime = dimmer.rampTime,
            },
            WSSceneRelayValue relay => new() { ValueKind = ValueKind.SceneRelay, RelayDelayTime = relay.delayTime, RelayValue = relay.relayValue },
            WSSceneShutterSimpleValue shutter => new()
            {
                ValueKind = ValueKind.SceneShutter,
                ShutterPositionIsUp = shutter.shutterPositionIsUp,
                ShutterDelayTime = shutter.delayTime,
            },
            _ => new() { ValueKind = ValueKind.NONE },
        };

        private static WSDateValue DateToWire(DateTimeOffset date) => new() { year = (short)date.Year, month = (sbyte)date.Month, day = (sbyte)date.Day };

        private static WSTimeValue TimeToWire(TimeSpan time) => new() { hours = time.Hours, minutes = time.Minutes, seconds = time.Seconds };

        private static WSEnumValue EnumToWire(EnumValue value) => new()
        {
            definitionTypeID = value.DefinitionTypeID,
            enumValueID = value.EnumValueID,
            enumName = value.EnumName,
        };

        /// <summary>A stored value whose kind names a part it does not carry is a test's mistake, reported as one
        /// rather than answered as a zero the test never set.</summary>
        private static T Present<T>(T? part) where T : struct => part ?? throw Absent(typeof(T).Name);

        private static InvalidOperationException Absent(string part) =>
            new($"a stored resource value of this kind carries no {part}");
    }
}
