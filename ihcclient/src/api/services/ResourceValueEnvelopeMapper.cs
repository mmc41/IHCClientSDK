using System;
using Ihc.Soap.Resourceinteraction;

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
        internal static ResourceValue ToDomain(WSResourceValueEnvelope v)
        {
            if (v == null)
                return null;

            var value = new ResourceValue.UnionValue() { };

            if (v.value is WSBooleanValue)
            {
                value.BoolValue = (v.value as WSBooleanValue).value;
                value.ValueKind = ResourceValue.ValueKind.BOOL;
            }

            if (v.value is WSDateValue)
            {
                value.DateValue = MapDate(v.value as WSDateValue);
                value.ValueKind = ResourceValue.ValueKind.DATE;
            }

            if (v.value is WSIntegerValue)
            {
                value.IntValue = (v.value as WSIntegerValue).integer;
                // TODO: What about min, max values ?
                value.ValueKind = ResourceValue.ValueKind.INT;
            }

            if (v.value is WSFloatingPointValue)
            {
                value.DoubleValue = (v.value as WSFloatingPointValue).floatingPointValue;
                // TODO: What about min, max values?
                value.ValueKind = ResourceValue.ValueKind.DOUBLE;
            }

            if (v.value is WSEnumValue)
            {
                value.EnumValue = MapEnumValue(v.value as WSEnumValue);
                value.ValueKind = ResourceValue.ValueKind.ENUM;
            }

            if (v.value is WSTimeValue)
            {
                value.TimeValue = MapTime(v.value as WSTimeValue);
                value.ValueKind = ResourceValue.ValueKind.TIME;
            }

            if (v.value is WSTimerValue)
            {
                value.TimerValue = MapTimer(v.value as WSTimerValue);
                value.ValueKind = ResourceValue.ValueKind.TIMER;
            }

            if (v.value is WSWeekdayValue)
            {
                value.WeekdayValue = MapWeekday(v.value as WSWeekdayValue);
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

        internal static WSResourceValueEnvelope ToWire(ResourceValue v)
        {
            if (v == null)
                return null;

            WSResourceValue val;

            switch (v.Value.ValueKind)
            {
                case ResourceValue.ValueKind.BOOL: val = new WSBooleanValue() { value = (bool)v.Value.BoolValue }; break;
                case ResourceValue.ValueKind.DATE: val = MapDate((DateTimeOffset)v.Value.DateValue); break;
                case ResourceValue.ValueKind.INT: val = new WSIntegerValue() { integer = (int)v.Value.IntValue }; break;
                case ResourceValue.ValueKind.DOUBLE: val = new WSFloatingPointValue() { floatingPointValue = (double)v.Value.DoubleValue }; break;
                case ResourceValue.ValueKind.ENUM: val = MapEnumValue(v.Value.EnumValue); break;
                case ResourceValue.ValueKind.TIME: val = MapTime((TimeSpan)v.Value.TimeValue); break;
                case ResourceValue.ValueKind.TIMER: val = MapTimer((long)v.Value.TimerValue); break;
                case ResourceValue.ValueKind.WEEKDAY: val = MapWeekday((int)v.Value.WeekdayValue); break;
                case ResourceValue.ValueKind.PhoneNumber: val = new WSPhoneNumberValue() { number = v.Value.PhoneNumberValue }; break;
                case ResourceValue.ValueKind.SceneDimmer: val = new WSSceneDimmerValue() { dimmerPercentage = (int)v.Value.DimmerPercentage, delayTime = (int)v.Value.DimmerDelayTime, rampTime = (int)v.Value.DimmerRampTime }; break;
                case ResourceValue.ValueKind.SceneRelay: val = new WSSceneRelayValue() { delayTime = (int)v.Value.RelayDelayTime, relayValue = (bool)v.Value.RelayValue }; break;
                case ResourceValue.ValueKind.SceneShutter: val = new WSSceneShutterSimpleValue() { shutterPositionIsUp = (bool)v.Value.ShutterPositionIsUp, delayTime = (int)v.Value.ShutterDelayTime }; break;
                default: throw new ErrorWithCodeException(Errors.FEATURE_NOT_IMPLEMENTED, "Support for value kind " + v.Value.ValueKind + " not (yet) implemented.");
            }

            return new WSResourceValueEnvelope()
            {
                resourceID = v.ResourceID,
                isValueRuntime = v.IsValueRuntime,
                typeString = v.TypeString,
                value = val
            };
        }

        internal static EnumValue MapEnumValue(WSEnumValue v)
        {
            if (v == null)
                return null;

            return new EnumValue() { DefinitionTypeID = v.definitionTypeID, EnumValueID = v.enumValueID, EnumName = v.enumName };
        }

        internal static WSEnumValue MapEnumValue(EnumValue v)
        {
            if (v == null)
                return null;

            return new WSEnumValue() { definitionTypeID = v.DefinitionTypeID, enumValueID = v.EnumValueID, enumName = v.EnumName };
        }

        private static DateTimeOffset MapDate(WSDateValue v)
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

        private static TimeSpan MapTime(WSTimeValue v)
        {
            if (v == null)
                return TimeSpan.Zero;

            return new TimeSpan(v.hours, v.minutes, v.seconds);
        }

        private static long MapTimer(WSTimerValue v)
        {
            if (v == null)
                return 0;

            return v.milliseconds;
        }

        private static WSTimerValue MapTimer(long v)
        {
            return new WSTimerValue() { milliseconds = v };
        }

        private static int MapWeekday(WSWeekdayValue v)
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
