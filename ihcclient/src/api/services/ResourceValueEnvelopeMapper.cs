using System;
using System.Diagnostics.CodeAnalysis;
using Ihc.Soap.Resourceinteraction;
using static Ihc.ResourceValuePayload;

namespace Ihc
{
    /// <summary>
    /// Pure translation between the high-level <see cref="ResourceValue"/> domain model and the
    /// generated SOAP <see cref="WSResourceValueEnvelope"/> wire type (plus the shared enum value
    /// type). Extracted from <see cref="ResourceInteractionService"/> so the 12-kind mapping can be
    /// unit-/property-tested in isolation; it holds no state and performs no I/O.
    ///
    /// Two conversions are intentionally lossy (preserved verbatim from the original code):
    /// DATE keeps only year/month/day - the round-trip reconstructs time-of-day as 00:00:00 at the
    /// WS offset (<see cref="DateHelper.GetWSTimeOffset"/>); TIME keeps only whole
    /// Hours/Minutes/Seconds (no Days, no milliseconds, magnitude &lt; 24h).
    /// </summary>
    internal static class ResourceValueEnvelopeMapper
    {
        internal static ResourceValue? ToDomain(WSResourceValueEnvelope? v)
        {
            if (v == null)
                return null;

            // Default to NONE so an envelope with a null (or otherwise unrecognized) inner value is
            // classified as "no readable value" rather than silently mislabeled as the enum-default BOOL.
            // Confirmed live: scene resources (type 'resource_scene') return a null <value> here.
            var value = new ResourceValue.UnionValue() { ValueKind = ResourceValue.ValueKind.NONE };

            if (v.value is WSBooleanValue boolVal)
            {
                value.BoolValue = boolVal.value;
                value.ValueKind = ResourceValue.ValueKind.BOOL;
            }

            if (v.value is WSDateValue dateVal)
            {
                value.DateValue = MapDate(dateVal);
                value.ValueKind = ResourceValue.ValueKind.DATE;
            }

            if (v.value is WSIntegerValue intVal)
            {
                value.IntValue = intVal.integer;
                // TODO: What about min, max values ?
                value.ValueKind = ResourceValue.ValueKind.INT;
            }

            if (v.value is WSFloatingPointValue floatVal)
            {
                value.DoubleValue = floatVal.floatingPointValue;
                // TODO: What about min, max values?
                value.ValueKind = ResourceValue.ValueKind.DOUBLE;
            }

            if (v.value is WSEnumValue enumVal)
            {
                value.EnumValue = MapEnumValue(enumVal);
                value.ValueKind = ResourceValue.ValueKind.ENUM;
            }

            if (v.value is WSTimeValue timeVal)
            {
                value.TimeValue = MapTime(timeVal);
                value.ValueKind = ResourceValue.ValueKind.TIME;
            }

            if (v.value is WSTimerValue timerVal)
            {
                value.TimerValue = MapTimer(timerVal);
                value.ValueKind = ResourceValue.ValueKind.TIMER;
            }

            if (v.value is WSWeekdayValue weekdayVal)
            {
                value.WeekdayValue = MapWeekday(weekdayVal);
                value.ValueKind = ResourceValue.ValueKind.WEEKDAY;
            }

            if (v.value is WSPhoneNumberValue phoneVal)
            {
                value.PhoneNumberValue = phoneVal.number;
                value.ValueKind = ResourceValue.ValueKind.PhoneNumber;
            }

            if (v.value is WSSceneDimmerValue dimmerVal)
            {
                value.DimmerPercentage = dimmerVal.dimmerPercentage;
                value.DimmerDelayTime = dimmerVal.delayTime;
                value.DimmerRampTime = dimmerVal.rampTime;
                value.ValueKind = ResourceValue.ValueKind.SceneDimmer;
            }

            if (v.value is WSSceneRelayValue relayVal)
            {
                value.RelayDelayTime = relayVal.delayTime;
                value.RelayValue = relayVal.relayValue;
                value.ValueKind = ResourceValue.ValueKind.SceneRelay;
            }

            if (v.value is WSSceneShutterSimpleValue shutterVal)
            {
                value.ShutterPositionIsUp = shutterVal.shutterPositionIsUp;
                value.ShutterDelayTime = shutterVal.delayTime;
                value.ValueKind = ResourceValue.ValueKind.SceneShutter;
            }

            return new ResourceValue() { ResourceID = v.resourceID, IsValueRuntime = v.isValueRuntime, TypeString = v.typeString, Value = value };
        }

        internal static WSResourceValueEnvelope? ToWire(ResourceValue? v)
        {
            if (v == null)
                return null;

            WSResourceValue val;

            var kind = v.Value.ValueKind;

            switch (kind)
            {
                case ResourceValue.ValueKind.BOOL: val = new WSBooleanValue() { value = Required(v.Value.BoolValue, kind, nameof(v.Value.BoolValue)) }; break;
                case ResourceValue.ValueKind.DATE: val = MapDate(Required(v.Value.DateValue, kind, nameof(v.Value.DateValue))); break;
                case ResourceValue.ValueKind.INT: val = new WSIntegerValue() { integer = Required(v.Value.IntValue, kind, nameof(v.Value.IntValue)) }; break;
                case ResourceValue.ValueKind.DOUBLE: val = new WSFloatingPointValue() { floatingPointValue = Required(v.Value.DoubleValue, kind, nameof(v.Value.DoubleValue)) }; break;
                case ResourceValue.ValueKind.ENUM: val = MapEnumValue(Required(v.Value.EnumValue, kind, nameof(v.Value.EnumValue))); break;
                case ResourceValue.ValueKind.TIME: val = MapTime(Required(v.Value.TimeValue, kind, nameof(v.Value.TimeValue))); break;
                case ResourceValue.ValueKind.TIMER: val = MapTimer(Required(v.Value.TimerValue, kind, nameof(v.Value.TimerValue))); break;
                case ResourceValue.ValueKind.WEEKDAY: val = MapWeekday(Required(v.Value.WeekdayValue, kind, nameof(v.Value.WeekdayValue))); break;
                case ResourceValue.ValueKind.PhoneNumber: val = new WSPhoneNumberValue() { number = Required(v.Value.PhoneNumberValue, kind, nameof(v.Value.PhoneNumberValue)) }; break;
                case ResourceValue.ValueKind.SceneDimmer: val = new WSSceneDimmerValue() { dimmerPercentage = Required(v.Value.DimmerPercentage, kind, nameof(v.Value.DimmerPercentage)), delayTime = Required(v.Value.DimmerDelayTime, kind, nameof(v.Value.DimmerDelayTime)), rampTime = Required(v.Value.DimmerRampTime, kind, nameof(v.Value.DimmerRampTime)) }; break;
                case ResourceValue.ValueKind.SceneRelay: val = new WSSceneRelayValue() { delayTime = Required(v.Value.RelayDelayTime, kind, nameof(v.Value.RelayDelayTime)), relayValue = Required(v.Value.RelayValue, kind, nameof(v.Value.RelayValue)) }; break;
                case ResourceValue.ValueKind.SceneShutter: val = new WSSceneShutterSimpleValue() { shutterPositionIsUp = Required(v.Value.ShutterPositionIsUp, kind, nameof(v.Value.ShutterPositionIsUp)), delayTime = Required(v.Value.ShutterDelayTime, kind, nameof(v.Value.ShutterDelayTime)) }; break;
                case ResourceValue.ValueKind.NONE: throw new ArgumentException($"Cannot write a ResourceValue with ValueKind.NONE — it represents a resource with no writable value (e.g. a scene). ResourceID {v.ResourceID}.");
                default: throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "Support for value kind " + kind + " not (yet) implemented.");
            }

            return new WSResourceValueEnvelope()
            {
                resourceID = v.ResourceID,
                isValueRuntime = v.IsValueRuntime,
                typeString = v.TypeString,
                value = val
            };
        }

        internal static EnumValue? MapEnumValue(WSEnumValue? v)
        {
            if (v == null)
                return null;

            return new EnumValue() { DefinitionTypeID = v.definitionTypeID, EnumValueID = v.enumValueID, EnumName = v.enumName };
        }

        [return: NotNullIfNotNull(nameof(v))]
        internal static WSEnumValue? MapEnumValue(EnumValue? v)
        {
            if (v == null)
                return null;

            return new WSEnumValue() { definitionTypeID = v.DefinitionTypeID, enumValueID = v.EnumValueID, enumName = v.EnumName };
        }

        private static DateTimeOffset MapDate(WSDateValue? v)
        {
            if (v == null)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(v.year, v.month, v.day, 0, 0, 0, DateHelper.GetWSTimeOffset());
        }

        private static WSDateValue MapDate(DateTimeOffset v)
        {
            return new WSDateValue() { year = (short)v.Year, month = (sbyte)v.Month, day = (sbyte)v.Day };
        }

        private static WSTimeValue MapTime(TimeSpan v)
        {
            return new WSTimeValue() { hours = v.Hours, minutes = v.Minutes, seconds = v.Seconds };
        }

        private static TimeSpan MapTime(WSTimeValue? v)
        {
            if (v == null)
                return TimeSpan.Zero;

            return new TimeSpan(v.hours, v.minutes, v.seconds);
        }

        private static long MapTimer(WSTimerValue? v)
        {
            if (v == null)
                return 0;

            return v.milliseconds;
        }

        private static WSTimerValue MapTimer(long v)
        {
            return new WSTimerValue() { milliseconds = v };
        }

        private static int MapWeekday(WSWeekdayValue? v)
        {
            if (v == null)
                return 0;

            return v.weekdayNumber;
        }

        private static WSWeekdayValue MapWeekday(int v)
        {
            return new WSWeekdayValue() { weekdayNumber = v };
        }
    }
}
